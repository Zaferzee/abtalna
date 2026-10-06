using System.Security.Claims;
using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using CyberLms.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

// Secrets (DB password, SMTP password) must come from env vars / secrets / appsettings.Production.json - never from source.
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<TimeDisplay>();
builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddSingleton<SmtpConfigProvider>();
builder.Services.AddSingleton<NotificationService>();
builder.Services.AddHostedService<EmailSenderService>();
builder.Services.AddScoped<BrandingAccessor>();
builder.Services.AddScoped<Localizer>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddScoped<AuthService>();

var storage = new StorageService(builder.Configuration, builder.Environment);
builder.Services.AddSingleton(storage);
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = storage.MaxRequestBytes);
builder.Services.Configure<FormOptions>(o => { o.MultipartBodyLengthLimit = storage.MaxRequestBytes; o.ValueCountLimit = 4000; });

var authMode = builder.Configuration["Authentication:Mode"] ?? "Local"; // Local | Windows | Both
var windowsEnabled = authMode is "Windows" or "Both";
var underIis = !string.IsNullOrEmpty(builder.Configuration["ASPNETCORE_IIS_HTTPAUTH"]);
var auth = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "CyberLms.Auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = builder.Configuration.GetValue("Security:RequireHttpsCookies", !builder.Environment.IsDevelopment())
            ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/Account/Denied";
        o.ExpireTimeSpan = TimeSpan.FromHours(builder.Configuration.GetValue("Security:SessionHours", 8));
        o.SlidingExpiration = true;
        o.Events.OnValidatePrincipal = async ctx =>
        {
            // Re-check (cached 1 min) that the account is still active and its roles unchanged.
            var id = ctx.Principal!.UserId();
            var cache = ctx.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
            var state = await cache.GetOrCreateAsync($"userstate.{id}", async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var u = await db.Users.AsNoTracking().Where(x => x.Id == id)
                    .Select(x => new { x.IsActive, Roles = x.UserRoles.Select(r => r.Role.Name).OrderBy(n => n).ToList() }).FirstOrDefaultAsync();
                return u == null ? null : new UserState(u.IsActive, string.Join(",", u.Roles));
            });
            var claimed = string.Join(",", ctx.Principal!.FindAll(ClaimTypes.Role).Select(c => c.Value).OrderBy(n => n));
            if (state is not { Active: true } || state.Roles != claimed)
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync();
            }
        };
    });
if (windowsEnabled && !underIis) auth.AddNegotiate();

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", p => p.RequireRole(RoleNames.Admin));

builder.Services.AddAntiforgery(o => { o.Cookie.Name = "CyberLms.Csrf"; o.Cookie.HttpOnly = true; o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; });
builder.Services.AddControllersWithViews(o => { o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()); o.Filters.Add<MustChangePasswordFilter>(); });
builder.Services.AddRouting(o => o.LowercaseUrls = false);

// Persist data-protection keys (cookies/antiforgery) so sign-ins survive restarts and app-pool recycles.
var dp = builder.Services.AddDataProtection().SetApplicationName("CyberLms")
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["Security:DataProtectionKeysPath"] ?? Path.Combine(storage.Root, "_keys")));
if (OperatingSystem.IsWindows()) dp.ProtectKeysWithDpapi(protectToLocalMachine: true);

builder.Services.AddWebEncoders(o => o.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(System.Text.Unicode.UnicodeRanges.All)); // readable Arabic in HTML source

builder.Services.AddHsts(o => { o.MaxAge = TimeSpan.FromDays(365); });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");   // no stack traces in production
    app.UseHsts();
}
if (builder.Configuration.GetValue("Security:RedirectToHttps", !app.Environment.IsDevelopment())) app.UseHttpsRedirection();

app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "SAMEORIGIN";
    h["Referrer-Policy"] = "same-origin";
    h["Content-Security-Policy"] = "default-src 'self'; img-src 'self' data:; media-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self'; frame-ancestors 'self'; object-src 'self'; form-action 'self'";
    await next();
});

// Raise the per-request body limit (IIS in-process defaults to 30 MB) so large videos can be uploaded; per-type limits are enforced in StorageService.
app.Use(async (ctx, next) =>
{
    var f = ctx.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
    if (f is { IsReadOnly: false }) f.MaxRequestBodySize = storage.MaxRequestBytes;
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute("areas", "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

await DbSeeder.MigrateAndSeedAsync(app.Services, builder.Configuration, app.Logger);
app.Run();

record UserState(bool Active, string Roles);
public partial class Program { }

