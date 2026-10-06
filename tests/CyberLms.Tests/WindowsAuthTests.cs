using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using CyberLms.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CyberLms.Tests;

/// <summary>
/// Verifies the application side of Windows Authentication. IIS itself cannot run here, so the IIS "Windows" authentication scheme is
/// replaced by a stand-in that behaves the same way from the application's point of view: it exposes the identity IIS would have
/// authenticated (taken from a test header) or challenges with 401 when there is none.
/// </summary>
public class WindowsTestApp : TestApp
{
    public WindowsTestApp()
    {
        Environment.SetEnvironmentVariable("Authentication__Mode", "Windows");
        Environment.SetEnvironmentVariable("Authentication__Windows__AllowedDomains__0", "CORP");
        Environment.SetEnvironmentVariable("ASPNETCORE_IIS_HTTPAUTH", "windows;"); // what the ASP.NET Core Module sets under IIS
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(s =>
        {
            s.AddAuthentication().AddScheme<AuthenticationSchemeOptions, FakeIisWindowsHandler>("Windows", _ => { });
            s.AddSingleton<IDirectoryLookup, FakeDirectory>();
        });
    }

    public class FakeDirectory : IDirectoryLookup
    {
        public DirectoryInfoResult? Find(string domain, string sam) => sam == "nodirectory" ? null : new DirectoryInfoResult("أحمد " + sam, sam + "@corp.local");
    }

    public class FakeIisWindowsHandler(IOptionsMonitor<AuthenticationSchemeOptions> o, ILoggerFactory l, UrlEncoder e) : AuthenticationHandler<AuthenticationSchemeOptions>(o, l, e)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Windows-User", out var v)) return Task.FromResult(AuthenticateResult.NoResult());
            var id = new ClaimsIdentity([new Claim(ClaimTypes.Name, v.ToString())], "Negotiate");
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(id), "Windows")));
        }
        protected override Task HandleChallengeAsync(AuthenticationProperties properties) { Response.StatusCode = 401; Response.Headers.WWWAuthenticate = "Negotiate"; return Task.CompletedTask; }
    }
}

public class WindowsAuthTests(WindowsTestApp app) : IClassFixture<WindowsTestApp>
{
    private HttpClient As(string? windowsUser)
    {
        var c = app.NewClient();
        if (windowsUser != null) c.DefaultRequestHeaders.Add("X-Test-Windows-User", windowsUser);
        return c;
    }

    [Fact]
    public async Task Windows_mode_signs_in_silently_and_challenges_when_IIS_gave_no_identity()
    {
        // The login page forwards straight to Windows sign-in (no local form).
        var login = await As(null).GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Contains("/Account/WindowsLogin", login.Headers.Location!.ToString());
        // no identity => challenge (what IIS does with Anonymous disabled)
        Assert.Equal(HttpStatusCode.Unauthorized, (await As(null).GetAsync("/Account/WindowsLogin")).StatusCode);
        // local password sign-in is not available
        var c = app.NewClient();
        var post = await c.PostForm("/Account/WindowsLogin", "/Account/Login", [HttpExt.F("Username", "admin"), HttpExt.F("Password", "x")]).ContinueWith(t => t.Exception == null ? t.Result : null);
        Assert.True(post == null || post.StatusCode != HttpStatusCode.Redirect || !post.Headers.Location!.ToString().EndsWith("/"));
    }

    [Fact]
    public async Task First_domain_login_creates_a_plain_employee_profile_without_password_and_never_an_admin()
    {
        var c = As(@"CORP\ahmed.salem");
        var r = await c.GetAsync("/Account/WindowsLogin");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);

        var u = await app.Db(d => d.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleAsync(x => x.NormalizedUsername == @"corp\ahmed.salem"));
        Assert.Equal(@"CORP\ahmed.salem", u.Username);
        Assert.Equal("Windows", u.AuthSource);
        Assert.Null(u.PasswordHash);                               // no AD password (or any password) stored
        Assert.Equal("أحمد ahmed.salem", u.DisplayName);           // from the directory
        Assert.Equal("ahmed.salem@corp.local", u.Email);
        Assert.Equal([RoleNames.User], u.UserRoles.Select(x => x.Role.Name).ToArray()); // NOT an administrator

