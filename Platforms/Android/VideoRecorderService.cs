using Android.Content;
using Android.OS;
using Android.Provider;
using AndroidX.Core.Content;
using AndroidX.Core.Content.PM;
using Microsoft.Maui.ApplicationModel;
using System.Diagnostics;
using SysDebug = System.Diagnostics.Debug;
using SysEnv = System.Environment;

namespace MomentaryMomentos.Services;

/// <summary>
/// Android implementation of video recorder with duration limit support
/// </summary>
public class VideoRecorderService : IVideoRecorderService
{
    private const int VideoRequestCode = 1001;
    private TaskCompletionSource<string?>? _tcs;
    private string? _outputPath;
    private Android.Net.Uri? _outputUri;

    public bool IsSupported => true;

    public VideoRecorderService()
    {
        // Register with MainActivity for activity result handling
        MainActivity.SetVideoRecorderService(this);
        SysDebug.WriteLine("VideoRecorderService: Initialized and registered with MainActivity");
    }

    public async Task<string?> CaptureVideoAsync(int maxDurationSeconds = 0, bool facingFront = false, bool muteAudio = false)
    {
        // Re-register to ensure we're connected
        MainActivity.SetVideoRecorderService(this);

        try
        {
            // Check camera permission
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Camera>();
                if (status != PermissionStatus.Granted)
                {
                    throw new PermissionException("Camera permission denied");
                }
            }

            // Check microphone permission
            var micStatus = await Permissions.CheckStatusAsync<Permissions.Microphone>();
            if (micStatus != PermissionStatus.Granted)
            {
                micStatus = await Permissions.RequestAsync<Permissions.Microphone>();
                if (micStatus != PermissionStatus.Granted)
                {
                    throw new PermissionException("Microphone permission denied");
                }
            }

            var activity = Platform.CurrentActivity;
            if (activity == null)
            {
                throw new InvalidOperationException("Unable to get current activity");
            }

            // Create output file path
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var fileName = $"video_{timestamp}.mp4";
            
            // Use the app's external cache directory (accessible by camera app)
            var cacheDir = activity.GetExternalFilesDir(Android.OS.Environment.DirectoryMovies);
            if (cacheDir == null)
            {
                // Fallback to internal cache
                cacheDir = activity.CacheDir;
            }
            
            if (!cacheDir.Exists())
            {
                cacheDir.Mkdirs();
            }
            
            var outputFile = new Java.IO.File(cacheDir, fileName);
            _outputPath = outputFile.AbsolutePath;
            
            SysDebug.WriteLine($"VideoRecorderService: Output path = {_outputPath}");

            // Create a content URI using FileProvider
            try
            {
                _outputUri = AndroidX.Core.Content.FileProvider.GetUriForFile(
                    activity,
                    $"{activity.PackageName}.fileprovider",
                    outputFile);
                    
                SysDebug.WriteLine($"VideoRecorderService: Output URI = {_outputUri}");
            }
            catch (Exception ex)
            {
                SysDebug.WriteLine($"VideoRecorderService: FileProvider error (will try without): {ex.Message}");
                // If FileProvider fails, try creating URI directly
                _outputUri = Android.Net.Uri.FromFile(outputFile);
                SysDebug.WriteLine($"VideoRecorderService: Using direct file URI = {_outputUri}");
            }

            // Create intent for video capture
            var intent = new Intent(MediaStore.ActionVideoCapture);
            
            // Specify where to save the video
            intent.PutExtra(MediaStore.ExtraOutput, _outputUri);

            // Set duration limit if specified
            if (maxDurationSeconds > 0)
            {
                intent.PutExtra(MediaStore.ExtraDurationLimit, maxDurationSeconds);
                SysDebug.WriteLine($"VideoRecorderService: Set duration limit to {maxDurationSeconds} seconds");
            }

            // Request front (1) or back (0) camera — hint only, honoured by most OEM camera apps
            intent.PutExtra("android.intent.extras.CAMERA_FACING", facingFront ? 1 : 0);

            // Set video quality (0 = low quality/smaller file, 1 = high quality).
            // High quality means 4K on many devices, and since EXTRA_DURATION_LIMIT above is
            // only a hint that plenty of OEM camera apps ignore, that produced clips far past
            // the 50 MB upload limit. A 10-second momento does not need 4K.
            intent.PutExtra(MediaStore.ExtraVideoQuality, 0);
            
            // Grant temporary permissions to the camera app
            intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);

            // Verify that the intent can be resolved
            if (intent.ResolveActivity(activity.PackageManager) == null)
            {
                throw new FeatureNotSupportedException("No camera app available");
            }

