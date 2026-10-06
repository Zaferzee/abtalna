using System.Globalization;
using System.Net;
using CyberLms.Web.Domain;
using Microsoft.AspNetCore.Html;

namespace CyberLms.Web.Services;

/// <summary>Presentation helpers shared by the views (no business logic).</summary>
public static class Ui
{
    /// <summary>
    /// A percentage as an isolated left-to-right run ("60%"), so inside Arabic text it reads "درجة النجاح: 60%" and never "%60".
    /// </summary>
    public static IHtmlContent Pct(double value, string format = "0.#") =>
        new HtmlString($"<bdi class=\"pct\" dir=\"ltr\">{value.ToString(format, CultureInfo.InvariantCulture)}%</bdi>");

    /// <summary>A number as an isolated LTR run (tabular digits).</summary>
    public static IHtmlContent Num(double value, string format = "0") =>
        new HtmlString($"<bdi class=\"num\" dir=\"ltr\">{value.ToString(format, CultureInfo.InvariantCulture)}</bdi>");

    public static int Percent(int part, int total) => total <= 0 ? 0 : (int)Math.Round(part * 100.0 / total);

    /// <summary>Animated progress ring (SVG, mirrored in RTL). The value is applied by ui.js; the label shows the number immediately.</summary>
    public static IHtmlContent Ring(double value, string? caption = null, int size = 120, int stroke = 10, double? mark = null, string? extraClass = null)
    {
        var v = Math.Clamp(value, 0, 100).ToString("0.##", CultureInfo.InvariantCulture);
        var shown = size < 80 ? Math.Round(Math.Clamp(value, 0, 100)).ToString(CultureInfo.InvariantCulture) : Math.Round(Math.Clamp(value, 0, 100), 1).ToString("0.#", CultureInfo.InvariantCulture);
        var markSvg = "";
        if (mark is double m and > 0 and < 100)
        {
            // A small tick on the track showing the passing threshold.
            var a = (m / 100.0 * 360 - 90) * Math.PI / 180;
            string P(double r) => $"{(60 + r * Math.Cos(a)).ToString("0.##", CultureInfo.InvariantCulture)}";
            string Q(double r) => $"{(60 + r * Math.Sin(a)).ToString("0.##", CultureInfo.InvariantCulture)}";
            markSvg = $"<line class=\"pass-mark\" x1=\"{P(44)}\" y1=\"{Q(44)}\" x2=\"{P(60)}\" y2=\"{Q(60)}\" />";
        }
        var cap = string.IsNullOrEmpty(caption) ? "" : $"<small>{WebUtility.HtmlEncode(caption)}</small>";
        return new HtmlString(
            $"<span class=\"ring-wrap {extraClass}\" style=\"--size:{size}px;--stroke:{stroke}\">" +
            $"<svg class=\"ring\" viewBox=\"0 0 120 120\" aria-hidden=\"true\"><g class=\"ring-g\"><circle class=\"ring-bg\" cx=\"60\" cy=\"60\" r=\"52\" />" +
            $"<circle class=\"ring-fg{(value <= 0 ? " is-zero" : "")}\" cx=\"60\" cy=\"60\" r=\"52\" pathLength=\"100\" data-ring=\"{v}\" />{markSvg}</g></svg>" +
            $"<span class=\"ring-label\"><strong><bdi class=\"pct\" dir=\"ltr\"><span data-count=\"{shown}\">{Math.Round(Math.Clamp(value, 0, 100)).ToString(CultureInfo.InvariantCulture)}</span>%</bdi></strong>{cap}</span></span>");
    }

    public static string TypeIcon(ContentType t) => t switch
    {
        ContentType.Policy => "bi-shield-check", ContentType.Control => "bi-sliders2-vertical", ContentType.Awareness => "bi-lightbulb",
        ContentType.Training => "bi-mortarboard", ContentType.Procedure => "bi-list-check", _ => "bi-journal-text",
    };

    public static string AttachmentIcon(ContentAttachment a)
    {
        var ext = Path.GetExtension(a.FileName).ToLowerInvariant();
        return a.Kind switch
        {
            AttachmentKind.Image => "bi-file-earmark-image",
            AttachmentKind.Video => "bi-file-earmark-play",
            _ => ext switch { ".pdf" => "bi-file-earmark-pdf", ".doc" or ".docx" => "bi-file-earmark-word", ".xls" or ".xlsx" => "bi-file-earmark-excel", ".ppt" or ".pptx" => "bi-file-earmark-ppt", _ => "bi-file-earmark-text" },
        };
    }

    /// <summary>Icon for an audit action code (e.g. "Content.Create" → plus).</summary>
    public static string AuditIcon(string action)
    {
        var a = action.ToLowerInvariant();
        if (a.Contains("login") || a.Contains("signin")) return "bi-box-arrow-in-left";
        if (a.Contains("logout")) return "bi-box-arrow-right";
        if (a.Contains("delete")) return "bi-trash3";
        if (a.Contains("create") || a.Contains("add")) return "bi-plus-lg";
        if (a.Contains("publish")) return "bi-broadcast";
        if (a.Contains("ack")) return "bi-check2-square";
        if (a.Contains("attempt") || a.Contains("submit")) return "bi-patch-check";
        if (a.Contains("setting") || a.Contains("brand")) return "bi-palette2";
        if (a.Contains("export")) return "bi-download";
        if (a.Contains("password")) return "bi-key";
        return "bi-pencil";
    }
}
