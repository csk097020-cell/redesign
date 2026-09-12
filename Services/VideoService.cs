// APPLICATION-SPECIFIC CODE - Property of Client (Corinne Kelley)
// Rewritten to use SupabaseService HTTP client - no Supabase C# SDK dependency
using System.Diagnostics;
using System.Net.Http.Headers;

namespace MomentaryMomentos.Services;

/// <summary>
/// Handles video upload/download/delete via Supabase Storage REST API.
/// Uses the same HttpClient as SupabaseService — no SDK dependency.
/// Contract Appendix A Section 2.5: Cloud Storage.
/// </summary>
public class VideoService : IVideoService
{
    private readonly SupabaseService _supabase;
    private readonly AppSettings _settings;
    private readonly HttpClient _http;

    public VideoService(SupabaseService supabase, AppSettings settings, HttpClient http)
    {
        _supabase = supabase;
        _settings = settings;
        _http = http;
    }

    public async Task<string> UploadVideoAsync(string filePath, string fileName, CancellationToken ct = default)
    {
        var session = _supabase.Session
            ?? throw new UnauthorizedAccessException("User must be authenticated to upload videos.");

        var bytes = await File.ReadAllBytesAsync(filePath, ct);
        var uniqueFileName = $"{session.UserId}/{DateTime.UtcNow:yyyyMMdd_HHmmss}_{fileName}";
        var bucket = _settings.StorageBucketUserVideos;
        var url = $"{_settings.SupabaseUrl}/storage/v1/object/{bucket}/{Uri.EscapeDataString(uniqueFileName)}";

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("apikey", _settings.SupabaseAnonKey);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        req.Content = new ByteArrayContent(bytes);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Upload failed: {err}");
        }

        Debug.WriteLine($"VideoService: uploaded {uniqueFileName}");
        return uniqueFileName;
    }

    public Task<string> GetVideoUrlAsync(string fileName)
    {
        var bucket = _settings.StorageBucketUserVideos;
        var url = $"{_settings.SupabaseUrl}/storage/v1/object/public/{bucket}/{Uri.EscapeDataString(fileName)}";
        return Task.FromResult(url);
    }

    public async Task<bool> DeleteVideoAsync(string fileName, CancellationToken ct = default)
    {
        var session = _supabase.Session;
        if (session is null) return false;

        var bucket = _settings.StorageBucketUserVideos;
        var url = $"{_settings.SupabaseUrl}/storage/v1/object/{bucket}/{Uri.EscapeDataString(fileName)}";

        using var req = new HttpRequestMessage(HttpMethod.Delete, url);
        req.Headers.Add("apikey", _settings.SupabaseAnonKey);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        using var resp = await _http.SendAsync(req, ct);
        Debug.WriteLine($"VideoService: delete {fileName} → {resp.StatusCode}");
        return resp.IsSuccessStatusCode;
    }

    public async Task<List<string>> ListUserVideosAsync(CancellationToken ct = default)
    {
        var session = _supabase.Session;
        if (session is null) return new List<string>();

        var bucket = _settings.StorageBucketUserVideos;
        var url = $"{_settings.SupabaseUrl}/storage/v1/object/list/{bucket}";

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("apikey", _settings.SupabaseAnonKey);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        req.Content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new { prefix = session.UserId + "/" }),
            System.Text.Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return new List<string>();

        var body = await resp.Content.ReadAsStringAsync(ct);
        using var doc = System.Text.Json.JsonDocument.Parse(body);
        return doc.RootElement.EnumerateArray()
            .Select(el => el.GetProperty("name").GetString() ?? "")
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();
    }
}
