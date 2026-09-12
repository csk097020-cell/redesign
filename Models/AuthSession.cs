namespace MomentaryMomentos.Models;

public sealed record AuthSession(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    string UserId,
    string Email
);
