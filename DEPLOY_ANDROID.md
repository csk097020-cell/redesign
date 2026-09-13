# Android Play Store Deployment Checklist
_Momentary Momentos — com.momentarymementos.app_

---

## 1. Code / Feature Gaps

- [ ] **Subscription config wiring (TODO #16 & #17)** — `SubscriptionService` already reads from DB, but verify `SubscriptionViewModel` calls `GetAvailablePlansAsync()` and displays live prices instead of hardcoded values on `SubscriptionPage`.
- [ ] **Google Play product IDs must match exactly** — Play Console product IDs must be `monthly_premium` and `annual_premium` (the exact IDs used in `SubscriptionService.GetAvailablePlansAsync()`). Mismatch = billing silently fails.
- [ ] **Account deletion flow** — Play Store policy requires a way for users to request account/data deletion. Add a "Delete Account" option in `ProfilePage` or link to a hosted deletion request form.
- [ ] **Memory Download/Export (TODO #12)** — Medium priority; decide whether to ship v1 without it.

---

## 2. Backend / Supabase

- [ ] **Run migration 001** — `docs/migrations/001_thumbnails_and_metrics.sql` (thumbnails + memory metrics/weights). Required for thumbnail display and AI-weighted surfacing to work in production.
- [ ] **Run migration 002** — `docs/migrations/002_subscription_pricing.sql` (adds `paid_video_limit`, sets $9.99/$99.99 pricing). Run in Supabase SQL editor: https://supabase.com/dashboard/project/wnumxvfvabqpgwenweff/sql
- [ ] **Verify RLS policies** — confirm `momo_profiles`, `momo_memories`, `momo_tags`, `momo_memory_metrics`, `momo_subscription_config` all have correct Row Level Security policies enabled.
- [ ] **Supabase Storage bucket** — confirm `user-videos` bucket exists, CORS is set to allow the app, and file size limits are appropriate (50 MB per video).
- [ ] **Supabase auth email templates** — brand the confirm/reset emails with app name and logo (Authentication → Email Templates in Supabase dashboard).

---

## 3. Release Signing & Build

- [ ] **Generate release keystore** (do this once; store permanently and securely — losing it means you can never update the app):
  ```bash
  keytool -genkey -v -keystore momentarymomentos-release.jks \
    -alias momentarymomentos -keyalg RSA -keysize 2048 -validity 10000
  ```
- [ ] **Store keystore outside the repo** — never commit it to git. Keep a backup in a password manager or secure cloud storage.
- [ ] **Configure signing in the project** — add to `MomentaryMomentos.csproj` (or pass as MSBuild args) for Release builds:
  ```xml
  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <AndroidKeyStore>true</AndroidKeyStore>
    <AndroidSigningKeyStore>path\to\momentarymomentos-release.jks</AndroidSigningKeyStore>
    <AndroidSigningKeyAlias>momentarymomentos</AndroidSigningKeyAlias>
    <AndroidSigningKeyPass>$(KEYSTORE_PASS)</AndroidSigningKeyPass>
    <AndroidSigningStorePass>$(KEYSTORE_PASS)</AndroidSigningStorePass>
  </PropertyGroup>
  ```
- [ ] **Build release AAB** (Play Store requires AAB, not APK, for new apps):
  ```bash
  dotnet publish -f net10.0-android -c Release
  ```
  Output: `bin\Release\net10.0-android\publish\com.momentarymomentos.app.aab`
- [ ] **Install and smoke-test the release build on a real device** — R8/ProGuard shrinking can break reflection-heavy code (MAUI, Supabase JSON deserialization). Verify login, video capture, and playback all work.

---

## 4. App Icon & Store Assets

- [ ] **Hi-res app icon** — Play Store listing requires a 512×512 PNG. Check current `appicon.png` size; if not 512×512, export one. (The MAUI build resizes for device mipmaps automatically, but the store listing icon must be uploaded separately in Play Console.)
- [ ] **Feature graphic** — 1024×500 PNG/JPG required for Play Store listing. Design a branded banner (app name, tagline, sample screenshot).
- [ ] **Phone screenshots** — minimum 2, maximum 8. Capture on a real device or high-res emulator (1080p+). Required aspect ratios: 16:9 landscape or 9:16 portrait.
- [ ] **Tablet screenshots** — optional but recommended for broader reach (7-inch and 10-inch).

---

## 5. Google Play Console Setup

- [ ] **Developer account active** — confirm the $25 one-time registration is complete and the account is in good standing at https://play.google.com/console.
- [ ] **Create the app** — New app → "Momentary Momentos", Default language: English (United States), App type: App.
- [ ] **Create in-app subscription products** in Play Console → Monetize → Products → Subscriptions:
  - Product ID: `monthly_premium` — $4.99/month — "Premium Monthly"
  - Product ID: `annual_premium` — $49.99/year — "Premium Annual"
  - Activate both products (draft products cannot be purchased).
  - These must match the live `momo_subscription_config` row (`monthly_price`/`annual_price`) exactly — see `docs/migrations/002_subscription_pricing.sql`.
- [ ] **Set up billing test accounts** — Play Console → Setup → License Testing → add tester Gmail addresses. Test accounts can buy without being charged.
- [ ] **Upload AAB to Internal Testing track** — Play Console → Testing → Internal Testing → Create new release. Add internal testers by email.
- [ ] **Test billing end-to-end on a real device** with a tester account before promoting to production.

---

## 6. Store Listing

- [ ] **App name**: "Momentary Momentos" (30 chars — within the 30-char limit)
- [ ] **Short description** (max 80 chars): write a punchy one-liner, e.g., _"Capture 10-second video memories and relive them when you need a boost."_
- [ ] **Full description** (max 4000 chars): explain core features — video capture, emotion tags, Surprise Me, premium plans.
- [ ] **Category**: Lifestyle (or Photography — pick the most relevant).
- [ ] **Contact email**: set a support email visible to users.
- [ ] **Content rating**: complete the IARC questionnaire (likely "Everyone" — no violence/explicit content).
- [ ] **Upload feature graphic, screenshots, and hi-res icon** (from step 4).

---

## 7. Legal & Compliance

- [ ] **Host privacy policy publicly** — `docs/privacy_policy.html` exists but needs a public URL. Options: GitHub Pages, Netlify drop, or upload to Supabase Storage as a public file. The URL is required in Play Console.
- [ ] **Privacy policy URL** — paste the hosted URL into Play Console → Store Listing → Privacy Policy URL field.
- [ ] **Data Safety form** (Play Console → Store Listing → Data Safety) — declare what data the app collects:
  - **Personal info**: Name, Email address (collected, not shared with third parties)
  - **Videos**: User-generated video content uploaded to Supabase Storage
  - **Device identifiers**: Used by Sentry for crash reporting
  - **Financial info**: Managed entirely by Google Play Billing (no raw card data)
  - Data is encrypted in transit: ✅ Yes (HTTPS/TLS only — `usesCleartextTraffic="false"`)
  - Users can request deletion: ✅ must be true — requires account deletion flow (see Code section above)
- [ ] **Terms of Service URL** — optional but strongly recommended for an app with paid subscriptions.

---

## 8. Android Technical Checks

- [ ] **Target API level** — Play Store requires new apps to target API 35 (Android 15) or higher as of 2025. Verify the MAUI/Android SDK in your build produces a `targetSdkVersion` of 35+. Run `dotnet publish` and check `bin\Release\net10.0-android\...` AndroidManifest.xml to confirm.
- [ ] **Runtime permissions** — verify the permission request dialogs appear correctly on first launch on Android 13+ (`READ_MEDIA_VIDEO` instead of `READ_EXTERNAL_STORAGE`). Test on API 33+ device or emulator.
- [ ] **Camera + mic on real device** — emulator camera is simulated. Smoke-test video recording on a physical Android phone.
- [ ] **FileProvider in release** — confirm `file_paths.xml` is packaged and the `VideoRecorderService` can write temp files in release mode (no stripped-away debug paths).
- [ ] **Billing manifest deduplication target** — the `FixBillingManifestDuplicate` MSBuild target runs on `_GenerateJavaStubs` without a configuration condition. Verify the release AAB builds without manifest merge errors.

---

## 9. Crash Reporting

- [ ] **Sentry configured** — DSN is in `appsettings.json` ✅. Verify Sentry initializes before the first user interaction (check `MauiProgram.cs`).
- [ ] **Test a crash in release mode** — force an exception in a debug-release hybrid build and confirm it appears in the Sentry dashboard at https://sentry.io before shipping.
- [ ] **Set Sentry environment** — tag release builds as `"production"` and debug builds as `"development"` so errors are separated in the Sentry dashboard.

---

## 10. Version Management

- [ ] **Current version**: `ApplicationDisplayVersion=1.0`, `ApplicationVersion=9` (versionCode=9).
- [ ] **versionCode must increment for every upload** — even internal test uploads. Keep a record; you cannot reuse a versionCode.
- [ ] **Decide versioning strategy**: suggest `DisplayVersion = major.minor.patch` and `Version (versionCode) = auto-incrementing integer` from CI or manual.

---

## 11. Testing Tracks (in order)

1. **Internal Testing** — upload AAB, install via Play Console link, verify all core flows (auth, capture, relive, billing) on a real device.
2. **Closed Testing (Beta)** — invite a small group outside your team for real-world feedback before wide release.
3. **Production** — promote from closed testing when satisfied. Play review typically takes 1–3 days for a new app.

---

## 12. Post-Upload: Pre-Launch Report

- [ ] After uploading to any track, Play Console runs automated Robo tests and reports crashes. Review the **Pre-launch report** (Play Console → Android vitals → Pre-launch report) and fix any crashes before promoting to Production.

---

## Quick Reference: Build Commands

```bash
# Release AAB (for Play Store upload)
dotnet publish -f net10.0-android -c Release

# Debug APK (for local device testing)
dotnet build -f net10.0-android -c Debug
adb install --no-incremental bin\Debug\net10.0-android\com.momentarymomentos.app-Signed.apk
```
