# Google Play Console — declarations and submission path

Answers for every Play Console form that currently blocks Momentary Momentos, derived from the
actual code and schema rather than from intent. Where an answer depends on a decision only you can
make, it is flagged **DECIDE**.

Verified state at time of writing: developer account **ThunderPeak** (personal), app **in Draft**,
production access **locked**, installed audience **0**, temporary app name still in use.

---

## The gate you cannot shortcut

A personal developer account must run a **closed** test with **at least 12 testers opted in for 14
continuous days**, then apply for production access, which is itself reviewed. You have 5 testers,
and internal testing does **not** count toward the requirement.

The 14 days run in wall-clock time no matter what else happens, so create the closed track and
recruit testers before polishing anything else. Everything below is what unblocks that.

---

## 1. App content declarations

### Ads
**No, this app does not contain ads.** Verified: no ad SDK in `MomentaryMomentos.csproj` (no AdMob,
Google Ads, AppLovin, or equivalent).

### Content rating questionnaire
Category: **Utility, Productivity, Communication, or Other**.

| Question | Answer | Why |
|---|---|---|
| Violence, sexual content, profanity, drugs | No | App content is entirely user-recorded video |
| Does the app share user-generated content? | **No** | Momentos are private to the account; there is no feed, sharing, or discovery between users |
| Does the app allow users to communicate? | No | No messaging, comments, or social features |
| Does the app share location? | No | No location permission in the manifest, no location API calls |
| Digital purchases | **Yes** | Play Billing subscription (`Plugin.InAppBilling`) |

Expected outcome: **Everyone / PEGI 3** or equivalent.

> The user-generated-content answer is worth care. The app stores UGC but never exposes one user's
> content to another, so Play's UGC moderation obligations don't attach. If you ever add sharing,
> this answer changes and a moderation policy becomes mandatory.

### Target audience and content
- Target age groups: **18 and over**. **DECIDE** — including under-18 groups triggers Families
  Policy requirements (no personalised ads, stricter data rules, possible extra review). The app has
  no child-directed features, so 18+ is the clean answer.
- Is the app appealing to children? **No**.

### Government apps
**No** — not affiliated with any government.

### Financial features
**No financial features.** A subscription is not a financial product; this section covers lending,
banking, crypto, and investments.

### Health
**No health features.** No health data is collected or inferred.

### Data safety
See section 2.

### Privacy policy URL

```
https://jsnewtonian.github.io/momo-privacy/
```

Source of truth is `docs/privacy_policy.html` in this repo, published to the `jsnewtonian/momo-privacy`
Pages repo as `index.html`. Support page: `docs/support.html`. Terms: `docs/terms_of_service.html`.

> **Do not use `https://tex413.github.io/MoMo2/privacy_policy.html`.** That URL was in Play Console
> until August 12, 2026. It is hosted on a third party's GitHub account, so it cannot be updated and
> could disappear without warning, and it still serves the May 6 policy containing a dead
> `@momentarymementos.com` contact address. Check App Store Connect for the same stale URL.

### Data deletion URLs

Play requires these separately from the privacy policy, and each must name the app, show the steps
prominently, and state what is deleted or kept.

```
Delete account URL:  https://jsnewtonian.github.io/momo-privacy/#delete-account
Delete data URL:     https://jsnewtonian.github.io/momo-privacy/#delete-data
```

---

## 2. Data safety

Answer these on the "Data safety" form. Everything here is traceable to the schema in
`docs/supabase_schema.sql` and the services in `Services/`.

### Global answers
- Is data encrypted in transit? **Yes** — all traffic is HTTPS, and
  `Platforms/Android/AndroidManifest.xml` sets `android:usesCleartextTraffic="false"`.
- Can users request data deletion? **Yes** — in-app account deletion via the `delete-account`
  Supabase edge function, plus a documented web request path in the privacy policy.
- Has the app committed to Play's Families Policy? **No** (18+ target audience).

### Data types

Submitted and accepted on August 12, 2026. This table is what was actually entered.

| Data type | Collected | Shared | Optional? | Purpose |
|---|---|---|---|---|
| **Email address** | Yes | No | Required | Account management, App functionality |
| **Name** | Yes | No | Optional | Account management (`momo_profiles.full_name`) |
| **User IDs** | Yes | No | Required | Account management, App functionality |
| **Photos** | Yes | No | Optional | App functionality — profile avatar (`avatars` bucket) |
| **Videos** | Yes | No | Required | App functionality — the momentos (`user-videos` bucket) |
| **Purchase history** | Yes | No | Optional | App functionality — entitlement (`is_premium`) |
| **Other user-generated content** | Yes | No | Optional | App functionality, Personalisation — titles, tags, capture dates, favourites |
| **Crash logs** | Yes | **Yes** | Required | Analytics — via Sentry |
| **Diagnostics** | Yes | **Yes** | Required | Analytics — via Sentry |
| **Device or other IDs** | Yes | **Yes** | Required | Analytics — Sentry installation ID |

