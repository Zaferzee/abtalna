using CyberLms.Web.Domain;

namespace CyberLms.Web.Services;

public record UploadRule(AttachmentKind Kind, string[] Mimes, long MaxBytes);

public class StorageOptions
{
    public string RootPath { get; set; } = "";
    public int MaxImageMB { get; set; } = 10;
    public int MaxDocumentMB { get; set; } = 50;
    public int MaxVideoMB { get; set; } = 500;
}

/// <summary>Upload rejection: a resource key plus format arguments (translated by the caller).</summary>
public record UploadError(string Key, params object?[] Args);

public record StoredFile(string RelativePath, string ContentType, long Size, AttachmentKind Kind, string OriginalName);

/// <summary>Validates and stores uploads under a configurable root. Binary data never goes into PostgreSQL.</summary>
public class StorageService
{
    private readonly string _root;
    private readonly StorageOptions _opt;

    public StorageService(IConfiguration cfg, IWebHostEnvironment env)
    {
        _opt = cfg.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();
        var configured = _opt.RootPath;
        if (string.IsNullOrWhiteSpace(configured))
        {
            if (!env.IsDevelopment()) throw new InvalidOperationException("Storage:RootPath is not configured. Set it to a dedicated absolute folder outside the application folder (e.g. via appsettings.Production.json).");
            configured = "storage-dev";
        }
        else if (!env.IsDevelopment() && !Path.IsPathRooted(configured))
            throw new InvalidOperationException("Storage:RootPath must be an absolute path outside the application folder.");
        _root = Path.GetFullPath(Path.IsPathRooted(configured) ? configured : Path.Combine(env.ContentRootPath, configured));
        Directory.CreateDirectory(_root);
    }

    public string Root => _root;
    public long MaxRequestBytes => (long)Math.Max(_opt.MaxVideoMB, Math.Max(_opt.MaxDocumentMB, _opt.MaxImageMB)) * 1024 * 1024 + 1024 * 1024;

    private static readonly Dictionary<string, UploadRule> Rules = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = new(AttachmentKind.Image, ["image/png"], 0),
        [".jpg"] = new(AttachmentKind.Image, ["image/jpeg"], 0),
        [".jpeg"] = new(AttachmentKind.Image, ["image/jpeg"], 0),
        [".gif"] = new(AttachmentKind.Image, ["image/gif"], 0),
        [".webp"] = new(AttachmentKind.Image, ["image/webp"], 0),
        [".ico"] = new(AttachmentKind.Image, ["image/x-icon"], 0),
        [".mp4"] = new(AttachmentKind.Video, ["video/mp4"], 0),
        [".webm"] = new(AttachmentKind.Video, ["video/webm"], 0),
        [".pdf"] = new(AttachmentKind.Document, ["application/pdf"], 0),
        [".docx"] = new(AttachmentKind.Document, ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"], 0),
        [".xlsx"] = new(AttachmentKind.Document, ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"], 0),
        [".pptx"] = new(AttachmentKind.Document, ["application/vnd.openxmlformats-officedocument.presentationml.presentation"], 0),
    };

    private long Max(AttachmentKind k) => (long)(k switch { AttachmentKind.Image => _opt.MaxImageMB, AttachmentKind.Video => _opt.MaxVideoMB, _ => _opt.MaxDocumentMB }) * 1024 * 1024;

    public static string AcceptList => string.Join(",", Rules.Keys);

    /// <summary>Validates extension, size and magic bytes. Returns an error (resource key + args) or null.</summary>
    public async Task<(UploadError? Error, StoredFile? Result)> SaveAsync(IFormFile file, string subFolder, AttachmentKind[]? allowedKinds = null)
    {
        if (file.Length == 0) return (new UploadError("The file is empty."), null);
        var original = Path.GetFileName(file.FileName);
        var ext = Path.GetExtension(original);
        if (!Rules.TryGetValue(ext, out var rule)) return (new UploadError("File type '{0}' is not allowed.", ext), null);
        if (allowedKinds != null && !allowedKinds.Contains(rule.Kind)) return (new UploadError("File type '{0}' is not allowed here.", ext), null);
        if (file.Length > Max(rule.Kind)) return (new UploadError("File is too large (max {0} MB for this type).", Max(rule.Kind) / 1024 / 1024), null);

        var header = new byte[16];
        int read;
        await using (var s = file.OpenReadStream()) read = await s.ReadAsync(header);
        if (!SignatureMatches(ext.ToLowerInvariant(), header.AsSpan(0, read))) return (new UploadError("File content does not match its extension."), null);

        // Server-generated name: user input never reaches the path (no traversal).
        var safeSub = string.Concat(subFolder.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '/'));
        var rel = $"{safeSub}/{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var full = Resolve(rel)!;
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using (var fs = new FileStream(full, FileMode.CreateNew, FileAccess.Write)) await file.CopyToAsync(fs);
        return (null, new StoredFile(rel, rule.Mimes[0], file.Length, rule.Kind, original));
    }

    /// <summary>Maps a stored relative path to a physical path, refusing anything outside the root.</summary>
    public string? Resolve(string relative)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relative));
        return full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? full : null;
    }

    /// <summary>Duplicates a stored file under a new server-generated name. Returns the new relative path, or null if the source is missing.</summary>
    public string? Copy(string relative, string subFolder)
    {
        var src = Resolve(relative);
        if (src == null || !File.Exists(src)) return null;
        var safeSub = string.Concat(subFolder.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '/'));
        var rel = $"{safeSub}/{Guid.NewGuid():N}{Path.GetExtension(relative).ToLowerInvariant()}";
        var dest = Resolve(rel)!;
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.Copy(src, dest);
        return rel;
    }

    public void Delete(string? relative)
    {
        if (string.IsNullOrEmpty(relative)) return;
        var p = Resolve(relative);
        try { if (p != null && File.Exists(p)) File.Delete(p); } catch (IOException) { }
    }

    private static bool SignatureMatches(string ext, ReadOnlySpan<byte> h)
    {
        static bool Starts(ReadOnlySpan<byte> h, params byte[] sig) => h.Length >= sig.Length && h[..sig.Length].SequenceEqual(sig);
        return ext switch
        {
            ".png" => Starts(h, 0x89, 0x50, 0x4E, 0x47),
            ".jpg" or ".jpeg" => Starts(h, 0xFF, 0xD8, 0xFF),
            ".ico" => Starts(h, 0x00, 0x00, 0x01, 0x00),
            ".gif" => Starts(h, 0x47, 0x49, 0x46, 0x38),
            ".webp" => Starts(h, 0x52, 0x49, 0x46, 0x46) && h.Length >= 12 && h[8..12].SequenceEqual("WEBP"u8),
            ".pdf" => Starts(h, 0x25, 0x50, 0x44, 0x46),
            ".mp4" => h.Length >= 12 && h[4..8].SequenceEqual("ftyp"u8),
            ".webm" => Starts(h, 0x1A, 0x45, 0xDF, 0xA3),
            ".docx" or ".xlsx" or ".pptx" => Starts(h, 0x50, 0x4B, 0x03, 0x04),
            _ => false,
        };
    }

    public static string ContentTypeFor(string path) => Rules.TryGetValue(Path.GetExtension(path), out var r) ? r.Mimes[0] : "application/octet-stream";
}
