# Verify Subscription Edge Function

Validates an App Store receipt with Apple and writes the resulting entitlement to
`momo_profiles` using the service role.

Before this function existed, `is_premium` was a one-way latch: the client set it
to `true` after a purchase and nothing ever set it back. An expired, cancelled,
refunded, or Apple-revoked subscription kept Premium forever, and because the
client wrote the column with the user's own JWT, any user could grant it to
themselves. Entitlement is now derived from the receipt, server-side, and can
move in both directions.

## Behavior

1. `POST` only, with the caller's Supabase JWT in `Authorization: Bearer …`. The
   user id comes from `auth.getUser`, never from the request body.
2. **Comped accounts short-circuit.** If the caller's `premium_source` is
   `comp`, the function returns `isPremium: true` without contacting Apple.
   These grants (the app owner, staff) have no receipt, so verification must
   never be able to revoke them.
3. Body is `{ "platform": "apple", "receipt": "<base64>" }`. Any other platform
   returns 400 — Android still runs the pre-existing client-side path.
4. The receipt goes to `buy.itunes.apple.com`, retried against
   `sandbox.itunes.apple.com` on status `21007`. **The retry is load-bearing:**
   TestFlight and sandbox testers produce sandbox receipts, and without it every
   one of them would verify as "not subscribed".
5. Entitlement is the furthest-future `expires_date_ms` among transactions with
   no `cancellation_date_ms`. That field is how a refund or an Apple revocation
   surfaces, so excluding those entries — rather than reading only the newest
   one — is what makes a refund actually revoke access.

### The rule that governs failures

**Downgrade only on an authoritative "not active" from Apple.** An unreachable
store, an HTTP error, or a non-zero validation status returns 502 and writes
nothing at all. A network blip must never strip a paying customer's Premium.
This is covered by two tests; do not "simplify" them away.

## Local behavior tests

From the repository root:

```powershell
node --experimental-strip-types supabase/functions/verify-subscription/handler.test.ts
```

The tests use an in-memory Supabase client and a stubbed `fetch`. They never
contact Apple or production.

## Deploy

```powershell
supabase secrets set APPLE_SHARED_SECRET=<app-specific shared secret>
supabase functions deploy verify-subscription --no-verify-jwt
```

The shared secret comes from App Store Connect → the app → App-Specific Shared
Secret. `--no-verify-jwt` matches `delete-account`: the function validates the
bearer token itself.

Requires migration `docs/migrations/006_subscription_entitlement.sql` for the
`premium_*` columns.

## Known tradeoff: `verifyReceipt` is deprecated

Apple deprecated `/verifyReceipt` in 2023. It still works, and it is by far the
simplest thing to call from Deno — the successor App Store Server API needs an
ES256-signed JWT and a transaction id rather than a receipt. Keeping validation
behind this one function means that migration, and any later move to App Store
Server Notifications V2 for real-time refund handling, is a single-file change.
