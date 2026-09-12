import { createClient } from "https://esm.sh/@supabase/supabase-js@2";
import {
  createVerifySubscriptionHandler,
  type VerifySubscriptionClient,
} from "./handler.ts";

const handler = createVerifySubscriptionHandler({
  env: (name) => Deno.env.get(name),
  createAdminClient: (url, serviceRoleKey) =>
    createClient(url, serviceRoleKey, {
      auth: { autoRefreshToken: false, persistSession: false },
    }) as unknown as VerifySubscriptionClient,
  logError: (message, details) => console.error(message, details),
});

Deno.serve(handler);
