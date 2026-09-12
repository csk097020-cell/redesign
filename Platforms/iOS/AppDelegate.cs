using AVFoundation;
using Foundation;
using UIKit;

namespace MomentaryMomentos;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        var result = base.FinishedLaunching(application, launchOptions);

        // Configure audio session for video playback — ensures audio plays even when
        // the silent switch is on, and that AVPlayer/MediaElement can auto-play.
        AVAudioSession.SharedInstance().SetCategory(AVAudioSessionCategory.Playback.GetConstant());
        AVAudioSession.SharedInstance().SetActive(true, out _);

        return result;
    }
}
