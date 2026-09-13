using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MomentaryMomentos.Services;

namespace MomentaryMomentos.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly SupabaseService _supabase;
    private readonly SecureTokenStore _tokens;
    private readonly OfflineService _offline;
    private readonly UserPreferencesService _preferences;

    [ObservableProperty] private string email = "";
    [ObservableProperty] private string password = "";
    [ObservableProperty] private bool isOfflineMode;
    [ObservableProperty] private string connectionStatus = "Offline Mode";

    // A masked field is a common cause of failed sign-ins on phones, where autocorrect and the
    // cramped keyboard make typos easy and invisible. Starts hidden; the user opts in to seeing it.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasswordToggleText))]
    [NotifyPropertyChangedFor(nameof(PasswordToggleDescription))]
    private bool isPasswordHidden = true;

    public string PasswordToggleText => IsPasswordHidden ? "Show" : "Hide";

    /// <summary>Spoken label for screen readers — the visible text alone is ambiguous out of context.</summary>
    public string PasswordToggleDescription => IsPasswordHidden ? "Show password" : "Hide password";

    public LoginViewModel(
        SupabaseService supabase, 
        SecureTokenStore tokens,
        OfflineService offline,
        UserPreferencesService preferences)
    {
        _supabase = supabase;
        _tokens = tokens;
        _offline = offline;
        _preferences = preferences;
        
        // Check offline mode preference
        IsOfflineMode = _preferences.OfflineModeEnabled;
        UpdateConnectionStatus();
    }

    [RelayCommand]
    private void TogglePasswordVisibility() => IsPasswordHidden = !IsPasswordHidden;

    [RelayCommand]
    private async Task GoToRegister()
        => await Shell.Current.GoToAsync("//register");

    [RelayCommand]
    private async Task GoToForgotPassword()
        => await Shell.Current.GoToAsync("forgotpassword");

    [RelayCommand]
    private async Task ToggleOfflineMode()
    {
        IsOfflineMode = !IsOfflineMode;
        _preferences.OfflineModeEnabled = IsOfflineMode;
        UpdateConnectionStatus();
    }

    [RelayCommand]
    private async Task SignIn()
    {
        await RunBusyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
                throw new InvalidOperationException("Please enter email and password.");

            if (IsOfflineMode)
            {
                // Use offline service (accepts any credentials)
                var session = await _offline.SignInAsync(Email.Trim(), Password.Trim());
                await _tokens.SaveAsync(session);

                System.Diagnostics.Debug.WriteLine("LoginViewModel: Signed in offline mode");
            }
            else
            {
                // Use real Supabase authentication
                System.Diagnostics.Debug.WriteLine("LoginViewModel: Signing in with Supabase");
                // Trim the password too: soft keyboards append a trailing space after an
                // autocompleted word, and the field is masked, so the user cannot see why
                // sign-in keeps failing. Register trims the same way.
                var session = await _supabase.SignInAsync(Email.Trim(), Password.Trim());
                await _tokens.SaveAsync(session);

                System.Diagnostics.Debug.WriteLine($"LoginViewModel: Successfully signed in as {session.Email}");

                // Trigger initial data sync from Supabase
                try
                {
                    System.Diagnostics.Debug.WriteLine("LoginViewModel: Starting initial sync");
                    var sync = Shell.Current.Handler?.MauiContext?.Services.GetService<SyncService>();

                    if (sync != null)
                    {
                        // Run sync in background — intentionally not awaited so it doesn't block navigation
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var result = await sync.FullSyncAsync(session.UserId);
                                System.Diagnostics.Debug.WriteLine($"LoginViewModel: Initial sync completed - {result.Message}");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"LoginViewModel: Background sync failed: {ex.Message}");
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"LoginViewModel: Initial sync failed: {ex.Message}");
                    // Don't block login if sync fails
                }
            }

            await Shell.Current.GoToAsync("//main/home");
        });
    }

    private void UpdateConnectionStatus()
    {
        ConnectionStatus = IsOfflineMode ? "🔴 Offline Mode - All data saved locally" : "🟢 Online Mode - Syncing to cloud";
    }
}
