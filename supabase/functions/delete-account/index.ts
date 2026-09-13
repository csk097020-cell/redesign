import { createClient } from "https://esm.sh/@supabase/supabase-js@2";
import {
  createDeleteAccountHandler,
  type DeleteAccountClient,
} from "./handler.ts";

const handler = createDeleteAccountHandler({
  env: (name) => Deno.env.get(name),
  createAdminClient: (url, serviceRoleKey) =>
    createClient(url, serviceRoleKey, {
      auth: { autoRefreshToken: false, persistSession: false },
    }) as unknown as DeleteAccountClient,
  logError: (message, details) => console.error(message, details),
});

Deno.serve(handler);
