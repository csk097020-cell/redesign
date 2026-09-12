# Android Store Deployment Ticklist

Audited against commit `ab39e15` on July 26, 2026.

Legend: `[x]` verified in this repository; `[ ]` still required or cannot be
verified without Play Console, Supabase, or a physical device.

## Already done

- [x] Android package ID is `com.momentarymementos.app`.
- [x] Version is configured as `1.0` (version code `5`).
- [x] Minimum Android version is API 26.
- [x] Effective target SDK is API 36.
- [x] Android 13+ media permission and legacy permission cutoff are configured.
- [x] Camera, microphone, internet, network, and billing permissions exist.
- [x] Cleartext network traffic is disabled.
- [x] Android `FileProvider` and `file_paths.xml` are configured.
- [x] Release keystore, alias, and password environment variable exist locally.
- [x] Keystores and `appsettings.json` are excluded from Git.
- [x] Registration, login, logout, session restore, and password-reset code exist.
- [x] In-app account/data deletion UI exists with confirmation and export offer.
- [x] Privacy-policy and terms source documents exist under `docs/`.
- [x] Privacy policy is linked in the app, from the About card on the Profile page.
- [x] Privacy policy is published at `https://jsnewtonian.github.io/momo-privacy/`, including the
  `#delete-account` and `#delete-data` sections Play requires (August 12, 2026).
- [x] Subscription UI, purchase, acknowledgement, and restore code exist.
- [x] Product IDs are implemented as `monthly_premium` and `annual_premium`.
- [x] Billing prices/configuration are loaded from Supabase.
- [x] Sentry crash reporting is configured.
- [x] Local notifications and Android notification channels are implemented.
- [x] The store icon source is 1024 x 1024.
- [x] Android display name is `Momentary Momentos`.
- [x] The $19.95 `data_export_pack` purchase has been removed; email export remains free.
- [x] Account deletion now calls an authenticated server-side deletion function.
- [x] The deletion function removes Storage objects and the Supabase Auth identity.
- [x] The SQLite Android vulnerability has been resolved.
- [x] A fresh Android Release build completes successfully.
- [x] A new signed AAB was generated and its signature verified on July 26, 2026.

## Remaining code and build work

- [ ] **Add an in-app Terms of Use link.** The privacy policy is linked from the About card on the
  Profile page (`Views/ProfilePage.xaml:171` → `ProfileViewModel.OpenPrivacyPolicy`), but there is no
  terms link anywhere in the source. Apple Review Guideline 3.1.2 requires **both** inside the binary
  for auto-renewable subscriptions. `docs/terms_of_service.html` exists but is unpublished — host it
  alongside the privacy policy and add a second label next to the existing one.
- [ ] Verify version code `9` has never been uploaded; increment it if it has. Internal testing
  currently shows Build Version 8, so confirm which was actually pushed.
- [ ] Deploy and test the new `delete-account` Supabase Edge Function.
- [ ] Add automated tests or explicitly accept manual release validation.

## Supabase and Play Console confirmation

- [ ] Confirm all SQL migrations are deployed, especially migration `004`.
- [ ] Confirm production RLS and Storage policies match the app behavior.
- [ ] Confirm the export, storage-cleanup, and `delete-account` Edge Functions are deployed.
- [ ] Create and activate `monthly_premium` and `annual_premium` in Play Console.
- [x] Complete Play Console Data safety, including both deletion URLs (August 12, 2026). Answers
  recorded in `docs/google-play-declarations.md`.
- [ ] Point the Play Console privacy policy URL at `https://jsnewtonian.github.io/momo-privacy/`.
  It was set to `https://tex413.github.io/MoMo2/privacy_policy.html`, a page on a third party's
  account that cannot be updated and serves the stale May 6 policy.
- [ ] Check App Store Connect for the same stale privacy policy URL — build 6 is awaiting review.
- [ ] Complete Play Console App access, Content rating, Target audience, and Ads declarations.
- [ ] Confirm developer-account verification and whether the 12-testers-for-
  14-days closed-test requirement applies.

## Store listing and release testing

- [ ] Finalize store name, descriptions, category, and support contact.
- [ ] Create the 1024 x 500 feature graphic.
- [ ] Capture at least two current phone screenshots.
- [ ] Upload the new signed AAB to Internal testing.
- [ ] Install from Google Play and smoke-test core flows on a physical device.
- [ ] Review and clear Play Console errors, pre-launch crashes, and ANRs.
- [ ] Complete required Closed testing.
- [ ] Submit the tested build to Production.

## Current release artifact

- Path: `bin/Release/net10.0-android/com.momentarymementos.app-Signed.aab`
- SHA-256: _regenerate after the build 9 publish_
- Package: `com.momentarymementos.app`
- Version: `1.0` (`9`)
- Target SDK: `36`
- Architecture: `arm64-v8a`

