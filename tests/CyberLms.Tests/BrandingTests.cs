using System.Net;
using CyberLms.Web.Services;
using static CyberLms.Tests.HttpExt;

namespace CyberLms.Tests;

/// <summary>
/// Branding & Appearance studio: preset quality, server-side validation of every studio field, multi-line names, new assets,
/// theme tokens and how the login page and the application render them. (Basic save/inject/reset is in EndToEndTests.)
/// </summary>
public class BrandingTests(TestApp app) : IClassFixture<TestApp>
{
    private const string NewAdminPass = "Admin#NewPass99";

    private async Task<HttpClient> AdminAsync()
    {
        async Task<HttpClient> Login(string pass)
        {
            var c = app.NewClient();
            (await c.PostForm("/Account/Login", "/Account/Login", [F("Username", "admin"), F("Password", pass)])).EnsureRedirect();
            var home = await c.GetAsync("/");
            if (home.StatusCode == HttpStatusCode.Redirect && home.Headers.Location!.ToString().Contains("ChangePassword"))
                (await c.PostForm("/Account/ChangePassword", "/Account/ChangePassword", [F("Current", pass), F("New", NewAdminPass), F("Confirm", NewAdminPass)])).EnsureRedirect();
            return c;
        }
        try { return await Login(NewAdminPass); } catch (Exception) { return await Login(TestApp.AdminPass); }
    }

    private static Task<HttpResponseMessage> Save(HttpClient admin, Dictionary<string, string> fields, Action<MultipartFormDataContent>? files = null) =>
        admin.PostMultipart("/Admin/Settings", "/Admin/Settings/SaveBranding", mp =>
        {
            foreach (var (k, v) in fields) mp.Add(new StringContent(v), k);
            files?.Invoke(mp);
        });

    [Fact]
    public void Presets_are_complete_and_readable()
    {
        Assert.True(BrandingPresets.All.Length >= 8);
        Assert.Contains(BrandingPresets.All, p => p.Id == "mauve");
        Assert.Contains(BrandingPresets.All, p => p.Id == "lavender");
        Assert.Contains(BrandingPresets.All, p => p.Id == "navy");
        foreach (var p in BrandingPresets.All)
        {
            Assert.All(Branding.Colors, c => Assert.True(p.Colors.ContainsKey(c.Name), $"{p.Id} misses {c.Name}"));
            Assert.All(p.Colors.Values.Where(v => v != ""), v => Assert.Matches(Branding.ColorRegex, v));
            var c = p.Colors;
            Assert.True(Branding.Contrast(c["HeaderColor"], c["HeaderTextColor"]) >= 4.5, p.Id + " header");
            Assert.True(Branding.Contrast(c["SidebarColor"], c["SidebarTextColor"]) >= 4.5, p.Id + " sidebar");
            Assert.True(Branding.Contrast(c["PrimaryColor"], Branding.OnColor(c["PrimaryColor"])) >= 4.5, p.Id + " button");
            Assert.True(Branding.Contrast(c["PrimaryColor"], c["HeroTextColor"]) >= 4.5, p.Id + " banner");
            Assert.True(Branding.Contrast(c["LoginOverlayColor"], c["LoginTextColor"]) >= 4.5, p.Id + " login");
            Assert.True(Branding.Contrast(c["BackgroundColor"], "#14213d") >= 4.5, p.Id + " page");
        }
    }

    [Fact]
    public async Task Studio_fields_are_validated_and_rendered_on_the_login_page_and_in_the_application()
    {
        var admin = await AdminAsync();
        Assert.Contains("data-studio", await admin.GetStringAsync("/Admin/Settings"));

        var r = await Save(admin, new()
        {
            ["OrgName"] = "الجهة التجريبية\r\nإدارة الأمن", ["SystemName"] = "منصة\nالتوعية",
            ["LoginBadge"] = "شارة\nمخصصة", ["LoginHeroTitle"] = "عنوان\nعلى سطرين", ["LoginHeroDescription"] = "وصف مخصص للوحة.",
            ["LoginFeature1"] = "نقطة أولى", ["LoginFeature2"] = "نقطة ثانية", ["LoginFeature3"] = "نقطة ثالثة",
            ["LoginCardSubtitle"] = "نص بطاقة مخصص", ["SupportText"] = "الدعم: 4455", ["LoginTitle"] = new string('x', 400),
            ["LoginImageFit"] = "contain", ["LoginImagePosition"] = "nowhere;}body{display:none", ["LoginImageZoom"] = "999", ["LoginOverlayOpacity"] = "-5",
            ["LoginLayout"] = "full", ["LoginTextAlign"] = "center", ["LoginLogoPlacement"] = "card",
            ["ShowOrgInSidebar"] = "false", ["ShowLoginFeatures"] = "false", ["LoginDecor"] = "false",
            ["Preset"] = "navy", ["PrimaryColor"] = "#1B3A6B", ["ButtonColor"] = "", ["BackgroundColor"] = "#f3f5f9", ["LoginTextColor"] = "#fafafa",
        }, mp =>
        {
            mp.AddFile("loginLogo", "login.png", Png, "image/png");
            mp.AddFile("icon", "icon.png", Png, "image/png");
            mp.AddFile("loginBackground", "bg.png", Png, "image/png");
        });
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);

