using MomentaryMomentos.Services;
using MomentaryMomentos.ViewModels;

namespace MomentaryMomentos.Views;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _vm;
    private readonly IAuthService _auth;
    private readonly SupabaseService _supabase;

    public ProfilePage(ProfileViewModel vm, IAuthService auth, SupabaseService supabase)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _auth    = auth;
        _supabase = supabase;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadCommand.Execute(null);
    }

    private async void OnSignOutClicked(object sender, EventArgs e)
    {
        var ok = await DisplayAlert("Sign Out", "Are you sure?", "Sign Out", "Cancel");
        if (!ok) return;
        await _auth.SignOutAsync();
        await Shell.Current.GoToAsync("//login");
    }

    private void OnUpgradeClicked(object sender, EventArgs e)
        => Shell.Current.GoToAsync("subscription");

    private async void OnDeleteAccountClicked(object sender, EventArgs e)
    {
        var email  = _vm.Email;
        var userId = _vm.UserId;

        // Step 1 — offer export options before deletion
        var choice = await DisplayActionSheet(
            "Before you go...",
            "Cancel",
            null,
            "📧  Email me my videos (Free)",
            "Skip — just delete my account");

        if (choice == "Cancel" || choice is null) return;

        bool exportRequested = false;

        if (choice == "📧  Email me my videos (Free)")
        {
            exportRequested = await RequestFreeExportAsync(userId, email);
            if (!exportRequested) return; // error shown inside — don't proceed
        }
        // Step 2 — if they got an export, give them a chance to keep their account
        if (exportRequested)
        {
            var keepAccount = await DisplayAlert(
                "Export Sent!",
                $"Download links have been sent to {email}. Links expire in 7 days.\n\nWould you still like to delete your account?",
                "Yes, Delete My Account",
                "No, Keep My Account");

            if (!keepAccount) return;
        }

        // Step 3 — final confirmation before permanent deletion
        var confirmed = await DisplayAlert(
            "Delete Account",
            "This will permanently delete all your memories and account data. This cannot be undone.",
            "Delete Forever",
            "Cancel");

        if (!confirmed) return;

        // Step 4 — delete and sign out
        try
        {
            await _supabase.DeleteAccountAsync();
            await _auth.SignOutAsync();
            await Shell.Current.GoToAsync("//login");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not delete account: {ex.Message}", "OK");
        }
    }

    private async Task<bool> RequestFreeExportAsync(string userId, string email)
    {
        try
        {
            await _supabase.RequestDataExportAsync(userId, email);
            return true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Export Failed", $"Could not send your export email: {ex.Message}\n\nPlease try again or contact support.", "OK");
            return false;
        }
    }

}
