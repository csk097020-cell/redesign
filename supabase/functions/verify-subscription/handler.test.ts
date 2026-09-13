import assert from "node:assert/strict";
import test from "node:test";
import {
  createVerifySubscriptionHandler,
  readEntitlement,
  type ProfileRow,
  type VerifySubscriptionClient,
} from "./handler.ts";

const NOW = Date.parse("2026-08-30T12:00:00Z");
const HOUR = 60 * 60 * 1000;

type AppleReply = { status: number; latest_receipt_info?: unknown[] };

type MockOptions = {
  validUser?: boolean;
  profile?: ProfileRow | null;
  appleReplies?: AppleReply[];
  appleHttpStatus?: number;
  updateError?: string;
  missingSecret?: boolean;
};

function setup(options: MockOptions = {}) {
  const updates: Record<string, unknown>[] = [];
  const appleCalls: string[] = [];
  const replies = [...(options.appleReplies ?? [{ status: 0 }])];

  const client: VerifySubscriptionClient = {
    auth: {
      async getUser() {
        return options.validUser === false
          ? { data: { user: null }, error: { message: "invalid token" } }
          : { data: { user: { id: "user-123" } }, error: null };
      },
    },
    from() {
      return {
        select() {
          return {
            eq() {
              return {
                async maybeSingle() {
                  return {
                    data: options.profile === undefined
                      ? { premium_source: "apple", is_premium: false }
                      : options.profile,
                    error: null,
                  };
                },
              };
            },
          };
        },
        update(values: Record<string, unknown>) {
          return {
            async eq() {
              updates.push(values);
              return {
                error: options.updateError
                  ? { message: options.updateError }
                  : null,
              };
            },
          };
        },
      };
    },
  };

  const env = new Map([
    ["SUPABASE_URL", "https://example.supabase.co"],
    ["SUPABASE_SERVICE_ROLE_KEY", "service-role-key"],
    ["APPLE_SHARED_SECRET", "shared-secret"],
  ]);
  if (options.missingSecret) env.delete("APPLE_SHARED_SECRET");

  const doFetch = (async (url: string | URL) => {
    appleCalls.push(String(url));
    if (options.appleHttpStatus && options.appleHttpStatus >= 400) {
      return new Response("upstream down", {
        status: options.appleHttpStatus,
      });
    }
    const reply = replies.shift() ?? { status: 0 };
    return new Response(JSON.stringify(reply), { status: 200 });
  }) as unknown as typeof globalThis.fetch;

  const handler = createVerifySubscriptionHandler({
    env: (name) => env.get(name),
    createAdminClient: () => client,
    fetch: doFetch,
    now: () => NOW,
  });

  return { handler, updates, appleCalls };
}

function post(body: unknown = { platform: "apple", receipt: "cmVjZWlwdA==" }) {
  return new Request("http://local", {
    method: "POST",
    headers: { Authorization: "Bearer valid" },
    body: JSON.stringify(body),
  });
}

function activeReceipt() {
  return {
    status: 0,
    latest_receipt_info: [
      {
        expires_date_ms: String(NOW + 24 * HOUR),
        original_transaction_id: "1000000123",
      },
    ],
  };
}

test("rejects non-POST requests", async () => {
  const { handler } = setup();
  const res = await handler(new Request("http://local", { method: "GET" }));
  assert.equal(res.status, 405);
});

test("rejects requests without a bearer token", async () => {
  const { handler } = setup();
  const res = await handler(new Request("http://local", { method: "POST" }));
  assert.equal(res.status, 401);
});

test("rejects an invalid session", async () => {
  const { handler } = setup({ validUser: false });
  const res = await handler(post());
  assert.equal(res.status, 401);
});

test("returns 500 when the shared secret is not configured", async () => {
  const { handler } = setup({ missingSecret: true });
  const res = await handler(post());
  assert.equal(res.status, 500);
});

