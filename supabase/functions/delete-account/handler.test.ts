import assert from "node:assert/strict";
import test from "node:test";
import {
  createDeleteAccountHandler,
  type DeleteAccountClient,
  type StorageEntry,
} from "./handler.ts";

type MockOptions = {
  validUser?: boolean;
  authDeleteError?: string;
  listError?: string;
};

function setup(options: MockOptions = {}) {
  const removed: Record<string, string[][]> = {};
  const deletedUsers: string[] = [];
  const listings: Record<string, Record<string, StorageEntry[]>> = {
    "user-videos": {
      "user-123": [
        { id: "video-id", name: "video.mp4" },
        { id: null, name: "nested" },
      ],
      "user-123/nested": [{ id: "thumb-id", name: "thumb.jpg" }],
    },
    avatars: {
      "user-123": [{ id: "avatar-id", name: "avatar.png" }],
    },
  };

  const client: DeleteAccountClient = {
    auth: {
      async getUser() {
        return options.validUser === false
          ? { data: { user: null }, error: { message: "invalid token" } }
          : { data: { user: { id: "user-123" } }, error: null };
      },
      admin: {
        async deleteUser(userId) {
          deletedUsers.push(userId);
          return {
            error: options.authDeleteError
              ? { message: options.authDeleteError }
              : null,
          };
        },
      },
    },
    storage: {
      from(bucket) {
        return {
          async list(folder, listOptions) {
            if (options.listError) {
              return { data: null, error: { message: options.listError } };
            }
            const all = listings[bucket]?.[folder] ?? [];
            return {
              data: all.slice(
                listOptions.offset,
                listOptions.offset + listOptions.limit,
              ),
              error: null,
            };
          },
          async remove(paths) {
            (removed[bucket] ??= []).push(paths);
            return { error: null };
          },
        };
      },
    },
  };

  const env = new Map([
    ["SUPABASE_URL", "https://example.supabase.co"],
    ["SUPABASE_SERVICE_ROLE_KEY", "service-role-key"],
  ]);
  const handler = createDeleteAccountHandler({
    env: (name) => env.get(name),
    createAdminClient: () => client,
  });

  return { handler, env, removed, deletedUsers };
}

test("rejects non-POST requests", async () => {
  const { handler } = setup();
  const response = await handler(new Request("http://local", { method: "GET" }));
  assert.equal(response.status, 405);
});

test("rejects requests without a bearer token", async () => {
  const { handler } = setup();
  const response = await handler(new Request("http://local", { method: "POST" }));
  assert.equal(response.status, 401);
});

test("rejects an invalid session", async () => {
  const { handler } = setup({ validUser: false });
  const response = await handler(new Request("http://local", {
    method: "POST",
    headers: { Authorization: "Bearer invalid" },
  }));
  assert.equal(response.status, 401);
});

test("recursively removes files before deleting the Auth user", async () => {
  const { handler, removed, deletedUsers } = setup();
  const response = await handler(new Request("http://local", {
    method: "POST",
    headers: { Authorization: "Bearer valid" },
  }));

  assert.equal(response.status, 200);
  assert.deepEqual(await response.json(), { deleted: true });
  assert.deepEqual(removed["user-videos"], [[
    "user-123/video.mp4",
    "user-123/nested/thumb.jpg",
  ]]);
  assert.deepEqual(removed.avatars, [["user-123/avatar.png"]]);
  assert.deepEqual(deletedUsers, ["user-123"]);
});

test("tolerates a missing optional Storage bucket", async () => {
  const { handler, deletedUsers } = setup({ listError: "Bucket not found" });
  const response = await handler(new Request("http://local", {
    method: "POST",
    headers: { Authorization: "Bearer valid" },
  }));
  assert.equal(response.status, 200);
  assert.deepEqual(deletedUsers, ["user-123"]);
});

test("returns 500 when Auth deletion fails", async () => {
  const { handler } = setup({ authDeleteError: "delete failed" });
  const response = await handler(new Request("http://local", {
    method: "POST",
    headers: { Authorization: "Bearer valid" },
  }));
  assert.equal(response.status, 500);
  assert.deepEqual(await response.json(), {
    error: "Could not delete account and associated data",
  });
});