        var anon = app.NewClient();
        var login = await anon.GetStringAsync("/Account/Login");
        // multi-line names/texts keep their line breaks (HTML-encoded as &#xA;, shown with white-space: pre-line);
        // single-line fields are flattened
        Assert.Contains("منصة&#xA;التوعية", login);
        Assert.Contains("عنوان&#xA;على سطرين", login);
        Assert.Contains("شارة مخصصة", login);
        Assert.Contains("نص بطاقة مخصص", login); Assert.Contains("الدعم: 4455", login); Assert.Contains("نقطة ثانية", login);
        Assert.Contains("<title>" + new string('x', 150) + " - منصة التوعية</title>", login);    // title cut to 150, name on one line
        // choices: allowed values kept, invalid ones replaced by the default; numbers clamped
        Assert.Matches("class=\"auth layout-full align-center logo-card no-decor has-image[^\"]*no-features", login);
        Assert.Contains("--img-size:contain;--img-pos:50% 50%;--img-zoom:2;", login);
        Assert.Contains("--ov-op:0;", login);                                                     // -5 clamped (culture-independent parsing)
        Assert.DoesNotContain("display:none", login);
        Assert.Contains("auth-card-logo", login); Assert.Contains("kind=loginlogo", login);
        // theme tokens
        var css = await anon.GetStringAsync("/branding/theme.css");
        Assert.Contains("--color-primary:#1b3a6b", css);
        Assert.Contains("--color-button:#1b3a6b", css);                                         // automatic = primary
        Assert.Contains("--color-on-button:#ffffff", css);
        Assert.Contains("--color-bg:#f3f5f9", css); Assert.Contains("--color-login-text:#fafafa", css);
        // new assets are served
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync("/Files/Brand?kind=loginlogo")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync("/Files/Brand?kind=icon")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/Files/Brand?kind=../appsettings")).StatusCode);
        // application: sidebar names on several lines, organization name hidden by the option, icon used without a sidebar logo
        var dash = await admin.GetStringAsync("/Admin/Dashboard");
        Assert.Contains("<strong class=\"ml\" data-b=\"SystemName\">منصة&#xA;التوعية</strong>", dash);
        Assert.Matches("data-b=\"OrgName\" hidden", dash);
        Assert.Contains("kind=icon", dash);
        Assert.Contains("<title>لوحة التحكم - منصة التوعية</title>", dash);

        // fields not in a submission keep their value; an unknown preset is not stored; favicon must be .ico/.png
        (await Save(admin, new() { ["Preset"] = "neon", ["LoginLayout"] = "split" }, mp => mp.AddFile("favicon", "fav.jpg", Png, "image/jpeg"))).EnsureRedirect();
        login = await anon.GetStringAsync("/Account/Login");
        Assert.Contains("عنوان&#xA;على سطرين", login);
        Assert.Matches("class=\"auth layout-split ", login);
        Assert.DoesNotContain("kind=favicon", login);
        Assert.DoesNotContain("data-preset-id=\"neon\"", await admin.GetStringAsync("/Admin/Settings"));

        // restore defaults removes everything again
        (await admin.PostForm("/Admin/Settings", "/Admin/Settings/ResetBranding", [])).EnsureRedirect();
        login = await anon.GetStringAsync("/Account/Login");
        Assert.DoesNotContain("عنوان&#xA;على سطرين", login);
        Assert.Matches("class=\"auth layout-split align-start logo-hero\"", login);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/Files/Brand?kind=loginlogo")).StatusCode);
    }
}
