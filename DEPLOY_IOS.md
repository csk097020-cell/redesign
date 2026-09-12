# iOS App Store Deployment Runbook
_Momentary Momentos — com.momentarymementos.app — Apple App ID 6780306487_

The app is **live**: version 1.0, build 16, "Ready for Distribution" since Aug 2026.
This is the loop for shipping an update to it. Android has its own file,
`DEPLOY_ANDROID.md`.

---

## 0. Pick the cheapest tier that solves the problem

Not every change needs Apple. Only reach for a new build when the first two can't do it.

| Tier | Mechanism | Reaches users | Review? |
|---|---|---|---|
| **Config** | `momo_subscription_config` row — prices, `free_video_limit`, `paid_video_limit` | Next app launch | No |
| **Server** | Supabase migration, `supabase functions deploy` | Immediately | No |
| **App** | New build → App Store Connect → review | 1–3 days + phased rollout | **Yes** |

A change that spans server and app should ship the server half **first** — an Edge Function
is inert until a client calls it, so deploying early is safe and de-risks the build.

---

## 1. Facts that are already decided — don't re-litigate them

| Setting | Value | Where |
|---|---|---|
| Bundle ID | `com.momentarymementos.app` | `MomentaryMomentos.csproj:21` |
| Signing identity | `Apple Distribution: Corinne Kelley (27X6A8Y78D)` | `csproj:56` |
| Provisioning profile | `MoMo App Store Distribution`, manual | `csproj:57-58` |
| Min iOS | 15.0 | `csproj:28` |
| Team | Corinne Kelley, `8355941707` | App Store Connect |

> **The "mementos" spelling in the bundle ID is deliberate.** It matches the App Store
> record and cannot be changed on a shipped app. The *domain* is `momentarymomentos.com`.
> Commit `9136dd0` reverted a well-meaning "fix" to this. Leave it alone.

> **iOS 15.0**, not 14, clears App Store Connect warning 90068. It still satisfies the
> contract's "iOS 14+" requirement — iOS 15 runs on every device that ran iOS 14
> (iPhone 6s and later), so no supported device is dropped.

---

## 2. Bump the version — both fields

In `MomentaryMomentos.csproj`:

- [ ] **`ApplicationVersion`** — the build number. Must be unique for every upload, ever.
- [ ] **`ApplicationDisplayVersion`** — the marketing version. **Bump this for any public
      release.** App Store Connect will not accept a new *public release* under a version
      string that has already been released. 1.0 is live, so the next update is 1.0.1.
      (TestFlight-only builds may reuse a version string; a store update may not.)

Commit as `Build <N>: <what it fixes>` — matches `d2d69b9`, `23f234b`, `ed2cba8`.

> **Version-bump commits are not a reliable record of what shipped.** Build 16's bump commit
> `d2d69b9` (1:34 PM) is followed by two feature commits at 1:38 and 1:47 PM, and the binary
> was uploaded at 2:23 PM — so build 16 contains all three. When writing release notes, trust
> the upload timestamp in App Store Connect over the commit order, or check the live app.

---

## 3. Build on the paired Mac

**This is the established workflow — use it, don't reinvent it:** Visual Studio on the ThinkPad
with **Pair to Mac** connected, then **Archive in Xcode on the Mac** and distribute from the
Xcode Organizer. Visual Studio drives the build; the Mac does the actual compiling.

iOS targets are excluded from Windows builds by default, to halve build time for Android-only
work (`csproj:5-8`). The iOS TFM only appears when the machine environment variable
`PairToMac=true` is set:

```xml
<TargetFrameworks Condition="'$(PairToMac)' == 'true' or !$([MSBuild]::IsOSPlatform('windows'))">net10.0-android;net10.0-ios</TargetFrameworks>
```

A command-line `dotnet publish -f net10.0-ios -c Release` also works over Pair to Mac, but the
Xcode Archive route above is what has actually shipped every build to date.

