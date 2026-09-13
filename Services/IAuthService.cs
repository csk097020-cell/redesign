// APPLICATION-SPECIFIC CODE - Property of Client (Corinne Kelley)
using MomentaryMomentos.Models;

namespace MomentaryMomentos.Services;

public interface IAuthService
{
    bool IsAuthenticated { get; }
    string? CurrentUserId { get; }
    string? CurrentEmail { get; }
    Task<AuthSession?> SignInAsync(string email, string password, CancellationToken ct = default);
    Task<AuthSession?> SignUpAsync(string email, string password, string? fullName = null, CancellationToken ct = default);
    Task SignOutAsync();
    Task<bool> TryRestoreSessionAsync(CancellationToken ct = default);
    Task SendPasswordResetEmailAsync(string email, CancellationToken ct = default);
}
