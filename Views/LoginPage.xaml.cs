using MomentaryMomentos.ViewModels;

namespace MomentaryMomentos.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private void OnOfflineModeToggled(object sender, ToggledEventArgs e)
    {
        if (BindingContext is LoginViewModel vm)
            vm.ToggleOfflineModeCommand.Execute(null);
    }
}
