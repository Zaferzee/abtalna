using CyberLms.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace CyberLms.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Content> Contents => Set<Content>();
    public DbSet<ContentAttachment> ContentAttachments => Set<ContentAttachment>();
    public DbSet<UserAcknowledgment> UserAcknowledgments => Set<UserAcknowledgment>();
    public DbSet<ContentCompletion> ContentCompletions => Set<ContentCompletion>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
    public DbSet<AssessmentAttempt> AssessmentAttempts => Set<AssessmentAttempt>();
    public DbSet<AssessmentAnswer> AssessmentAnswers => Set<AssessmentAnswer>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<OrgEntity> Entities => Set<OrgEntity>();
    public DbSet<EntityAdminAssignment> EntityAdminAssignments => Set<EntityAdminAssignment>();
    public DbSet<ContentEntityTarget> ContentEntityTargets => Set<ContentEntityTarget>();
    public DbSet<EntitySetting> EntitySettings => Set<EntitySetting>();
    public DbSet<EntityDirectorySource> EntityDirectorySources => Set<EntityDirectorySource>();

    /// <summary>
    /// Safety net for code paths that do not choose an entity (tests, tools, legacy callers): new users, content and assessments
    /// without an entity are attached to the root entity, never left unscoped. Content defaults to "owner only" (least visible).
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var added = ChangeTracker.Entries().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToList();
        if (added.Any(x => x is User { EntityId: 0 } or Content { OwnerEntityId: 0 } or Assessment { OwnerEntityId: 0 }))
        {
            var root = await Entities.Where(e => e.ParentEntityId == null).Select(e => e.Id).FirstAsync(cancellationToken);
            foreach (var x in added)
                switch (x)
                {
                    case User { EntityId: 0 } u: u.EntityId = root; break;
                    case Content { OwnerEntityId: 0 } c: c.OwnerEntityId = root; break;
                    case Assessment { OwnerEntityId: 0 } a:
                        a.OwnerEntityId = a.ContentId is int cid ? await Contents.Where(c => c.Id == cid).Select(c => c.OwnerEntityId).FirstOrDefaultAsync(cancellationToken) : root;
                        if (a.OwnerEntityId == 0) a.OwnerEntityId = root;
                        break;
                }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.NormalizedUsername).IsUnique();
            e.HasIndex(x => x.ExternalId);
            e.Property(x => x.Username).HasMaxLength(256);
            e.Property(x => x.NormalizedUsername).HasMaxLength(256);
            e.Property(x => x.DisplayName).HasMaxLength(256);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Department).HasMaxLength(256);
            e.Property(x => x.AuthSource).HasMaxLength(32);
            e.HasOne(x => x.Entity).WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.EntityId);
        });
        b.Entity<Role>(e => e.HasIndex(x => x.Name).IsUnique());
        b.Entity<UserRole>(e =>
        {
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.User).WithMany(u => u.UserRoles).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Role).WithMany(r => r.UserRoles).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Content>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.ExternalUrl).HasMaxLength(1000);
            e.Property(x => x.AcknowledgmentText).HasMaxLength(1000);
            e.HasIndex(x => new { x.Status, x.Type });
            e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.OwnerEntity).WithMany().HasForeignKey(x => x.OwnerEntityId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.OwnerEntityId);
        });
        b.Entity<ContentAttachment>(e =>
        {
            e.HasOne(x => x.Content).WithMany(c => c.Attachments).HasForeignKey(x => x.ContentId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.FileName).HasMaxLength(300);
            e.Property(x => x.StoredPath).HasMaxLength(500);
            e.Property(x => x.ContentType).HasMaxLength(150);
        });
        b.Entity<UserAcknowledgment>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.ContentId, x.ContentVersion }).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Content).WithMany().HasForeignKey(x => x.ContentId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Status).HasMaxLength(32);
        });
        b.Entity<ContentCompletion>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.ContentId, x.ContentVersion }).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Content).WithMany().HasForeignKey(x => x.ContentId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Assessment>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.HasOne(x => x.Content).WithMany().HasForeignKey(x => x.ContentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.OwnerEntity).WithMany().HasForeignKey(x => x.OwnerEntityId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("ck_assessment_pass", "\"PassingPercentage\" BETWEEN 0 AND 100"));
        });
        b.Entity<Question>(e =>
        {
            e.HasOne(x => x.Assessment).WithMany(a => a.Questions).HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Text).HasMaxLength(2000);
            e.HasIndex(x => new { x.AssessmentId, x.SortOrder });
        });
        b.Entity<QuestionOption>(e =>
        {
            e.HasOne(x => x.Question).WithMany(q => q.Options).HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Text).HasMaxLength(1000);
        });
        b.Entity<AssessmentAttempt>(e =>
        {
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Assessment).WithMany().HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.AssessmentId, x.UserId });
            e.Property(x => x.Percentage).HasPrecision(5, 2);
        });
        b.Entity<AssessmentAnswer>(e =>
        {
            e.HasOne(x => x.Attempt).WithMany(a => a.Answers).HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Cascade);
            // Historical answers must survive; questions with answers cannot be deleted.
            e.HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.AttemptId, x.QuestionId }).IsUnique();
        });
        b.Entity<AuditLog>(e =>
        {
            e.HasIndex(x => x.Timestamp);
            e.Property(x => x.Username).HasMaxLength(256);
            e.Property(x => x.Action).HasMaxLength(100);
            e.Property(x => x.EntityType).HasMaxLength(100);
            e.Property(x => x.EntityId).HasMaxLength(100);
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.HasIndex(x => x.ScopeEntityId);
        });
        b.Entity<OrgEntity>(e =>
        {
            e.ToTable("Entities");
            e.Property(x => x.Name).HasMaxLength(300);
            e.Property(x => x.Code).HasMaxLength(50);
            e.Property(x => x.Path).HasMaxLength(1000);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.Path);
            e.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentEntityId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.ParentEntityId);
            // exactly one root: enforced by the unique expression index "IX_Entities_SingleRoot" created in the migration (raw SQL;
            // a unique index on the nullable column itself would not work because NULLs are distinct)
            e.ToTable(t => t.HasCheckConstraint("ck_entity_not_own_parent", "\"ParentEntityId\" IS NULL OR \"ParentEntityId\" <> \"Id\""));
        });
        b.Entity<EntityAdminAssignment>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.EntityId }).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Entity).WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<ContentEntityTarget>(e =>
        {
            e.HasKey(x => new { x.ContentId, x.EntityId });
            e.HasOne(x => x.Content).WithMany(c => c.Targets).HasForeignKey(x => x.ContentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Entity).WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.EntityId);
        });
        b.Entity<EntitySetting>(e =>
        {
            e.HasKey(x => new { x.EntityId, x.Key });
            e.Property(x => x.Key).HasMaxLength(150);
            e.HasOne<OrgEntity>().WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<EntityDirectorySource>(e =>
        {
            e.HasKey(x => x.EntityId);
            e.HasOne(x => x.Entity).WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Domain).HasMaxLength(100);
            e.HasIndex(x => x.Domain);
            e.Property(x => x.LdapPath).HasMaxLength(500);
            e.Property(x => x.SearchBase).HasMaxLength(500);
            e.Property(x => x.BindUser).HasMaxLength(256);
            e.Property(x => x.ValidationNote).HasMaxLength(1000);
        });
        b.Entity<SystemSetting>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(150);
        });
    }
}
