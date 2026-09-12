using MomentaryMomentos.Models;

namespace MomentaryMomentos.Services;

/// <summary>
/// Provides offline/demo mode functionality with mock data.
/// When offline mode is enabled, the app works without Supabase connection.
/// </summary>
public class OfflineService
{
    private bool _isOfflineMode = true; // Start in offline mode by default
    private readonly List<Memory> _mockMemories = new();
    private readonly List<Tag> _mockTags = new();
    private AuthSession? _mockSession;

    public bool IsOfflineMode
    {
        get => _isOfflineMode;
        set => _isOfflineMode = value;
    }

    public OfflineService()
    {
        InitializeMockData();
    }

    /// <summary>
    /// Create mock data for testing the app offline.
    /// </summary>
    private void InitializeMockData()
    {
        // Mock user session
        _mockSession = new AuthSession(
            AccessToken: "mock_access_token",
            RefreshToken: "mock_refresh_token",
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1),
            UserId: "mock_user_123",
            Email: "demo@momentarymomentos.com"
        );

        // Mock tags \u2014 life-category defaults (name + color; emoji retired). Colors are
        // spread across the curated palette so the set looks good together.
        _mockTags.AddRange(new[]
        {
            new Tag { Id = "tag_adventure",    Name = "Adventure",    Color = "#F97316", IsActive = true },
            new Tag { Id = "tag_celebrations", Name = "Celebrations", Color = "#EC4899", IsActive = true },
            new Tag { Id = "tag_family",       Name = "Family",       Color = "#22C55E", IsActive = true },
            new Tag { Id = "tag_friends",      Name = "Friends",      Color = "#3B82F6", IsActive = true },
            new Tag { Id = "tag_pets",         Name = "Pets",         Color = "#F59E0B", IsActive = true },
            new Tag { Id = "tag_travel",       Name = "Travel",       Color = "#06B6D4", IsActive = true },
            new Tag { Id = "tag_work",         Name = "Work",         Color = "#6366F1", IsActive = true },
            new Tag { Id = "tag_happy",        Name = "Happy",        Color = "#EAB308", IsActive = true },
            new Tag { Id = "tag_sad",          Name = "Sad",          Color = "#8B5CF6", IsActive = true },
            new Tag { Id = "tag_awe",          Name = "Awe",          Color = "#14B8A6", IsActive = true },
        });

        // Start with empty memories - user will record their own!
        // _mockMemories is now empty, ready for real memories
    }

    // Mock Auth Methods
    public Task<AuthSession> SignInAsync(string email, string password)
    {
        // Accept any credentials in offline mode
        return Task.FromResult(_mockSession!);
    }

    public Task<AuthSession> SignUpAsync(string email, string password, string? fullName = null)
    {
        // Create a new mock session
        return Task.FromResult(_mockSession!);
    }

    public AuthSession? GetMockSession() => _mockSession;

    // Mock Memory Methods
    public Task<List<Memory>> GetMemoriesAsync(string userId)
    {
        return Task.FromResult(_mockMemories.Where(m => m.UserId == userId).ToList());
    }

    public Task<Memory> CreateMemoryAsync(Memory memory)
    {
        memory.Id = $"mem_{Guid.NewGuid():N}";
        memory.CreatedAt = DateTimeOffset.Now;
        _mockMemories.Add(memory);
        return Task.FromResult(memory);
    }

    public Task UpdateMemoryAsync(Memory memory)
    {
        var existing = _mockMemories.FirstOrDefault(m => m.Id == memory.Id);
        if (existing != null)
        {
            var index = _mockMemories.IndexOf(existing);
            _mockMemories[index] = memory;
        }
        return Task.CompletedTask;
    }

    public Task DeleteMemoryAsync(string memoryId)
    {
        var memory = _mockMemories.FirstOrDefault(m => m.Id == memoryId);
        if (memory != null)
            _mockMemories.Remove(memory);
        
        return Task.CompletedTask;
    }

    // Mock Tag Methods
    public Task<List<Tag>> GetTagsAsync(string? userId)
    {
        return Task.FromResult(_mockTags.ToList());
    }

    public Task<Tag> CreateTagAsync(Tag tag)
    {
        tag.Id = $"tag_{Guid.NewGuid():N}";
        _mockTags.Add(tag);
        return Task.FromResult(tag);
    }

    public Task UpdateTagAsync(Tag tag)
    {
        var existing = _mockTags.FirstOrDefault(t => t.Id == tag.Id);
        if (existing != null)
        {
            var index = _mockTags.IndexOf(existing);
            _mockTags[index] = tag;
        }
        return Task.CompletedTask;
    }

    // Stats Methods
    public int GetTotalMemories(string userId) => _mockMemories.Count(m => m.UserId == userId);
    public int GetFavoriteCount(string userId) => _mockMemories.Count(m => m.UserId == userId && m.IsFavorite);
    public int GetThisWeekCount(string userId)
    {
        var weekAgo = DateTimeOffset.Now.AddDays(-7);
        return _mockMemories.Count(m => m.UserId == userId && m.CreatedAt >= weekAgo);
    }
    public int GetCategoryCount(string userId) => _mockTags.Count;

    /// <summary>
    /// Clear all mock data (useful for testing).
    /// </summary>
    public void ResetMockData()
    {
        _mockMemories.Clear();
        _mockTags.Clear();
        InitializeMockData();
    }
}
