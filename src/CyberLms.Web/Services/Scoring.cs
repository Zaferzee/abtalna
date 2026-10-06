using CyberLms.Web.Domain;

namespace CyberLms.Web.Services;

public record QuestionResult(int QuestionId, int[] Selected, bool Correct, int Points);
public record ScoreResult(int TotalQuestions, int CorrectAnswers, int TotalPoints, int EarnedPoints, decimal Percentage, bool Passed, List<QuestionResult> Questions);

public static class Scoring
{
    /// <summary>A question is correct only if the selected options exactly equal the set of correct options.</summary>
    public static ScoreResult Score(IEnumerable<Question> questions, IReadOnlyDictionary<int, int[]> selections, int passingPercentage)
    {
        var results = new List<QuestionResult>();
        int total = 0, earned = 0, correct = 0, count = 0;
        foreach (var q in questions)
        {
            count++;
            total += q.Points;
            var valid = q.Options.Select(o => o.Id).ToHashSet();
            var sel = (selections.TryGetValue(q.Id, out var s) ? s : Array.Empty<int>()).Where(valid.Contains).Distinct().ToArray();
            var right = q.Options.Where(o => o.IsCorrect).Select(o => o.Id).ToHashSet();
            var ok = sel.Length > 0 && right.SetEquals(sel);
            if (ok) { earned += q.Points; correct++; }
            results.Add(new QuestionResult(q.Id, sel, ok, ok ? q.Points : 0));
        }
        var pct = total == 0 ? 0m : Math.Round(earned * 100m / total, 2);
        return new ScoreResult(count, correct, total, earned, pct, pct >= passingPercentage, results);
    }
}
