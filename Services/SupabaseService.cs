using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MomentaryMomentos.Models;
using MomentaryMomentos.Utils;

namespace MomentaryMomentos.Services;

/// <summary>
/// Thin, dependency-free Supabase client for Auth, PostgREST and Storage.
/// Matches the behavior used in the attached PWA (Supabase URL + anon key).
/// </summary>
public sealed class SupabaseService
{
    private readonly AppSettings _settings;
    private readonly HttpClient _http;
    private AuthSession? _session;

    public SupabaseService(AppSettings settings, HttpClient http)
    {
        _settings = settings;
        _http = http;
    }

    public bool IsAuthenticated => _session is not null;
    public AuthSession? Session => _session;

    public void SetSession(AuthSession session) => _session = session;

    public async Task<AuthSession> SignInAsync(string email, string password, CancellationToken ct = default)
    {
        var url = $"{_settings.SupabaseUrl}/auth/v1/token?grant_type=password";
        var payload = new { email, password };

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        AddAnonHeaders(req);
        req.Content = new StringContent(JsonSerializer.Serialize(payload, Json.Options), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(ParseAuthError(body, "Sign-in failed. Please check your credentials."));

        using var doc = JsonDocument.Parse(body);
        var access = doc.RootElement.GetProperty("access_token").GetString()!;
        var refresh = doc.RootElement.GetProperty("refresh_token").GetString()!;
        var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();
        var user = doc.RootElement.GetProperty("user");
        var userId = user.GetProperty("id").GetString()!;
        var userEmail = user.GetProperty("email").GetString() ?? email;

        var session = new AuthSession(
            AccessToken: access,
            RefreshToken: refresh,
            ExpiresAt: DateTimeOffset.UtcNow.AddSeconds(expiresIn - 30),
            UserId: userId,
            Email: userEmail
        );

        _session = session;
        return session;
    }

    public async Task<AuthSession> SignUpAsync(string email, string password, string? fullName = null, CancellationToken ct = default)
    {
        var url = $"{_settings.SupabaseUrl}/auth/v1/signup";
        var payload = new
        {
            email,
            password,
            data = new { full_name = fullName }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        AddAnonHeaders(req);
        req.Content = new StringContent(JsonSerializer.Serialize(payload, Json.Options), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(ParseAuthError(body, "Sign-up failed. Please try again."));

        // Signup may return a session immediately (email confirmation disabled)
        // or just a user object (email confirmation required — no tokens yet).
        using var doc = JsonDocument.Parse(body);

        if (doc.RootElement.TryGetProperty("access_token", out _))
        {
            var access = doc.RootElement.GetProperty("access_token").GetString()!;
            var refresh = doc.RootElement.GetProperty("refresh_token").GetString()!;
            var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();
            var user = doc.RootElement.GetProperty("user");
            var userId = user.GetProperty("id").GetString()!;
            var userEmail = user.GetProperty("email").GetString() ?? email;

            var session = new AuthSession(access, refresh, DateTimeOffset.UtcNow.AddSeconds(expiresIn - 30), userId, userEmail);
            _session = session;
            return session;
        }

        // Supabase returned 200 but no tokens — email confirmation is required.
        // Trying to sign in now would fail with 400 "Email not confirmed".
        throw new EmailConfirmationRequiredException();
    }

    public async Task<AuthSession> RefreshAsync(CancellationToken ct = default)
    {
        if (_session is null) throw new InvalidOperationException("No session to refresh.");

        var url = $"{_settings.SupabaseUrl}/auth/v1/token?grant_type=refresh_token";
        var payload = new { refresh_token = _session.RefreshToken };

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        AddAnonHeaders(req);
        req.Content = new StringContent(JsonSerializer.Serialize(payload, Json.Options), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Refresh failed: {body}");

        using var doc = JsonDocument.Parse(body);
        var access = doc.RootElement.GetProperty("access_token").GetString()!;
        var refresh = doc.RootElement.GetProperty("refresh_token").GetString()!;
        var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();
        var user = doc.RootElement.GetProperty("user");
        var userId = user.GetProperty("id").GetString()!;
        var userEmail = user.GetProperty("email").GetString() ?? _session.Email;

        var session = new AuthSession(access, refresh, DateTimeOffset.UtcNow.AddSeconds(expiresIn - 30), userId, userEmail);
        _session = session;
        return session;
    }

    public void SignOut()
    {
        _session = null;
    }

    private void AddAnonHeaders(HttpRequestMessage req)
    {
        req.Headers.Add("apikey", _settings.SupabaseAnonKey);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// Extracts the "msg" field from a Supabase JSON error body and maps known
    /// error_code values to user-friendly strings.
    /// </summary>
    private static string ParseAuthError(string body, string fallback)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var code = doc.RootElement.TryGetProperty("error_code", out var ec) ? ec.GetString() : null;
            var msg  = doc.RootElement.TryGetProperty("msg", out var m) ? m.GetString() : null;

            return code switch
            {
                "over_email_send_rate_limit" => "Too many attempts. Please wait a minute and try again.",
                "email_not_confirmed"        => "Please confirm your email before signing in.",
                "invalid_credentials"        => "Incorrect email or password.",
                "user_already_exists"        => "An account with this email already exists.",
                "weak_password"              => "Password is too weak — use at least 6 characters.",
                "validation_failed"          => msg ?? "Please check your email and password.",
                _ => msg ?? fallback
            };
        }
        catch
        {
            return fallback;
        }
    }

    private async Task AddAuthHeadersAsync(HttpRequestMessage req, CancellationToken ct)
    {
        AddAnonHeaders(req);

        if (_session is null) throw new InvalidOperationException("Not authenticated.");

        if (DateTimeOffset.UtcNow >= _session.ExpiresAt)
        {
            await RefreshAsync(ct);
        }

        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session!.AccessToken);
    }

    // -------- PostgREST helpers --------

    public async Task<UserProfile?> GetProfileAsync(CancellationToken ct = default)
    {
        if (_session is null) return null;

        // Field sets ordered richest → poorest. avatar_url requires migration 003;
        // if it isn't applied yet the query 400s naming the column, so we fall back
        // to a set without it rather than letting one missing optional column null
        // the entire profile read (which previously blanked the user's name).
        const string full       = "id,full_name,is_admin,is_premium,avatar_url,premium_source,premium_expires_at";
        const string withAvatar = "id,full_name,is_admin,is_premium,avatar_url";
        const string baseline   = "id,full_name,is_admin,is_premium";

        // Columns that may be absent on an un-migrated DB; a 400 naming one triggers a retry.
        // The premium_* columns require migration 006; withAvatar sits between the two so a
        // DB without 006 still keeps the avatar rather than falling all the way to baseline.
        string[] optionalColumns = { "avatar_url", "premium_source", "premium_expires_at" };

        var fieldSets = new[] { full, withAvatar, baseline };
        for (var i = 0; i < fieldSets.Length; i++)
        {
            var fields = fieldSets[i];
            var url = $"{_settings.SupabaseUrl}/rest/v1/momo_profiles?id=eq.{Uri.EscapeDataString(_session.UserId)}&select={fields}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            await AddAuthHeadersAsync(req, ct);
            req.Headers.Add("Prefer", "count=exact");

            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (resp.IsSuccessStatusCode)
            {
                var arr = JsonSerializer.Deserialize<List<JsonElement>>(body, Json.Options) ?? new();
                if (arr.Count == 0) return null;

                var el = arr[0];
                return new UserProfile
                {
                    Id        = el.GetProperty("id").GetString() ?? _session.UserId,
                    FullName  = el.TryGetProperty("full_name",  out var fn) ? fn.GetString() : null,
                    AvatarUrl = el.TryGetProperty("avatar_url", out var av) ? av.GetString() : null,
                    IsAdmin   = el.TryGetProperty("is_admin",   out var ia) && ia.GetBoolean(),
                    IsPremium = el.TryGetProperty("is_premium", out var ip) && ip.GetBoolean(),
                    PremiumSource = el.TryGetProperty("premium_source", out var ps) ? ps.GetString() : null,
                    PremiumExpiresAt = el.TryGetProperty("premium_expires_at", out var pe)
                                       && pe.ValueKind is not JsonValueKind.Null
                                       && DateTimeOffset.TryParse(pe.GetString(), out var parsedExpiry)
                        ? parsedExpiry
                        : null
                };
            }

            // If a leaner set remains and the error names an optional (un-migrated) column, retry.
            var missingColumn = optionalColumns.FirstOrDefault(c => fields.Contains(c) && body.Contains(c));
            if (missingColumn is not null && i < fieldSets.Length - 1)
            {
                System.Diagnostics.Debug.WriteLine($"GetProfileAsync: '{missingColumn}' column not found — retrying with a leaner field set. Run migration 003.");
                continue;
            }

            // Any other error: return null rather than crashing.
            System.Diagnostics.Debug.WriteLine($"GetProfileAsync failed ({resp.StatusCode}): {body}");
            return null;
        }

        return null;
    }

    public async Task<List<Tag>> GetTagsAsync(string? userId, bool includeInactive = false, CancellationToken ct = default)
    {
        // Match PWA: default tags (user_id is null) OR user-specific tags.
        // We also support includeInactive for Admin use.
        var baseUrl = $"{_settings.SupabaseUrl}/rest/v1/momo_tags?select=id,name,color,icon,is_active,user_id&order=name.asc";

        string filter;
        if (!string.IsNullOrWhiteSpace(userId))
            filter = $"&or=(user_id.is.null,user_id.eq.{Uri.EscapeDataString(userId)})";
        else
            filter = $"&user_id=is.null";

        if (!includeInactive)
            filter += "&is_active=eq.true";

        var url = baseUrl + filter;

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        AddAnonHeaders(req); // tags are readable via anon in many setups; if not, auth will still work.
        if (_session is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            return new List<Tag>();

        var arr = JsonSerializer.Deserialize<List<JsonElement>>(body, Json.Options) ?? new();
        return arr.Select(el => new Tag
        {
            Id = el.GetProperty("id").GetString() ?? "",
            Name = el.GetProperty("name").GetString() ?? "",
            Color = el.TryGetProperty("color", out var c) ? (c.GetString() ?? "#3B82F6") : "#3B82F6",
            Icon = el.TryGetProperty("icon", out var i) ? (i.GetString() ?? "✨") : "✨",
            IsActive = el.TryGetProperty("is_active", out var a) ? a.GetBoolean() : true,
            UserId = el.TryGetProperty("user_id", out var u) ? u.GetString() : null
        }).ToList();
    }

    public async Task<List<Memory>> GetMemoriesAsync(string userId, CancellationToken ct = default)
    {
        // Field sets ordered richest → poorest. Optional columns require migrations
        // (thumbnail_url → 001; caption, date_captured → 002). If Supabase returns a
        // 400 mentioning a column that isn't migrated yet, fall back to a leaner set
        // so memories always load. The baseline set contains only original columns.
        const string full        = "id,user_id,title,caption,video_url,thumbnail_url,tags,is_favorite,created_at,date_captured";
        const string withThumb   = "id,user_id,title,video_url,thumbnail_url,tags,is_favorite,created_at";
        const string baseline    = "id,user_id,title,video_url,tags,is_favorite,created_at";

        // Columns that may be absent on an un-migrated DB; a 400 naming one triggers a retry.
        string[] optionalColumns = { "thumbnail_url", "caption", "date_captured" };

        var fieldSets = new[] { full, withThumb, baseline };
        for (var i = 0; i < fieldSets.Length; i++)
        {
            var fields = fieldSets[i];
            var url = $"{_settings.SupabaseUrl}/rest/v1/momo_memories?user_id=eq.{Uri.EscapeDataString(userId)}&select={fields}&order=created_at.desc";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            await AddAuthHeadersAsync(req, ct);

            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (resp.IsSuccessStatusCode)
            {
                var arr = JsonSerializer.Deserialize<List<JsonElement>>(body, Json.Options) ?? new();
                return arr.Select(ParseMemory).ToList();
            }

            // If a leaner set remains and the error names an optional (un-migrated) column, retry with it.
            var missingColumn = optionalColumns.FirstOrDefault(c => fields.Contains(c) && body.Contains(c));
            if (missingColumn is not null && i < fieldSets.Length - 1)
            {
                System.Diagnostics.Debug.WriteLine($"GetMemoriesAsync: '{missingColumn}' column not found in DB — retrying with a leaner field set. Run the pending migration to enable it.");
                continue;
            }

            // Any other error: return empty rather than crashing
            System.Diagnostics.Debug.WriteLine($"GetMemoriesAsync failed ({resp.StatusCode}): {body}");
            return new List<Memory>();
        }

        return new List<Memory>();
    }

    public async Task<Memory> InsertMemoryAsync(Memory memory, CancellationToken ct = default)
    {
        var url = $"{_settings.SupabaseUrl}/rest/v1/momo_memories";
        // Note: thumbnail_url is intentionally excluded here.
        // It is set via a separate UpdateMemoryThumbnailAsync PATCH call after video upload,
        // which is a no-op if the column doesn't exist yet (pre-migration).
        var payload = new
        {
            user_id = memory.UserId,
            title = memory.Title,
            caption = memory.Caption,
            video_url = memory.VideoUrl,
            tags = memory.Tags,
            is_favorite = memory.IsFavorite,
            created_at = memory.CreatedAt.ToUniversalTime().ToString("O"),
            date_captured = memory.DateCaptured?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        await AddAuthHeadersAsync(req, ct);
        req.Headers.Add("Prefer", "return=representation");
        req.Content = new StringContent(JsonSerializer.Serialize(payload, Json.Options), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Insert memory failed: {body}");

        var arr = JsonSerializer.Deserialize<List<JsonElement>>(body, Json.Options) ?? new();
        return arr.Count > 0 ? ParseMemory(arr[0]) : memory;
    }

    public async Task UpdateFavoriteAsync(string memoryId, bool isFavorite, CancellationToken ct = default)
    {
        var url = $"{_settings.SupabaseUrl}/rest/v1/momo_memories?id=eq.{Uri.EscapeDataString(memoryId)}";
        using var req = new HttpRequestMessage(new HttpMethod("PATCH"), url);
        await AddAuthHeadersAsync(req, ct);
        req.Content = new StringContent(JsonSerializer.Serialize(new { is_favorite = isFavorite }, Json.Options), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Update favorite failed: {body}");
        }
    }

    /// <summary>Retag panel save (#3) — updates title, caption, and tags in a single PATCH.</summary>
    public async Task UpdateMemoryDetailsAsync(string memoryId, string title, string? caption, List<string> tags, CancellationToken ct = default)
    {
        var url = $"{_settings.SupabaseUrl}/rest/v1/momo_memories?id=eq.{Uri.EscapeDataString(memoryId)}";
        using var req = new HttpRequestMessage(new HttpMethod("PATCH"), url);
        await AddAuthHeadersAsync(req, ct);
        req.Content = new StringContent(JsonSerializer.Serialize(new { title, caption, tags }, Json.Options), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Update memory details failed: {body}");
        }
    }

    public async Task DeleteMemoryAsync(string memoryId, CancellationToken ct = default)
    {
        var url = $"{_settings.SupabaseUrl}/rest/v1/momo_memories?id=eq.{Uri.EscapeDataString(memoryId)}";
        using var req = new HttpRequestMessage(HttpMethod.Delete, url);
        await AddAuthHeadersAsync(req, ct);

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Delete failed: {body}");
        }
    }

    public async Task DeleteMemoryWithStorageTrashAsync(Memory memory, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(memory.VideoUrl))
            await MoveStorageObjectToTrashAsync(memory.UserId, memory.VideoUrl, ct);

        if (!string.IsNullOrWhiteSpace(memory.ThumbnailUrl))
            await MoveStorageObjectToTrashAsync(memory.UserId, memory.ThumbnailUrl, ct);

        await DeleteMemoryAsync(memory.Id, ct);
    }

    private async Task MoveStorageObjectToTrashAsync(string userId, string publicUrl, CancellationToken ct)
    {
        if (!TryGetStorageObjectKey(publicUrl, _settings.StorageBucketUserVideos, out var sourceKey))
            return;

        var fileName = Path.GetFileName(sourceKey);
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = $"{Guid.NewGuid():N}.bin";

        var destinationKey = $"{userId}/to_be_deleted/{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{fileName}";
        if (string.Equals(sourceKey, destinationKey, StringComparison.Ordinal))
            return;

        var url = $"{_settings.SupabaseUrl}/storage/v1/object/move";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        await AddAuthHeadersAsync(req, ct);
        req.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                bucketId = _settings.StorageBucketUserVideos,
                sourceKey,
                destinationKey
            }, Json.Options),
            Encoding.UTF8,
            "application/json");

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Storage trash move failed: {body}");
        }
    }

    private static bool TryGetStorageObjectKey(string publicUrl, string bucket, out string key)
    {
        key = "";
        if (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var uri))
            return false;

        var marker = $"/storage/v1/object/public/{bucket}/";
        var path = uri.AbsolutePath;
        var index = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return false;

        key = Uri.UnescapeDataString(path[(index + marker.Length)..]);
        return !string.IsNullOrWhiteSpace(key);
    }


    public async Task<Tag> CreateTagAsync(Tag tag, CancellationToken ct = default)
    {
        var url = $"{_settings.SupabaseUrl}/rest/v1/momo_tags";
        var payload = new
        {
            name = tag.Name,
            color = tag.Color,
            icon = tag.Icon,
            is_active = tag.IsActive,
            user_id = tag.UserId
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        await AddAuthHeadersAsync(req, ct);
        req.Headers.Add("Prefer", "return=representation");
        req.Content = new StringContent(JsonSerializer.Serialize(payload, Json.Options), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Create tag failed: {body}");

        var arr = JsonSerializer.Deserialize<List<JsonElement>>(body, Json.Options) ?? new();
        if (arr.Count == 0) return tag;

        var el = arr[0];
        tag.Id = el.GetProperty("id").GetString() ?? tag.Id;
        return tag;
    }

    public async Task SetTagActiveAsync(string tagId, bool isActive, CancellationToken ct = default)
    {
        var url = $"{_settings.SupabaseUrl}/rest/v1/momo_tags?id=eq.{Uri.EscapeDataString(tagId)}";
        using var req = new HttpRequestMessage(new HttpMethod("PATCH"), url);
        await AddAuthHeadersAsync(req, ct);
        req.Content = new StringContent(JsonSerializer.Serialize(new { is_active = isActive }, Json.Options), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Update tag failed: {body}");
        }
    }

    public async Task DeleteTagAsync(string tagId, CancellationToken ct = default)
    {
        var url = $"{_settings.SupabaseUrl}/rest/v1/momo_tags?id=eq.{Uri.EscapeDataString(tagId)}";
        using var req = new HttpRequestMessage(HttpMethod.Delete, url);
        await AddAuthHeadersAsync(req, ct);

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Delete tag failed: {body}");
        }
    }

    public async Task UpdateTagAsync(string tagId, string name, string color, string icon, CancellationToken ct = default)
    {
        var url = $"{_settings.SupabaseUrl}/rest/v1/momo_tags?id=eq.{Uri.EscapeDataString(tagId)}";
        using var req = new HttpRequestMessage(new HttpMethod("PATCH"), url);
        await AddAuthHeadersAsync(req, ct);
        req.Content = new StringContent(
            JsonSerializer.Serialize(new { name, color, icon }, Json.Options),
            Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Update tag failed: {body}");
        }
    }

    private static Memory ParseMemory(JsonElement el)
    {
        var tags = new List<string>();
        if (el.TryGetProperty("tags", out var t) && t.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in t.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                    tags.Add(item.GetString() ?? "");
            }
        }

        return new Memory
        {
            Id = el.TryGetProperty("id", out var id) ? (id.GetString() ?? "") : "",
            UserId = el.TryGetProperty("user_id", out var uid) ? (uid.GetString() ?? "") : "",
            Title = el.TryGetProperty("title", out var title) ? (title.GetString() ?? "Untitled Memory") : "Untitled Memory",
            Caption = el.TryGetProperty("caption", out var cap) ? cap.GetString() : null,
            VideoUrl = el.TryGetProperty("video_url", out var vu) ? vu.GetString() : null,
            ThumbnailUrl = el.TryGetProperty("thumbnail_url", out var tu) ? tu.GetString() : null,
            Tags = tags,
            IsFavorite = el.TryGetProperty("is_favorite", out var fav) && fav.GetBoolean(),
            CreatedAt = el.TryGetProperty("created_at", out var ca) && ca.ValueKind == JsonValueKind.String
                ? DateTimeOffset.Parse(ca.GetString()!)
                : DateTimeOffset.UtcNow,
            DateCaptured = el.TryGetProperty("date_captured", out var dc) && dc.ValueKind == JsonValueKind.String
                && DateOnly.TryParse(dc.GetString(), System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var parsedDc)
                ? parsedDc
                : null
        };
    }

    // -------- Storage helpers --------

    public async Task<string> UploadThumbnailAsync(string userId, string localFilePath, CancellationToken ct = default)
    {
        var fileName = $"{userId}/thumb_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.jpg";
        var uploadUrl = $"{_settings.SupabaseUrl}/storage/v1/object/{_settings.StorageBucketUserThumbnails}/{fileName}";
        var publicUrl = $"{_settings.SupabaseUrl}/storage/v1/object/public/{_settings.StorageBucketUserThumbnails}/{fileName}";

        using var fileStream = File.OpenRead(localFilePath);
        using var req = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        await AddAuthHeadersAsync(req, ct);
        req.Headers.Add("x-upsert", "false");
        req.Content = new StreamContent(fileStream);
        req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Thumbnail upload failed: {body}");

        return publicUrl;
    }

    /// <summary>
    /// Sets thumbnail_url on the remote momo_memories row. Returns true only if a row was actually
    /// updated; false on failure or when the id matches no remote row (e.g. a local-only/pending
    /// memory). Uses Prefer: return=representation so the response body lists the affected rows.
    /// </summary>
    public async Task<bool> UpdateMemoryThumbnailAsync(string memoryId, string thumbnailUrl, CancellationToken ct = default)
    {
        var url = $"{_settings.SupabaseUrl}/rest/v1/momo_memories?id=eq.{Uri.EscapeDataString(memoryId)}";
        using var req = new HttpRequestMessage(new HttpMethod("PATCH"), url);
        await AddAuthHeadersAsync(req, ct);
        req.Headers.Add("Prefer", "return=representation");   // needed so [] body distinguishes "no row matched"
        req.Content = new StringContent(
            JsonSerializer.Serialize(new { thumbnail_url = thumbnailUrl }, Json.Options),
            Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateMemoryThumbnailAsync failed (non-fatal): {body}");
            return false;
        }

        // 200 with an empty array means the id matched no remote row.
        try
        {
            var arr = JsonSerializer.Deserialize<List<JsonElement>>(body, Json.Options) ?? new();
            return arr.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> UploadVideoAsync(string userId, string localFilePath, string contentType, CancellationToken ct = default)
    {
        var fileName = $"{userId}/{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{Path.GetExtension(localFilePath)}";
        var uploadUrl = $"{_settings.SupabaseUrl}/storage/v1/object/{_settings.StorageBucketUserVideos}/{fileName}";
        var publicUrl = $"{_settings.SupabaseUrl}/storage/v1/object/public/{_settings.StorageBucketUserVideos}/{fileName}";

        using var fileStream = File.OpenRead(localFilePath);

        using var req = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        await AddAuthHeadersAsync(req, ct);
        req.Headers.Add("x-upsert", "false");
        req.Content = new StreamContent(fileStream);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Upload failed: {body}");

        return publicUrl;
    }

    // -------- Profile Management --------

    // ── Admin: all users ─────────────────────────────────────────────────────
    // Requires an RLS policy on momo_profiles that allows admin users to SELECT all rows.

    public async Task<List<AdminUserSummary>> GetAllProfilesAsync(CancellationToken ct = default)
    {
        if (_session is null) throw new InvalidOperationException("Not authenticated.");

        var url = $"{_settings.SupabaseUrl}/rest/v1/momo_profiles?select=id,full_name,is_admin,email&order=full_name.asc";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        await AddAuthHeadersAsync(req, ct);

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return new();

        var json = await resp.Content.ReadAsStringAsync(ct);
        var doc  = JsonDocument.Parse(json);
        var list = new List<AdminUserSummary>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            list.Add(new AdminUserSummary
            {
                Id       = el.TryGetProperty("id",        out var id)  ? id.GetString()  ?? "" : "",
                FullName = el.TryGetProperty("full_name", out var fn)  ? fn.GetString()       : null,
                Email    = el.TryGetProperty("email",     out var em)  ? em.GetString()       : null,
                IsAdmin  = el.TryGetProperty("is_admin",  out var ia)  && ia.GetBoolean()
            });
        }
        return list;
    }

    public async Task<Dictionary<string, int>> GetMemoryCountsByUserAsync(CancellationToken ct = default)
    {
        if (_session is null) return new();

        var url = $"{_settings.SupabaseUrl}/rest/v1/momo_memories?select=user_id";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        await AddAuthHeadersAsync(req, ct);

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return new();

        var json   = await resp.Content.ReadAsStringAsync(ct);
        var doc    = JsonDocument.Parse(json);
        var counts = new Dictionary<string, int>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            if (el.TryGetProperty("user_id", out var uid) && uid.GetString() is { } id)
                counts[id] = counts.GetValueOrDefault(id, 0) + 1;
        }
        return counts;
    }

    public async Task SetUserAdminAsync(string userId, bool isAdmin, CancellationToken ct = default)
    {
        if (_session is null) throw new InvalidOperationException("Not authenticated.");

        // Goes through the set_user_admin RPC rather than a direct PATCH: migration 006
        // revokes the authenticated role's UPDATE grant on is_admin (so users cannot promote
        // themselves), and the definer function re-checks the caller's own is_admin instead.
        var url = $"{_settings.SupabaseUrl}/rest/v1/rpc/set_user_admin";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        await AddAuthHeadersAsync(req, ct);
        req.Content = new StringContent(
            JsonSerializer.Serialize(new { target_id = userId, make_admin = isAdmin }, Json.Options),
            Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Failed to update admin status: {body}");
        }
    }

    public async Task DeleteUserProfileAsync(string userId, CancellationToken ct = default)
    {
        if (_session is null) throw new InvalidOperationException("Not authenticated.");

        var url = $"{_settings.SupabaseUrl}/rest/v1/momo_profiles?id=eq.{Uri.EscapeDataString(userId)}";
        using var req = new HttpRequestMessage(HttpMethod.Delete, url);
        await AddAuthHeadersAsync(req, ct);

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Failed to delete user profile: {body}");
        }
    }
    /// <summary>
    /// Permanently deletes the authenticated user's Storage objects, Auth identity,
    /// and database rows that reference auth.users with ON DELETE CASCADE. The
    /// service-role key remains inside the delete-account Edge Function.
    /// </summary>
    public async Task DeleteAccountAsync(CancellationToken ct = default)
    {
        if (_session is null) throw new InvalidOperationException("Not authenticated.");

        var url = $"{_settings.SupabaseUrl}/functions/v1/delete-account";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        await AddAuthHeadersAsync(req, ct);
        req.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Account deletion failed. Please try again or contact support. ({body})");
        }
    }
    /// <summary>
    /// Triggers the send-export-email Edge Function which generates 7-day signed URLs
    /// for every video the user owns and emails them to the given address.
    /// </summary>
    public async Task RequestDataExportAsync(string userId, string email, CancellationToken ct = default)
    {
        if (_session is null) throw new InvalidOperationException("Not authenticated.");

        var url     = $"{_settings.SupabaseUrl}/functions/v1/send-export-email";
        var payload = JsonSerializer.Serialize(new { userId, email }, Json.Options);

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        await AddAuthHeadersAsync(req, ct);
        req.Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Export request failed: {body}");
        }
    }

    public async Task<string> UploadAvatarAsync(string userId, string localFilePath, CancellationToken ct = default)
    {
        var ext       = Path.GetExtension(localFilePath).ToLowerInvariant();
        var fileName  = $"{userId}/avatar{ext}";
        var uploadUrl = $"{_settings.SupabaseUrl}/storage/v1/object/avatars/{fileName}";

        // The object path is stable per user, so without the version the public URL is byte-identical
        // after every upload. That made changing your picture look broken twice over: AvatarUrl was
        // being assigned its current value, so PropertyChanged never fired and the UI did nothing;
        // and when the URL did change (picking a .png after a .jpg) the image cache still had the
        // previous image under that exact URL and served it back. Versioning makes each upload a
        // distinct URL, which both raises the change notification and misses the cache.
        var version   = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var publicUrl = $"{_settings.SupabaseUrl}/storage/v1/object/public/avatars/{fileName}?v={version}";

        var mime = ext == ".png" ? "image/png" : "image/jpeg";

        using var fileStream = File.OpenRead(localFilePath);
        using var req        = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        await AddAuthHeadersAsync(req, ct);
        req.Headers.Add("x-upsert", "true"); // overwrite existing avatar
        req.Content = new StreamContent(fileStream);
        req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mime);

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Avatar upload failed: {body}");

        return publicUrl;
    }

    public async Task UpdateAvatarUrlAsync(string avatarUrl, CancellationToken ct = default)
    {
        if (_session is null) throw new InvalidOperationException("Not authenticated.");

        // Upsert (not PATCH) so this still lands if the user has no momo_profiles row yet —
        // a plain PATCH matches zero rows in that case and PostgREST reports success anyway,
        // silently discarding the change.
        var url = $"{_settings.SupabaseUrl}/rest/v1/momo_profiles";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        await AddAuthHeadersAsync(req, ct);
        req.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");
        req.Content = new StringContent(
            JsonSerializer.Serialize(new { id = _session.UserId, avatar_url = avatarUrl }, Json.Options),
            Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            System.Diagnostics.Debug.WriteLine($"UpdateAvatarUrlAsync failed (non-fatal): {body}");
        }
    }

    public async Task UpdateFullNameAsync(string fullName, CancellationToken ct = default)
    {
        if (_session is null) throw new InvalidOperationException("Not authenticated.");

        // Upsert (not PATCH) so this still lands if the user has no momo_profiles row yet —
        // a plain PATCH matches zero rows in that case and PostgREST reports success anyway,
        // silently discarding the change.
        var url = $"{_settings.SupabaseUrl}/rest/v1/momo_profiles";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        await AddAuthHeadersAsync(req, ct);
        req.Headers.Add("Prefer", "resolution=merge-duplicates,return=representation");
        req.Content = new StringContent(
            JsonSerializer.Serialize(new { id = _session.UserId, full_name = fullName }, Json.Options),
            Encoding.UTF8,
            "application/json");

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Failed to update profile: {body}");

        // Belt-and-suspenders: the upsert above should always affect exactly one row,
        // but return=representation lets us confirm that rather than assume it.
        var updated = JsonSerializer.Deserialize<List<JsonElement>>(body, Json.Options) ?? new();
        if (updated.Count == 0)
            throw new InvalidOperationException("Profile update didn't save. Please try again.");
    }

    /// <summary>
    /// Asks the verify-subscription Edge Function to validate the store receipt and record
    /// the resulting entitlement. Replaces the old UpdatePremiumStatusAsync, which let the
    /// client assert its own premium status — a one-way latch that could never revoke, and
    /// that any user could set on themselves. The service role now owns is_premium.
    /// </summary>
    /// <returns>True if the store says the subscription is currently active.</returns>
    /// <exception cref="InvalidOperationException">
    /// The store could not be reached or the receipt was rejected. Callers must treat this as
    /// "unknown", never as "not subscribed" — a network blip must not strip a paying customer.
    /// </exception>
    public async Task<bool> VerifySubscriptionAsync(string platform, string proof, CancellationToken ct = default)
    {
        if (_session is null) throw new InvalidOperationException("Not authenticated.");

        var url = $"{_settings.SupabaseUrl}/functions/v1/verify-subscription";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        await AddAuthHeadersAsync(req, ct);
        req.Content = new StringContent(
            JsonSerializer.Serialize(new { platform, receipt = proof }, Json.Options),
            Encoding.UTF8,
            "application/json");

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Could not verify subscription: {body}");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("isPremium", out var premium) && premium.GetBoolean();
    }

    // -------- Password Reset (Contract Section 2.1) --------
    // APPLICATION-SPECIFIC CODE - Property of Client (Corinne Kelley)

    /// <summary>
    /// Sends password reset email to user (Contract requirement Section 2.1).
    /// </summary>
    public async Task SendPasswordResetEmailAsync(string email, CancellationToken ct = default)
    {
        var url = $"{_settings.SupabaseUrl}/auth/v1/recover";
        var payload = new { email };

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        AddAnonHeaders(req);
        req.Content = new StringContent(JsonSerializer.Serialize(payload, Json.Options), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        // Supabase returns 200 even if email doesn't exist (security best practice)
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Password reset request failed: {body}");
    }

    // -------- Subscription Management (Contract Section 2.6, 2.7) --------
    // APPLICATION-SPECIFIC CODE - Property of Client (Corinne Kelley)

    /// <summary>
    /// Gets subscription configuration from backend (Contract Section 4.3).
    /// Allows client to configure pricing and limits without app updates.
    /// </summary>
    public async Task<SubscriptionConfig> GetSubscriptionConfigAsync(CancellationToken ct = default)
    {
        var url = $"{_settings.SupabaseUrl}/rest/v1/momo_subscriptions?select=*&limit=1";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        AddAnonHeaders(req);

        try
        {
            using var resp = await _http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                var arr = JsonSerializer.Deserialize<List<JsonElement>>(body, Json.Options) ?? new();

                if (arr.Count > 0)
                {
                    var el = arr[0];
                    return new SubscriptionConfig
                    {
                        FreeUserVideoLimit = el.TryGetProperty("free_video_limit",  out var fvl) ? fvl.GetInt32()   : 100,
                        PaidUserVideoLimit = el.TryGetProperty("paid_video_limit",  out var pvl) ? pvl.GetInt32()   : 500,
                        MonthlyPrice       = el.TryGetProperty("monthly_price",     out var mp)  ? mp.GetDecimal()  : 4.99m,
                        AnnualPrice        = el.TryGetProperty("annual_price",      out var ap)  ? ap.GetDecimal()  : 49.99m,
                        MonthlyEnabled     = el.TryGetProperty("monthly_enabled",   out var me)  ? me.GetBoolean()  : true,
                        AnnualEnabled      = el.TryGetProperty("annual_enabled",    out var ae)  ? ae.GetBoolean()  : true
                    };
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetSubscriptionConfigAsync: {ex.Message}");
        }

        return new SubscriptionConfig();
    }

    public async Task SaveSubscriptionConfigAsync(SubscriptionConfig config, CancellationToken ct = default)
    {
        var url = $"{_settings.SupabaseUrl}/rest/v1/momo_subscriptions?id=eq.1";

        var payload = new
        {
            free_video_limit = config.FreeUserVideoLimit,
            paid_video_limit = config.PaidUserVideoLimit,
            monthly_price    = config.MonthlyPrice,
            annual_price     = config.AnnualPrice,
            monthly_enabled  = config.MonthlyEnabled,
            annual_enabled   = config.AnnualEnabled
        };

        using var req = new HttpRequestMessage(HttpMethod.Patch, url);
        await AddAuthHeadersAsync(req, ct);
        req.Headers.Add("Prefer", "return=minimal");
        req.Content = new StringContent(JsonSerializer.Serialize(payload, Json.Options), System.Text.Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Failed to save subscription config: {body}");
        }
    }

    /// <summary>
    /// Gets the count of videos for a user (Contract Section 2.7).
    /// </summary>
    public async Task<int> GetUserVideoCountAsync(string userId, CancellationToken ct = default)
    {
        var url = $"{_settings.SupabaseUrl}/rest/v1/momo_memories?user_id=eq.{Uri.EscapeDataString(userId)}&select=id";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        await AddAuthHeadersAsync(req, ct);
        req.Headers.Add("Prefer", "count=exact");

        using var resp = await _http.SendAsync(req, ct);

        if (!resp.IsSuccessStatusCode)
            return 0;

        // Get count from Content-Range header
        if (resp.Content.Headers.TryGetValues("Content-Range", out var values))
        {
            var range = values.FirstOrDefault();
            if (range != null && range.Contains('/'))
            {
                var parts = range.Split('/');
                if (parts.Length > 1 && int.TryParse(parts[1], out var count))
                    return count;
            }
        }

        // Fallback: parse array length
        var body = await resp.Content.ReadAsStringAsync(ct);
        var arr = JsonSerializer.Deserialize<List<JsonElement>>(body, Json.Options) ?? new();
        return arr.Count;
    }

    // -------- Memory Metrics (AI-weighted surfacing) --------

    /// <summary>
    /// Loads the weight for every memory belonging to this user from momo_memory_metrics.
    /// Returns an empty dictionary if the table doesn't exist yet (graceful pre-migration).
    /// </summary>
    public async Task<Dictionary<string, int>> GetMemoryWeightsAsync(string userId, CancellationToken ct = default)
    {
        try
        {
            var url = $"{_settings.SupabaseUrl}/rest/v1/momo_memory_metrics?user_id=eq.{Uri.EscapeDataString(userId)}&select=memory_id,weight";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            await AddAuthHeadersAsync(req, ct);

            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return new Dictionary<string, int>();

            var body2 = await resp.Content.ReadAsStringAsync(ct);
            var arr2 = JsonSerializer.Deserialize<List<JsonElement>>(body2, Json.Options) ?? new();
            return arr2
                .Where(el => el.TryGetProperty("memory_id", out _))
                .ToDictionary(
                    el => el.GetProperty("memory_id").GetString() ?? "",
                    el => el.TryGetProperty("weight", out var w) ? w.GetInt32() : 1
                );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetMemoryWeightsAsync: {ex.Message}");
            return new Dictionary<string, int>();
        }
    }

    /// <summary>
    /// Upserts a weight record for a single memory.
    /// Uses PostgREST merge-duplicates resolution on the (memory_id, user_id) unique key.
    /// Silently no-ops if the momo_memory_metrics table doesn't exist yet.
    /// </summary>
    public async Task UpsertMemoryWeightAsync(string memoryId, string userId, int newWeight, CancellationToken ct = default)
    {
        try
        {
            var url = $"{_settings.SupabaseUrl}/rest/v1/momo_memory_metrics";
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            await AddAuthHeadersAsync(req, ct);
            req.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");

            var payload = new
            {
                memory_id = memoryId,
                user_id   = userId,
                weight    = Math.Max(0, newWeight)   // enforce minimum of 0
            };
            req.Content = new StringContent(
                JsonSerializer.Serialize(payload, Json.Options),
                Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body2 = await resp.Content.ReadAsStringAsync(ct);
                System.Diagnostics.Debug.WriteLine($"UpsertMemoryWeightAsync failed (non-fatal): {body2}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpsertMemoryWeightAsync: {ex.Message}");
        }
    }
}

/// <summary>
/// Thrown when Supabase signup succeeds but email confirmation is required before sign-in.
/// </summary>
public sealed class EmailConfirmationRequiredException : Exception
{
    public EmailConfirmationRequiredException()
        : base("Your account was created. Please check your email and click the confirmation link before signing in.") { }
}
