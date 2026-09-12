using MomentaryMomentos.ViewModels;

namespace MomentaryMomentos.Views;

public partial class ForgotPasswordPage : ContentPage
{
    public ForgotPasswordPage(ForgotPasswordViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private async void OnBackClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync("//login");
}
