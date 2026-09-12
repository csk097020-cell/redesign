// APPLICATION-SPECIFIC CODE - Property of Client (Corinne Kelley)
using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using MomentaryMomentos.Models;
using MomentaryMomentos.Services;
using MomentaryMomentos.Utils;

namespace MomentaryMomentos.ViewModels;

/// <summary>Wraps a Tag with a mutable selection state for the retag panel.</summary>
public partial class TagSelection : ObservableObject
{
    public Tag Tag { get; set; } = null!;

    [ObservableProperty] private bool isSelected;
}

/// <summary>
/// Full-screen single-memory view: carousel nav, retag, see-less, favorite, delete.
/// TODO #2 (carousel), #3 (retag), #4 (see-less).
/// </summary>
public partial class MemoryDetailViewModel : BaseViewModel, IQueryAttributable
{
    private const string SkippedPrefsKey = "momo_skipped_memories";

    private readonly SupabaseService _supabase;
    private readonly SyncService _sync;

    private List<Memory>             _allMemories   = new();
    private List<Tag>                _allTags       = new();
    private Dictionary<string, int>  _memoryWeights = new();
    private int                      _currentIndex;
    private bool                     _singleMemoryMode;

    // FIX 1 — post-playback return. Captured immediately before playback so we can re-select
    // the exact memory that played on return (by ID, never by list position or a re-fetch).
    private string?                  _lastPlayedMemoryId;
    // Guards against a back-nav re-fire of ApplyQueryAttributes reloading the pool/index and
    // clobbering the current selection. This page instance loads exactly once.
    private bool                     _hasLoaded;

    public bool IsSingleMemoryMode => _singleMemoryMode;

    // ── Current memory state ─────────────────────────────────────────────────
    [ObservableProperty] private Memory? currentMemory;
    [ObservableProperty] private bool    isCurrentFavorite;
    [ObservableProperty] private bool    hasPrevious;
    [ObservableProperty] private bool    hasNext;
    [ObservableProperty] private string  positionLabel = "";

    // Tag display info (name + color) resolved from IDs — used for colored pills and gradient
    [ObservableProperty] private List<TagInfo> currentDisplayTags = new();

    // Dynamic gradient brush derived from tag colors, matching PWA MemoryPlayback behavior
    [ObservableProperty] private Brush tagGradientBrush = new SolidColorBrush(Color.FromArgb("#050B1F"));

    // Relative date string ("Today", "3 days ago", etc.)
    [ObservableProperty] private string relativeDate = "";

    // Short calendar date shown on the preview cover above "You're about to relive:"
    // (e.g. "6/12/26") — the captured date when known, otherwise the upload date. Pairs
    // with the relative counter ("5 weeks ago") so both are always visible together.
    [ObservableProperty] private string coverDate = "";

    // Combined date line: "Captured … · Uploaded …" (or just "Uploaded …" for legacy
    // mementos with no captured date). Computed so the XAML stays a simple binding.
    [ObservableProperty] private string dateLine = "";

    // FIX 5 — shown BELOW the image, only when the captured date differs from the upload date;
    // empty (hidden) otherwise. Days-ago and tags live only up top now, so there's no dup.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCapturedDate))]
    private string capturedDateDisplay = "";

    public bool HasCapturedDate => !string.IsNullOrEmpty(CapturedDateDisplay);

