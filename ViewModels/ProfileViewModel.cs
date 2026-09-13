using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MomentaryMomentos.Services;

namespace MomentaryMomentos.ViewModels;

public partial class ProfileViewModel : BaseViewModel
{
    private readonly SupabaseService _supabase;
    private readonly LocalStorageService _localStorage;

    [ObservableProperty] private string email = "";
    [ObservableProperty] private string userId = "";
    [ObservableProperty] private string? fullName;
    [ObservableProperty] private bool isAdmin;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAvatar))]
    private string? avatarUrl;

    public bool HasAvatar => !string.IsNullOrEmpty(AvatarUrl);

    // Live app version for the About card — reads the built values rather than a
    // hardcoded string. e.g. "v1.0 (build 1)".
    public string AppVersion => $"v{AppInfo.Current.VersionString} (build {AppInfo.Current.BuildString})";

    // ── Edit state ───────────────────────────────────────────────────────────
    [ObservableProperty] private bool isEditing;
    [ObservableProperty] private string editName = "";
    [ObservableProperty] private string? saveError;

    public ProfileViewModel(SupabaseService supabase, LocalStorageService localStorage)
    {
        _supabase     = supabase;
        _localStorage = localStorage;
    }

    [RelayCommand]
    private async Task Load()
    {
        await RunBusyAsync(async () =>
        {
            var session = _supabase.Session ?? throw new InvalidOperationException("Not logged in.");
            Email = session.Email;
            UserId = session.UserId;

            var profile = await _supabase.GetProfileAsync();
            FullName  = profile?.FullName;
            IsAdmin   = profile?.IsAdmin ?? false;
            AvatarUrl = profile?.AvatarUrl;
        });
    }

    [RelayCommand]
    private void StartEdit()
    {
        EditName  = FullName ?? "";
        SaveError = null;
        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        SaveError = null;
    }

    [RelayCommand]
    private async Task SaveProfile()
    {
        var name = EditName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            SaveError = "Name cannot be empty.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            await _supabase.UpdateFullNameAsync(name);
            FullName  = name;
            IsEditing = false;
            SaveError = null;
        });
    }

    [RelayCommand]
    private async Task OpenPrivacyPolicy()
    {
        await Launcher.OpenAsync(new Uri(LegalUrls.PrivacyPolicy));
    }

    [RelayCommand]
    private async Task OpenTermsOfUse()
    {
        await Launcher.OpenAsync(new Uri(LegalUrls.TermsOfUse));
    }

    [RelayCommand]
    private async Task PickAvatar()
    {
        try
        {
            var photo = await MediaPicker.Default.PickPhotoAsync();
            if (photo is null) return;

            await RunBusyAsync(async () =>
            {
                var userId = _supabase.Session?.UserId;
                if (string.IsNullOrEmpty(userId)) return;

                // Copy picked photo to a temp file we own
                var ext      = Path.GetExtension(photo.FileName).ToLowerInvariant();
                var tempPath = Path.Combine(FileSystem.CacheDirectory, $"avatar_{userId}{ext}");
                await using (var src  = await photo.OpenReadAsync())
                await using (var dest = File.Create(tempPath))
                {
                    await src.CopyToAsync(dest);
                }

                // Our copy is written — release the picker's staged copy, which is otherwise
                // kept in the cache forever.
                _localStorage.DiscardPickerTempFile(photo.FullPath);

                var url = await _supabase.UploadAvatarAsync(userId, tempPath);
                await _supabase.UpdateAvatarUrlAsync(url);
                AvatarUrl = url;
                Diagnostics.Trace("profile.avatar", "updated");
            });
        }
        catch (Exception ex)
        {
            // Was Debug.WriteLine only, which is invisible in a release build — a failed upload
            // was indistinguishable from "I tapped Done and nothing happened".
            Diagnostics.Report(ex, "profile.avatar");
            await Shell.Current.DisplayAlert(
                "Couldn't update your picture", ex.Message, "OK");
        }
    }
}
