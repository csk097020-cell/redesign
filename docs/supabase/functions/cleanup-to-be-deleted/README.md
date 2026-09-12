# cleanup-to-be-deleted

Scheduled Supabase Edge Function for permanently removing Storage objects that the app moved into a `to_be_deleted` folder.

The app moves deleted memory files to:

```text
{userId}/to_be_deleted/{timestamp}_{originalFileName}
```

Required secrets:

```bash
supabase secrets set SUPABASE_SERVICE_ROLE_KEY=...
supabase secrets set USER_VIDEOS_BUCKET=user-videos
supabase secrets set DELETE_RETENTION_DAYS=7
```

Deploy:

```bash
supabase functions deploy cleanup-to-be-deleted
```

Schedule it from the Supabase dashboard or CLI to run daily. The function queries Storage metadata for objects whose path contains `/to_be_deleted/` and whose `created_at` is older than `DELETE_RETENTION_DAYS`, then deletes them through the Storage API.

Do not delete rows directly from `storage.objects`; use the Storage API so files are removed from the bucket, not just orphaned in metadata.