> ### The AOT cache lives on the Mac. This cost a full TestFlight cycle once.
>
> With Pair to Mac, the iOS compile, **AOT, and linking all happen on the Mac**, in a mirrored
> build tree at:
>
> ```
> ~/Library/Caches/Xamarin/mtbs/builds/MomentaryMomentos/<guid>/
> ```
>
> The Windows-side `obj/iPhone`, `bin/iPhone`, `obj/Release/net10.0-ios` and
> `bin/Release/net10.0-ios` folders are only a mirror that gets synced *to* that directory.
> **Cleaning them on Windows does not clear where AOT actually ran.** Build 9 crashed on launch
> from stale AOT modules for exactly this reason, and a Windows-only "clean" leaves that state
> intact.
>
> For a genuinely cold archive, delete the Windows folders **and** run on the Mac:
>
> ```bash
> rm -rf ~/Library/Caches/Xamarin/mtbs/builds/MomentaryMomentos
> ```
>
> Disconnecting and reconnecting Pair to Mac does **not** reliably purge it. `bin/Release/net10.0-ios`
> is easy to miss — it holds the built `.app` and is a sibling of the `obj` path, not under it.
> Expect a long first build afterward; it re-restores the iOS workload packs.

> **iOS-only code does not compile on Windows Android builds.** Anything under
> `Platforms/iOS/` — StoreKit especially — is unverified until it builds on the Mac. Build 15
> needed a follow-up commit (`831f672`) for a StoreKit API name that Windows never checked.
> Budget for one compile-fix round trip.

- [ ] Smoke-test the Release build on a real device before uploading. Release differs from
      Debug in trimming and AOT, and this app is reflection-heavy (MAUI + JSON).

---

## 4. Upload and submit

- [ ] Upload via Xcode Organizer or Transporter.
- [ ] Wait for processing, then assign the build to the version in App Store Connect.
- [ ] **Export compliance** — answer the encryption question.
- [ ] **"What's New"** — describe user-visible change only. Check what actually shipped in the
      previous build first (see the note in §2).
- [ ] Screenshots — only if the UI changed.
- [ ] **Phased release: on.** With paying subscribers, a bad build should reach 1% before 100%.
- [ ] Submit for review.

### EU trader status — not a blocker while the EU is out of scope

App Store Connect shows a standing banner saying trader status must be provided "to submit
new apps or app updates for distribution in the European Union." Read the last five words:
the requirement is scoped to **EU distribution**, and the EU release is deliberately not being
pursued right now.

So it does **not** gate build 17. The consequence of leaving it unanswered is that Apple drops
the app from EU storefronts; every other territory is unaffected, and submissions go through
normally. The banner appears for every account that hasn't answered, whether or not the app
actually ships to the EU, so it will keep showing — that is not a sign something is wrong.

When the EU does come into scope: it needs an Admin or Account Holder (Corinne), under
**Business → Compliance Requirements**, and it is paperwork with a lead time, so start it a
good while before the release that depends on it.

---

## 5. Subscriptions

- Product IDs must match `SubscriptionService.GetAvailablePlansAsync()` exactly:
  **`monthly_premium`** and **`annual_premium`**. A mismatch surfaces as
  "Product not found", not as an obvious error.
- Entitlement is server-verified as of build 17. See
  `supabase/functions/verify-subscription/README.md` and migration 006.
- **Billing Grace Period is not configured** (App Store Connect → Subscriptions). Turning it
  on keeps a subscriber's access alive through a transient card failure while Apple retries,
  instead of revoking immediately. Recommended now that revocation actually works.
- **Sandbox testing has an accelerated clock** — a 1-month subscription renews every 5 minutes
  and auto-renews 6 times before expiring. The full purchase → lapse → downgrade cycle is
  observable in about 30 minutes with a Sandbox Apple ID
  (Users and Access → Sandbox → Test Accounts).

---

## 6. Rejection history — read before submitting

| Build | Rejected for | Fix |
|---|---|---|
| 12 | **3.1.2(c)** — subscription terms | Privacy Policy and Terms links must be in the **purchase flow itself**, not only on Profile. See `SubscriptionPage.xaml` and `LegalUrls.cs`. |
| 12 | **2.1(b)** — IAP did not work | The **Paid Applications Agreement was inactive**, so every product lookup returned "Product not found". Now Active (Aug 17 2026 – May 9 2027). If IAP breaks again, check the agreement before the code. |

Both are fixed. They are recorded here so the next person doesn't rediscover them.

---

## 7. Post-release

- [ ] Watch Sentry for a crash spike during the phased rollout.
- [ ] Confirm the paywall renders and a sandbox purchase completes on the live build.
- [ ] TestFlight builds expire 90 days after upload — the App Store build does not.