    // Preview state: true = show "about to relive" screen; false = show thumbnail + actions
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPreviewVisible))]
    private bool isPreview = true;

    // ── Retag panel ──────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPreviewVisible))]
    private bool isRetagging;

    // The preview hero overlay must hide while the retag panel is open, otherwise
    // it draws on top of the panel (it's the last child of the root grid).
    public bool IsPreviewVisible => IsPreview && !IsRetagging;

    public ObservableCollection<TagSelection> AllTags { get; } = new();

    // ── Retag panel — Title/Caption editing (#3) ─────────────────────────────
    [ObservableProperty] private string? retagTitleInput;
    [ObservableProperty] private string? retagCaptionInput;

    // ── Constructor ──────────────────────────────────────────────────────────
    public MemoryDetailViewModel(
        SupabaseService supabase,
        SyncService sync)
    {
        _supabase = supabase;
        _sync = sync;
    }

    // ── Navigation entry point ───────────────────────────────────────────────
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        // Load once per page instance. Shell can re-invoke this on back-navigation with the
        // original query; ignoring re-fires keeps the selection the user actually played
        // (e.g. after the 🎲 dice) instead of snapping back to the route's original memoryId.
        if (_hasLoaded) return;

        string? memoryId = null;

        if (query.TryGetValue("memoryId", out var mid))
            memoryId = Uri.UnescapeDataString(mid.ToString()!);
        // The route may carry a "userId" param, but we intentionally ignore it:
        // the memory pool (and the FindAnother dice) must be scoped to the current
        // session user, never a route-supplied id, so this page can never fetch or
        // play another user's memories. See ReliveViewModel/HomeViewModel which
        // already scope to the session.
        if (query.TryGetValue("single", out var single))
            _singleMemoryMode = bool.TryParse(Uri.UnescapeDataString(single.ToString()!), out var parsed) && parsed;
        else
            _singleMemoryMode = false;

        var userId = _supabase.Session?.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            System.Diagnostics.Debug.WriteLine("MemoryDetailViewModel: no active session — aborting load.");
            return;
        }

        if (!string.IsNullOrEmpty(memoryId))
        {
            _hasLoaded = true;
            _ = LoadAsync(memoryId, userId);
        }
    }

    /// <summary>
    /// Called by the page's OnAppearing. When returning from the video player, re-select the
    /// exact memory that just played (matched by the ID captured before playback) and reset to
    /// its title card. No-op on first appearance (nothing has played yet). This is the fix for
    /// "post-playback returns to the wrong memory": it never trusts list position or a re-fetch.
    /// </summary>
    public void RestorePlayedMemory()
    {
        if (_lastPlayedMemoryId is null) return;

        var id = _lastPlayedMemoryId;
        _lastPlayedMemoryId = null;

        var idx = _allMemories.FindIndex(m => m.Id == id);
        if (idx >= 0)
        {
            _currentIndex = idx;
            UpdateCurrentMemory(); // resets IsPreview = true → the exact memory's title card
        }
        else
        {
            // The memory is gone (e.g. deleted during playback) — just show the title card.
            IsPreview = true;
        }
    }

    private async Task LoadAsync(string memoryId, string userId)
    {
        await RunBusyAsync(async () =>
        {
            // Load memories, tags, and weights in parallel
            var memoriesTask = _sync.GetMemoriesWithCacheAsync(userId);
            // Include inactive tags so chips for deactivated (legacy) tags still resolve on existing memories.
            var tagsTask     = _sync.GetTagsWithCacheAsync(userId, includeInactive: true);
            var weightsTask  = _supabase.GetMemoryWeightsAsync(userId);

            _allMemories   = await memoriesTask;
            _allTags       = await tagsTask;
            _memoryWeights = await weightsTask;

            // Defense-in-depth: never let a row belonging to another user into the
            // carousel/dice, even if the server query ever returns extra rows. The
            // pool is always scoped to the current session user.
            _allMemories = _allMemories
                .Where(m => m.UserId == userId)
                .ToList();

            // Momentos paused by a hidden tag (#8) stay out of the carousel and dice.
            // The entry memory survives the filter so a direct navigation never lands
            // on an empty page.
            var hiddenTagIds = MemoryVisibility.HiddenTagIds(_allTags);
            _allMemories = _allMemories
                .Where(m => m.Id == memoryId || !MemoryVisibility.IsPaused(m, hiddenTagIds))
                .ToList();

            _currentIndex = _allMemories.FindIndex(m => m.Id == memoryId);
            if (_currentIndex < 0) _currentIndex = 0;
            if (_singleMemoryMode && _allMemories.Count > 0)
            {
                _allMemories = _allMemories
                    .Skip(_currentIndex)
                    .Take(1)
                    .ToList();
                _currentIndex = 0;
            }

            UpdateCurrentMemory();
        });
    }

    // ── Navigation ───────────────────────────────────────────────────────────
    private void UpdateCurrentMemory()
    {
        if (_allMemories.Count == 0)
        {
            CurrentMemory        = null;
            HasPrevious          = false;
            HasNext              = false;
            PositionLabel        = "";
            CurrentDisplayTags   = new();
            TagGradientBrush     = new SolidColorBrush(Color.FromArgb("#050B1F"));
            RelativeDate         = "";
            CoverDate            = "";
            DateLine             = "";
            IsCurrentFavorite    = false;
            IsRetagging          = false;
            return;
        }

        CurrentMemory     = _allMemories[_currentIndex];
        HasPrevious       = _currentIndex > 0;
        HasNext           = _currentIndex < _allMemories.Count - 1;
        PositionLabel     = $"{_currentIndex + 1} / {_allMemories.Count}";
        IsCurrentFavorite = CurrentMemory.IsFavorite;
        IsRetagging       = false;

        // Resolve tag IDs → display info (name + color + icon), matching RelivePage approach
        CurrentDisplayTags = CurrentMemory.Tags
            .Select(id => _allTags.FirstOrDefault(t => t.Id == id))
            .Where(t => t is not null)
            .Select(t => new TagInfo(t!.Name, t.Color, t.Icon))
            .ToList();

        // Dynamic gradient from tag colors, matching PWA MemoryPlayback.tsx behavior
        TagGradientBrush = BuildTagGradient(CurrentDisplayTags);

        // Relative date ("Today", "3 days ago", etc.) shown below absolute date
        RelativeDate = GetRelativeDate(CurrentMemory.CreatedAt);

        // Cover date (#7): prefer the user-entered captured date; fall back to upload date.
        var coverDateOnly = CurrentMemory.DateCaptured
                            ?? DateOnly.FromDateTime(CurrentMemory.CreatedAt.LocalDateTime.Date);
        CoverDate = coverDateOnly.ToString("M/d/yy");

        // Combined captured/uploaded date line (retained for other surfaces).
        var uploaded = CurrentMemory.CreatedAtLocal.ToString("MMM d, yyyy");
        DateLine = CurrentMemory.DateCaptured is { } captured
            ? $"Captured {captured:MMM d, yyyy}  ·  Uploaded {uploaded}"
            : $"Uploaded {uploaded}";

        // FIX 5: below the image show ONLY the captured date, and only when it differs from
        // the upload date; otherwise hide it entirely.
        // .LocalDateTime.Date, not .Date: the latter is the UTC calendar day, so an evening
        // upload read as tomorrow and this "do they differ?" test fired on dates that were
        // actually the same day, showing a redundant "Captured ..." line. Matches coverDateOnly.
        var uploadedDate = DateOnly.FromDateTime(CurrentMemory.CreatedAt.LocalDateTime.Date);
        CapturedDateDisplay = CurrentMemory.DateCaptured is { } cap && cap != uploadedDate
            ? $"Captured {cap:MMM d, yyyy}"
            : "";

        // Reset to preview state whenever we land on a new memory
        IsPreview = true;
    }

    [RelayCommand]
    private void Previous()
    {
        if (_currentIndex > 0)
        {
            _currentIndex--;
            UpdateCurrentMemory();
        }
    }

    [RelayCommand]
    private void Next()
    {
        if (_currentIndex < _allMemories.Count - 1)
        {
            _currentIndex++;
            UpdateCurrentMemory();
        }
    }

    [RelayCommand]
    private void FindAnother()
    {
        if (_allMemories.Count <= 1) return;

        // Build weighted pool (same logic as ReliveViewModel.SurpriseMe)
        var pool = _allMemories
            .Select((m, i) => (Memory: m, Index: i))
            .Where(x => x.Index != _currentIndex)
            .SelectMany(x => Enumerable.Repeat(x, Math.Max(1, _memoryWeights.GetValueOrDefault(x.Memory.Id, 1))))
            .ToList();

        if (pool.Count == 0) return;

        var pick = pool[Random.Shared.Next(pool.Count)];
        _currentIndex = pick.Index;
        UpdateCurrentMemory();
    }

    // ── Playback ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task Play()
    {
        try
        {
            if (CurrentMemory is null) return;

            var url = CurrentMemory.VideoUrl ?? CurrentMemory.LocalVideoPath;
            if (string.IsNullOrWhiteSpace(url))
            {
                _ = Toast.Make("No video available for this memory yet.").Show();
                return;
            }

            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !File.Exists(url))
            {
                _ = Toast.Make("Local video file is no longer available.").Show();
                return;
            }

            // Remember exactly which memory is playing so we return to ITS title card, not
            // whatever the list/index happens to resolve to afterwards (FIX 1).
            _lastPlayedMemoryId = CurrentMemory.Id;

            IsPreview = false;
            await Shell.Current.GoToAsync(
                $"video?url={Uri.EscapeDataString(url)}" +
                $"&title={Uri.EscapeDataString(CurrentMemory.Title)}" +
                $"&recordedAt={Uri.EscapeDataString(CurrentMemory.CreatedAt.ToString("O"))}" +
                $"&memoryId={Uri.EscapeDataString(CurrentMemory.Id)}" +
                $"&isFavorite={CurrentMemory.IsFavorite}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Play failed: {ex}");
            _ = Toast.Make("Couldn't open this memory.").Show();
        }
    }

    // ── Favorite ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task ToggleFavorite()
    {
        if (CurrentMemory is null) return;

        var newState = !IsCurrentFavorite;
        try
        {
            await _supabase.UpdateFavoriteAsync(CurrentMemory.Id, newState);
            CurrentMemory.IsFavorite = newState;
            IsCurrentFavorite        = newState;
            _ = Toast.Make(newState ? "❤️ Added to favorites" : "Removed from favorites").Show();

            // When favoriting (not un-favoriting) boost AI weight by +2, matching PWA behavior
            var userId = _supabase.Session?.UserId;
            if (newState && !string.IsNullOrEmpty(userId))
            {
                var current  = _memoryWeights.GetValueOrDefault(CurrentMemory.Id, 1);
                var boosted  = current + 2;
                _memoryWeights[CurrentMemory.Id] = boosted;
                _ = _supabase.UpsertMemoryWeightAsync(CurrentMemory.Id, userId, boosted);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ToggleFavorite: {ex.Message}");
        }
    }

    // ── Retag (#3) ────────────────────────────────────────────────────────────
    [RelayCommand]
    private void ToggleRetag()
    {
        if (CurrentMemory is null) return;

        IsRetagging = !IsRetagging;
        if (IsRetagging)
            PopulateRetagPanel();
    }

    private void PopulateRetagPanel()
    {
        RetagTitleInput   = CurrentMemory?.Title;
        RetagCaptionInput = CurrentMemory?.Caption;

        AllTags.Clear();
        var applied = CurrentMemory?.Tags ?? new List<string>();

        // Offer the current (active) tags, plus any deactivated tag already applied to
        // this memory so it stays visible and is preserved on save rather than dropped.
        // Defensively de-dupe case-insensitively (FIX 2c) so nothing shows twice even before
        // the 004 merge migration runs; prefer an applied tag as the survivor so the current
        // selection still resolves to a visible chip.
        var candidates = _allTags.Where(t => t.IsActive || applied.Contains(t.Id));
        foreach (var tag in TagText.Dedupe(candidates, prefer: t => applied.Contains(t.Id)))
        {
            AllTags.Add(new TagSelection
            {
                Tag        = tag,
                IsSelected = applied.Contains(tag.Id)
            });
        }
    }

    [RelayCommand]
    private void ToggleRetagSelection(TagSelection ts)
    {
        ts.IsSelected = !ts.IsSelected;
    }

    [RelayCommand]
    private async Task SaveRetag()
    {
        if (CurrentMemory is null) return;

        // Title is NOT NULL in momo_memories — never save it empty (#3).
        var trimmedTitle = RetagTitleInput?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedTitle))
        {
            _ = Toast.Make("Title can't be empty.").Show();
            return;
        }
        var trimmedCaption = string.IsNullOrWhiteSpace(RetagCaptionInput) ? null : RetagCaptionInput.Trim();

        try
        {
            var selectedIds = AllTags
                .Where(t => t.IsSelected)
                .Select(t => t.Tag.Id)
                .ToList();

            await _supabase.UpdateMemoryDetailsAsync(CurrentMemory.Id, trimmedTitle, trimmedCaption, selectedIds);

            CurrentMemory.Title   = trimmedTitle;
            CurrentMemory.Caption = trimmedCaption;
            CurrentMemory.Tags    = selectedIds;
            // Memory isn't INotifyPropertyChanged — the preview card binds CurrentMemory.Title/
            // .Caption directly, so re-notify on the parent property to refresh those bindings.
            OnPropertyChanged(nameof(CurrentMemory));

            // Refresh displayed tags (name + color) and gradient after retag
            CurrentDisplayTags = selectedIds
                .Select(id => _allTags.FirstOrDefault(t => t.Id == id))
                .Where(t => t is not null)
                .Select(t => new TagInfo(t!.Name, t.Color, t.Icon))
                .ToList();
            TagGradientBrush = BuildTagGradient(CurrentDisplayTags);

            IsRetagging = false;
            _ = Toast.Make("Momento updated!").Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SaveRetag: {ex.Message}");
            _ = Toast.Make("Couldn't save changes. Try again.").Show();
        }
    }

    // ── Create custom tag (from retag panel) ─────────────────────────────────
    [RelayCommand]
    private async Task CreateCustomTag()
    {
        var name = await Shell.Current.DisplayPromptAsync(
            "New Tag", "Name your tag:", placeholder: "e.g. Grateful", maxLength: 30);
        if (string.IsNullOrWhiteSpace(name)) return;

        var normalized = TagText.Normalize(name);

        // Don't create a second tag that already exists (case-insensitive) — select it instead.
        var existingTag = _allTags.FirstOrDefault(t => TagText.SameName(t.Name, normalized));
        if (existingTag is not null)
        {
            var panelItem = AllTags.FirstOrDefault(ts => ts.Tag.Id == existingTag.Id)
                            ?? AllTags.FirstOrDefault(ts => TagText.SameName(ts.Tag.Name, normalized));
            if (panelItem is null)
            {
                panelItem = new TagSelection { Tag = existingTag };
                AllTags.Add(panelItem);
            }
            panelItem.IsSelected = true;
            _ = Toast.Make($"\"{existingTag.Name}\" already exists — selected it for you.").Show();
            return;
        }

        var color = TagPalette.NextColor(_allTags.Select(t => t.Color));

        try
        {
            var userId = _supabase.Session?.UserId;
            var newTag = await _supabase.CreateTagAsync(new Tag
            {
                Id       = Guid.NewGuid().ToString(),
                Name     = normalized,
                Color    = color,
                // Icon keeps its model default ('✨'); emoji is no longer part of a tag.
                IsActive = true,
                UserId   = userId
            });

            _allTags.Add(newTag);
            AllTags.Add(new TagSelection { Tag = newTag, IsSelected = true });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CreateCustomTag: {ex.Message}");
            _ = Toast.Make("Couldn't create tag. Try again.").Show();
        }
    }

    // ── See Less (#4) ─────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task SeeLess()
    {
        if (CurrentMemory is null) return;

        // Reduce AI weight without creating a hard skip; low-weight items can still appear.
        var userId = _supabase.Session?.UserId;
        if (!string.IsNullOrEmpty(userId))
        {
            var current = _memoryWeights.GetValueOrDefault(CurrentMemory.Id, 1);
            var reduced = Math.Max(1, current - 1);
            _memoryWeights[CurrentMemory.Id] = reduced;
            _ = _supabase.UpsertMemoryWeightAsync(CurrentMemory.Id, userId, reduced);
        }

        _ = Toast.Make("Got it — we'll show this less in Surprise Me.").Show();

        // Advance to next or previous; leave if last
        if (HasNext)
            Next();
        else if (HasPrevious)
            Previous();
        else
            await Shell.Current.GoToAsync("..");
    }

    // ── Delete ────────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task Delete()
    {
        if (CurrentMemory is null) return;

        var ok = await Shell.Current.DisplayAlert(
            "Delete Memory",
            "This permanently deletes the memory and moves its video file into the deletion queue. It cannot be undone.",
            "Delete", "Cancel");
        if (!ok) return;

        try
        {
            await _supabase.DeleteMemoryWithStorageTrashAsync(CurrentMemory);
            _allMemories.RemoveAt(_currentIndex);

            if (_allMemories.Count == 0)
            {
                await Shell.Current.GoToAsync("..");
                return;
            }

            if (_currentIndex >= _allMemories.Count)
                _currentIndex = _allMemories.Count - 1;

            UpdateCurrentMemory();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Delete: {ex.Message}");
            _ = Toast.Make("Couldn't delete memory. Try again.").Show();
        }
    }

    // ── Share / Download ──────────────────────────────────────────────────────
    [RelayCommand]
    private async Task ShareVideo()
    {
        if (CurrentMemory is null) return;
        var url = CurrentMemory.VideoUrl ?? CurrentMemory.LocalVideoPath;
        if (string.IsNullOrEmpty(url)) return;

        try
        {
            IsBusy = true;

            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = CurrentMemory.Title,
                    File  = new ShareFile(url)
                });
                return;
            }

            // Download to temp cache then share
            var ext      = ".mp4";
            var tempPath = Path.Combine(FileSystem.CacheDirectory, $"momo_{CurrentMemory.Id}{ext}");
            using var client = new HttpClient();
            var bytes = await client.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(tempPath, bytes);
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = CurrentMemory.Title,
                File  = new ShareFile(tempPath)
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ShareVideo: {ex.Message}");
            _ = Toast.Make("Couldn't share this memory.").Show();
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Gradient + date helpers (matching PWA MemoryPlayback.tsx logic) ───────

    private static Brush BuildTagGradient(List<TagInfo> tags)
    {
        // Default purple→pink gradient when no tags (matches PWA fallback)
        var fallback = new LinearGradientBrush(
            new GradientStopCollection
            {
                new GradientStop(Color.FromArgb("#E69333EA"), 0f),
                new GradientStop(Color.FromArgb("#E6EC4899"), 1f)
            },
            new Point(0, 0), new Point(1, 1));

        if (tags.Count == 0) return fallback;

        try
        {
            if (tags.Count == 1)
            {
                var baseColor = Color.FromArgb(tags[0].Color);
                var light = LightenColor(baseColor, 0.3f);
                var dark  = DarkenColor(baseColor,  0.2f);
                return new LinearGradientBrush(
                    new GradientStopCollection
                    {
                        new GradientStop(light.WithAlpha(0.92f), 0f),
                        new GradientStop(baseColor.WithAlpha(0.90f), 0.5f),
                        new GradientStop(dark.WithAlpha(0.90f),  1f)
                    },
                    new Point(0, 0), new Point(1, 1));
            }

            // Multiple tags: spread each color evenly across the gradient
            var stops = new GradientStopCollection();
            for (int i = 0; i < tags.Count; i++)
            {
                var pos   = (float)i / (tags.Count - 1);
                var color = Color.FromArgb(tags[i].Color).WithAlpha(0.88f);
                stops.Add(new GradientStop(color, pos));
            }
            return new LinearGradientBrush(stops, new Point(0, 0), new Point(1, 1));
        }
        catch
        {
            return fallback;
        }
    }

    private static Color LightenColor(Color c, float amount) =>
        Color.FromRgba(
            (float)Math.Min(1.0, c.Red   + (1.0 - c.Red)   * amount),
            (float)Math.Min(1.0, c.Green + (1.0 - c.Green) * amount),
            (float)Math.Min(1.0, c.Blue  + (1.0 - c.Blue)  * amount),
            c.Alpha);

    private static Color DarkenColor(Color c, float amount) =>
        Color.FromRgba(
            (float)Math.Max(0.0, c.Red   * (1.0 - amount)),
            (float)Math.Max(0.0, c.Green * (1.0 - amount)),
            (float)Math.Max(0.0, c.Blue  * (1.0 - amount)),
            c.Alpha);

    private static string GetRelativeDate(DateTimeOffset date)
        => RelativeTimeFormatter.FormatAgo(date);

    // ── Public helper used by ReliveViewModel.SurpriseMeCommand ──────────────
    public static IReadOnlyList<string> GetSkippedIds()
    {
        var json = Preferences.Get(SkippedPrefsKey, "[]");
        return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
    }
}
