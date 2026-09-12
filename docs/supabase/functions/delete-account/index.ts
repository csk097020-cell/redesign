import { createClient, SupabaseClient } from "https://esm.sh/@supabase/supabase-js@2";

const jsonHeaders = { "Content-Type": "application/json" };

Deno.serve(async (request) => {
  if (request.method !== "POST") {
    return response(405, { error: "Method not allowed" });
  }

  const supabaseUrl = Deno.env.get("SUPABASE_URL");
  const serviceRoleKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
  const authorization = request.headers.get("Authorization");

  if (!supabaseUrl || !serviceRoleKey) {
    return response(500, { error: "Function is not configured" });
  }
  if (!authorization?.startsWith("Bearer ")) {
    return response(401, { error: "Missing authorization token" });
  }

  const accessToken = authorization.slice("Bearer ".length);
  const admin = createClient(supabaseUrl, serviceRoleKey, {
    auth: { autoRefreshToken: false, persistSession: false },
  });
  const { data: userData, error: userError } = await admin.auth.getUser(accessToken);
  const user = userData.user;
  if (userError || !user) {
    return response(401, { error: "Invalid or expired session" });
  }

  try {
    await removeFolder(admin, Deno.env.get("USER_VIDEOS_BUCKET") ?? "user-videos", user.id);
    await removeFolder(admin, Deno.env.get("AVATARS_BUCKET") ?? "avatars", user.id);

    // Referencing application tables use ON DELETE CASCADE.
    const { error: deleteError } = await admin.auth.admin.deleteUser(user.id);
    if (deleteError) throw deleteError;

    return response(200, { deleted: true });
  } catch (error) {
    console.error("delete-account failed", { userId: user.id, error });
    return response(500, { error: "Could not delete account and associated data" });
  }
});

async function removeFolder(
  admin: SupabaseClient,
  bucket: string,
  folder: string,
): Promise<void> {
  const paths = await listFilesRecursively(admin, bucket, folder);
  for (let offset = 0; offset < paths.length; offset += 1000) {
    const { error } = await admin.storage
      .from(bucket)
      .remove(paths.slice(offset, offset + 1000));
    if (error) throw error;
  }
}

async function listFilesRecursively(
  admin: SupabaseClient,
  bucket: string,
  folder: string,
): Promise<string[]> {
  const files: string[] = [];
  let offset = 0;

  while (true) {
    const { data, error } = await admin.storage.from(bucket).list(folder, {
      limit: 1000,
      offset,
      sortBy: { column: "name", order: "asc" },
    });
    if (error) {
      if (error.message.toLowerCase().includes("bucket not found")) return files;
      throw error;
    }
    if (!data?.length) break;

    for (const entry of data) {
      const path = `${folder}/${entry.name}`;
      if (entry.id) files.push(path);
      else files.push(...await listFilesRecursively(admin, bucket, path));
    }

    if (data.length < 1000) break;
    offset += data.length;
  }

  return files;
}

function response(status: number, body: Record<string, unknown>): Response {
  return new Response(JSON.stringify(body), { status, headers: jsonHeaders });
}
