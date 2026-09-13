# Light Home Screen Experiment

This experiment changes only `Views/HomePage.xaml`.

## Design direction
- Soft off-white / lavender-blue background instead of dark navy
- Hot pink + purple remain the main Momentary Momentos brand accents
- Capture is the single dominant action
- Relive and Upload are quieter secondary actions
- Stats use clean white/lilac cards
- Featured Momento is presented like a keepsake card rather than a neon dashboard panel
- Recent media thumbnails remain visually rich/dark so photos and videos still carry the emotional weight
- Sign Out is visually de-emphasized

## Functionality preserved
The existing HomePage code-behind and bindings are unchanged, including Capture, Relive, Upload, Help, Sign Out, categories/tags, refresh, featured memory, and recent memories.

## Revert
The original Home page XAML is stored as `DesignExperiments/HomePage.dark-original.xaml.txt`.
To revert, copy its contents back into `Views/HomePage.xaml`.
