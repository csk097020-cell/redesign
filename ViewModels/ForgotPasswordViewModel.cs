// APPLICATION-SPECIFIC CODE - Property of Client (Corinne Kelley)
// Password reset functionality for Momentary Momentos

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MomentaryMomentos.Services;

namespace MomentaryMomentos.ViewModels;

/// <summary>
/// Handles password reset flow as required by contract Section 2.1.
/// </summary>
public partial class ForgotPasswordViewModel : BaseViewModel
{
    private readonly SupabaseService _supabase;

    [ObservableProperty] private string emailAddress = "";
    [ObservableProperty] private bool resetSent;

    public ForgotPasswordViewModel(SupabaseService supabase)
    {
        _supabase = supabase;
    }

    [RelayCommand]
    private async Task SendResetEmail()
    {
        await RunBusyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(EmailAddress))
                throw new InvalidOperationException("Please enter your email address.");

            await _supabase.SendPasswordResetEmailAsync(EmailAddress.Trim());

            ResetSent = true;
        });
    }

    [RelayCommand]
    private async Task BackToLogin()
    {
        await Shell.Current.GoToAsync("//login");
    }
}
