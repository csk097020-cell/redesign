import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const RETENTION_DAYS = Number(Deno.env.get("DELETE_RETENTION_DAYS") ?? "7");
const BATCH_SIZE = Number(Deno.env.get("DELETE_BATCH_SIZE") ?? "100");

Deno.serve(async () => {
  const supabaseUrl = Deno.env.get("SUPABASE_URL");
  const serviceRoleKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
  const bucket = Deno.env.get("USER_VIDEOS_BUCKET") ?? "user-videos";

  if (!supabaseUrl || !serviceRoleKey) {
    return new Response("Missing Supabase cleanup environment variables", { status: 500 });
  }

  const cutoff = new Date(Date.now() - RETENTION_DAYS * 24 * 60 * 60 * 1000).toISOString();
  const supabase = createClient(supabaseUrl, serviceRoleKey, {
    auth: { persistSession: false, autoRefreshToken: false },
  });

  const { data, error } = await supabase
    .schema("storage")
    .from("objects")
    .select("name")
    .eq("bucket_id", bucket)
    .lt("created_at", cutoff)
    .ilike("name", "%/to_be_deleted/%")
    .limit(BATCH_SIZE);

  if (error) {
    return Response.json({ ok: false, error: error.message }, { status: 500 });
  }

  const paths = (data ?? []).map((row) => row.name).filter(Boolean);
  if (paths.length === 0) {
    return Response.json({ ok: true, deleted: 0, retentionDays: RETENTION_DAYS });
  }

  const { error: removeError } = await supabase.storage.from(bucket).remove(paths);
  if (removeError) {
    return Response.json({ ok: false, error: removeError.message, attempted: paths.length }, { status: 500 });
  }

  return Response.json({ ok: true, deleted: paths.length, retentionDays: RETENTION_DAYS });
});
