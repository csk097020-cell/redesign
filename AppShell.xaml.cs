using MomentaryMomentos.Views;

namespace MomentaryMomentos;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register modal/push routes not in the tab bar
        Routing.RegisterRoute("forgotpassword", typeof(ForgotPasswordPage));
        Routing.RegisterRoute("subscription", typeof(SubscriptionPage));
        Routing.RegisterRoute("admin", typeof(AdminPage));
        Routing.RegisterRoute("video", typeof(VideoPlayerPage));
        Routing.RegisterRoute("trim", typeof(TrimPage));
        Routing.RegisterRoute("memorydetail", typeof(MemoryDetailPage));
        Routing.RegisterRoute("managetags", typeof(TagManagementPage));
        Routing.RegisterRoute("help", typeof(HelpPage));
    }
}
