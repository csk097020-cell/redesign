using MomentaryMomentos.Models;

namespace MomentaryMomentos.Services;

/// <summary>
/// Manages dynamic color themes based on selected tags/emotions.
/// Changes the app's color scheme to match the mood of the memory.
/// </summary>
public class ColorThemeService
{
    private ColorTheme _currentTheme = ColorTheme.Default;
    
    // Map tag names to color themes
    private readonly Dictionary<string, ColorTheme> _tagThemeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Emotions
        { "Happy", ColorTheme.Happy },
        { "Excited", ColorTheme.Excited },
        { "Love", ColorTheme.Love },
        { "Calm", ColorTheme.Calm },
        { "Peaceful", ColorTheme.Peaceful },
        { "Energetic", ColorTheme.Energetic },
        { "Sad", ColorTheme.Sad },
        { "Fun", ColorTheme.Fun },
        { "Grateful", ColorTheme.Grateful },
        { "Adventure", ColorTheme.Adventure },
        { "Chill", ColorTheme.Chill },
        { "Romantic", ColorTheme.Romantic },
        
        // Activity-based themes (map to appropriate moods)
        { "Family", ColorTheme.Love },
        { "Friends", ColorTheme.Fun },
        { "Travel", ColorTheme.Adventure },
        { "Food", ColorTheme.Happy },
        { "Work", ColorTheme.Calm },
        { "Fitness", ColorTheme.Energetic },
        { "Pets", ColorTheme.Love },
        { "Hobbies", ColorTheme.Chill },
        { "Party", ColorTheme.Excited },
        { "Celebration", ColorTheme.Happy },
        { "Date", ColorTheme.Romantic },
        { "Nature", ColorTheme.Peaceful },
        { "Beach", ColorTheme.Chill },
        { "Birthday", ColorTheme.Excited }
    };
    
    public event EventHandler<ColorTheme>? ThemeChanged;
    
    public ColorTheme CurrentTheme
    {
        get => _currentTheme;
        private set
        {
            if (_currentTheme != value)
            {
                _currentTheme = value;
                ThemeChanged?.Invoke(this, value);
            }
        }
    }
    
    /// <summary>
    /// Update theme based on selected tags.
    /// Uses the first recognized emotion/mood tag.
    /// </summary>
    public void UpdateThemeFromTags(IEnumerable<Tag> tags)
    {
        if (tags == null || !tags.Any())
        {
            CurrentTheme = ColorTheme.Default;
            return;
        }
        
        // Find first tag that has a theme mapping
        foreach (var tag in tags)
        {
            if (_tagThemeMap.TryGetValue(tag.Name, out var theme))
            {
                CurrentTheme = theme;
                return;
            }
        }
        
        // No matching theme found, use default
        CurrentTheme = ColorTheme.Default;
    }
    
    /// <summary>
    /// Update theme based on tag names.
    /// </summary>
    public void UpdateThemeFromTagNames(IEnumerable<string> tagNames)
    {
        if (tagNames == null || !tagNames.Any())
        {
            CurrentTheme = ColorTheme.Default;
            return;
        }
        
        foreach (var tagName in tagNames)
        {
            if (_tagThemeMap.TryGetValue(tagName, out var theme))
            {
                CurrentTheme = theme;
                return;
            }
        }
        
        CurrentTheme = ColorTheme.Default;
    }
    
    /// <summary>
    /// Set theme directly by name.
    /// </summary>
    public void SetTheme(string themeName)
    {
        if (_tagThemeMap.TryGetValue(themeName, out var theme))
        {
            CurrentTheme = theme;
        }
        else
        {
            CurrentTheme = ColorTheme.Default;
        }
    }
    
    /// <summary>
    /// Reset to default theme.
    /// </summary>
    public void ResetTheme()
    {
        CurrentTheme = ColorTheme.Default;
    }
    
    /// <summary>
    /// Get available theme names.
    /// </summary>
    public List<string> GetAvailableThemes()
    {
        return _tagThemeMap.Keys.ToList();
    }
    
    /// <summary>
    /// Get theme for a specific tag.
    /// </summary>
    public ColorTheme? GetThemeForTag(string tagName)
    {
        return _tagThemeMap.TryGetValue(tagName, out var theme) ? theme : null;
    }
}
