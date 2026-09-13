// Validates a store receipt with Apple and writes the resulting entitlement to
// momo_profiles using the service role. The client never asserts entitlement --
// it only presents a receipt -- so is_premium can move down as well as up.

export type ProfileRow = {
  premium_source: string | null;
  is_premium: boolean | null;
};

export type VerifySubscriptionClient = {
  auth: {
    getUser(token: string): Promise<{
      data: { user: { id: string } | null };
      error: { message: string } | null;
    }>;
  };
  from(table: string): {
    select(columns: string): {
      eq(column: string, value: string): {
        maybeSingle(): Promise<
          { data: ProfileRow | null; error: { message: string } | null }
        >;
      };
    };
    update(values: Record<string, unknown>): {
      eq(
        column: string,
        value: string,
      ): Promise<{ error: { message: string } | null }>;
    };
  };
};

type Dependencies = {
  env(name: string): string | undefined;
  createAdminClient(
    url: string,
    serviceRoleKey: string,
  ): VerifySubscriptionClient;
  fetch?: typeof globalThis.fetch;
  now?(): number;
  logError?(message: string, details: unknown): void;
};

// Apple's documented "this receipt is from the sandbox" signal. TestFlight and
// sandbox testers produce sandbox receipts, so without this retry every one of
// them verifies as "not subscribed".
const APPLE_SANDBOX_STATUS = 21007;
// The mirror case: a production receipt sent to the sandbox endpoint.
const APPLE_PRODUCTION_STATUS = 21008;

const PRODUCTION_URL = "https://buy.itunes.apple.com/verifyReceipt";
const SANDBOX_URL = "https://sandbox.itunes.apple.com/verifyReceipt";

const jsonHeaders = { "Content-Type": "application/json" };

type AppleTransaction = {
  expires_date_ms?: string;
  cancellation_date_ms?: string;
  original_transaction_id?: string;
};

export type Entitlement = {
  isPremium: boolean;
  expiresAt: string | null;
  originalTransactionId: string | null;
};

export function createVerifySubscriptionHandler(deps: Dependencies) {
  const doFetch = deps.fetch ?? globalThis.fetch;
  const now = deps.now ?? (() => Date.now());

  return async (request: Request): Promise<Response> => {
    if (request.method !== "POST") {
      return response(405, { error: "Method not allowed" });
    }

    const supabaseUrl = deps.env("SUPABASE_URL");
    const serviceRoleKey = deps.env("SUPABASE_SERVICE_ROLE_KEY");
    const sharedSecret = deps.env("APPLE_SHARED_SECRET");
    const authorization = request.headers.get("Authorization");

    if (!supabaseUrl || !serviceRoleKey || !sharedSecret) {
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
    const userId = data.user.id;

    // Comped accounts (the app owner, staff) hold entitlement that Apple knows
    // nothing about. Answer before touching the store so verification can never
    // revoke a grant that was never a purchase.
    const { data: profile } = await admin
      .from("momo_profiles")
      .select("premium_source,is_premium")
      .eq("id", userId)
      .maybeSingle();

    if (profile?.premium_source === "comp") {
      return response(200, {
        isPremium: true,
        expiresAt: null,
        source: "comp",
      });
    }

    let body: { platform?: string; receipt?: string };
    try {
      body = await request.json();
    } catch {
      return response(400, { error: "Malformed request body" });
    }

    // Android still runs the pre-existing client-side path.
    if (body.platform !== "apple") {
      return response(400, { error: "Unsupported platform" });
    }
    if (!body.receipt) {
      return response(400, { error: "Missing receipt" });
    }

    let entitlement: Entitlement;
    try {
      const receiptJson = await verifyWithApple(
        doFetch,
        body.receipt,
        sharedSecret,
      );
      entitlement = readEntitlement(receiptJson, now());
    } catch (appleError) {
      // Deliberately no write. A store outage must never strip a paying
      // customer entitlement -- only an authoritative "not active" downgrades.
      deps.logError?.("verify-subscription: Apple validation failed", {
        userId,
        error: appleError,
      });
      return response(502, { error: "Could not reach the App Store" });
    }

    const { error: updateError } = await admin
      .from("momo_profiles")
      .update({
        is_premium: entitlement.isPremium,
        premium_expires_at: entitlement.expiresAt,
        premium_source: "apple",
        premium_original_transaction_id: entitlement.originalTransactionId,
        premium_checked_at: new Date(now()).toISOString(),
      })
      .eq("id", userId);

    if (updateError) {
      deps.logError?.("verify-subscription: entitlement write failed", {
        userId,
        error: updateError,
      });
      return response(500, { error: "Could not record entitlement" });
    }

    return response(200, {
      isPremium: entitlement.isPremium,
      expiresAt: entitlement.expiresAt,
      source: "apple",
    });
  };
}

async function verifyWithApple(
  doFetch: typeof globalThis.fetch,
  receipt: string,
  sharedSecret: string,
): Promise<Record<string, unknown>> {
  const post = async (url: string) => {
    const res = await doFetch(url, {
      method: "POST",
      headers: jsonHeaders,
      body: JSON.stringify({
        "receipt-data": receipt,
        password: sharedSecret,
        "exclude-old-transactions": true,
      }),
    });
    if (!res.ok) throw new Error("App Store returned HTTP " + res.status);
    return await res.json() as Record<string, unknown>;
  };

  let json = await post(PRODUCTION_URL);
  if (json.status === APPLE_SANDBOX_STATUS) {
    json = await post(SANDBOX_URL);
  } else if (json.status === APPLE_PRODUCTION_STATUS) {
    json = await post(PRODUCTION_URL);
  }

  if (json.status !== 0) {
    throw new Error("App Store validation status " + String(json.status));
  }
  return json;
}

/**
 * Picks the furthest-future expiry among transactions that have not been
 * cancelled. cancellation_date_ms is how a refund or an Apple revocation
 * surfaces in a receipt, so excluding those entries -- rather than reading only
 * the newest one -- is what makes a refund actually revoke access.
 */
export function readEntitlement(
  receiptJson: Record<string, unknown>,
  nowMs: number,
): Entitlement {
  const transactions = Array.isArray(receiptJson.latest_receipt_info)
    ? receiptJson.latest_receipt_info as AppleTransaction[]
    : [];

  let bestExpiry = 0;
  let originalTransactionId: string | null = null;

  for (const transaction of transactions) {
    if (transaction.cancellation_date_ms) continue;
    const expiry = Number(transaction.expires_date_ms ?? 0);
    if (!Number.isFinite(expiry) || expiry <= bestExpiry) continue;
    bestExpiry = expiry;
    originalTransactionId = transaction.original_transaction_id ?? null;
  }

  return {
    isPremium: bestExpiry > nowMs,
    expiresAt: bestExpiry > 0 ? new Date(bestExpiry).toISOString() : null,
    originalTransactionId,
  };
}

function response(status: number, body: Record<string, unknown>): Response {
  return new Response(JSON.stringify(body), { status, headers: jsonHeaders });
}