        var home = await c.GetAsync("/");                          // signed in via cookie
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Contains("لوحة التحكم", await home.Content.ReadAsStringAsync());
        var admin = await c.GetAsync("/Admin/Dashboard");           // domain user cannot reach admin pages
        Assert.Equal(HttpStatusCode.Redirect, admin.StatusCode);
        Assert.Contains("Denied", admin.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Same_identity_in_other_case_maps_to_the_same_profile_and_directory_failure_does_not_block()
    {
        await As(@"CORP\Sara.Ali").GetAsync("/Account/WindowsLogin");
        await As(@"corp\SARA.ALI").GetAsync("/Account/WindowsLogin");
        Assert.Equal(1, await app.Db(d => d.Users.CountAsync(x => x.NormalizedUsername == @"corp\sara.ali")));

        await As(@"CORP\nodirectory").GetAsync("/Account/WindowsLogin");  // lookup returns nothing: profile still created
        var u = await app.Db(d => d.Users.SingleAsync(x => x.NormalizedUsername == @"corp\nodirectory"));
        Assert.Equal("nodirectory", u.DisplayName); Assert.Null(u.Email);
    }

    [Fact]
    public async Task Other_domains_and_disabled_accounts_are_rejected_with_an_Arabic_message()
    {
        var evil = await As(@"EVIL\mallory").GetAsync("/Account/WindowsLogin");
        Assert.Equal(HttpStatusCode.Redirect, evil.StatusCode);
        Assert.False(await app.Db(d => d.Users.AnyAsync(x => x.NormalizedUsername == @"evil\mallory")));
        var c = As(@"EVIL\mallory");
        await c.GetAsync("/Account/WindowsLogin");
        Assert.Contains("نطاق Windows الخاص بك غير مسموح به", await c.GetStringAsync("/Account/Login"));

        await As(@"CORP\leaver").GetAsync("/Account/WindowsLogin");
        await app.Db(async d => { var u = await d.Users.SingleAsync(x => x.NormalizedUsername == @"corp\leaver"); u.IsActive = false; await d.SaveChangesAsync(); return 0; });
        var c2 = As(@"CORP\leaver");
        await c2.GetAsync("/Account/WindowsLogin");
        Assert.Contains("حسابك معطّل", await c2.GetStringAsync("/Account/Login"));
        Assert.Equal(HttpStatusCode.Redirect, (await c2.GetAsync("/Content")).StatusCode);   // no session
    }

    [Fact]
    public async Task Administrator_comes_only_from_the_server_bootstrap_command_and_survives_independent_of_AD()
    {
        // invalid forms are refused
        using (var scope = app.Services.CreateScope())
        {
            var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("t");
            Assert.False((await DbSeeder.BootstrapAdminAsync(app.Services, "justaname", log)).Ok);
            Assert.False((await DbSeeder.BootstrapAdminAsync(app.Services, @"CORP\a b", log)).Ok);
            var ok = await DbSeeder.BootstrapAdminAsync(app.Services, @"CORP\itsec.admin", log);
            Assert.True(ok.Ok, ok.Message);
            Assert.True((await DbSeeder.BootstrapAdminAsync(app.Services, @"CORP\itsec.admin", log)).Ok);   // idempotent
        }
        Assert.Equal(1, await app.Db(d => d.Users.CountAsync(u => u.NormalizedUsername == @"corp\itsec.admin")));

        var admin = As(@"CORP\ITSEC.ADMIN");
        await admin.GetAsync("/Account/WindowsLogin");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/Admin/Dashboard")).StatusCode);
        // an unrelated domain user is still not an administrator
        var other = As(@"CORP\someone.else"); await other.GetAsync("/Account/WindowsLogin");
        Assert.Equal(HttpStatusCode.Redirect, (await other.GetAsync("/Admin/Dashboard")).StatusCode);
        // the bootstrap is auditable, and admins are then managed in the application
        Assert.True(await app.Db(d => d.AuditLogs.AnyAsync(a => a.Action == "ADMIN_BOOTSTRAPPED")));
        var promote = await admin.PostForm("/Admin/Users", "/Admin/Users/Create", [HttpExt.F("Username", @"CORP\second.admin"), HttpExt.F("DisplayName", "مدير ثان"), HttpExt.F("AuthSource", "Windows"), HttpExt.F("IsAdmin", "true"), HttpExt.F("IsActive", "true")]);
        Assert.Equal(HttpStatusCode.Redirect, promote.StatusCode);
        Assert.True(await app.Db(d => d.Users.AnyAsync(u => u.NormalizedUsername == @"corp\second.admin" && u.UserRoles.Any(r => r.Role.Name == RoleNames.Admin))));
    }

    [Fact]
    public void Ldap_filter_values_are_escaped()
    {
        Assert.Equal(@"a\2a\28b\29\5c", ActiveDirectoryLookup.EscapeFilter(@"a*(b)\"));
    }
}
