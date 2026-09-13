// APPLICATION-SPECIFIC CODE - Property of Client (Corinne Kelley)
using Foundation;
using MomentaryMomentos.Services;
using UIKit;

namespace MomentaryMomentos.Services;

/// <summary>
/// iOS video recorder using UIImagePickerController with VideoMaximumDuration.
/// The OS enforces the time limit natively and stops recording automatically.
/// </summary>
public class VideoRecorderService : IVideoRecorderService
{
    public bool IsSupported =>
        UIImagePickerController.IsSourceTypeAvailable(UIImagePickerControllerSourceType.Camera);

    public Task<string?> CaptureVideoAsync(int maxDurationSeconds = 10, bool facingFront = false, bool muteAudio = false)
    {
        var tcs = new TaskCompletionSource<string?>();

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var picker = new UIImagePickerController
                {
                    SourceType        = UIImagePickerControllerSourceType.Camera,
                    MediaTypes        = new[] { "public.movie" },
                    VideoMaximumDuration = maxDurationSeconds,
                    VideoQuality      = UIImagePickerControllerQualityType.High,
                    AllowsEditing     = false,
                    CameraDevice      = facingFront && UIImagePickerController.IsCameraDeviceAvailable(
                                            UIImagePickerControllerCameraDevice.Front)
                                        ? UIImagePickerControllerCameraDevice.Front
                                        : UIImagePickerControllerCameraDevice.Rear
                };

                picker.Delegate = new RecorderDelegate(tcs);

                var root = Platform.GetCurrentUIViewController();
                if (root is null)
                {
                    tcs.TrySetResult(null);
                    return;
                }

                await root.PresentViewControllerAsync(picker, animated: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VideoRecorderService] Present failed: {ex.Message}");
                tcs.TrySetResult(null);
            }
        });

        return tcs.Task;
    }
}

file sealed class RecorderDelegate : UIImagePickerControllerDelegate
{
    private readonly TaskCompletionSource<string?> _tcs;

    internal RecorderDelegate(TaskCompletionSource<string?> tcs) => _tcs = tcs;

    public override void FinishedPickingMedia(UIImagePickerController picker, NSDictionary info)
    {
        picker.DismissViewController(animated: true, completionHandler: null);

        var url = info[UIImagePickerController.MediaURL] as NSUrl;
        if (url?.Path is null)
        {
            _tcs.TrySetResult(null);
            return;
        }

        try
        {
            // Copy out of the temp location iOS provides before it gets cleaned up
            var dest = Path.Combine(FileSystem.CacheDirectory,
                $"capture_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.mp4");
            File.Copy(url.Path, dest, overwrite: true);
            _tcs.TrySetResult(dest);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VideoRecorderService] Copy failed: {ex.Message}");
            _tcs.TrySetResult(url.Path); // fall back to temp path
        }
    }

    public override void Canceled(UIImagePickerController picker)
    {
        picker.DismissViewController(animated: true, completionHandler: null);
        _tcs.TrySetResult(null);
    }
}
