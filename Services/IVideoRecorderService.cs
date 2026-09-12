// APPLICATION-SPECIFIC CODE - Property of Client (Corinne Kelley)
namespace MomentaryMomentos.Services;

/// <summary>
/// Abstracts platform-specific video capture so CaptureViewModel stays cross-platform.
/// Android: uses VideoRecorderService with MediaStore intent + 10-second limit.
/// iOS: NoOpVideoRecorderService — falls back to MediaPicker in CaptureViewModel.
/// </summary>
public interface IVideoRecorderService
{
    bool IsSupported { get; }
    Task<string?> CaptureVideoAsync(int maxDurationSeconds = 0, bool facingFront = false, bool muteAudio = false);
}
