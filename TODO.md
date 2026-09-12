# MomentaryMomentos — Feature Parity TODO
_Cross-reference: PWA source (Background Information/pwa_source) vs MAUI app_
_Schema reference: docs/supabase_schema.sql_
_Current version: v1.0.0 (build 1) — visible in HomePage header_

---

## 🚀 Deployment Notes (Android Emulator)

> **Critical:** Debug APKs embed assemblies and are ~100 MB. Ensure the emulator has ≥500 MB free on `/data` before installing.

| Setting | Value | Reason |
|---------|-------|--------|
| `EmbedAssembliesIntoApk` | `true` | `libxamarin-debug-app-helper.so` in Debug overrides `AndroidFastDeployment=false`; assemblies must be in the APK for plain `adb install` to work |
| `AndroidFastDeployment` | `false` | Prevents VS from using fast-deploy push to `.__override__/` |
| `RunAOTCompilation` | `false` | Keeps build fast in Debug |
| Emulator AVD | pixel_8_pro_-_api_36_0 (API 36) | |
| Package | `com.momentarymomentos.app` | |
| MainActivity | `crc649477d106b5c00c06.MainActivity` | |

**Install commands (after `dotnet build`):**
```bash
adb uninstall com.momentarymomentos.app
adb install --no-incremental bin\Debug\net10.0-android\com.momentarymomentos.app-Signed.apk
adb shell am start -n com.momentarymomentos.app/crc649477d106b5c00c06.MainActivity
```

**If emulator storage is full** (`INSTALL_FAILED_INSUFFICIENT_STORAGE`):
```bash
# Option 1: Wipe emulator data (fastest, safe for test device)
adb emu kill
emulator.exe -avd pixel_8_pro_-_api_36_0 -wipe-data
# Wait for boot, then reinstall

# Option 2: Check space
adb shell df /data
```

---

## 🔴 CRITICAL — Core experience is broken/missing without these

### ~~1. Video Trimmer~~ ✅ DONE
**PWA:** Full `VideoTrimmer` component using FFmpeg.wasm — interactive slider to pick a 10-second window, live preview, client-side processing.
**MAUI:** ✅ Implemented — `IVideoTrimService` (Android: MediaExtractor+MediaMuxer, iOS: AVAssetExportSession), `TrimPage` + `TrimViewModel`, integrated into `PickVideo` flow with 50 MB guard and duration check.
**What to build:**
- A `TrimPage.xaml` / `TrimViewModel` with a video scrub bar
- Use platform MediaCodec (Android) / AVFoundation (iOS) to trim the clip server-side or via a native call
- Integrate into the CapturePage flow after video is picked: if duration > 10s → push TrimPage before tag selection
- Validate: reject files > 50 MB and < 1 second

---

### ~~2. Relive Carousel / One-at-a-Time Playback Experience~~ ✅ DONE
**PWA:** `MemoryPlayback` shows one memory at a time — full-screen card with swipe left/right navigation, play button, tag pills, date. The "Surprise Me" flow opens this single-memory view.
**MAUI:** ✅ Implemented — `MemoryDetailPage` + `MemoryDetailViewModel`. Full-screen aero layout with thumbnail background, play button, prev/next arrows, swipe-left/right gestures, position label, tag pills with resolved names, title, date. `SurpriseMeCommand` and card taps both navigate here. `PlayCommand` in `ReliveViewModel` now routes to `memorydetail` instead of `video` directly.

---

### ~~3. Retag Memory (Edit Tags After Capture)~~ ✅ DONE
**PWA:** `MemoryPlayback` has a "Retag" button that opens a full tag-selector modal and saves updated tags to `momo_memories.tags`.
**MAUI:** ✅ Implemented — "🏷️ Retag" button on `MemoryDetailPage` toggles an inline retag panel with a FlexLayout of tag toggle buttons. `SaveRetagCommand` calls `SupabaseService.UpdateTagsAsync(memoryId, selectedIds)`. Tag display updates immediately after save.

---

