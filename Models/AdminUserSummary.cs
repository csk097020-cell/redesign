namespace MomentaryMomentos.Models;

public sealed class AdminUserSummary
{
    public string Id { get; set; } = "";
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public bool IsAdmin { get; set; }
    public int MemoryCount { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(FullName) ? "(No name)" : FullName;
    public string ShortId => Id.Length > 8 ? Id[..8] + "…" : Id;
}
