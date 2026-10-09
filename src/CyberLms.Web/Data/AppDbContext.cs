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
        });
        b.Entity<SystemSetting>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(150);
        });
    }
}
