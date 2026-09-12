using CommunityToolkit.Mvvm.ComponentModel;

namespace MomentaryMomentos.Models;

public sealed partial class Tag : ObservableObject
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#3B82F6";

    /// <summary>
    /// Legacy emoji. Tags no longer display an icon — this is kept only as a
    /// faithful representation of the NOT NULL momo_tags.icon column so it
    /// round-trips through the Supabase/SQLite payloads. Never shown in the UI.
    /// </summary>
    public string Icon { get; set; } = "✨";
    public bool IsActive { get; set; } = true;
    public string? UserId { get; set; } // null = default tag

    /// <summary>
    /// UI-only selection state for the capture/tagging flow. Not persisted —
    /// neither the Supabase payload nor the local TagRow reads this property.
    /// </summary>
    [ObservableProperty] private bool isSelected;

    public string DisplayLabel => Name;
}
