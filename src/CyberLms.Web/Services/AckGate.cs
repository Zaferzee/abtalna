using CyberLms.Web.Data;
using CyberLms.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace CyberLms.Web.Services;

/// <summary>
/// Mandatory acknowledgment gate: an assessment linked to content that requires acknowledgment stays locked for a user until
/// that user has acknowledged the content (current version). Enforced on the server for every assessment endpoint; the views
/// only reflect it.
/// </summary>
public static class AckGate
{
    /// <summary>Of the given content items, those that require acknowledgment and that the user has not acknowledged yet.</summary>
    public static async Task<HashSet<int>> PendingContentIds(AppDbContext db, int userId, IEnumerable<int?> contentIds)
    {
        var ids = contentIds.Where(i => i != null).Select(i => i!.Value).Distinct().ToList();
        if (ids.Count == 0) return [];
        return (await db.Contents.AsNoTracking()
            .Where(c => ids.Contains(c.Id) && c.RequiresAcknowledgment &&
                        !db.UserAcknowledgments.Any(a => a.UserId == userId && a.ContentId == c.Id && a.ContentVersion == c.Version))
            .Select(c => c.Id).ToListAsync()).ToHashSet();
    }

    /// <summary>The content the user must acknowledge before using this assessment, or null when the assessment is open to them.</summary>
    public static async Task<Content?> BlockingContent(AppDbContext db, int userId, Assessment assessment)
    {
        if (assessment.ContentId == null) return null;
        return await db.Contents.AsNoTracking().FirstOrDefaultAsync(c => c.Id == assessment.ContentId && c.RequiresAcknowledgment &&
            !db.UserAcknowledgments.Any(a => a.UserId == userId && a.ContentId == c.Id && a.ContentVersion == c.Version));
    }
}
