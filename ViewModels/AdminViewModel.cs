using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MomentaryMomentos.Models;
using MomentaryMomentos.Services;
using MomentaryMomentos.Utils;

namespace MomentaryMomentos.ViewModels;

public partial class AdminViewModel : BaseViewModel
{
    private readonly SupabaseService _supabase;
    private readonly SyncService _sync;
    private readonly ISubscriptionService _subscription;

    // ── Tags ─────────────────────────────────────────────────────────────────
    public ObservableCollection<Tag> Tags { get; } = new();

    [ObservableProperty] private bool isAdmin;
    [ObservableProperty] private string newTagName = "";

    // ── Users ─────────────────────────────────────────────────────────────────
    private List<AdminUserSummary> _allUsers = new();
    public ObservableCollection<AdminUserSummary> FilteredUsers { get; } = new();

    [ObservableProperty] private string userSearch = "";
    [ObservableProperty] private int totalUsers;
    [ObservableProperty] private int totalMemories;
    [ObservableProperty] private int totalTags;

    partial void OnUserSearchChanged(string value) => ApplyUserFilter();

    // ── Subscription Config ───────────────────────────────────────────────────
    [ObservableProperty] private int freeMemoryLimit    = 1000;
    [ObservableProperty] private int paidMemoryLimit    = 10000;
    [ObservableProperty] private decimal monthlyPrice   = 4.99m;
    [ObservableProperty] private decimal annualPrice    = 49.99m;

    public AdminViewModel(SupabaseService supabase, SyncService sync, ISubscriptionService subscription)
    {
        _supabase     = supabase;
        _sync         = sync;
        _subscription = subscription;
    }

    [RelayCommand]
    private async Task Load()
    {
        await RunBusyAsync(async () =>
        {
            var profile = await _supabase.GetProfileAsync();
            IsAdmin = profile?.IsAdmin ?? false;

            if (!IsAdmin)
            {
                Tags.Clear();
                _allUsers.Clear();
                FilteredUsers.Clear();
                return;
            }

            var session = _supabase.Session!;

            // Load tags, users, and memory counts in parallel
            var tagsTask   = _sync.GetTagsWithCacheAsync(session.UserId, includeInactive: true);
            var usersTask  = _supabase.GetAllProfilesAsync();
            var countsTask = _supabase.GetMemoryCountsByUserAsync();

            var tags   = await tagsTask;
            var users  = await usersTask;
            var counts = await countsTask;

            Tags.Clear();
            foreach (var t in tags) Tags.Add(t);

            foreach (var u in users)
                u.MemoryCount = counts.GetValueOrDefault(u.Id, 0);

            _allUsers      = users;
            TotalUsers     = users.Count;
            TotalMemories  = counts.Values.Sum();
            TotalTags      = tags.Count;
            ApplyUserFilter();

            // Load subscription config
            var cfg = await _supabase.GetSubscriptionConfigAsync();
            FreeMemoryLimit = cfg.FreeUserVideoLimit;
            PaidMemoryLimit = cfg.PaidUserVideoLimit;
            MonthlyPrice    = cfg.MonthlyPrice;
            AnnualPrice     = cfg.AnnualPrice;
        });
    }

    [RelayCommand]
    private async Task SaveSubscriptionConfig()
    {
        await RunBusyAsync(async () =>
        {
            if (FreeMemoryLimit < 1 || PaidMemoryLimit < FreeMemoryLimit || MonthlyPrice < 0 || AnnualPrice < 0)
            {
                await Shell.Current.DisplayAlert("Validation",
                    "Free limit must be ≥ 1, paid limit must be > free limit, prices must be ≥ 0.", "OK");
                return;
            }

            var cfg = new Models.SubscriptionConfig
            {
                FreeUserVideoLimit = FreeMemoryLimit,
                PaidUserVideoLimit = PaidMemoryLimit,
                MonthlyPrice       = MonthlyPrice,
                AnnualPrice        = AnnualPrice,
                MonthlyEnabled     = true,
                AnnualEnabled      = true
            };

            await _supabase.SaveSubscriptionConfigAsync(cfg);
            (_subscription as SubscriptionService)?.InvalidateConfigCache();
            _ = Toast.Make("✅ Subscription limits updated.").Show();
        });
    }