### ~~4. "See Less" / Disinterest Feedback on Memories~~ ✅ DONE
**PWA:** Thumbs-down / "See Less" button in MemoryPlayback reduces a memory's display frequency.
**MAUI:** ✅ Implemented — "👎 Less" button on `MemoryDetailPage` stores memory IDs in `Preferences` (key `momo_skipped_memories`, JSON array). No DB migration needed. `ReliveViewModel.SurpriseMeCommand` reads skipped IDs via `MemoryDetailViewModel.GetSkippedIds()` and excludes them from the random pick (falls back to full list if all are skipped).

---

## 🟠 HIGH — Significantly impacts usability

### ~~5. Profile Name Editing~~ ✅ DONE
**PWA:** `ProfileEditModal` allows users to update their `full_name` stored in `momo_profiles`.
**MAUI:** ✅ Implemented — `ProfileViewModel` exposes `EditNameCommand` / `SaveNameCommand`; inline edit Entry on `ProfilePage`; calls `SupabaseService.UpdateProfileAsync(fullName)`.

---

### ~~6. User-Created Custom Tags~~ ✅ DONE
**PWA:** Users can create personal tags during the capture flow (name, color, emoji).
**MAUI:** ✅ Implemented — "＋ Add your own tag" button on `CapturePage` triggers `CreateCustomTagCommand` in `CaptureViewModel`; prompts for name, color swatch, emoji; calls `SupabaseService.CreateTagAsync()`; new tag appears immediately in the emotion grid.

---

### ~~7. Admin — User Management Panel~~ ✅ DONE
**PWA:** `admin/UserManagement.tsx` — lists all users, shows email + memory count + join date, allows toggling `is_admin` and deleting users.
**MAUI:** ✅ Implemented — `AdminPage` has a Users section with CollectionView; `AdminViewModel` exposes `LoadUsersCommand`, `ToggleAdminCommand`, `DeleteUserCommand`; `SupabaseService.GetAllProfilesAsync()` and `DeleteUserAsync()` added.

---

### ~~8. Admin — System Statistics Dashboard~~ ✅ DONE
**PWA:** `admin/SystemStats.tsx` — shows Total Profiles, Total Memories, Total Tags, Active Users.
**MAUI:** ✅ Implemented — stats card row at top of `AdminPage` with 4 glass tiles; `AdminViewModel` binds `TotalUsers`, `TotalMemories`, `TotalTags`, `ActiveUsers`; `LoadStatsCommand` queries Supabase counts.

---

### ~~9. Admin — Tag Delete~~ ✅ DONE
**PWA:** TagManagement allows deleting tags with confirmation.
**MAUI:** ✅ Implemented — 🗑 delete button in each tag row (AdminPage Grid col 4); `AdminViewModel.DeleteTagCommand` shows confirmation alert then calls `SupabaseService.DeleteTagAsync(tagId)`; tag removed from `Tags` collection immediately.

---

