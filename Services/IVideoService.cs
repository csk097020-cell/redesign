// APPLICATION-SPECIFIC CODE - Property of Client (Corinne Kelley)
namespace MomentaryMomentos.Services;

public interface IVideoService
{
    Task<string> UploadVideoAsync(string filePath, string fileName, CancellationToken ct = default);
    Task<string> GetVideoUrlAsync(string fileName);
    Task<bool> DeleteVideoAsync(string fileName, CancellationToken ct = default);
    Task<List<string>> ListUserVideosAsync(CancellationToken ct = default);
}
