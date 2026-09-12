using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MomentaryMomentos.Models;
using MomentaryMomentos.Services;

namespace MomentaryMomentos.ViewModels;

public partial class RegisterViewModel : BaseViewModel
{
    private readonly SupabaseService _supabase;
    private readonly SecureTokenStore _tokens;

    [ObservableProperty] private string fullName = "";
    [ObservableProperty] private string email = "";
    [ObservableProperty] private string password = "";
    [ObservableProperty] private string confirmPassword = "";

    // Same rationale as the sign-in screen, and it matters more here: the user is inventing a
    // password and typing it twice, so an invisible typo means a failed match with no explanation.
    // Each field gets its own toggle so the affordance sits where the user is looking.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasswordToggleText))]
    [NotifyPropertyChangedFor(nameof(PasswordToggleDescription))]
    private bool isPasswordHidden = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfirmPasswordToggleText))]
    [NotifyPropertyChangedFor(nameof(ConfirmPasswordToggleDescription))]
    private bool isConfirmPasswordHidden = true;

    public string PasswordToggleText => IsPasswordHidden ? "Show" : "Hide";
    public string ConfirmPasswordToggleText => IsConfirmPasswordHidden ? "Show" : "Hide";

    /// <summary>Spoken labels for screen readers — "Show" alone is ambiguous with two fields on screen.</summary>
    public string PasswordToggleDescription => IsPasswordHidden ? "Show password" : "Hide password";
    public string ConfirmPasswordToggleDescription => IsConfirmPasswordHidden ? "Show confirm password" : "Hide confirm password";

    public RegisterViewModel(SupabaseService supabase, SecureTokenStore tokens)
    {
        _supabase = supabase;
        _tokens = tokens;
    }

    [RelayCommand]
    private void TogglePasswordVisibility() => IsPasswordHidden = !IsPasswordHidden;

    [RelayCommand]
    private void ToggleConfirmPasswordVisibility() => IsConfirmPasswordHidden = !IsConfirmPasswordHidden;

    [RelayCommand]
    private async Task GoToLogin()
        => await Shell.Current.GoToAsync("//login");

    [RelayCommand]
    private async Task SignUp()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        SuccessMessage = null;

        try
        {
            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Passwords do not match.";
                return;
            }

            System.Diagnostics.Debug.WriteLine($"RegisterViewModel: Signing up with Supabase - {Email}");

            AuthSession session;
            try
            {
                session = await _supabase.SignUpAsync(Email.Trim(), Password.Trim(), string.IsNullOrWhiteSpace(FullName) ? null : FullName.Trim());
            }
            catch (EmailConfirmationRequiredException)
            {
                // Account created — Supabase requires the user to confirm their email first.
                SuccessMessage = "Account created! Please check your email and click the confirmation link, then come back to sign in.";
                await Task.Delay(200);
                await Shell.Current.GoToAsync("//login");
                return;
            }

            await _tokens.SaveAsync(session);
            System.Diagnostics.Debug.WriteLine($"RegisterViewModel: Successfully registered as {session.Email}");

            // Trigger initial data sync (download default tags)
            try
            {
                var sync = Shell.Current.Handler?.MauiContext?.Services.GetService<SyncService>();
                if (sync != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var result = await sync.FullSyncAsync(session.UserId);
                            System.Diagnostics.Debug.WriteLine($"RegisterViewModel: Initial sync completed - {result.Message}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"RegisterViewModel: Background sync failed: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RegisterViewModel: Initial sync failed: {ex.Message}");
            }

            await Shell.Current.GoToAsync("//main/home");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            OnPropertyChanged(nameof(HasError));
        }
        finally
        {
            IsBusy = false;
        }
    }
}
