// GENERAL-PURPOSE COMPONENT - Licensed to Client per contract Section 6.2
using System.Text.Json;
using MomentaryMomentos.Models;

namespace MomentaryMomentos.Services;

/// <summary>
/// Persists JWT session tokens using platform secure storage.
/// iOS: Keychain. Android: EncryptedSharedPreferences via MAUI SecureStorage.
/// </summary>
public sealed class SecureTokenStore
{
    private const string Key = "mm_auth_session_v1";

    public async Task SaveAsync(AuthSession session)
    {
        var json = JsonSerializer.Serialize(session);
        await SecureStorage.Default.SetAsync(Key, json);
    }

    /// <summary>Load a previously saved session. Returns null if none exists or JSON is invalid.</summary>
    public async Task<AuthSession?> LoadAsync()
    {
        try
        {
            var json = await SecureStorage.Default.GetAsync(Key);
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonSerializer.Deserialize<AuthSession>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Alias kept for callers that use TryLoadAsync name.</summary>
    public Task<AuthSession?> TryLoadAsync() => LoadAsync();

    public Task ClearAsync()
    {
        SecureStorage.Default.Remove(Key);
        return Task.CompletedTask;
    }

    /// <summary>Synchronous clear — kept for legacy callers.</summary>
    public void Clear() => SecureStorage.Default.Remove(Key);
}
