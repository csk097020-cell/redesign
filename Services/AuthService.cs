// APPLICATION-SPECIFIC CODE - Property of Client (Corinne Kelley)
using MomentaryMomentos.Models;

namespace MomentaryMomentos.Services;

/// <summary>
/// Auth facade that delegates to SupabaseService and persists session tokens securely.
/// Contract Appendix A Section 2.1: Create account, Login, Logout, Reset password.
/// </summary>
public class AuthService : IAuthService
{
    private readonly SupabaseService _supabase;
    private readonly SecureTokenStore _tokenStore;

    public AuthService(SupabaseService supabase, SecureTokenStore tokenStore)
    {
        _supabase = supabase;
        _tokenStore = tokenStore;
    }

    public bool IsAuthenticated => _supabase.IsAuthenticated;
    public string? CurrentUserId => _supabase.Session?.UserId;
    public string? CurrentEmail => _supabase.Session?.Email;

    public async Task<AuthSession?> SignInAsync(string email, string password, CancellationToken ct = default)
    {
        var session = await _supabase.SignInAsync(email, password, ct);
        await _tokenStore.SaveAsync(session);
        return session;
    }

    public async Task<AuthSession?> SignUpAsync(string email, string password, string? fullName = null, CancellationToken ct = default)
    {
        var session = await _supabase.SignUpAsync(email, password, fullName, ct);
        await _tokenStore.SaveAsync(session);
        return session;
    }

    public async Task SignOutAsync()
    {
        _supabase.SignOut();
        await _tokenStore.ClearAsync();
    }

    public async Task<bool> TryRestoreSessionAsync(CancellationToken ct = default)
    {
        var stored = await _tokenStore.LoadAsync();
        if (stored is null) return false;

        _supabase.SetSession(stored);

        // Refresh if near expiry
        if (DateTimeOffset.UtcNow >= stored.ExpiresAt)
        {
            try { await _supabase.RefreshAsync(ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TryRestoreSessionAsync: Token refresh failed, clearing session: {ex.Message}");
                await _tokenStore.ClearAsync();
                return false;
            }
        }
        return true;
    }

    public async Task SendPasswordResetEmailAsync(string email, CancellationToken ct = default)
    {
        // Delegates to SupabaseService which has direct HTTP access
        await _supabase.SendPasswordResetEmailAsync(email, ct);
    }
}
