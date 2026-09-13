# Momentary Momentos
### Single-codebase iOS + Android video diary app — .NET MAUI

---

## Why MAUI (and not React Native / Flutter)?

| Factor | MAUI | RN / Flutter |
|---|---|---|
| Language | C# — your existing codebase | JS/Dart — start over |
| Contract requirement | Explicitly specified in Appendix A | N/A |
| Single codebase | ✅ 1 project, 2 targets | ✅ but different DI model |
| Native camera/FileProvider | Platform folder shims | Plugin ecosystem |
| Supabase C# SDK | Official, typed | Third-party wrappers |
| SQLite offline | sqlite-net-pcl, zero config | Various |

**Bottom line:** MAUI is the right call. The contract mandates it and the existing code already uses it correctly.

---

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 9.0+ | `dotnet --version` |
| MAUI workload | latest | `dotnet workload install maui` |
| Xcode | 15+ | iOS builds (macOS only) |
| Android SDK | API 26+ | Via VS / Android Studio |
| Visual Studio 2022+ | 17.8+ | Windows; or VS for Mac / Rider |

---

## Quick Start

### 1. Clone / open the project
```bash
cd MomentaryMomentos
```

### 2. Configure Supabase
Edit `appsettings.json` (or use User Secrets for dev):

```json
{
  "Supabase": {
    "Url": "https://YOUR_PROJECT.supabase.co",
    "AnonKey": "YOUR_ANON_KEY",
    "StorageBucket": "user-videos"
  }
}
```

**Supabase setup (one-time):**
- Create project at https://supabase.com
- Run the SQL schema (see `docs/supabase_schema.sql`)
- Create storage bucket `user-videos` (public or signed URLs)
- Enable Email auth under Authentication → Providers

### 3. Restore packages
```bash
dotnet restore
```

### 4. Run on Android emulator
```bash
dotnet build -t:Run -f net9.0-android
```

### 5. Run on iOS simulator (macOS only)
```bash
dotnet build -t:Run -f net9.0-ios
```

---

## Project Structure

```
MomentaryMomentos/
├── Models/                    # Data transfer objects
│   ├── Memory.cs              # Core entity (10-sec video)
│   ├── Tag.cs                 # Emotion/category tag
│   ├── UserProfile.cs         # User with isPremium flag
│   ├── AuthSession.cs         # JWT session record
│   └── SubscriptionConfig.cs  # Admin-configurable pricing
│
├── Services/                  # Business logic layer
│   ├── SupabaseService.cs     # HTTP client for Supabase REST + Auth
│   ├── AuthService.cs         # Sign-in / sign-up / session restore
│   ├── SyncService.cs         # Online/offline sync orchestration
│   ├── OfflineService.cs      # Mock data for offline mode
│   ├── SubscriptionService.cs # Free/Premium tier logic
│   ├── VideoService.cs        # Upload/list/delete videos in Supabase Storage
│   ├── SecureTokenStore.cs    # iOS Keychain / Android Keystore via MAUI SecureStorage
│   └── ConnectivityService.cs # Network state observer
│
├── Data/
│   └── LocalDb.cs             # SQLite cache (sqlite-net-pcl)
│
├── ViewModels/                # MVVM — CommunityToolkit.Mvvm source generators
│   ├── BaseViewModel.cs       # IsBusy, ErrorMessage, HasError
│   ├── LoginViewModel.cs
│   ├── RegisterViewModel.cs
│   ├── HomeViewModel.cs       # Dashboard stats
│   ├── CaptureViewModel.cs    # Record / tag / save flow
│   ├── ReliveViewModel.cs     # Browse / play / favorite / delete
│   ├── ProfileViewModel.cs
│   ├── SubscriptionViewModel.cs
│   └── AdminViewModel.cs      # Manage default tags (admin only)
│
├── Views/                     # XAML UI pages
│   ├── LoginPage.xaml
│   ├── RegisterPage.xaml
│   ├── ForgotPasswordPage.xaml
│   ├── HomePage.xaml          # Dashboard
│   ├── CapturePage.xaml       # 3-step: Record → Emotion → Save
│   ├── RelivePage.xaml        # Memory list with filter/play/delete
│   ├── ProfilePage.xaml
│   ├── SubscriptionPage.xaml
│   ├── AdminPage.xaml
│   └── VideoPlayerPage.xaml   # MediaElement fullscreen player
│
├── Converters/                # IValueConverter implementations
├── Utils/Json.cs              # Shared JsonSerializerOptions
├── Platforms/
│   ├── Android/
│   │   ├── MainActivity.cs         # Routes OnActivityResult → VideoRecorderService
│   │   ├── MainApplication.cs
│   │   ├── AndroidManifest.xml     # Camera, mic, internet permissions
│   │   └── Resources/xml/file_paths.xml  # FileProvider config
│   └── iOS/
│       ├── AppDelegate.cs
│       └── Program.cs
│
├── App.xaml / App.xaml.cs     # Application entry, session restore
├── AppShell.xaml              # Shell routing: auth pages + 4-tab main
├── MauiProgram.cs             # DI composition root
└── appsettings.json           # Config (replace with real keys)
```

