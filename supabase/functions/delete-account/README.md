# Delete Account Edge Function

This function authenticates the caller, recursively removes the caller's files
from the `user-videos` and `avatars` buckets, and deletes the Supabase Auth user.
Application rows referencing `auth.users` are removed by `ON DELETE CASCADE`.

## Local behavior tests

From the repository root:

```powershell
node --experimental-strip-types supabase/functions/delete-account/handler.test.ts
```

These tests use an in-memory Supabase client. They never contact production or
delete real accounts.

## Deploy

```powershell
supabase functions deploy delete-account --no-verify-jwt
```

The function validates the bearer token itself with `auth.getUser`.

After deployment, invoke it using a disposable account, then confirm that the
Auth user, database rows, videos, thumbnails, and avatar are gone and that the
deleted credentials can no longer sign in.

