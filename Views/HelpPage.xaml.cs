using MomentaryMomentos.Services;

namespace MomentaryMomentos.Views;

public partial class HelpPage : ContentPage
{
    private readonly IAppNotifications _notifications;

    public HelpPage(IAppNotifications notifications)
    {
        InitializeComponent();
        _notifications = notifications;
    }

    private void OnCloseClicked(object sender, EventArgs e)
        => Shell.Current.GoToAsync("..");

    /// <summary>
    /// Every other notification this app schedules is at least 12 hours out, which left users
    /// with no way to tell "notifications are broken" from "nothing is due yet".
    /// </summary>
    private async void OnTestNotificationClicked(object sender, EventArgs e)
    {
        TestNotificationButton.IsEnabled = false;
        try
        {
            var granted = await _notifications.SendTestNotificationAsync();

            if (granted)
            {
                await DisplayAlert(
                    "Test sent",
                    "Your test notification should arrive in about 15 seconds. " +
                    "You can close the app — it will still appear.",
                    "OK");
            }
            else
            {
                await DisplayAlert(
                    "Notifications are turned off",
                    "Momentary Momentos doesn't have permission to send you notifications. " +
                    "You can turn them on in your device settings.",
                    "OK");
            }
        }
        finally
        {
            TestNotificationButton.IsEnabled = true;
        }
    }
}