Nothing is processed ephemerally; every field above is persisted.

Not collected — answer **No** to all: location, health/fitness, messages, **audio files**, files and
docs, calendar, contacts, web browsing, installed apps, search history, advertising IDs.

### Two entries that were wrong in the first draft of this document

**Audio is not a separate data type.** An earlier version declared "Audio (in videos)". Audio is only
ever muxed into the video file — `Platforms/Android/VideoCompressor.cs` copies the AAC track through
into the same MP4, and there is no standalone recording path. Play wants the type that best describes
the data, so this is **Videos** only. Declaring "Voice or sound recordings" separately would be
over-declaring, which is as much a mismatch as under-declaring.

**Device or other IDs are collected.** An earlier version listed these as not collected. Sentry .NET
generates a persistent installation identifier and sends it as the default user id; Play's definition
names Firebase installation ID as an example of exactly this. It must be declared, and marked Shared
because Sentry receives it.

Confirmed safe by inspection: `SendDefaultPii` is never set (defaults to false) and nothing calls
`SetUser`, so Sentry receives no email, username, or IP address. That is why Personal info stays out
of the Shared column.

### Notes on two entries that are easy to get wrong

**Crash logs and diagnostics are marked "Shared"** because Sentry is a third-party processor
receiving the data. Play distinguishes "collected" (leaves the device to your servers) from
"shared" (goes to a third party). Supabase and Mailgun are also third parties, but they act as
your infrastructure processors for data users knowingly submit, so those stay "collected, not
shared". Sentry receives data the user never chose to send, which is why it is called out.

**This only became true in build 9.** Sentry previously had no DSN configured, so nothing was ever
transmitted. Build 9 enables it, which means the Data Safety form must now declare it. Declaring
data collection you don't do is as much a violation as the reverse, so this line and the shipped
build have to stay in sync.

### Deletion and retention
- Account deletion removes the profile row, all memory rows, and all storage objects
  (cascading foreign keys plus the `delete-account` function).
- Users can export their momentos by email (`send-export-email`) before deleting.

---

## 3. Store listing

Copy is ready in `docs/google-play-listing.md` — app name, short description, full description,
category (Lifestyle) and tags. Still needed:

- **Feature graphic** 1024×500 (required; Play will not let you publish without it)
- **App icon** 512×512
- **Phone screenshots** — at least 2, up to 8. `iPhone_16_Pro_Max_Screenshots/` exists in the repo
  root but those are iOS aspect ratios; capture Android ones from the S21.
- **Contact email** and app category confirmation

---

## 4. Order of operations

1. Complete sections 1–3 above. This is what moves the app out of **Draft**; nothing else can
   proceed first.
2. Create a **closed testing** track and upload the build 9 AAB
   (`bin/Release/net10.0-android/com.momentarymementos.app-Signed.aab`, versionCode 9).
   Upload through the Console UI — `scripts/play_publish.py` needs
   `scripts/play-service-account.json`, which does not exist yet.
3. Recruit to **12 testers** and hold them for **14 continuous days**.
4. Apply for production access.

---

## 5. Known gaps to resolve before production

- **No in-app Terms of Use link.** The privacy policy *is* linked, from the About card on the Profile
  page (`Views/ProfilePage.xaml:171` → `ProfileViewModel.OpenPrivacyPolicy`, pointing at the correct
  `jsnewtonian.github.io/momo-privacy/` URL). Terms are not linked anywhere. Apple Review Guideline
  3.1.2 requires **both** inside the binary for auto-renewable subscriptions, so this is a plausible
  iOS rejection reason. `docs/terms_of_service.html` exists but has never been published.
- **Patrick's copy of the privacy policy is still published** at
  `https://tex413.github.io/MoMo2/privacy_policy.html`, serving the stale May 6 version with a dead
  contact address. Ask him to take it down; a second policy for this app that nobody can update is a
  liability whichever URL the listing points at.
- **`scripts/play-service-account.json` is missing**, so the publish script cannot run. Fine for
  manual uploads; needed if you ever want automated releases.
- **Subscription products** must exist and be active in the Console for `Plugin.InAppBilling` to
  return anything. Untested against a real closed-track build.
- **Mailgun is on a sandbox domain**
  (`sandbox8886bcad2a524eefb0d493918607eced.mailgun.org`), which only delivers to pre-authorised
  recipients. The email-export feature will silently fail for real testers until a verified sending
  domain is configured.
