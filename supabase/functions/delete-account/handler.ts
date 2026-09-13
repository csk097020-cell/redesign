export type StorageEntry = { id?: string | null; name: string };

export type DeleteAccountClient = {
  auth: {
    getUser(token: string): Promise<{
      data: { user: { id: string } | null };
      error: { message: string } | null;
    }>;
    admin: {
      deleteUser(userId: string): Promise<{ error: { message: string } | null }>;
    };
  };
  storage: {
    from(bucket: string): {
      list(folder: string, options: {
        limit: number;
        offset: number;
        sortBy: { column: string; order: string };
      }): Promise<{ data: StorageEntry[] | null; error: { message: string } | null }>;
      remove(paths: string[]): Promise<{ error: { message: string } | null }>;
    };
  };
};

type Dependencies = {
  env(name: string): string | undefined;
  createAdminClient(url: string, serviceRoleKey: string): DeleteAccountClient;
  logError?(message: string, details: unknown): void;
};

const jsonHeaders = { "Content-Type": "application/json" };

export function createDeleteAccountHandler(deps: Dependencies) {
  return async (request: Request): Promise<Response> => {
    if (request.method !== "POST") {
      return response(405, { error: "Method not allowed" });
    }

    const supabaseUrl = deps.env("SUPABASE_URL");
    const serviceRoleKey = deps.env("SUPABASE_SERVICE_ROLE_KEY");
    const authorization = request.headers.get("Authorization");

    if (!supabaseUrl || !serviceRoleKey) {
      return response(500, { error: "Function is not configured" });
    }
    if (!authorization?.startsWith("Bearer ")) {
      return response(401, { error: "Missing authorization token" });
    }

    const admin = deps.createAdminClient(supabaseUrl, serviceRoleKey);
    const { data, error } = await admin.auth.getUser(
      authorization.slice("Bearer ".length),
    );
    if (error || !data.user) {
      return response(401, { error: "Invalid or expired session" });
    }

    try {
      const userId = data.user.id;
      await removeFolder(
        admin,
        deps.env("USER_VIDEOS_BUCKET") ?? "user-videos",
        userId,
      );
      await removeFolder(
        admin,
        deps.env("AVATARS_BUCKET") ?? "avatars",
        userId,
      );

      const { error: deleteError } = await admin.auth.admin.deleteUser(userId);
      if (deleteError) throw new Error(deleteError.message);

      return response(200, { deleted: true });
    } catch (error) {
      deps.logError?.("delete-account failed", {
        userId: data.user.id,
        error,
      });
      return response(500, {
        error: "Could not delete account and associated data",
      });
    }
  };
}

async function removeFolder(
  admin: DeleteAccountClient,
  bucket: string,
  folder: string,
): Promise<void> {
  const paths = await listFilesRecursively(admin, bucket, folder);
  for (let offset = 0; offset < paths.length; offset += 1000) {
    const { error } = await admin.storage
      .from(bucket)
      .remove(paths.slice(offset, offset + 1000));
    if (error) throw new Error(error.message);
  }
}

async function listFilesRecursively(
  admin: DeleteAccountClient,
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
      throw new Error(error.message);
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
