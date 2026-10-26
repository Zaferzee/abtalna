using System.Net;
using System.Text.RegularExpressions;
using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using static CyberLms.Tests.HttpExt;

namespace CyberLms.Tests;

/// <summary>
/// Hierarchical entity administration: root/backward compatibility, hierarchy, capability delegation, cross-entity isolation (including
/// direct URL attempts), content targeting, scoped reports/exports, branding inheritance, directory configuration and audit.
/// Every test builds its own small organization (unique codes), so tests are independent:
///
///   ROOT ── Agency A (AGA-x) ── Department A1 (A1-x)
///        └─ Agency B (AGB-x)
/// </summary>
public class EntitiesTests(TestApp app) : IClassFixture<TestApp>
{
    private const string NewAdminPass = "Admin#NewPass99", UserPass = "Employee#Pass123", UserNewPass = "Employee#NewPass99";

    // ------------------------------------------------------------------------------------------------ helpers

    private async Task<HttpClient> LoginAsync(string user, string pass, string? newPass = null)
    {
        var c = app.NewClient();
        (await c.PostForm("/Account/Login", "/Account/Login", [F("Username", user), F("Password", pass)])).EnsureRedirect();
        var home = await c.GetAsync("/");
        if (home.StatusCode == HttpStatusCode.Redirect && home.Headers.Location!.ToString().Contains("ChangePassword"))
            (await c.PostForm("/Account/ChangePassword", "/Account/ChangePassword", [F("Current", pass), F("New", newPass!), F("Confirm", newPass!)])).EnsureRedirect();
        return c;
    }

    private async Task<HttpClient> CentralAsync()
    {
        try { return await LoginAsync("admin", NewAdminPass); } catch (Exception) { return await LoginAsync("admin", TestApp.AdminPass, NewAdminPass); }
    }

    private Task<HttpClient> UserAsync(string username) => LoginAsync(username, UserPass, UserNewPass);

    private static bool Denied(HttpResponseMessage r) => r.StatusCode == HttpStatusCode.Forbidden || (r.StatusCode == HttpStatusCode.Redirect && r.Headers.Location!.ToString().Contains("Denied"));

    private Task<int> RootId() => app.Db(d => d.Entities.Where(e => e.ParentEntityId == null).Select(e => e.Id).SingleAsync());

    private async Task<int> EntityAsync(HttpClient central, string name, string code, int parent, params Capability[] allowed)
    {
        var fields = new List<KeyValuePair<string, string>> { F("Name", name), F("Code", code), F("ParentEntityId", parent.ToString()), F("IsActive", "true") };
        fields.AddRange((allowed.Length == 0 ? [Capability.ManageUsers, Capability.ManageContent, Capability.PublishContent, Capability.ManageAssessments, Capability.ManageAcknowledgments, Capability.ViewReports, Capability.ExportReports, Capability.ManageNotifications, Capability.ViewAudit] : allowed)
            .Select(c => F("Allowed", c.ToString())));
        var r = await central.PostForm("/Admin/Entities/Create", "/Admin/Entities/Create", fields);
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        return int.Parse(Regex.Match(r.Headers.Location!.ToString(), @"/Details/(\d+)").Groups[1].Value);
    }

    private async Task<int> UserIn(HttpClient admin, string username, int entityId)
    {
        var r = await admin.PostForm("/Admin/Users/Create", "/Admin/Users/Create",
            [F("Username", username), F("DisplayName", "موظف " + username), F("Email", username + "@x.local"), F("AuthSource", "Local"), F("Password", UserPass), F("IsActive", "true"), F("EntityId", entityId.ToString())]);
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        return await app.Db(d => d.Users.Where(u => u.NormalizedUsername == username.ToLowerInvariant()).Select(u => u.Id).SingleAsync());
    }

    private static Task<HttpResponseMessage> Assign(HttpClient actor, int entityId, string username, bool desc, params Capability[] caps) =>
        actor.PostForm($"/Admin/Entities/Details/{entityId}?tab=admins", $"/Admin/Entities/AssignAdmin/{entityId}",
            [F("username", username), F("includeDescendants", desc ? "true" : "false"), .. caps.Select(c => F("caps", c.ToString()))]);

    /// <summary>Creates content through the wizard; returns its id.</summary>
    private async Task<int> ContentAsync(HttpClient admin, string title, int owner, ContentTargetScope scope, int[]? targets = null, bool desc = true, bool publish = true, bool ack = false)
    {
        var fields = new List<KeyValuePair<string, string>> { F("Title", title), F("Type", "6"), F("OwnerEntityId", owner.ToString()), F("Audience", ((int)scope).ToString()),
            F("TargetsIncludeDescendants", desc ? "true" : "false"), F("go", "next") };
        fields.AddRange((targets ?? []).Select(t => F("TargetIds", t.ToString())));
        var r = await admin.PostForm("/Admin/Authoring/New", "/Admin/Authoring/Basics", fields);
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        var id = int.Parse(Regex.Match(r.Headers.Location!.ToString(), @"/(\d+)").Groups[1].Value);
        if (ack) (await admin.PostForm($"/Admin/Authoring/Acknowledgment/{id}", $"/Admin/Authoring/Acknowledgment/{id}", [F("RequiresAcknowledgment", "true"), F("Statement", "أقر"), F("go", "stay")])).EnsureRedirect();
        if (publish) (await admin.PostForm($"/Admin/Authoring/Publish/{id}", $"/Admin/Authoring/Publish/{id}", [F("go", "publish")])).EnsureRedirect();
        return id;
    }

    private record World(string S, int Root, int A, int A1, int B, HttpClient Central);

