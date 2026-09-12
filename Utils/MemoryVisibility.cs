using MomentaryMomentos.Models;

namespace MomentaryMomentos.Utils;

/// <summary>
/// Hiding a tag in Manage Tags "pauses" every momento carrying that tag: it stops
/// appearing in the Relive feed, Surprise Me, the detail carousel/dice, and the Home
/// featured/recent cards until the tag is restored. This applies even to favorites —
/// hiding is an explicit emotional-consent action, so it wins over the favorite boost.
/// The momentos themselves are untouched; restoring the tag brings them all back.
/// </summary>
public static class MemoryVisibility
{
    /// <summary>IDs of tags currently hidden (inactive) — the pause list.</summary>
    public static HashSet<string> HiddenTagIds(IEnumerable<Tag> tags) =>
        tags.Where(t => !t.IsActive).Select(t => t.Id).ToHashSet();

    /// <summary>True when the memory carries at least one hidden tag.</summary>
    public static bool IsPaused(Memory memory, HashSet<string> hiddenTagIds) =>
        hiddenTagIds.Count > 0
        && memory.Tags is not null
        && memory.Tags.Any(hiddenTagIds.Contains);

    /// <summary>Filters a memory list down to the ones not paused by a hidden tag.</summary>
    public static List<Memory> ExcludePaused(IEnumerable<Memory> memories, IEnumerable<Tag> tags)
    {
        var hidden = HiddenTagIds(tags);
        return hidden.Count == 0
            ? memories.ToList()
            : memories.Where(m => !IsPaused(m, hidden)).ToList();
    }
}