---

## Architecture Notes

### Single-codebase Strategy
- **Shared:** 100% of Models, ViewModels, Services, XAML pages
- **Platform-specific:** Only `VideoRecorderService` (Android) vs `NoOpVideoRecorderService` (iOS)
  - iOS uses MAUI's built-in `MediaPicker.CaptureVideoAsync()` — no shim needed
  - The `#if ANDROID` condition in `MauiProgram.cs` handles DI registration

### Offline-First
- `OfflineService` provides mock data and accepts any credentials in offline mode
- `SyncService` wraps all reads — tries Supabase first, falls back to `LocalDb` SQLite cache
- `SyncService.SaveMemoryAsync()` uploads immediately if online, queues in `PendingUploadRow` if not

### MVVM Pattern
- `CommunityToolkit.Mvvm` source generators — `[ObservableProperty]` → auto-generates `INotifyPropertyChanged`
- `[RelayCommand]` → auto-generates `ICommand` implementation
- **Learning note:** Source generators mean less boilerplate code. `partial` keyword on the class is required for them to work.

### Navigation
- MAUI Shell: `//login` and `//register` are standalone (no tab bar)
- `//main/home`, `//main/capture`, `//main/relive`, `//main/profile` are the 4 main tabs
- Modal routes: `subscription`, `admin`, `video`, `forgotpassword`

### DI Lifetime Rules
- **Singleton:** Services, LocalDb — one instance for app lifetime
- **Transient:** ViewModels and Pages — fresh instance per navigation

---

## Supabase Schema (run in SQL editor)

```sql
-- User profiles (extended auth.users)
CREATE TABLE profiles (
  id UUID REFERENCES auth.users PRIMARY KEY,
  full_name TEXT,
  is_admin BOOLEAN DEFAULT false,
  is_premium BOOLEAN DEFAULT false,
  created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Emotion / category tags
CREATE TABLE tags (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name TEXT NOT NULL,
  color TEXT DEFAULT '#3B82F6',
  icon TEXT DEFAULT '✨',
  is_active BOOLEAN DEFAULT true,
  user_id UUID REFERENCES auth.users  -- null = global default tag
);

-- Video memories
CREATE TABLE memories (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id UUID REFERENCES auth.users NOT NULL,
  title TEXT NOT NULL DEFAULT 'Untitled Memory',
  video_url TEXT,
  tags TEXT[] DEFAULT '{}',
  is_favorite BOOLEAN DEFAULT false,
  created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Admin subscription config
CREATE TABLE subscription_config (
  id INT PRIMARY KEY DEFAULT 1,
  free_user_video_limit INT DEFAULT 50,
  monthly_price DECIMAL DEFAULT 4.99,
  annual_price DECIMAL DEFAULT 49.99,
  monthly_enabled BOOLEAN DEFAULT true,
  annual_enabled BOOLEAN DEFAULT true
);
INSERT INTO subscription_config DEFAULT VALUES;

-- Seed default emotion tags
INSERT INTO tags (name, color, icon) VALUES
  ('Happy', '#F59E0B', '😊'),
  ('Grateful', '#10B981', '🙏'),
  ('Excited', '#3B82F6', '🎉'),
  ('Peaceful', '#8B5CF6', '🌿'),
  ('Sad', '#6B7280', '😢'),
  ('Family', '#EC4899', '👨‍👩‍👧'),
  ('Friends', '#F97316', '👯'),
  ('Travel', '#06B6D4', '✈️'),
  ('Work', '#64748B', '💼');

-- RLS Policies
ALTER TABLE profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE memories ENABLE ROW LEVEL SECURITY;
ALTER TABLE tags ENABLE ROW LEVEL SECURITY;

CREATE POLICY "Users own their profile" ON profiles FOR ALL USING (auth.uid() = id);
CREATE POLICY "Users own their memories" ON memories FOR ALL USING (auth.uid() = user_id);
CREATE POLICY "Tags visible to all auth users" ON tags FOR SELECT USING (auth.role() = 'authenticated');
CREATE POLICY "Admins manage tags" ON tags FOR ALL USING (
  EXISTS (SELECT 1 FROM profiles WHERE id = auth.uid() AND is_admin = true)
);
```

---

## App Store Deployment

### iOS
1. Open in Xcode (generate Xcode project: `dotnet build -f net10.0-ios`)
2. Set signing in Xcode → Signing & Capabilities
3. Archive → Distribute App → App Store Connect

### Android
```bash
dotnet publish -f net10.0-android -c Release
# Produces signed .aab for Play Console
```

---

## Budget Tracker (Contract §5)

| Phase | Est. Hours | At $40/hr |
|---|---|---|
| Architecture + setup | 8 | $320 |
| Auth + Supabase integration | 10 | $400 |
| Video capture + upload | 12 | $480 |
| Tag/emotion system | 6 | $240 |
| Offline mode + sync | 10 | $400 |
| Subscription UI | 8 | $320 |
| Admin panel | 6 | $240 |
| App Store prep + deploy | 8 | $320 |
| **Total (est.)** | **68 hrs** | **$2,720** |

Under the $3,000 cap with buffer for revisions.