test("rejects a non-Apple platform", async () => {
  const { handler } = setup();
  const res = await handler(post({ platform: "google", receipt: "token" }));
  assert.equal(res.status, 400);
});

test("grants premium for an active subscription", async () => {
  const { handler, updates } = setup({ appleReplies: [activeReceipt()] });
  const res = await handler(post());

  assert.equal(res.status, 200);
  assert.deepEqual(await res.json(), {
    isPremium: true,
    expiresAt: new Date(NOW + 24 * HOUR).toISOString(),
    source: "apple",
  });
  assert.equal(updates.length, 1);
  assert.equal(updates[0].is_premium, true);
  assert.equal(updates[0].premium_original_transaction_id, "1000000123");
});

test("revokes premium once the subscription has expired", async () => {
  const { handler, updates } = setup({
    appleReplies: [{
      status: 0,
      latest_receipt_info: [{ expires_date_ms: String(NOW - HOUR) }],
    }],
  });
  const res = await handler(post());

  assert.equal(res.status, 200);
  assert.equal((await res.json()).isPremium, false);
  assert.equal(updates[0].is_premium, false);
});

test("revokes premium for a refunded or revoked purchase", async () => {
  // Still in its paid window, but Apple stamped a cancellation date on it.
  const { handler, updates } = setup({
    appleReplies: [{
      status: 0,
      latest_receipt_info: [{
        expires_date_ms: String(NOW + 24 * HOUR),
        cancellation_date_ms: String(NOW - HOUR),
      }],
    }],
  });
  const res = await handler(post());

  assert.equal(res.status, 200);
  assert.equal((await res.json()).isPremium, false);
  assert.equal(updates[0].is_premium, false);
});

test("retries against the sandbox endpoint on status 21007", async () => {
  const { handler, appleCalls } = setup({
    appleReplies: [{ status: 21007 }, activeReceipt()],
  });
  const res = await handler(post());

  assert.equal(res.status, 200);
  assert.equal((await res.json()).isPremium, true);
  assert.match(appleCalls[0], /buy\.itunes\.apple\.com/);
  assert.match(appleCalls[1], /sandbox\.itunes\.apple\.com/);
});

test("never writes when the App Store cannot be reached", async () => {
  const { handler, updates } = setup({ appleHttpStatus: 503 });
  const res = await handler(post());

  assert.equal(res.status, 502);
  assert.deepEqual(updates, []);
});

test("never writes when Apple rejects the receipt", async () => {
  const { handler, updates } = setup({ appleReplies: [{ status: 21002 }] });
  const res = await handler(post());

  assert.equal(res.status, 502);
  assert.deepEqual(updates, []);
});

test("keeps a comped account premium without calling Apple", async () => {
  const { handler, updates, appleCalls } = setup({
    profile: { premium_source: "comp", is_premium: true },
  });
  const res = await handler(post());

  assert.equal(res.status, 200);
  assert.deepEqual(await res.json(), {
    isPremium: true,
    expiresAt: null,
    source: "comp",
  });
  assert.deepEqual(appleCalls, []);
  assert.deepEqual(updates, []);
});

test("readEntitlement ignores cancelled entries when a live one remains", () => {
  const entitlement = readEntitlement({
    latest_receipt_info: [
      {
        expires_date_ms: String(NOW + 48 * HOUR),
        cancellation_date_ms: String(NOW - HOUR),
      },
      {
        expires_date_ms: String(NOW + 24 * HOUR),
        original_transaction_id: "1000000456",
      },
    ],
  }, NOW);

  assert.equal(entitlement.isPremium, true);
  assert.equal(entitlement.originalTransactionId, "1000000456");
});

test("readEntitlement treats an empty receipt as not premium", () => {
  const entitlement = readEntitlement({}, NOW);
  assert.deepEqual(entitlement, {
    isPremium: false,
    expiresAt: null,
    originalTransactionId: null,
  });
});