    private async Task<World> WorldAsync()
    {
        var s = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        var central = await CentralAsync();
        var root = await RootId();
        var a = await EntityAsync(central, "هيئة أ " + s, "AGA-" + s, root);
        var a1 = await EntityAsync(central, "إدارة أ1 " + s, "A1-" + s, a);
        var b = await EntityAsync(central, "هيئة ب " + s, "AGB-" + s, root);
        return new World(s, root, a, a1, b, central);
    }

    // ------------------------------------------------------------------------------------------------ backward compatibility

    [Fact]
    public async Task Fresh_installation_has_a_single_root_and_everything_belongs_to_it()
    {
        await CentralAsync();
        var (roots, adminEntity, rootId) = await app.Db(async d => (await d.Entities.CountAsync(e => e.ParentEntityId == null),
            await d.Users.Where(u => u.NormalizedUsername == "admin").Select(u => u.EntityId).SingleAsync(), await d.Entities.Where(e => e.ParentEntityId == null).Select(e => e.Id).SingleAsync()));
        Assert.Equal(1, roots);
        Assert.Equal(rootId, adminEntity);
        Assert.Equal($"/{rootId}/", await app.Db(d => d.Entities.Where(e => e.Id == rootId).Select(e => e.Path).SingleAsync()));
        // the database refuses a second root
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => app.Db(async d => { d.Entities.Add(new OrgEntity { Name = "x", Code = "SECOND-ROOT", Path = "/x/" }); return await d.SaveChangesAsync(); }));
        // central content defaults to "all entities"; the central administrator keeps the full admin area
        var central = await CentralAsync();
        var id = await ContentAsync(central, "محتوى مركزي افتراضي", rootId, ContentTargetScope.OwnerAndDescendants);
        var c = await app.Db(d => d.Contents.SingleAsync(x => x.Id == id));
        Assert.Equal((rootId, ContentTargetScope.OwnerAndDescendants), (c.OwnerEntityId, c.TargetScope));
        foreach (var p in new[] { "/Admin/Dashboard", "/Admin/Entities", "/Admin/Settings", "/Admin/Users", "/Admin/Content", "/Admin/Reports/Entities", "/Admin/Audit" })
            Assert.Equal(HttpStatusCode.OK, (await central.GetAsync(p)).StatusCode);
    }

    [Fact]
    public async Task Migration_moves_an_existing_single_organization_into_the_root_entity()
    {
        // a database at the previous schema version with legacy data, then the entity migration
        var cs = Environment.GetEnvironmentVariable("TEST_PG")!;
        var dbName = "cyberlms_mig_" + Guid.NewGuid().ToString("N")[..8];
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs + ";Database=" + dbName).Options;
        try
        {
            await using (var db = new AppDbContext(options))
            {
                await db.GetInfrastructure().GetRequiredService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().MigrateAsync("20261009014228_AddContentCompletion");
                await db.Database.ExecuteSqlRawAsync("""
                    INSERT INTO "SystemSettings" ("Key","Value") VALUES ('Branding.OrgName', E'وزارة\nالاختبار');
                    INSERT INTO "Users" ("Username","NormalizedUsername","DisplayName","AuthSource","IsActive","MustChangePassword","FailedLoginCount","CreatedAt") VALUES ('legacy','legacy','Legacy','Local',true,false,0,now());
                    INSERT INTO "Contents" ("Title","Type","Status","RequiresAcknowledgment","Version","CreatedAt") VALUES ('legacy content',6,1,false,1,now());
                    INSERT INTO "Assessments" ("Title","PassingPercentage","MaxAttempts","IsPublished","CreatedAt") VALUES ('legacy quiz',70,1,true,now());
                    """);
                await db.Database.MigrateAsync();
            }
            await using (var db = new AppDbContext(options))
            {
                var root = await db.Entities.SingleAsync();
                Assert.Null(root.ParentEntityId);
                Assert.Equal("وزارة الاختبار", root.Name);           // named after the existing organization (one line)
                Assert.Equal($"/{root.Id}/", root.Path);
                Assert.Equal(Capability.All, root.AllowedCapabilities);
                Assert.Equal(root.Id, (await db.Users.SingleAsync()).EntityId);
                var c = await db.Contents.SingleAsync();
                Assert.Equal((root.Id, ContentTargetScope.OwnerAndDescendants), (c.OwnerEntityId, c.TargetScope)); // stays visible to everyone
                Assert.Equal(root.Id, (await db.Assessments.SingleAsync()).OwnerEntityId);
                Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            }
        }
        finally
        {
            Npgsql.NpgsqlConnection.ClearAllPools();
            await using var c = new Npgsql.NpgsqlConnection(cs + ";Database=postgres");
            await c.OpenAsync();
            await using var cmd = new Npgsql.NpgsqlCommand($"DROP DATABASE IF EXISTS \"{dbName}\" WITH (FORCE)", c);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    // ------------------------------------------------------------------------------------------------ hierarchy

    [Fact]
    public async Task Hierarchy_supports_any_depth_moves_rewrite_paths_and_cycles_are_rejected()
    {
        var w = await WorldAsync();
        var deep = await EntityAsync(w.Central, "قسم عميق " + w.S, "DEEP-" + w.S, w.A1);
        var e = await app.Db(d => d.Entities.AsNoTracking().ToDictionaryAsync(x => x.Id));
        Assert.Equal($"/{w.Root}/{w.A}/{w.A1}/{deep}/", e[deep].Path);
        Assert.Equal(3, e[deep].Depth);
        // move A1 (with its child) under B
        var move = await w.Central.PostForm($"/Admin/Entities/Edit/{w.A1}", $"/Admin/Entities/Edit/{w.A1}",
            [F("Name", "إدارة أ1 " + w.S), F("Code", "A1-" + w.S), F("ParentEntityId", w.B.ToString()), F("IsActive", "true"), F("Allowed", "ManageUsers")]);
        Assert.Equal(HttpStatusCode.Redirect, move.StatusCode);
        e = await app.Db(d => d.Entities.AsNoTracking().ToDictionaryAsync(x => x.Id));
        Assert.Equal($"/{w.Root}/{w.B}/{w.A1}/{deep}/", e[deep].Path);
        // B under its own descendant: refused, nothing changes
        var cycle = await w.Central.PostForm($"/Admin/Entities/Edit/{w.B}", $"/Admin/Entities/Edit/{w.B}",
            [F("Name", "هيئة ب " + w.S), F("Code", "AGB-" + w.S), F("ParentEntityId", deep.ToString()), F("IsActive", "true")]);
        Assert.Equal(HttpStatusCode.OK, cycle.StatusCode);
        Assert.Equal(w.Root, await app.Db(d => d.Entities.Where(x => x.Id == w.B).Select(x => x.ParentEntityId).SingleAsync()));
        // duplicate code refused; the root cannot be disabled
        Assert.Equal(HttpStatusCode.OK, (await w.Central.PostForm("/Admin/Entities/Create", "/Admin/Entities/Create", [F("Name", "x"), F("Code", "AGA-" + w.S), F("ParentEntityId", w.Root.ToString())])).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await w.Central.PostForm($"/Admin/Entities/Edit/{w.Root}", $"/Admin/Entities/Edit/{w.Root}", [F("Name", "Root"), F("Code", "ROOT"), F("IsActive", "false")])).StatusCode);
        Assert.True(await app.Db(d => d.Entities.Where(x => x.Id == w.Root).Select(x => x.IsActive).SingleAsync()));
        var audit = await app.Db(d => d.AuditLogs.Where(a => a.ScopeEntityId == w.A1).Select(a => a.Action).ToListAsync());
        Assert.Contains("ENTITY_CREATED", audit); Assert.Contains("ENTITY_MOVED", audit);
    }

    // ------------------------------------------------------------------------------------------------ delegation

    [Fact]
    public async Task Capabilities_are_granted_explicitly_capped_by_the_entity_and_revocable()
    {
        var w = await WorldAsync();
        await UserIn(w.Central, "admA-" + w.S.ToLower(), w.A);
        var adminA = "admA-" + w.S.ToLower();
        Assert.Equal(HttpStatusCode.Redirect, (await Assign(w.Central, w.A, adminA, true, Capability.ManageUsers, Capability.ViewReports)).StatusCode);
        var a = await UserAsync(adminA);
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync("/Admin/Dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync("/Admin/Users")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync("/Admin/Reports/Assessments")).StatusCode);
        // not granted -> refused on the server, whatever the menu shows
        foreach (var p in new[] { "/Admin/Content", "/Admin/Authoring/New", "/Admin/Assessments", "/Admin/Audit", "/Admin/Settings", "/Admin/Entities/Create", $"/Admin/EntityBranding/Index/{w.A}" })
            Assert.True(Denied(await a.GetAsync(p)), p);
        Assert.True(Denied(await a.GetAsync("/Admin/Reports/Assessments?export=csv")));   // ViewReports without ExportReports
        // the entity ceiling: branding is not allowed for A, so it cannot be granted at all
        Assert.True(Denied(await Assign(w.Central, w.A, adminA, true, Capability.ManageBranding)));
        // a plain employee never enters the administration area
        await UserIn(w.Central, "empA-" + w.S.ToLower(), w.A);
        Assert.True(Denied(await (await UserAsync("empA-" + w.S.ToLower())).GetAsync("/Admin/Dashboard")));
        // revoke: access ends immediately (capabilities are read from the database on every request)
        var assignment = await app.Db(d => d.EntityAdminAssignments.Where(x => x.EntityId == w.A && x.User.NormalizedUsername == adminA.ToLowerInvariant()).Select(x => x.Id).SingleAsync());
        (await w.Central.PostForm($"/Admin/Entities/Details/{w.A}?tab=admins", $"/Admin/Entities/RevokeAdmin/{w.A}", [F("assignmentId", assignment.ToString())])).EnsureRedirect();
        Assert.True(Denied(await a.GetAsync("/Admin/Users")));
        Assert.True(Denied(await a.GetAsync("/Admin/Dashboard")));
        var actions = await app.Db(d => d.AuditLogs.Where(x => x.ScopeEntityId == w.A).Select(x => x.Action).ToListAsync());
        Assert.Contains("ADMIN_ASSIGNED", actions); Assert.Contains("ADMIN_REVOKED", actions);
    }

    [Fact]
    public async Task Delegated_administrators_can_only_delegate_what_they_hold_inside_their_scope()
    {
        var w = await WorldAsync();
        var lead = "lead-" + w.S.ToLower(); var helper = "help-" + w.S.ToLower(); var outsider = "outB-" + w.S.ToLower();
        await UserIn(w.Central, lead, w.A); await UserIn(w.Central, helper, w.A1); await UserIn(w.Central, outsider, w.B);
        // allow ManageAdmins for A, then give the lead ManageAdmins + ManageUsers
        (await w.Central.PostForm($"/Admin/Entities/Edit/{w.A}", $"/Admin/Entities/Edit/{w.A}", [F("Name", "هيئة أ " + w.S), F("Code", "AGA-" + w.S), F("ParentEntityId", w.Root.ToString()), F("IsActive", "true"),
            F("Allowed", "ManageUsers"), F("Allowed", "ManageAdmins"), F("Allowed", "ViewReports")])).EnsureRedirect();
        (await w.Central.PostForm($"/Admin/Entities/Edit/{w.A1}", $"/Admin/Entities/Edit/{w.A1}", [F("Name", "إدارة أ1 " + w.S), F("Code", "A1-" + w.S), F("ParentEntityId", w.A.ToString()), F("IsActive", "true"),
            F("Allowed", "ManageUsers"), F("Allowed", "ViewReports")])).EnsureRedirect();
        (await Assign(w.Central, w.A, lead, true, Capability.ManageAdmins, Capability.ManageUsers)).EnsureRedirect();
        var l = await UserAsync(lead);
        // A1 does not allow ManageAdmins, so the lead cannot manage administrators there at all
        Assert.True(Denied(await Assign(l, w.A1, helper, false, Capability.ManageUsers)));
        Assert.False(await app.Db(d => d.EntityAdminAssignments.AnyAsync(x => x.EntityId == w.A1)));
        // in A: a subset of what the lead holds is granted; anything beyond it (ViewReports) is refused
        var peer = "peer-" + w.S.ToLower();
        await UserIn(w.Central, peer, w.A);
        Assert.True(Denied(await Assign(l, w.A, peer, false, Capability.ManageUsers, Capability.ViewReports)));
        Assert.False(await app.Db(d => d.EntityAdminAssignments.AnyAsync(x => x.User.NormalizedUsername == peer)));
        (await Assign(l, w.A, peer, false, Capability.ManageUsers)).EnsureRedirect();
        Assert.Equal(Capability.ManageUsers, await app.Db(d => d.EntityAdminAssignments.Where(x => x.User.NormalizedUsername == peer).Select(x => x.Capabilities).SingleAsync()));
        // a user from another entity -> not found in scope (no assignment created)
        (await Assign(l, w.A, outsider, false, Capability.ManageUsers)).EnsureRedirect();
        Assert.False(await app.Db(d => d.EntityAdminAssignments.AnyAsync(x => x.User.NormalizedUsername == outsider.ToLowerInvariant())));
        // nobody edits their own delegation; another entity is invisible
        var own = await app.Db(d => d.EntityAdminAssignments.Where(x => x.User.NormalizedUsername == lead.ToLowerInvariant()).Select(x => x.Id).SingleAsync());
        Assert.True(Denied(await l.PostForm($"/Admin/Entities/Details/{w.A}?tab=admins", $"/Admin/Entities/UpdateAdmin/{w.A}", [F("assignmentId", own.ToString()), F("caps", "ManageUsers"), F("caps", "ManageAdmins"), F("caps", "ViewReports")])));
        Assert.Equal(HttpStatusCode.NotFound, (await l.GetAsync($"/Admin/Entities/Details/{w.B}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await l.PostForm($"/Admin/Entities/Details/{w.A}?tab=admins", $"/Admin/Entities/AssignAdmin/{w.B}", [F("username", lead), F("caps", "ManageUsers")])).StatusCode);
    }

    // ------------------------------------------------------------------------------------------------ isolation

    [Fact]
    public async Task Entity_administrators_are_isolated_from_other_entities_including_direct_urls()
    {
        var w = await WorldAsync();
        var s = w.S.ToLower();
        string admA = "adA-" + s, admB = "adB-" + s, empA = "eA-" + s, empB = "eB-" + s;
        await UserIn(w.Central, admA, w.A); await UserIn(w.Central, admB, w.B);
        var empAId = await UserIn(w.Central, empA, w.A); var empBId = await UserIn(w.Central, empB, w.B);
        Capability[] full = [Capability.ManageUsers, Capability.ManageContent, Capability.PublishContent, Capability.ManageAssessments, Capability.ManageAcknowledgments, Capability.ViewReports, Capability.ExportReports, Capability.ViewAudit];
        (await Assign(w.Central, w.A, admA, true, full)).EnsureRedirect();
        (await Assign(w.Central, w.B, admB, true, full)).EnsureRedirect();
        var a = await UserAsync(admA); var b = await UserAsync(admB);

        // users: list and every action on another entity's employee
        var list = await a.GetStringAsync("/Admin/Users");
        Assert.Contains(empA, list); Assert.DoesNotContain(empB, list);
        Assert.DoesNotContain(empB, await a.GetStringAsync($"/Admin/Users?entity={w.B}"));
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/Admin/Users/Edit/{empBId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.PostForm("/Admin/Users", $"/Admin/Users/Edit/{empBId}", [F("DisplayName", "pwned"), F("IsActive", "false"), F("EntityId", w.A.ToString())])).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.PostForm("/Admin/Users", $"/Admin/Users/ResetPassword/{empBId}", [F("newPassword", "Pwned#Pass12345")])).StatusCode);
        Assert.Equal("موظف " + empB, await app.Db(d => d.Users.Where(u => u.Id == empBId).Select(u => u.DisplayName).SingleAsync()));
        // cannot create a user in B, move one of its own users to B, or create a central administrator
        Assert.Equal(HttpStatusCode.OK, (await a.PostForm("/Admin/Users/Create", "/Admin/Users/Create", [F("Username", "x-" + s), F("DisplayName", "x"), F("AuthSource", "Local"), F("Password", UserPass), F("IsActive", "true"), F("EntityId", w.B.ToString())])).StatusCode);
        Assert.False(await app.Db(d => d.Users.AnyAsync(u => u.NormalizedUsername == "x-" + s)));
        Assert.Equal(HttpStatusCode.OK, (await a.PostForm("/Admin/Users", $"/Admin/Users/Edit/{empAId}", [F("DisplayName", "x"), F("IsActive", "true"), F("EntityId", w.B.ToString())])).StatusCode);
        Assert.Equal(w.A, await app.Db(d => d.Users.Where(u => u.Id == empAId).Select(u => u.EntityId).SingleAsync()));
        Assert.True(Denied(await a.PostForm("/Admin/Users/Create", "/Admin/Users/Create", [F("Username", "y-" + s), F("DisplayName", "y"), F("AuthSource", "Local"), F("Password", UserPass), F("IsActive", "true"), F("EntityId", w.A.ToString()), F("IsAdmin", "true")])));
        // a delegated administrator cannot take over another administrator's account
        var admBId = await app.Db(d => d.Users.Where(u => u.NormalizedUsername == admB.ToLowerInvariant()).Select(u => u.Id).SingleAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/Admin/Users/Edit/{admBId}")).StatusCode);

        // content of B and central content are out of reach for A's administrator
        var cB = await ContentAsync(b, "محتوى محلي ب " + s, w.B, ContentTargetScope.OwnerOnly, ack: true);
        var central = await ContentAsync(w.Central, "محتوى مركزي " + s, w.Root, ContentTargetScope.OwnerAndDescendants);
        foreach (var id in new[] { cB, central })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/Admin/Authoring/Basics/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/Admin/Content/Edit/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await a.PostForm("/Admin/Content", $"/Admin/Content/SetStatus/{id}", [F("publish", "false")])).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await a.PostForm("/Admin/Content", $"/Admin/Content/Delete/{id}", [])).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await a.PostForm($"/Admin/Authoring/New", $"/Admin/Authoring/Publish/{id}", [F("go", "unpublish")])).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await a.PostForm("/Admin/Content", $"/Admin/Content/ResetAcknowledgments/{id}", [])).StatusCode);
        }
        Assert.Equal(ContentStatus.Published, await app.Db(d => d.Contents.Where(c => c.Id == central).Select(c => c.Status).SingleAsync()));
        var aList = await a.GetStringAsync("/Admin/Content");
        Assert.DoesNotContain("محتوى محلي ب " + s, aList); Assert.DoesNotContain("محتوى مركزي " + s, aList);
        // assessments of B
        (await b.PostForm("/Admin/Assessments/Create", "/Admin/Assessments/Create", [F("Title", "اختبار ب " + s), F("PassingPercentage", "50"), F("MaxAttempts", "1")])).EnsureRedirect();
        var asmB = await app.Db(d => d.Assessments.Where(x => x.Title == "اختبار ب " + s).Select(x => x.Id).SingleAsync());
        Assert.Equal(w.B, await app.Db(d => d.Assessments.Where(x => x.Id == asmB).Select(x => x.OwnerEntityId).SingleAsync()));
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/Admin/Assessments/Questions/{asmB}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.PostForm("/Admin/Assessments", $"/Admin/Assessments/Delete/{asmB}", [])).StatusCode);
        Assert.DoesNotContain("اختبار ب " + s, await a.GetStringAsync("/Admin/Assessments"));
        // entities, branding, directory and audit of B
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/Admin/Entities/Details/{w.B}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync($"/Admin/Entities/Details/{w.A1}")).StatusCode);   // own subtree
        Assert.DoesNotContain("هيئة ب " + w.S, await a.GetStringAsync("/Admin/Entities"));
        Assert.True(Denied(await a.PostForm($"/Admin/Entities/Details/{w.A}", $"/Admin/Entities/SaveDirectory/{w.A}", [F("Enabled", "true"), F("Domain", "AGENCY")])));
        var auditA = await a.GetStringAsync("/Admin/Audit");
        Assert.DoesNotContain("محتوى محلي ب " + s, auditA);
        Assert.Contains("محتوى محلي ب " + s, await w.Central.GetStringAsync("/Admin/Audit"));   // the central administrator sees everything

        // reports and exports: only A's employees, also when B is requested explicitly
        var ackReport = await a.GetStringAsync("/Admin/Reports/Acknowledgments");
        Assert.DoesNotContain(empB, ackReport);
        var csv = await (await a.GetAsync("/Admin/Reports/Assessments?export=csv")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(empB, csv);
        Assert.DoesNotContain(empB, await a.GetStringAsync($"/Admin/Reports/Acknowledgments?entityId={w.B}"));
        Assert.True(Denied(await a.GetAsync($"/Admin/Reports/Acknowledgments?entityId={w.B}&export=csv")));
        var centralCsv = await (await w.Central.GetAsync("/Admin/Reports/Acknowledgments?export=csv")).Content.ReadAsStringAsync();
        Assert.Contains(empB, centralCsv);
    }

    // ------------------------------------------------------------------------------------------------ targeting

    [Fact]
    public async Task Central_content_reaches_only_the_targeted_entities_and_local_content_stays_local()
    {
        var w = await WorldAsync();
        var s = w.S.ToLower();
        string eRoot = "er-" + s, eA = "ea-" + s, eA1 = "ea1-" + s, eB = "eb-" + s, admA = "ada-" + s;
        foreach (var (u, e) in new[] { (eRoot, w.Root), (eA, w.A), (eA1, w.A1), (eB, w.B), (admA, w.A) }) await UserIn(w.Central, u, e);
        var rootOnly = await ContentAsync(w.Central, "جذر فقط " + s, w.Root, ContentTargetScope.OwnerOnly);
        var all = await ContentAsync(w.Central, "لكل الجهات " + s, w.Root, ContentTargetScope.OwnerAndDescendants);
        var selA = await ContentAsync(w.Central, "لهيئة أ وتوابعها " + s, w.Root, ContentTargetScope.Selected, [w.A]);
        var selAOnly = await ContentAsync(w.Central, "لهيئة أ فقط " + s, w.Root, ContentTargetScope.Selected, [w.A], desc: false);
        var selAB = await ContentAsync(w.Central, "لهيئتي أ وب " + s, w.Root, ContentTargetScope.Selected, [w.A1, w.B], desc: false);
        // one content record per item, never a copy per entity
        Assert.Equal(1, await app.Db(d => d.Contents.CountAsync(c => c.Title == "لهيئتي أ وب " + s)));
        Assert.Equal(2, await app.Db(d => d.ContentEntityTargets.CountAsync(t => t.ContentId == selAB)));

        (await Assign(w.Central, w.A, admA, true, Capability.ManageContent, Capability.PublishContent)).EnsureRedirect();
        var adminA = await UserAsync(admA);
        var localA = await ContentAsync(adminA, "محلي أ " + s, w.A, ContentTargetScope.OwnerOnly);
        var localATree = await ContentAsync(adminA, "محلي أ مع التوابع " + s, w.A, ContentTargetScope.OwnerAndDescendants);
        Assert.Equal(w.A, await app.Db(d => d.Contents.Where(c => c.Id == localA).Select(c => c.OwnerEntityId).SingleAsync()));
        // an entity administrator cannot target outside the scope (B) or own content outside it
        Assert.Equal(HttpStatusCode.OK, (await adminA.PostForm("/Admin/Authoring/New", "/Admin/Authoring/Basics", [F("Title", "تجاوز " + s), F("Type", "6"), F("OwnerEntityId", w.A.ToString()), F("Audience", "2"), F("TargetIds", w.B.ToString()), F("go", "next")])).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await adminA.PostForm("/Admin/Authoring/New", "/Admin/Authoring/Basics", [F("Title", "تجاوز2 " + s), F("Type", "6"), F("OwnerEntityId", w.Root.ToString()), F("Audience", "1"), F("go", "next")])).StatusCode);
        Assert.False(await app.Db(d => d.Contents.AnyAsync(c => c.Title.StartsWith("تجاوز"))));

        var expect = new Dictionary<string, int[]>
        {
            [eRoot] = [rootOnly, all],
            [eA] = [all, selA, selAOnly, localA, localATree],
            [eA1] = [all, selA, selAB, localATree],
            [eB] = [all, selAB],
        };
        var every = new[] { rootOnly, all, selA, selAOnly, selAB, localA, localATree };
        foreach (var (user, visible) in expect)
        {
            var c = await UserAsync(user);
            var dashboard = await c.GetStringAsync("/Content");
            foreach (var id in every)
            {
                var r = await c.GetAsync($"/Content/Details/{id}");
                Assert.True(visible.Contains(id) ? r.StatusCode == HttpStatusCode.OK : r.StatusCode == HttpStatusCode.NotFound, $"{user} content {id}: {r.StatusCode}");
                Assert.Equal(visible.Contains(id), dashboard.Contains($"/Content/Details/{id}\""));
            }
        }
        var audit = await app.Db(d => d.AuditLogs.Where(a => a.Action == "CONTENT_TARGETING_CHANGED" && a.EntityId == selAB.ToString()).Select(a => a.Details).SingleAsync());
        Assert.Contains($"targets={Math.Min(w.A1, w.B)},{Math.Max(w.A1, w.B)}", audit);
    }

    [Fact]
    public async Task Linked_and_standalone_assessments_follow_the_audience()
    {
        var w = await WorldAsync();
        var s = w.S.ToLower();
        string eA = "qa-" + s, eB = "qb-" + s, admB = "qadb-" + s;
        await UserIn(w.Central, eA, w.A); await UserIn(w.Central, eB, w.B); await UserIn(w.Central, admB, w.B);
        (await Assign(w.Central, w.B, admB, true, Capability.ManageAssessments, Capability.PublishContent)).EnsureRedirect();
        var b = await UserAsync(admB);
        (await b.PostForm("/Admin/Assessments/Create", "/Admin/Assessments/Create", [F("Title", "اختبار مستقل ب " + s), F("PassingPercentage", "50"), F("MaxAttempts", "0")])).EnsureRedirect();
        var asm = await app.Db(d => d.Assessments.Where(x => x.Title == "اختبار مستقل ب " + s).Select(x => x.Id).SingleAsync());
        (await b.PostForm($"/Admin/Assessments/AddQuestion/{asm}", $"/Admin/Assessments/AddQuestion/{asm}", [F("Text", "سؤال"), F("Type", "1"), F("Points", "1"), F("Options[0]", "أ"), F("Options[1]", "ب"), F("Correct", "0")])).EnsureRedirect();
        (await b.PostForm("/Admin/Assessments", $"/Admin/Assessments/SetStatus/{asm}", [F("publish", "true")])).EnsureRedirect();
        var cb = await UserAsync(eB); var ca = await UserAsync(eA);
        Assert.Contains("اختبار مستقل ب " + s, await cb.GetStringAsync("/Assessments"));
        Assert.DoesNotContain("اختبار مستقل ب " + s, await ca.GetStringAsync("/Assessments"));
        Assert.Equal(HttpStatusCode.NotFound, (await ca.PostForm("/Assessments", $"/Assessments/Start/{asm}", [])).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await cb.PostForm("/Assessments", $"/Assessments/Start/{asm}", [])).StatusCode);
    }

    // ------------------------------------------------------------------------------------------------ reports

    [Fact]
    public async Task Entity_comparison_aggregates_subtrees_and_respects_scope()
    {
        var w = await WorldAsync();
        var s = w.S.ToLower();
        string eA1 = "ca1-" + s, eB = "cb-" + s, admA = "cada-" + s;
        await UserIn(w.Central, eA1, w.A1); await UserIn(w.Central, eB, w.B); await UserIn(w.Central, admA, w.A);
        var ack = await ContentAsync(w.Central, "إقرار للجميع " + s, w.Root, ContentTargetScope.OwnerAndDescendants, ack: true);
        var ua = await UserAsync(eA1);
        (await ua.PostForm($"/Content/Details/{ack}", $"/Content/Acknowledge/{ack}", [])).EnsureRedirect();
        var central = Regex.Match(await w.Central.GetStringAsync("/Admin/Reports/Entities"), "<tbody>.*?</tbody>", RegexOptions.Singleline).Value;
        Assert.Contains("هيئة أ " + w.S, central); Assert.Contains("هيئة ب " + w.S, central);
        static string Table(string html) => Regex.Match(html, "<tbody>.*?</tbody>", RegexOptions.Singleline).Value;
        var drillA = Table(await w.Central.GetStringAsync($"/Admin/Reports/Entities?entityId={w.A}"));
        Assert.Contains("إدارة أ1 " + w.S, drillA); Assert.DoesNotContain("هيئة ب " + w.S, drillA);
        Assert.Contains("100%", drillA);   // A1's only employee acknowledged the only requirement
        (await Assign(w.Central, w.A, admA, true, Capability.ViewReports)).EnsureRedirect();
        var mine = Table(await (await UserAsync(admA)).GetStringAsync("/Admin/Reports/Entities"));
        Assert.Contains("هيئة أ " + w.S, mine); Assert.DoesNotContain("هيئة ب " + w.S, mine);
    }

    // ------------------------------------------------------------------------------------------------ branding

    [Fact]
    public async Task Entity_branding_is_inherited_overridden_and_permission_controlled()
    {
        var w = await WorldAsync();
        var s = w.S.ToLower();
        string eA = "ba-" + s, eA1 = "ba1-" + s, eB = "bb-" + s, admA = "bada-" + s, admB = "badb-" + s;
        foreach (var (u, e) in new[] { (eA, w.A), (eA1, w.A1), (eB, w.B), (admA, w.A), (admB, w.B) }) await UserIn(w.Central, u, e);
        // central override for A (allowed or not, the central administrator decides)
        var save = await w.Central.PostMultipart($"/Admin/EntityBranding/Index/{w.A}", $"/Admin/EntityBranding/Save/{w.A}", mp =>
        {
            mp.Add(new StringContent("#7a1f3d"), "PrimaryColor"); mp.Add(new StringContent("نظام الهيئة أ"), "SystemName");
            mp.AddFile("logo", "a.png", Png, "image/png");
        });
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        var rootPrimary = Regex.Match(await app.NewClient().GetStringAsync("/branding/theme.css"), "--color-primary:(#[0-9a-f]{6})").Groups[1].Value;
        Assert.Contains("--color-primary:#7a1f3d", await (await UserAsync(eA)).GetStringAsync("/branding/theme.css"));
        var a1 = await UserAsync(eA1);
        Assert.Contains("--color-primary:#7a1f3d", await a1.GetStringAsync("/branding/theme.css"));          // inherited by the sub-entity
        Assert.Contains("نظام الهيئة أ", await a1.GetStringAsync("/"));
        var cb = await UserAsync(eB);
        Assert.Contains("--color-primary:" + rootPrimary, await cb.GetStringAsync("/branding/theme.css"));    // unrelated entity unaffected
        Assert.DoesNotContain("نظام الهيئة أ", await cb.GetStringAsync("/"));
        Assert.DoesNotContain("نظام الهيئة أ", await app.NewClient().GetStringAsync("/Account/Login"));       // login page stays central
        // the entity logo: its own subtree only
        Assert.Contains($"kind=logo&amp;e={w.A}", await a1.GetStringAsync("/"));
        Assert.Equal(HttpStatusCode.OK, (await a1.GetAsync($"/Files/Brand?kind=logo&e={w.A}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cb.GetAsync($"/Files/Brand?kind=logo&e={w.A}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await app.NewClient().GetAsync($"/Files/Brand?kind=logo&e={w.A}")).StatusCode);

        // local branding permission: refused while the entity does not allow it, possible once allowed + granted
        (await Assign(w.Central, w.A, admA, true, Capability.ManageUsers)).EnsureRedirect();
        var la = await UserAsync(admA);
        Assert.True(Denied(await la.GetAsync($"/Admin/EntityBranding/Index/{w.A}")));
        Assert.True(Denied(await la.PostMultipart("/Admin/Users", $"/Admin/EntityBranding/Save/{w.A}", mp => mp.Add(new StringContent("#000000"), "PrimaryColor"))));
        (await w.Central.PostForm($"/Admin/Entities/Edit/{w.A}", $"/Admin/Entities/Edit/{w.A}", [F("Name", "هيئة أ " + w.S), F("Code", "AGA-" + w.S), F("ParentEntityId", w.Root.ToString()), F("IsActive", "true"),
            F("Allowed", "ManageUsers"), F("Allowed", "ManageBranding")])).EnsureRedirect();
        (await Assign(w.Central, w.A, admA, true, Capability.ManageUsers, Capability.ManageBranding)).EnsureRedirect();
        Assert.Equal(HttpStatusCode.OK, (await la.GetAsync($"/Admin/EntityBranding/Index/{w.A}")).StatusCode);
        (await la.PostMultipart($"/Admin/EntityBranding/Index/{w.A}", $"/Admin/EntityBranding/Save/{w.A}", mp => mp.Add(new StringContent("#123456"), "AccentColor"))).EnsureRedirect();
        Assert.Contains("--color-accent:#123456", await a1.GetStringAsync("/branding/theme.css"));
        // ... but never for another entity, and never invalid values
        (await Assign(w.Central, w.B, admB, true, Capability.ManageUsers)).EnsureRedirect();
        Assert.Equal(HttpStatusCode.NotFound, (await la.GetAsync($"/Admin/EntityBranding/Index/{w.B}")).StatusCode);
        Assert.True(Denied(await (await UserAsync(admB)).GetAsync($"/Admin/EntityBranding/Index/{w.A}")));
        (await la.PostMultipart($"/Admin/EntityBranding/Index/{w.A}", $"/Admin/EntityBranding/Save/{w.A}", mp => mp.Add(new StringContent("red;}body{x"), "AccentColor"))).EnsureRedirect();
        Assert.Contains("--color-accent:#123456", await a1.GetStringAsync("/branding/theme.css"));
        // reset -> full inheritance again
        (await w.Central.PostForm($"/Admin/EntityBranding/Index/{w.A}", $"/Admin/EntityBranding/Reset/{w.A}", [])).EnsureRedirect();
        Assert.Contains("--color-primary:" + rootPrimary, await a1.GetStringAsync("/branding/theme.css"));
        var actions = await app.Db(d => d.AuditLogs.Where(x => x.ScopeEntityId == w.A).Select(x => x.Action).ToListAsync());
        Assert.Contains("ENTITY_BRANDING_UPDATED", actions); Assert.Contains("ENTITY_BRANDING_RESET", actions);
    }

    // ------------------------------------------------------------------------------------------------ directory

    [Fact]
    public async Task Directory_configuration_needs_permission_and_never_exposes_the_secret()
    {
        var w = await WorldAsync();
        var s = w.S.ToLower();
        const string secret = "Sv©-Directory#Secret-42";
        string admA = "dada-" + s;
        await UserIn(w.Central, admA, w.A);
        (await Assign(w.Central, w.A, admA, true, Capability.ManageUsers)).EnsureRedirect();
        var la = await UserAsync(admA);
        Assert.True(Denied(await la.PostForm($"/Admin/Entities/Details/{w.A}?tab=directory", $"/Admin/Entities/SaveDirectory/{w.A}", [F("Enabled", "true"), F("Domain", "AGA" + w.S[..3])])));
        Assert.DoesNotContain("SaveDirectory", await la.GetStringAsync($"/Admin/Entities/Details/{w.A}?tab=directory"));
        // central configures it
        (await w.Central.PostForm($"/Admin/Entities/Details/{w.A}?tab=directory", $"/Admin/Entities/SaveDirectory/{w.A}",
            [F("Enabled", "true"), F("Domain", "aga" + s[..3]), F("LdapPath", "LDAP://dc01.agency.local"), F("BindUser", @"AGA\svc"), F("BindSecret", secret)])).EnsureRedirect();
        var d = await app.Db(x => x.EntityDirectorySources.AsNoTracking().SingleAsync(y => y.EntityId == w.A));
        Assert.Equal("AGA" + w.S[..3], d.Domain);
        Assert.NotNull(d.BindSecretProtected); Assert.DoesNotContain(secret, d.BindSecretProtected);
        var page = await w.Central.GetStringAsync($"/Admin/Entities/Details/{w.A}?tab=directory");
        Assert.DoesNotContain(secret, page); Assert.DoesNotContain(d.BindSecretProtected!, page);
        var details = await app.Db(x => x.AuditLogs.Where(a => a.Action == "DIRECTORY_CONFIG_CHANGED" && a.ScopeEntityId == w.A).Select(a => a.Details).SingleAsync());
        Assert.DoesNotContain(secret, details); Assert.Contains("replaced", details);
        // saving without a password keeps it; the same domain cannot be mapped twice
        (await w.Central.PostForm($"/Admin/Entities/Details/{w.A}?tab=directory", $"/Admin/Entities/SaveDirectory/{w.A}", [F("Enabled", "true"), F("Domain", "AGA" + w.S[..3])])).EnsureRedirect();
        Assert.Equal(d.BindSecretProtected, await app.Db(x => x.EntityDirectorySources.Where(y => y.EntityId == w.A).Select(y => y.BindSecretProtected).SingleAsync()));
        (await w.Central.PostForm($"/Admin/Entities/Details/{w.B}?tab=directory", $"/Admin/Entities/SaveDirectory/{w.B}", [F("Enabled", "true"), F("Domain", "AGA" + w.S[..3])])).EnsureRedirect();
        Assert.False(await app.Db(x => x.EntityDirectorySources.AnyAsync(y => y.EntityId == w.B)));
    }

    // ------------------------------------------------------------------------------------------------ lifecycle / audit

    [Fact]
    public async Task Disabling_an_entity_blocks_its_subtree_and_audit_records_actor_and_scope()
    {
        var w = await WorldAsync();
        var s = w.S.ToLower();
        string eA1 = "da1-" + s, eB = "db-" + s;
        await UserIn(w.Central, eA1, w.A1); await UserIn(w.Central, eB, w.B);
        var b = await UserAsync(eB);
        (await w.Central.PostForm($"/Admin/Entities/Edit/{w.A}", $"/Admin/Entities/Edit/{w.A}", [F("Name", "هيئة أ " + w.S), F("Code", "AGA-" + w.S), F("ParentEntityId", w.Root.ToString()), F("IsActive", "false"), F("Allowed", "ManageUsers")])).EnsureRedirect();
        // users of the disabled entity's subtree can no longer sign in; others are unaffected
        var r = await app.NewClient().PostForm("/Account/Login", "/Account/Login", [F("Username", eA1), F("Password", UserPass)]);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await b.GetAsync("/")).StatusCode);
        var row = await app.Db(x => x.AuditLogs.Where(a => a.Action == "ENTITY_DISABLED" && a.ScopeEntityId == w.A).SingleAsync());
        Assert.Equal(w.Root, row.ActorEntityId);
        Assert.Equal("admin", row.Username);
    }
}
