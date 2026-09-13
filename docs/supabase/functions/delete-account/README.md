# Delete Account Edge Function

Permanently removes the authenticated user's video/avatar Storage objects and
Supabase Auth identity. Database rows are removed by their `ON DELETE CASCADE`
foreign keys.

Deploy from the repository root:

```bash
supabase functions deploy delete-account --no-verify-jwt
```

The function performs its own JWT validation with `auth.getUser`. Supabase
automatically supplies `SUPABASE_URL` and `SUPABASE_SERVICE_ROLE_KEY`.

After deployment, test with a disposable account and confirm that the Auth user,
database rows, and Storage objects are gone and the credentials cannot sign in.