            SysDebug.WriteLine($"VideoRecorderService: Starting video capture");

            // Start the camera activity
            _tcs = new TaskCompletionSource<string?>();
            activity.StartActivityForResult(intent, VideoRequestCode);

            return await _tcs.Task;
        }
        catch (Exception ex)
        {
            SysDebug.WriteLine($"VideoRecorderService: Error - {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            _tcs?.TrySetException(ex);
            throw;
        }
    }

    public void OnActivityResult(int requestCode, Android.App.Result resultCode, Intent? data)
    {
        if (requestCode != VideoRequestCode)
            return;

        SysDebug.WriteLine($"VideoRecorderService: OnActivityResult - ResultCode = {resultCode}, HasData = {data != null}, HasDataUri = {data?.Data != null}");

        try
        {
            if (resultCode == Android.App.Result.Ok)
            {
                // Check if the file exists at the output path we specified
                if (!string.IsNullOrEmpty(_outputPath) && File.Exists(_outputPath))
                {
                    var fileInfo = new FileInfo(_outputPath);
                    SysDebug.WriteLine($"VideoRecorderService: Video saved successfully! Size: {fileInfo.Length} bytes, Path: {_outputPath}");
                    
                    // Copy to app's documents folder for consistency
                    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var finalFileName = $"video_{timestamp}.mp4";
                    var documentsPath = SysEnv.GetFolderPath(SysEnv.SpecialFolder.MyDocuments);
                    var finalPath = Path.Combine(documentsPath, finalFileName);
                    
                    // Ensure directory exists
                    var directory = Path.GetDirectoryName(finalPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    
                    // Copy the file
                    File.Copy(_outputPath, finalPath, overwrite: true);
                    
                    // Clean up the temp file
                    try
                    {
                        File.Delete(_outputPath);
                    }
                    catch (Exception ex)
                    {
                        SysDebug.WriteLine($"VideoRecorderService: Warning - couldn't delete temp file: {ex.Message}");
                    }
                    
                    SysDebug.WriteLine($"VideoRecorderService: Copied to final location: {finalPath}");
                    _tcs?.TrySetResult(finalPath);
                    return;
                }
                
                // Fallback: try to get URI from intent data
                if (data?.Data != null)
                {
                    var uri = data.Data;
                    SysDebug.WriteLine($"VideoRecorderService: Got video URI from intent data: {uri}");

                    var activity = Platform.CurrentActivity;
                    if (activity == null)
                    {
                        SysDebug.WriteLine("VideoRecorderService: Activity is null");
                        _tcs?.TrySetResult(null);
                        return;
                    }

                    try
                    {
                        // Save to documents folder
                        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        var fileName = $"video_{timestamp}.mp4";
                        var documentsPath = SysEnv.GetFolderPath(SysEnv.SpecialFolder.MyDocuments);
                        var finalPath = Path.Combine(documentsPath, fileName);
                        
                        // Ensure directory exists
                        var directory = Path.GetDirectoryName(finalPath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        // Copy video from URI
                        using var inputStream = activity.ContentResolver?.OpenInputStream(uri);
                        if (inputStream == null)
                        {
                            SysDebug.WriteLine("VideoRecorderService: Failed to open input stream from URI");
                            _tcs?.TrySetResult(null);
                            return;
                        }

                        using var outputStream = File.Create(finalPath);
                        inputStream.CopyTo(outputStream);
                        outputStream.Flush();

                        var fileInfo = new FileInfo(finalPath);
                        SysDebug.WriteLine($"VideoRecorderService: Video saved from URI! Size: {fileInfo.Length} bytes, Path: {finalPath}");
                        
                        _tcs?.TrySetResult(finalPath);
                        return;
                    }
                    catch (Exception ex)
                    {
                        SysDebug.WriteLine($"VideoRecorderService: Error copying video from URI - {ex.GetType().Name}: {ex.Message}");
                        _tcs?.TrySetException(ex);
                        return;
                    }
                }
                
                SysDebug.WriteLine("VideoRecorderService: Result OK but no video file found");
                _tcs?.TrySetResult(null);
            }
            else if (resultCode == Android.App.Result.Canceled)
            {
                SysDebug.WriteLine("VideoRecorderService: User cancelled video capture");
                _tcs?.TrySetResult(null);
            }
            else
            {
                SysDebug.WriteLine($"VideoRecorderService: Unexpected result code: {resultCode}");
                _tcs?.TrySetResult(null);
            }
        }
        catch (Exception ex)
        {
            SysDebug.WriteLine($"VideoRecorderService: Unhandled error in OnActivityResult - {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            _tcs?.TrySetException(ex);
        }
    }
}




