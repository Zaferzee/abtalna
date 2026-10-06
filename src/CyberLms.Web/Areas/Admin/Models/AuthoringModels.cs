using System.ComponentModel.DataAnnotations;
using CyberLms.Web.Domain;
using CyberLms.Web.Models;

namespace CyberLms.Web.Areas.Admin.Models;

/// <summary>Steps of the "create learning material" wizard.</summary>
public enum AuthorStep { Basics = 1, Write = 2, Acknowledgment = 3, Assessment = 4, Preview = 5, Publish = 6 }

/// <summary>What every wizard page needs for its header and step bar.</summary>
public abstract class AuthorPageVm
{
    /// <summary>Null only on step 1 of a brand-new item (nothing saved yet).</summary>
    public Content? Content { get; set; }
    public Assessment? Assessment { get; set; }
    public abstract AuthorStep Step { get; }
    /// <summary>Id of the step's main form, so the step bar can save before switching steps.</summary>
    public virtual string? FormId => "author-form";
    public int QuestionCount => Assessment?.Questions.Count ?? 0;
}

public class BasicsStepVm : AuthorPageVm
{
    public override AuthorStep Step => AuthorStep.Basics;
    [Required, StringLength(300), Display(Name = "Content title")] public string Title { get; set; } = "";
    public ContentType Type { get; set; } = ContentType.Regulation;
    [StringLength(2000), Display(Name = "Short description")] public string? Description { get; set; }
}

public class WriteStepVm : AuthorPageVm
{
    public override AuthorStep Step => AuthorStep.Write;
    public string? Body { get; set; }
    [StringLength(1000), Display(Name = "Internal link / video URL")] public string? ExternalUrl { get; set; }
}

public class AckStepVm : AuthorPageVm
{
    public override AuthorStep Step => AuthorStep.Acknowledgment;
    public bool RequiresAcknowledgment { get; set; }
    [StringLength(1000), Display(Name = "Acknowledgment statement")] public string? Statement { get; set; }
    public string Suggested { get; set; } = "";
    /// <summary>How many employees acknowledged the current version (editing an existing item).</summary>
    public int Acknowledged { get; set; }
}

public class AssessmentStepVm : AuthorPageVm
{
    public override AuthorStep Step => AuthorStep.Assessment;
    public override string? FormId => "asm-form";
    public bool Include { get; set; }
    public AssessmentFormVm Settings { get; set; } = new();
    public QuestionFormVm Question { get; set; } = new();
    public List<Question> Questions { get; set; } = new();
    public bool Locked { get; set; }
    public int Attempts { get; set; }
    /// <summary>Other assessments linked to the same content (managed from the assessments page).</summary>
    public List<Assessment> Others { get; set; } = new();
    /// <summary>Show the question editor open (after a validation error or when editing).</summary>
    public bool EditorOpen { get; set; }
}

public class PreviewStepVm : AuthorPageVm
{
    public override AuthorStep Step => AuthorStep.Preview;
    public override string? FormId => null;
    public ContentDetailsVm Reader { get; set; } = null!;
}

public class PublishStepVm : AuthorPageVm
{
    public override AuthorStep Step => AuthorStep.Publish;
    public override string? FormId => null;
    public string AckText { get; set; } = "";
    public bool HasBody { get; set; }
    public int Attachments { get; set; }
    public List<string> Blockers { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public int Acknowledged { get; set; }
    public int Attempts { get; set; }
}
