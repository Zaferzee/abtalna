using System.Net;
using System.Threading.Channels;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace CyberLms.Web.Services;

public record MailJob(string To, string Subject, string HtmlBody);

public class EmailQueue
{
    private readonly Channel<MailJob> _ch = Channel.CreateUnbounded<MailJob>();
    public ChannelReader<MailJob> Reader => _ch.Reader;
    public void Enqueue(MailJob j) => _ch.Writer.TryWrite(j);
}

public class SmtpConfig
{
    public string Host { get; init; } = "";
    public int Port { get; init; } = 25;
    public string Sender { get; init; } = "";
    public string SenderName { get; init; } = "";
    public string? Username { get; init; }
    public string? Password { get; init; }
    public SecureSocketOptions Security { get; init; } = SecureSocketOptions.Auto;
    public bool Configured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(Sender);
}

/// <summary>SMTP host/port/sender live in Settings (DB). The password is NEVER stored in the DB: it comes from configuration (env var Smtp__Password / user-secrets / appsettings.Production.json).</summary>
public class SmtpConfigProvider(SettingsService settings, IConfiguration cfg)
{
    public SmtpConfig Get()
    {
        var sec = settings.Get("Smtp.Security", cfg["Smtp:Security"] ?? "Auto");
        return new SmtpConfig
        {
            Host = settings.Get("Smtp.Host", cfg["Smtp:Host"]) ?? "",
            Port = settings.GetInt("Smtp.Port", int.TryParse(cfg["Smtp:Port"], out var p) ? p : 25),
            Sender = settings.Get("Smtp.Sender", cfg["Smtp:Sender"]) ?? "",
            SenderName = settings.Get("Smtp.SenderName", cfg["Smtp:SenderName"]) ?? "",
            Username = settings.Get("Smtp.Username", cfg["Smtp:Username"]),
            Password = cfg["Smtp:Password"],
            Security = sec switch { "None" => SecureSocketOptions.None, "StartTls" => SecureSocketOptions.StartTls, "Ssl" => SecureSocketOptions.SslOnConnect, _ => SecureSocketOptions.Auto },
        };
    }
}

public static class SmtpSender
{
    public static MimeMessage Build(SmtpConfig c, MailJob j)
    {
        var m = new MimeMessage();
        m.From.Add(new MailboxAddress(c.SenderName, c.Sender));
        m.To.Add(MailboxAddress.Parse(j.To));
        m.Subject = j.Subject;
        m.Body = new BodyBuilder { HtmlBody = j.HtmlBody }.ToMessageBody();
        return m;
    }

    public static async Task ConnectAsync(SmtpClient client, SmtpConfig c, CancellationToken ct)
    {
        await client.ConnectAsync(c.Host, c.Port, c.Security, ct);
        if (!string.IsNullOrEmpty(c.Username)) await client.AuthenticateAsync(c.Username, c.Password ?? "", ct);
    }
}

public class EmailSenderService(EmailQueue queue, SmtpConfigProvider provider, ILogger<EmailSenderService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (await queue.Reader.WaitToReadAsync(stop))
        {
            var cfg = provider.Get();
            if (!cfg.Configured) { while (queue.Reader.TryRead(out _)) { } log.LogWarning("SMTP not configured; queued emails discarded."); continue; }
            using var client = new SmtpClient();
            try
            {
                await SmtpSender.ConnectAsync(client, cfg, stop);
                while (queue.Reader.TryRead(out var job))
                {
                    try { await client.SendAsync(SmtpSender.Build(cfg, job), stop); }
                    catch (Exception ex) when (ex is not OperationCanceledException) { log.LogWarning(ex, "Failed to send mail to {To}", job.To); }
                }
                await client.DisconnectAsync(true, stop);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var dropped = 0;
                while (queue.Reader.TryRead(out _)) dropped++;
                log.LogError(ex, "SMTP connection to {Host}:{Port} failed ({Security}); {Dropped} queued e-mail(s) in this batch were not sent.", cfg.Host, cfg.Port, cfg.Security, dropped);
            }
        }
    }
}

public class NotificationService(EmailQueue queue, SmtpConfigProvider smtp, SettingsService settings, IConfiguration cfg)
{
    public bool Ready => smtp.Get().Configured;

    public string BaseUrl => (settings.Get("App.BaseUrl", cfg["App:BaseUrl"]) ?? "").TrimEnd('/');

    /// <summary>
    /// Queues one e-mail per recipient. Subject/message are resource keys with <paramref name="args"/>; e-mails are always rendered
    /// in the system language (Arabic, right-to-left) regardless of who triggered them.
    /// </summary>
    public int Send(IEnumerable<(string? Email, string Name)> recipients, string subjectKey, string messageKey, object?[] args, string? path)
    {
        var n = 0;
        foreach (var job in Compose(recipients, subjectKey, messageKey, args, path)) { queue.Enqueue(job); n++; }
        return n;
    }

    public IEnumerable<MailJob> Compose(IEnumerable<(string? Email, string Name)> recipients, string subjectKey, string messageKey, object?[] args, string? path)
    {
        var brand = Branding.From(settings, Res.Arabic);
        var link = string.IsNullOrEmpty(BaseUrl) ? null : BaseUrl + (path ?? "/");
        var subject = Res.Ar(subjectKey, args);
        var message = Res.Ar(messageKey, args);
        foreach (var (email, name) in recipients)
        {
            if (string.IsNullOrWhiteSpace(email)) continue;
            var body = $"<div dir=\"rtl\" style=\"direction:rtl;text-align:right;font-family:Segoe UI,Tahoma,Arial,sans-serif;line-height:1.7\">" +
                       $"<p>{WebUtility.HtmlEncode(Res.Ar("Dear {0},", name))}</p><p>{WebUtility.HtmlEncode(message)}</p>" +
                       (link != null ? $"<p>{WebUtility.HtmlEncode(Res.Ar("Open the platform"))}: <a href=\"{WebUtility.HtmlEncode(link)}\">{WebUtility.HtmlEncode(link)}</a></p>" : "") +
                       $"<hr><small style=\"color:#666\">{WebUtility.HtmlEncode(brand.OrgName)} - {WebUtility.HtmlEncode(brand.SystemName)}</small></div>";
            yield return new MailJob(email, subject, body);
        }
    }
}
