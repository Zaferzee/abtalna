using System.Text.Json;
using System.Text.Json.Serialization;
using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace CyberLms.Web.Services;

/// <summary>
/// Optional starter cybersecurity-awareness content pack (12 learning items with assessments + 4 policy templates). It is installed
/// only on explicit request (configuration <c>Seed:StarterContentPack=true</c> or the console flag <c>--install-starter-pack</c>),
/// through the normal content model: bodies go through the content sanitizer, everything is owned by the root entity, and
/// nothing existing is changed or deleted. Idempotent: the <see cref="MarkerKey"/> setting records the installation, and items
/// already installed (recorded per content id) are never created twice. No schema change: governance metadata (framework and
/// control reference, audience) is kept in settings and shown to administrators only.
/// </summary>
public static class StarterContentPack
{
    public const string MarkerKey = "StarterContentPack.v1.Installed";
    public const string ItemPrefix = "StarterContentPack.v1.Item.";
    public const string DemoMarkerKey = "StarterContentPack.v1.DemoData";
    private const string Resource = "CyberLms.Web.Data.StarterPack.starter-content-pack.v1.json";
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };

    // ---------------------------------------------------------------- pack file (embedded in the application assembly)

    public record PackOption(string Text, bool Correct);
    public record PackQuestion(string Text, QuestionType Type, string Kind, List<PackOption> Options, string Explanation);
    public record PackAssessment(string Title, string Description, int PassingPercentage, int MaxAttempts, bool Published, List<PackQuestion> Questions);
    public record PackItem(string Code, string Title, ContentType Type, ContentStatus Status, ContentTargetScope Scope, string Description, string Body,
        bool RequiresAcknowledgment, string? AcknowledgmentText, bool AcknowledgmentIsTemplate, string Framework, string Reference, string Audience,
        string Kind, int? Minutes, string Illustration, string? OfficialMediaSlot, PackAssessment? Assessment);
    public record Pack(string Name, int Version, string Notice, List<PackItem> Items);

    public static Pack Load()
    {
        using var s = typeof(StarterContentPack).Assembly.GetManifestResourceStream(Resource)
            ?? throw new InvalidOperationException("Starter content pack resource is missing from the build.");
        var doc = JsonDocument.Parse(s).RootElement;
        return new Pack(doc.GetProperty("pack").GetString()!, doc.GetProperty("version").GetInt32(), doc.GetProperty("notice").GetString()!,
            doc.GetProperty("items").Deserialize<List<PackItem>>(Json)!);
    }

    /// <summary>Administrator-facing metadata stored per installed content item.</summary>
    public record ItemInfo(string Code, string Framework, string Reference, string Audience, string Kind, bool Template, string? MediaSlot)
    {
        public string ReferenceLine => $"مرجع استرشادي: {Framework} — {Reference}";
    }

    /// <summary>Governance metadata of a starter-pack content item (null for any other content).</summary>
    public static ItemInfo? Info(this SettingsService settings, int contentId)
    {
        var v = settings.Get(ItemPrefix + contentId);
        if (v == null) return null;
        try { return JsonSerializer.Deserialize<ItemInfo>(v, Json); } catch (JsonException) { return null; }
    }

    // ---------------------------------------------------------------- install

    public record Result(bool Ok, bool Installed, int Contents, int Questions, string Message);

    public static async Task<Result> InstallAsync(IServiceProvider sp, ILogger log)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<SettingsService>();
        try
        {
            if (await db.SystemSettings.AnyAsync(s => s.Key == MarkerKey))
                return new(true, false, 0, 0, "Starter content pack v1 is already installed; nothing was changed.");
            var root = await db.Entities.AsNoTracking().FirstOrDefaultAsync(e => e.ParentEntityId == null);
            if (root == null) return new(false, false, 0, 0, "No root entity exists; apply the database migrations first.");

            var pack = Load();
            // Items recorded by an earlier (interrupted or marker-less) install are skipped while their content still exists.
            var recorded = (await db.SystemSettings.AsNoTracking().Where(s => s.Key.StartsWith(ItemPrefix)).ToListAsync())
                .Select(s => (Id: int.TryParse(s.Key[ItemPrefix.Length..], out var i) ? i : 0, Info: Parse(s.Value))).Where(x => x.Info != null).ToList();
            var existingIds = await db.Contents.Where(c => recorded.Select(r => r.Id).Contains(c.Id)).Select(c => c.Id).ToListAsync();
            var done = recorded.Where(r => existingIds.Contains(r.Id)).Select(r => r.Info!.Code).ToHashSet();

            await using var tx = await db.Database.BeginTransactionAsync();
            int contents = 0, questions = 0;
            foreach (var it in pack.Items.Where(i => !done.Contains(i.Code)))
            {
                var now = DateTime.UtcNow;
                var c = new Content
                {
                    Title = it.Title, Description = it.Description, Body = ContentAuthoring.CleanBody(it.Body), Type = it.Type, Status = it.Status,
                    PublishedAt = it.Status == ContentStatus.Published ? now : null, RequiresAcknowledgment = it.RequiresAcknowledgment,
                    AcknowledgmentText = it.AcknowledgmentText, OwnerEntityId = root.Id, TargetScope = it.Scope,
                };
                db.Contents.Add(c);
                await db.SaveChangesAsync();
                if (it.Assessment is { } a)
                {
                    var asm = new Assessment
                    {
                        Title = a.Title, Description = a.Description, ContentId = c.Id, OwnerEntityId = root.Id,
                        PassingPercentage = a.PassingPercentage, MaxAttempts = a.MaxAttempts, IsPublished = a.Published,
                    };
                    var order = 0;
                    foreach (var q in a.Questions)
                    {
                        var opt = 0;
                        asm.Questions.Add(new Question
                        {
                            Text = q.Text, Type = q.Type, Points = 1, SortOrder = ++order,
                            Options = q.Options.Select(o => new QuestionOption { Text = o.Text, IsCorrect = o.Correct, SortOrder = ++opt }).ToList(),
                        });
                    }
                    db.Assessments.Add(asm);
                    questions += asm.Questions.Count;
                }
                db.SystemSettings.Add(new SystemSetting
                {
                    Key = ItemPrefix + c.Id,
                    Value = JsonSerializer.Serialize(new ItemInfo(it.Code, it.Framework, it.Reference, it.Audience, it.Kind, it.AcknowledgmentIsTemplate, it.OfficialMediaSlot), Json),
                });
                contents++;
            }
            db.SystemSettings.Add(new SystemSetting { Key = MarkerKey, Value = JsonSerializer.Serialize(new { installedAt = DateTime.UtcNow, version = pack.Version, contents, questions }) });
            db.AuditLogs.Add(new AuditLog
            {
                Username = Res.Ar("System"), Action = "STARTER_PACK_INSTALLED", EntityType = "Content", ScopeEntityId = root.Id,
                Details = $"Starter content pack v{pack.Version}: {contents} content item(s), {questions} question(s)",
            });
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            settings.Invalidate();
            log.LogInformation("Starter content pack v{Version} installed: {Contents} content item(s), {Questions} question(s).", pack.Version, contents, questions);
            return new(true, true, contents, questions, $"Starter content pack v{pack.Version} installed: {contents} content item(s), {questions} question(s).");
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Starter content pack installation failed; nothing was installed.");
            return new(false, false, 0, 0, "Starter content pack installation failed: " + ex.Message);
        }
    }

    private static ItemInfo? Parse(string? v)
    {
        if (v == null) return null;
        try { return JsonSerializer.Deserialize<ItemInfo>(v, Json); } catch (JsonException) { return null; }
    }

    // ---------------------------------------------------------------- development-only demonstration data

    /// <summary>
    /// Development only: creates a few demonstration employees (no password, so they cannot sign in) and gives them
    /// acknowledgments and scored assessment attempts on the published starter items, so reports have something to show.
    /// Refused outside the Development environment, before the pack is installed, and when already done.
    /// </summary>
    public static async Task<Result> SeedDemoDataAsync(IServiceProvider sp, IHostEnvironment env, ILogger log)
    {
        if (!env.IsDevelopment()) return new(false, false, 0, 0, "Demo data is available in the Development environment only; nothing was changed.");
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<SettingsService>();
        if (!await db.SystemSettings.AnyAsync(s => s.Key == MarkerKey)) return new(false, false, 0, 0, "Install the starter content pack first (--install-starter-pack).");
        if (await db.SystemSettings.AnyAsync(s => s.Key == DemoMarkerKey)) return new(true, false, 0, 0, "Starter demo data already exists; nothing was changed.");

        var ids = (await db.SystemSettings.AsNoTracking().Where(s => s.Key.StartsWith(ItemPrefix)).Select(s => s.Key).ToListAsync())
            .Select(k => int.TryParse(k[ItemPrefix.Length..], out var i) ? i : 0).ToList();
        var assessments = await db.Assessments.Include(a => a.Questions).ThenInclude(q => q.Options).Include(a => a.Content)
            .Where(a => a.ContentId != null && ids.Contains(a.ContentId.Value) && a.IsPublished && a.Content!.Status == ContentStatus.Published)
            .OrderBy(a => a.Id).ToListAsync();
        var entities = await db.Entities.AsNoTracking().Where(e => e.IsActive).OrderBy(e => e.Depth).ThenBy(e => e.Id).Select(e => e.Id).ToListAsync();
        var userRole = await db.Roles.FirstAsync(r => r.Name == RoleNames.User);
        var rnd = new Random(2026);
        var names = new[] { "ريم العتيبي", "فهد القحطاني", "نوف الشهري", "سلطان الدوسري", "لمى الحربي", "ماجد الغامدي", "هيا المطيري", "تركي الزهراني" };
        int attempts = 0;
        for (var n = 0; n < names.Length; n++)
        {
            var uname = $"demo.employee{n + 1:00}";
            if (await db.Users.AnyAsync(u => u.NormalizedUsername == uname)) continue;
            var u = new User { Username = uname, NormalizedUsername = uname, DisplayName = names[n], AuthSource = "Local", PasswordHash = null, EntityId = entities[n % entities.Count], Department = "بيانات تجريبية" };
            u.UserRoles.Add(new UserRole { Role = userRole });
            db.Users.Add(u);
            await db.SaveChangesAsync();
            var skill = 0.55 + rnd.NextDouble() * 0.45;  // some users fail some assessments
            foreach (var a in assessments.Where(_ => rnd.NextDouble() < 0.75))
            {
                var when = DateTime.UtcNow.AddDays(-rnd.Next(1, 30)).AddMinutes(-rnd.Next(0, 600));
                if (a.Content!.RequiresAcknowledgment)
                    db.UserAcknowledgments.Add(new UserAcknowledgment { UserId = u.Id, ContentId = a.Content.Id, ContentVersion = a.Content.Version, AcknowledgedAt = when.AddMinutes(-5) });
                var picks = a.Questions.ToDictionary(q => q.Id, q =>
                {
                    var right = q.Options.Where(o => o.IsCorrect).ToList();
                    var wrong = q.Options.Where(o => !o.IsCorrect).ToList();
                    return new[] { (rnd.NextDouble() < skill || wrong.Count == 0 ? right[rnd.Next(right.Count)] : wrong[rnd.Next(wrong.Count)]).Id };
                });
                var s = Scoring.Score(a.Questions.OrderBy(q => q.SortOrder), picks, a.PassingPercentage);
                var att = new AssessmentAttempt
                {
                    UserId = u.Id, AssessmentId = a.Id, StartedAt = when.AddMinutes(-6), CompletedAt = when, Status = AttemptStatus.Completed,
                    TotalQuestions = s.TotalQuestions, CorrectAnswers = s.CorrectAnswers, TotalPoints = s.TotalPoints, EarnedPoints = s.EarnedPoints,
                    Percentage = s.Percentage, PassingPercentageSnapshot = a.PassingPercentage, Passed = s.Passed,
                };
                foreach (var r in s.Questions) att.Answers.Add(new AssessmentAnswer { QuestionId = r.QuestionId, SelectedOptionIds = r.Selected, IsCorrect = r.Correct, PointsAwarded = r.Points });
                db.AssessmentAttempts.Add(att);
                attempts++;
            }
        }
        db.SystemSettings.Add(new SystemSetting { Key = DemoMarkerKey, Value = JsonSerializer.Serialize(new { createdAt = DateTime.UtcNow, users = names.Length, attempts }) });
        db.AuditLogs.Add(new AuditLog { Username = Res.Ar("System"), Action = "STARTER_DEMO_DATA_CREATED", EntityType = "User", Details = $"Development demo data: {names.Length} user(s), {attempts} attempt(s)" });
        await db.SaveChangesAsync();
        settings.Invalidate();
        log.LogWarning("Development demo data created: {Users} demo user(s), {Attempts} attempt(s).", names.Length, attempts);
        return new(true, true, names.Length, attempts, $"Development demo data created: {names.Length} demo user(s) (no password), {attempts} scored attempt(s).");
    }
}
