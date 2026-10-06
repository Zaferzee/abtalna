using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using CyberLms.Web.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CyberLms.Tests;

/// <summary>Boots the real app against a throw-away PostgreSQL database (migrations run on startup).</summary>
public class TestApp : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminPass = "Admin#Pass12345";
    private readonly string _dbName = "cyberlms_test_" + Guid.NewGuid().ToString("N")[..10];
    // Connection to a PostgreSQL server whose role may CREATE DATABASE, e.g. "Host=localhost;Username=tester;Password=..." (never committed).
    private readonly string _baseCs = Environment.GetEnvironmentVariable("TEST_PG") ?? throw new InvalidOperationException("Set the TEST_PG environment variable to a PostgreSQL connection string (without Database=) for a role that can create databases.");
    public string StorageDir { get; } = Path.Combine(Path.GetTempPath(), "cyberlms-test-" + Guid.NewGuid().ToString("N"));

    public TestApp()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _baseCs + ";Database=" + _dbName + ";Include Error Detail=true");
        Environment.SetEnvironmentVariable("Seed__AdminUsername", "admin");
        Environment.SetEnvironmentVariable("Seed__AdminPassword", AdminPass);
        Environment.SetEnvironmentVariable("Storage__RootPath", StorageDir);
        Environment.SetEnvironmentVariable("Authentication__Mode", "Local");
        Environment.SetEnvironmentVariable("Authentication__Windows__AllowedDomains__0", null);
        Environment.SetEnvironmentVariable("ASPNETCORE_IIS_HTTPAUTH", null);
        Environment.SetEnvironmentVariable("Logging__File__Enabled", "false");
        // small limits so the "oversized upload" failure tests are cheap
        Environment.SetEnvironmentVariable("Storage__MaxImageMB", "1");
        Environment.SetEnvironmentVariable("Storage__MaxDocumentMB", "1");
        Environment.SetEnvironmentVariable("Storage__MaxVideoMB", "2");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var c = new NpgsqlConnection(_baseCs + ";Database=postgres");
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_dbName}\" WITH (FORCE)", c);
        await cmd.ExecuteNonQueryAsync();
        try { Directory.Delete(StorageDir, true); } catch { }
    }

    public async Task<T> Db<T>(Func<AppDbContext, Task<T>> f)
    {
        using var scope = Services.CreateScope();
        return await f(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public HttpClient NewClient() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
}

public static class HttpExt
{
    public static async Task<string> Token(this HttpClient c, string url)
    {
        var html = await c.GetStringAsync(url);
        var m = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(m.Success, "antiforgery token not found on " + url);
        return m.Groups[1].Value;
    }

    public static async Task<HttpResponseMessage> PostForm(this HttpClient c, string tokenPage, string url, IEnumerable<KeyValuePair<string, string>> fields)
    {
        var token = await c.Token(tokenPage);
        var list = fields.ToList(); list.Add(new("__RequestVerificationToken", token));
        return await c.PostAsync(url, new FormUrlEncodedContent(list));
    }

    public static async Task<HttpResponseMessage> PostMultipart(this HttpClient c, string tokenPage, string url, Action<MultipartFormDataContent> fill)
    {
        var token = await c.Token(tokenPage);
        var mp = new MultipartFormDataContent { { new StringContent(token), "__RequestVerificationToken" } };
        fill(mp);
        return await c.PostAsync(url, mp);
    }

    public static void AddFile(this MultipartFormDataContent mp, string field, string name, byte[] bytes, string mime)
    {
        var sc = new ByteArrayContent(bytes); sc.Headers.ContentType = new MediaTypeHeaderValue(mime);
        mp.Add(sc, field, name);
    }

    public static KeyValuePair<string, string> F(string k, string v) => new(k, v);

    // 1x1 transparent PNG
    public static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
}
