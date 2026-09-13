using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MomentaryMomentos.Models;
using MomentaryMomentos.Services;

namespace MomentaryMomentos.ViewModels;

public sealed partial class TagUsageItem : ObservableObject
{
    public string Id { get; init; } = "";

    /// <summary>Owner of the tag; null/empty = built-in default tag (hide-only, not deletable).</summary>
    public string? UserId { get; init; }

    public bool IsDeletable => !string.IsNullOrEmpty(UserId);

    [ObservableProperty] private string name = "";
    [ObservableProperty] private string color = "#3B82F6";
    [ObservableProperty] private string icon = "";
    [ObservableProperty] private bool isActive;
    [ObservableProperty] private int memoryCount;

    // Tags display name-only; Icon is retained for data round-trip but never shown.
    public string DisplayLabel => Name;

    public string UsageLabel => MemoryCount == 1 ? "1 memory" : $"{MemoryCount} memories";
    public string StatusLabel => IsActive ? "Active" : "Hidden";
    public string ToggleLabel => IsActive ? "Hide" : "Restore";

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(DisplayLabel));
    partial void OnMemoryCountChanged(int value) => OnPropertyChanged(nameof(UsageLabel));
    partial void OnIsActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(ToggleLabel));
    }
}

public partial class TagManagementViewModel : BaseViewModel
{
    private readonly SupabaseService _supabase;
    private readonly SyncService _sync;

    public ObservableCollection<TagUsageItem> Tags { get; } = new();

    // Kept from Load so Delete can strip the tag off each momento that carries it.
    private List<Memory> _memories = new();

    [ObservableProperty] private bool hasTags;

    public TagManagementViewModel(SupabaseService supabase, SyncService sync)
    {
        _supabase = supabase;
        _sync = sync;
    }

    [RelayCommand]
    private Task Load() => RunBusyAsync(async () =>
    {
        var session = _supabase.Session;
        if (session is null)
        {
            Tags.Clear();
            HasTags = false;
            return;
        }

        var tagsTask = _sync.GetTagsWithCacheAsync(session.UserId, includeInactive: true);
        var memoriesTask = _sync.GetMemoriesWithCacheAsync(session.UserId);

        await Task.WhenAll(tagsTask, memoriesTask);

        _memories = memoriesTask.Result;

        var counts = _memories
            .SelectMany(memory => memory.Tags ?? new List<string>())
            .GroupBy(tagId => tagId)
            .ToDictionary(group => group.Key, group => group.Count());

        Tags.Clear();
        foreach (var tag in tagsTask.Result
                     .OrderByDescending(tag => counts.GetValueOrDefault(tag.Id))
                     .ThenBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase))
        {
            Tags.Add(new TagUsageItem
            {
                Id = tag.Id,
                UserId = tag.UserId,
                Name = tag.Name,
                Color = tag.Color,
                Icon = tag.Icon,
                IsActive = tag.IsActive,
                MemoryCount = counts.GetValueOrDefault(tag.Id)
            });
        }

        HasTags = Tags.Count > 0;
    });

    [RelayCommand]
    private async Task Edit(TagUsageItem? item)
    {
        if (item is null || IsBusy)
            return;

        var page = Application.Current?.MainPage;
        if (page is null)
            return;

        var name = await page.DisplayPromptAsync(
            "Edit tag name",
            "Change the name shown on memories.",
            initialValue: item.Name,
            maxLength: 40);

        if (name is null)
            return;

        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            await page.DisplayAlert("Tag name required", "Tags need a visible name.", "OK");
            return;
        }

        await RunBusyAsync(async () =>
        {
            // Color and icon are not user-editable; pass the existing values through unchanged.
            await _supabase.UpdateTagAsync(item.Id, name, item.Color, item.Icon);

            item.Name = name;
        });
    }

    [RelayCommand]
    private async Task ToggleActive(TagUsageItem? item)
    {
        if (item is null || IsBusy)
            return;

        var page = Application.Current?.MainPage;
        var newActiveState = !item.IsActive;
        if (!newActiveState && page is not null)
        {
            var confirmed = await page.DisplayAlert(
                "Hide this tag?",
                "Momentos with this tag are paused — they won't appear in your feed, Surprise Me, or Today's Favorite until you restore the tag. The momentos and their tag are kept, and the tag stops being offered when tagging.",
                "Hide",
                "Cancel");

            if (!confirmed)
                return;
        }

        await RunBusyAsync(async () =>
        {
            await _supabase.SetTagActiveAsync(item.Id, newActiveState);
            item.IsActive = newActiveState;
        });
    }

    [RelayCommand]
    private async Task Delete(TagUsageItem? item)
    {
        if (item is null || IsBusy || !item.IsDeletable)
            return;

        var page = Application.Current?.MainPage;
        if (page is null)
            return;

        var usage = item.MemoryCount == 1 ? "1 momento" : $"{item.MemoryCount} momentos";
        var confirmed = await page.DisplayAlert(
            "Delete this tag?",
            $"\"{item.Name}\" will be removed from {usage}. The momentos themselves are not deleted. This cannot be undone.",
            "Delete",
            "Cancel");

        if (!confirmed)
            return;

        await RunBusyAsync(async () =>
        {
            // Strip the tag from every momento that carries it, then delete the tag row.
            // Order matters: if a PATCH fails midway, the tag still exists and a retry
            // is safe; a dangling tag id on a memory would otherwise linger invisibly.
            foreach (var memory in _memories.Where(m => m.Tags?.Contains(item.Id) == true))
            {
                var remaining = memory.Tags.Where(id => id != item.Id).ToList();
                await _supabase.UpdateMemoryDetailsAsync(memory.Id, memory.Title, memory.Caption, remaining);
                memory.Tags = remaining;
            }

            await _supabase.DeleteTagAsync(item.Id);
            Tags.Remove(item);
            HasTags = Tags.Count > 0;
        });
    }
}