    private void ApplyUserFilter()
    {
        FilteredUsers.Clear();
        var term = UserSearch.Trim().ToLowerInvariant();
        var source = string.IsNullOrEmpty(term)
            ? _allUsers
            : _allUsers.Where(u =>
                (u.FullName ?? "").ToLowerInvariant().Contains(term) ||
                (u.Email    ?? "").ToLowerInvariant().Contains(term));
        foreach (var u in source) FilteredUsers.Add(u);
    }

    [RelayCommand]
    private async Task ToggleUserAdmin(AdminUserSummary user)
    {
        var newState = !user.IsAdmin;
        try
        {
            await _supabase.SetUserAdminAsync(user.Id, newState);
            user.IsAdmin = newState;
            ApplyUserFilter(); // refresh display
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ToggleUserAdmin: {ex.Message}");
            _ = Toast.Make("Couldn't update admin status.").Show();
        }
    }

    [RelayCommand]
    private async Task DeleteUser(AdminUserSummary user)
    {
        var ok = await Shell.Current.DisplayAlert(
            "Delete User",
            $"Permanently delete {user.DisplayName}? This will also delete all their memories.",
            "Delete", "Cancel");
        if (!ok) return;

        try
        {
            await _supabase.DeleteUserProfileAsync(user.Id);
            _allUsers.Remove(user);
            TotalUsers = _allUsers.Count;
            ApplyUserFilter();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DeleteUser: {ex.Message}");
            _ = Toast.Make("Couldn't delete user.").Show();
        }
    }

    // ── Tags ─────────────────────────────────────────────────────────────────

    // Inline edit state — only one tag edited at a time
    [ObservableProperty] private string? editingTagId;
    [ObservableProperty] private string editTagName  = "";

    [RelayCommand]
    private async Task DeleteTag(Tag tag)
    {
        var ok = await Shell.Current.DisplayAlert(
            "Delete Tag",
            $"Permanently delete the tag \"{tag.Name}\"? This cannot be undone.",
            "Delete", "Cancel");
        if (!ok) return;

        try
        {
            await _supabase.DeleteTagAsync(tag.Id);
            Tags.Remove(tag);
            TotalTags = Tags.Count;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DeleteTag: {ex.Message}");
            _ = Toast.Make("Couldn't delete tag.").Show();
        }
    }

    [RelayCommand]
    private void StartEditTag(Tag tag)
    {
        EditingTagId = tag.Id;
        EditTagName  = tag.Name;
    }

    [RelayCommand]
    private void CancelEditTag() => EditingTagId = null;

    [RelayCommand]
    private async Task SaveEditTag(Tag tag)
    {
        var name  = EditTagName.Trim();

        if (string.IsNullOrEmpty(name))
        {
            _ = Toast.Make("Tag name cannot be empty.").Show();
            return;
        }

        await RunBusyAsync(async () =>
        {
            // Color and icon are not user-editable; pass the existing values through unchanged.
            await _supabase.UpdateTagAsync(tag.Id, name, tag.Color, tag.Icon);
            tag.Name  = name;

            // Refresh the collection so the swatch and label update
            var idx = Tags.IndexOf(tag);
            if (idx >= 0) { Tags.RemoveAt(idx); Tags.Insert(idx, tag); }

            EditingTagId = null;
        });
    }

    [RelayCommand]
    private async Task ToggleActive(Tag tag)
    {
        await RunBusyAsync(async () =>
        {
            tag.IsActive = !tag.IsActive;
            await _supabase.SetTagActiveAsync(tag.Id, tag.IsActive);
        });
    }

    [RelayCommand]
    private async Task Create()
    {
        await RunBusyAsync(async () =>
        {
            if (!IsAdmin) throw new InvalidOperationException("Admin only.");

            if (string.IsNullOrWhiteSpace(NewTagName))
                throw new InvalidOperationException("Tag name required.");

            var tag = new Tag
            {
                Name     = NewTagName.Trim(),
                Color    = TagPalette.NextColor(Tags.Select(t => t.Color)),
                // Icon keeps its model default ('✨'); emoji is no longer part of a tag.
                IsActive = true,
                UserId   = null
            };

            var created = await _supabase.CreateTagAsync(tag);
            Tags.Add(created);

            NewTagName  = "";
        });
    }
}