### ~~10. Video Upload Validation & Guided Flow~~ ✅ DONE
**PWA:** `UploadMemory` validates file type (video/*), file size (≤ 50 MB), and duration (checks if > 10s then routes to Trimmer).
**MAUI:** ✅ Implemented — 50 MB size check + duration check added to `CaptureViewModel.PickVideo()`, routes to TrimPage when needed.
**What to build:**
- In `CaptureViewModel.PickVideoAsync()`: after picking, check file size (reject > 50 MB with user message)
- Check video duration using `IVideoThumbnailService` or platform metadata; if > 10s → navigate to `TrimPage`
- Show explicit error messages for unsupported formats

---

## 🟡 MEDIUM — Noticeable gaps vs PWA experience

### ~~Tag Display Fix~~ ✅ DONE
**PWA:** Tags shown with proper name + color on memory cards.
**MAUI (was broken):** ✅ Fixed — `Memory.DisplayTags` (`List<TagInfo>`) is now resolved from tag IDs in `ReliveViewModel.Refresh()` and `HomeViewModel.Refresh()`. RelivePage and HomePage both bind to `DisplayTags` with per-tag color backgrounds.

### ~~Thumbnails~~ ✅ DONE (requires migration 001)
**PWA:** Video frame thumbnails shown on memory cards.
**MAUI:** ✅ Fixed — `GetMemoriesAsync` now fetches `thumbnail_url`; `ParseMemory` maps it; `LocalDb.MemoryRow` caches it. Thumbnails are generated locally from the video file — at capture time (`CaptureViewModel.SaveMemory`) and as a healing pass in `HomeViewModel`/`ReliveViewModel.BackfillMissingThumbnailsAsync` — then uploaded to the `user-videos` Storage bucket and recorded in `momo_memories.thumbnail_url` (via `SyncService.TryUploadThumbnailAsync` → `UploadThumbnailAsync` + `UpdateMemoryThumbnailAsync`) so previews propagate cross-device. Mementos whose video survives on no device fall back to a placeholder icon over the gradient. HomePage updated to show thumbnail cards (was text-only).
**Requires:** Run `docs/migrations/001_thumbnails_and_metrics.sql` in Supabase SQL editor.

### ~~Tag Filter (Search by Tag)~~ ✅ DONE
**PWA:** Users could browse/filter memories by tag category.
**MAUI:** ✅ Implemented — Horizontal tag chip strip at top of RelivePage (`FilterTags` ObservableCollection of `FilterTagChip`). Tapping a chip filters `FilteredMemories`; tapping again or "All" clears filter.

### ~~AI-Weighted Memory Surfacing~~ ✅ DONE (requires migration 001)
**PWA:** `memory_metrics` table with `weight` field — favorites boost weight +2, "See Less" reduces -1. Surprise Me uses weighted random selection.
**MAUI:** ✅ Implemented — `SupabaseService.GetMemoryWeightsAsync/UpsertMemoryWeightAsync` read/write `momo_memory_metrics`. `ReliveViewModel.SurpriseMeCommand` uses weighted pool expansion. `MemoryDetailViewModel` writes weight changes on favorite and see-less actions.
**Requires:** Run `docs/migrations/001_thumbnails_and_metrics.sql` in Supabase SQL editor.

---

### ~~11. Dashboard — Categories / Tag Distribution Modal~~ ✅ DONE
**PWA:** The Dashboard has a "Categories" stat tile that opens a tag breakdown modal.
**MAUI:** ✅ Implemented — stats Grid changed to 2×2 layout; Categories tile added; tap triggers `ShowCategoriesCommand` which calls `DisplayAlert` with tag name + count breakdown sorted by most-used. `HomeViewModel._tagCounts` list built during `Refresh()`.

---

### 12. Memory Detail — Download / Export
**PWA:** MemoryPlayback has a download button that fetches the signed URL and triggers a browser download with a descriptive filename (e.g., `happy-2024-01-15.mp4`).
**MAUI:** No export capability.
**What to build:**
- Add a share/download button on `MemoryDetailPage`
- Use `SupabaseService.GetSignedVideoUrlAsync(videoPath)` to get a temporary URL
- Use `Share.RequestAsync()` (MAUI built-in) to share the video file, or download to local storage and open via `Launcher`

---

### ~~13. Capture — Front/Back Camera Toggle~~ ✅ DONE
**PWA:** Camera capture has a flip button to toggle `facingMode: user` vs `facingMode: environment`.
**MAUI:** ✅ Implemented — `IVideoRecorderService.CaptureVideoAsync(facingFront: bool)` parameter added; Android `VideoRecorderService` passes `CameraSelector.DEFAULT_FRONT_CAMERA` vs `DEFAULT_BACK_CAMERA`; `CaptureViewModel.FacingFront` + `FlipCameraCommand`; 🔄/🤳 toggle button in `CapturePage` header row.

---

### ~~14. Capture — Audio Mute Toggle~~ ✅ DONE
**PWA:** A mic button toggles the audio track on/off before recording.
**MAUI:** ✅ Implemented — `IVideoRecorderService.CaptureVideoAsync(muteAudio: bool)` parameter added; Android `VideoRecorderService` skips `SetAudioSource/SetAudioEncoder` when muted; `CaptureViewModel.MuteAudio` + `ToggleMuteCommand` + `MuteLabel`; 🎙/🔇 button in `CapturePage` header row.

---

### ~~15. Capture — Live Countdown Timer Display~~ ✅ DONE
**PWA:** During recording, a progress bar and countdown timer (10 → 0) are shown.
**MAUI:** ✅ Implemented — 3-2-1 pre-record countdown: `CaptureViewModel.IsCountingDown` + `RecordingCountdown`; `CapturePage` shows a centered aero countdown Border visible during `IsCountingDown`; counts 3→2→1 with 1 s delays before handing off to `VideoRecorderService`.

---

### 16. Subscription Config — Respect Free Video Limit from DB
**PWA:** Reads `momo_subscription_config.free_video_limit` (default: 50) and enforces it.
**MAUI:** `SubscriptionService` exists but the free limit may be hardcoded rather than fetched from the DB table.
**What to build:**
- `SupabaseService.GetSubscriptionConfigAsync()` → reads from `momo_subscription_config` (single row, `id = 1`)
- `SubscriptionService`: on app start, fetch config and store `FreeVideoLimit`, `MonthlyPrice`, `AnnualPrice`
- Enforce limit in `CaptureViewModel.SaveMemoryAsync()`: check memory count vs limit; if exceeded and not premium → show upgrade prompt

---

### 17. Subscription — Pricing from DB on SubscriptionPage
**PWA:** Reads plan pricing from `momo_subscription_config` and displays dynamic prices.
**MAUI:** `SubscriptionPage` shows plans but prices may be hardcoded in `SubscriptionViewModel`.
**What to build:**
- Wire `SubscriptionViewModel` to fetch `monthly_price` and `annual_price` from `momo_subscription_config` via `SubscriptionService`
- Display live prices on the Subscribe buttons

---

### ~~18. Profile — Avatar / Photo Upload~~ ✅ DONE
**PWA:** ProfileEditModal has a placeholder for avatar (shows initials).
**MAUI:** ✅ Implemented — `ProfileViewModel.PickAvatarCommand` calls `MediaPicker.PickPhotoAsync()`; uploads to Supabase Storage `avatars/{userId}.jpg` via `SupabaseService.UploadAvatarAsync()`; `UpdateAvatarUrlAsync()` PATCHes `momo_profiles.avatar_url`; `ProfilePage` shows real image via `HasAvatar`/`AvatarUrl` with 📷 overlay tap-to-change.

---

## 🟢 LOW — Nice-to-have / polish

### ~~19. Video Preview in Capture Flow (Before Saving)~~ ✅ DONE
**PWA:** After recording/picking, shows a looping preview of the clip with play/pause controls before the tag selection step.
**MAUI:** ✅ Implemented — `CapturePage.xaml` has a `MediaElement` (lines 74-83) bound to `VideoPreviewSource`, visible when `HasVideo = true`, with `ShouldAutoPlay=True`, `ShouldMute=True`, `ShouldLoopPlayback=True`. `CaptureViewModel.VideoPreviewSource` is computed from `LastCapturedPath`.

---

### 20. Relive — Swipe Gesture Support
**PWA:** `MemoryPlayback` supports left/right swipe to navigate to previous/next memory.
**MAUI:** No swipe gesture on memory cards.
**What to build:**
- On `MemoryDetailPage`, add a `SwipeGestureRecognizer` (SwipeLeft → next, SwipeRight → previous)
- `MemoryDetailViewModel`: expose `NextMemoryCommand`, `PreviousMemoryCommand` cycling through the Memories list
- Alternatively, wrap in a `CarouselView` (MAUI built-in)

---

### 21. Toast Notifications (Success/Error Feedback)
**PWA:** Uses Sonner/toast library for non-blocking success/error toasts ("Memory saved!", "Upload failed").
**MAUI:** Uses `Label` error text and `DisplayAlert` for errors — more disruptive.
**What to build:**
- Use `CommunityToolkit.Maui.Alerts.Toast` (already in the toolkit) or `Snackbar`
- Replace `ErrorMessage` label + `DisplayAlert` success dialogs with non-blocking toasts in `CaptureViewModel`, `ReliveViewModel`

---

### 22. Admin — Tag Search/Filter
**PWA:** TagManagement has a search box to filter tags by name.
**MAUI:** AdminPage lists all tags in a CollectionView with no filter.
**What to build:**
- Add a search `Entry` above the tag CollectionView on `AdminPage`
- `AdminViewModel`: filter `Tags` observable by search text using a `FilteredTags` property

---

### 23. Offline Mode — Explicit UI Indicator
**PWA:** No offline mode (uses graceful degradation).
**MAUI:** Has `IsOfflineMode` toggle on LoginPage but no status indicator in the main app.
**What to build:**
- Show a small banner or colored dot in the HomePage header when `IsOfflineMode = true`
- `HomeViewModel.ConnectionStatus` already exists — make it visually prominent with an aero-style warning border

---

## 📋 Summary by Priority

| # | Feature | Priority | Screen(s) Affected |
|---|---------|----------|-------------------|
| ~~1~~ | ~~Video Trimmer~~ | ✅ Done | CapturePage + TrimPage |
| ~~2~~ | ~~Relive Carousel / MemoryDetailPage~~ | ✅ Done | RelivePage + MemoryDetailPage |
| ~~3~~ | ~~Retag Memory~~ | ✅ Done | MemoryDetailPage |
| ~~4~~ | ~~See Less / Skip Feedback~~ | ✅ Done | MemoryDetailPage |
| ~~5~~ | ~~Profile Name Editing~~ | ✅ Done | ProfilePage |
| ~~6~~ | ~~User-Created Custom Tags~~ | ✅ Done | CapturePage |
| ~~7~~ | ~~Admin — User Management~~ | ✅ Done | AdminPage |
| ~~8~~ | ~~Admin — System Stats~~ | ✅ Done | AdminPage |
| ~~9~~ | ~~Admin — Tag Delete~~ | ✅ Done | AdminPage |
| ~~10~~ | ~~Upload Validation + Trim Routing~~ | ✅ Done | CapturePage |
| ~~11~~ | ~~Dashboard Tags/Categories Modal~~ | ✅ Done | HomePage |
| 12 | Memory Download/Export | 🟡 Medium | MemoryDetailPage |
| ~~13~~ | ~~Camera Flip (Front/Back)~~ | ✅ Done | CapturePage |
| ~~14~~ | ~~Audio Mute Toggle~~ | ✅ Done | CapturePage |
| ~~15~~ | ~~Recording Countdown Timer~~ | ✅ Done | CapturePage |
| 16 | Free Video Limit from DB Config | 🟡 Medium | CaptureViewModel |
| 17 | Dynamic Subscription Pricing | 🟡 Medium | SubscriptionPage |
| ~~18~~ | ~~Avatar / Profile Photo~~ | ✅ Done | ProfilePage |
| ~~19~~ | ~~Video Preview Before Save~~ | ✅ Done | CapturePage |
| 20 | Swipe Gesture on Memory Detail | 🟢 Low | MemoryDetailPage |
| 21 | Toast Notifications | 🟢 Low | All pages |
| 22 | Admin Tag Search | 🟢 Low | AdminPage |
| 23 | Offline Mode Visual Indicator | 🟢 Low | HomePage |

---

## ✅ Already Implemented (Feature Parity Confirmed)

- Email/Password Sign In & Sign Up
- Sign Out
- Forgot Password page (ForgotPasswordPage exists)
- 10-second video recording (VideoRecorderService, platform-specific)
- Pick video from gallery (MediaPicker)
- Emotion tag display and multi-selection
- Memory save to Supabase (video upload + DB record)
- Memory list / RelivePage
- Favorite toggle on memories
- Delete memory with confirmation
- Video playback (VideoPlayerPage + MediaElement)
- Dashboard stats (Total, Favorites, This Week, Storage, Trend)
- Admin tag create + active/inactive toggle
- Offline mode with LocalDb cache
- Sync service (queue for deferred uploads)
- Subscription page (monthly/annual plans)
- Thumbnail generation (IVideoThumbnailService, platform-specific)
- Thumbnail display on memory cards in RelivePage
