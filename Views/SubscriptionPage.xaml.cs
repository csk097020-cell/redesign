using MomentaryMomentos.ViewModels;

namespace MomentaryMomentos.Views;

public partial class SubscriptionPage : ContentPage
{
    private readonly SubscriptionViewModel _vm;

    public SubscriptionPage(SubscriptionViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadCommand.Execute(null);
    }
}
