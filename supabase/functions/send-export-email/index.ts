import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const EXPIRY_SECONDS = 7 * 24 * 60 * 60; // 7 days

Deno.serve(async (req) => {
  if (req.method !== "POST") {
    return new Response("Method not allowed", { status: 405 });
  }

  const authHeader = req.headers.get("Authorization");
  if (!authHeader) return new Response("Unauthorized", { status: 401 });

  const supabaseUrl      = Deno.env.get("SUPABASE_URL")!;
  const serviceRoleKey   = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
  const anonKey          = Deno.env.get("SUPABASE_ANON_KEY")!;
  const mailgunKey       = Deno.env.get("MAILGUN_API_KEY");
  const mailgunDomain    = Deno.env.get("MAILGUN_DOMAIN") ?? "momentarymomentos.com";
  const fromAddress      = Deno.env.get("EMAIL_FROM") ?? "corinne.kelley@momentarymomentos.com";
  const bucket           = Deno.env.get("USER_VIDEOS_BUCKET") ?? "user-videos";

  if (!mailgunKey) {
    return Response.json({ ok: false, error: "Email service not configured." }, { status: 500 });
  }

  let userId: string;
  let email: string;
  try {
    ({ userId, email } = await req.json());
    if (!userId || !email) throw new Error("Missing userId or email");
  } catch {
    return Response.json({ ok: false, error: "Invalid request body." }, { status: 400 });
  }

  // Verify the JWT belongs to the claimed userId — prevents one user from exporting another's data
  const userClient = createClient(supabaseUrl, anonKey, {
    global: { headers: { Authorization: authHeader } },
    auth: { persistSession: false, autoRefreshToken: false },
  });
  const { data: { user }, error: authError } = await userClient.auth.getUser();
  if (authError || !user || user.id !== userId) {
    return new Response("Forbidden", { status: 403 });
  }

  const admin = createClient(supabaseUrl, serviceRoleKey, {
    auth: { persistSession: false, autoRefreshToken: false },
  });

  // Fetch all memories for this user
  const { data: memories, error: memError } = await admin
    .from("momo_memories")
    .select("id, title, video_url, created_at")
    .eq("user_id", userId)
    .order("created_at", { ascending: false });

  if (memError) {
    return Response.json({ ok: false, error: memError.message }, { status: 500 });
  }

  // Generate a signed download URL for each video
  const links: Array<{ title: string; date: string; url: string }> = [];
  const publicPrefix = `${supabaseUrl}/storage/v1/object/public/${bucket}/`;

  for (const memory of memories ?? []) {
    if (!memory.video_url) continue;

    // Strip the public URL prefix to get the storage object path
    const storagePath = memory.video_url.startsWith(publicPrefix)
      ? memory.video_url.slice(publicPrefix.length)
      : memory.video_url;

    const { data: signed } = await admin.storage
      .from(bucket)
      .createSignedUrl(decodeURIComponent(storagePath), EXPIRY_SECONDS);

    if (signed?.signedUrl) {
      links.push({
        title: memory.title || "Untitled Memory",
        date:  new Date(memory.created_at).toLocaleDateString("en-US", {
          year: "numeric", month: "long", day: "numeric",
        }),
        url: signed.signedUrl,
      });
    }
  }

  // Build the email body
  const count = links.length;
  const linkRows = count > 0
    ? links.map((l, i) =>
        `<tr>
          <td style="padding:10px 12px;border-bottom:1px solid #e2e8f0;color:#475569;font-size:13px">${i + 1}</td>
          <td style="padding:10px 12px;border-bottom:1px solid #e2e8f0">
            <strong style="color:#0f172a">${l.title}</strong><br>
            <span style="font-size:12px;color:#94a3b8">${l.date}</span>
          </td>
          <td style="padding:10px 12px;border-bottom:1px solid #e2e8f0">
            <a href="${l.url}" style="color:#2563eb;font-weight:600;text-decoration:none">Download ↓</a>
          </td>
        </tr>`
      ).join("\n")
    : `<tr><td colspan="3" style="padding:20px;text-align:center;color:#94a3b8">No videos found on your account.</td></tr>`;

  const html = `<!DOCTYPE html>
<html>
<head><meta charset="utf-8"></head>
<body style="margin:0;padding:0;background:#f8fafc;font-family:'Segoe UI',Arial,sans-serif">
  <div style="max-width:600px;margin:32px auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08)">
    <div style="background:linear-gradient(135deg,#1e3a8a,#0d1635);padding:32px 40px;text-align:center">
      <h1 style="margin:0;color:#fff;font-size:22px;letter-spacing:-0.3px">Your Momentary Momentos</h1>
      <p style="margin:8px 0 0;color:#93c5fd;font-size:14px">Your memories, ready to download</p>
    </div>
    <div style="padding:32px 40px">
      <p style="color:#475569;margin:0 0 20px">
        Hi there — here ${count === 1 ? "is your 1 memory" : `are all ${count} of your memories`}.
        Each link is valid for <strong>7 days</strong> from when this email was sent.
      </p>
      <table style="width:100%;border-collapse:collapse;border:1px solid #e2e8f0;border-radius:8px;overflow:hidden">
        <thead>
          <tr style="background:#f1f5f9">
            <th style="padding:10px 12px;text-align:left;font-size:11px;text-transform:uppercase;color:#64748b;letter-spacing:.5px">#</th>
            <th style="padding:10px 12px;text-align:left;font-size:11px;text-transform:uppercase;color:#64748b;letter-spacing:.5px">Memory</th>
            <th style="padding:10px 12px;text-align:left;font-size:11px;text-transform:uppercase;color:#64748b;letter-spacing:.5px">Link</th>
          </tr>
        </thead>
        <tbody>${linkRows}</tbody>
      </table>
      <div style="margin-top:24px;padding:16px;background:#eff6ff;border-radius:8px;border-left:4px solid #2563eb">
        <p style="margin:0;font-size:13px;color:#1e40af">
          <strong>⏱ Links expire in 7 days.</strong> Download your videos before they expire —
          we cannot regenerate them after your account is deleted.
        </p>
      </div>
    </div>
    <div style="padding:20px 40px 32px;border-top:1px solid #f1f5f9;text-align:center">
      <p style="margin:0;font-size:12px;color:#94a3b8">
        Momentary Momentos · Your memories, your moments<br>
        Questions? Reply to this email.
      </p>
    </div>
  </div>
</body>
</html>`;

  const subject = count === 0
    ? "Your Momentary Momentos — No Videos Found"
    : `Your ${count} Momentary ${count === 1 ? "Memory" : "Memories"} — Download Links`;

  // Mailgun uses Basic auth (api:KEY) and form-encoded body
  const form = new FormData();
  form.append("from", fromAddress);
  form.append("to", email);
  form.append("subject", subject);
  form.append("html", html);

  const credentials = btoa(`api:${mailgunKey}`);
  const emailResp = await fetch(`https://api.mailgun.net/v3/${mailgunDomain}/messages`, {
    method: "POST",
    headers: { "Authorization": `Basic ${credentials}` },
    body: form,
  });

  if (!emailResp.ok) {
    const errText = await emailResp.text();
    return Response.json({ ok: false, error: `Email send failed: ${errText}` }, { status: 500 });
  }

  return Response.json({ ok: true, count });
});
