using CyberLms.Web.Domain;
using CyberLms.Web.Services;

namespace CyberLms.Tests;

public class ScoringTests
{
    private static Question Q(int id, int points, QuestionType type, params (int id, bool correct)[] opts) => new()
    {
        Id = id, Points = points, Type = type,
        Options = opts.Select(o => new QuestionOption { Id = o.id, IsCorrect = o.correct }).ToList(),
    };

    [Fact]
    public void Single_choice_scores_by_points_and_passes_at_threshold()
    {
        var qs = new[] { Q(1, 1, QuestionType.SingleChoice, (10, true), (11, false)), Q(2, 3, QuestionType.TrueFalse, (20, false), (21, true)) };
        var r = Scoring.Score(qs, new Dictionary<int, int[]> { [1] = [10], [2] = [21] }, 70);
        Assert.Equal(2, r.CorrectAnswers); Assert.Equal(100m, r.Percentage); Assert.True(r.Passed);

        r = Scoring.Score(qs, new Dictionary<int, int[]> { [1] = [10], [2] = [20] }, 70);
        Assert.Equal(1, r.CorrectAnswers); Assert.Equal(25m, r.Percentage); Assert.False(r.Passed);
    }

    [Fact]
    public void Exactly_at_pass_mark_passes()
    {
        var qs = new[] { Q(1, 7, QuestionType.SingleChoice, (1, true), (2, false)), Q(2, 3, QuestionType.SingleChoice, (3, true), (4, false)) };
        var r = Scoring.Score(qs, new Dictionary<int, int[]> { [1] = [1], [2] = [4] }, 70);
        Assert.Equal(70m, r.Percentage); Assert.True(r.Passed);
    }

    [Fact]
    public void Multiple_choice_requires_exact_set()
    {
        var q = Q(1, 2, QuestionType.MultipleChoice, (1, true), (2, true), (3, false));
        Assert.True(Scoring.Score([q], new Dictionary<int, int[]> { [1] = [1, 2] }, 50).Passed);
        Assert.False(Scoring.Score([q], new Dictionary<int, int[]> { [1] = [1] }, 50).Passed);
        Assert.False(Scoring.Score([q], new Dictionary<int, int[]> { [1] = [1, 2, 3] }, 50).Passed);
    }

    [Fact]
    public void Unanswered_and_foreign_option_ids_score_zero()
    {
        var q = Q(1, 1, QuestionType.SingleChoice, (1, true), (2, false));
        Assert.Equal(0m, Scoring.Score([q], new Dictionary<int, int[]>(), 50).Percentage);
        Assert.Equal(0m, Scoring.Score([q], new Dictionary<int, int[]> { [1] = [999] }, 50).Percentage);
    }

    [Fact]
    public void Csv_neutralises_formula_injection()
    {
        Assert.Equal("'=cmd|' /C calc'!A0", Csv.Safe("=cmd|' /C calc'!A0"));
        Assert.Equal("normal", Csv.Safe("normal"));
    }
}
