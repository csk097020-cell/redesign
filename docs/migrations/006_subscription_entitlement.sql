-- Migration 006: Server-verified subscription entitlement
--
-- Why: is_premium was a one-way latch. The client set it true after a purchase
-- and nothing ever set it false, so an expired, cancelled, refunded, or
-- Apple-revoked subscription kept Premium forever. Worse, the client wrote the
-- column with the *user's own* JWT, so any user could grant themselves Premium
-- (and is_admin sat behind the same policy).
--
-- After this migration the verify-subscription Edge Function owns is_premium via
-- the service role, and users can only write their own name and avatar.
--
-- Run in Supabase SQL editor: https://supabase.com/dashboard/project/wnumxvfvabqpgwenweff/sql
--
-- ============================================================================
-- RUN SECTION 0 FIRST AND READ THE OUTPUT. Sections 2 and 3 change privileges
-- on a live table that real users are writing to right now. docs/supabase_schema.sql
-- is known to be out of date -- profile saves use a PostgREST upsert, which needs
-- INSERT privilege, and the checked-in schema records no INSERT policy even though
-- saving demonstrably works. Confirm reality before revoking anything.
-- ============================================================================


-- ─────────────────────────────────────────────────────────────────────────────
-- Section 0: inspect the live state (read-only -- run this first)
-- ─────────────────────────────────────────────────────────────────────────────

-- 0a. Which privileges does each role actually hold today?
--     Expect to see UPDATE (and probably INSERT) for `authenticated`.
--     Record this output somewhere before running section 2 -- it is the rollback.
--
-- select grantee, privilege_type, column_name
-- from information_schema.column_privileges
-- where table_schema = 'public' and table_name = 'momo_profiles'
-- union all
-- select grantee, privilege_type, '(table-level)'
-- from information_schema.table_privileges
-- where table_schema = 'public' and table_name = 'momo_profiles'
-- order by grantee, privilege_type, column_name;

-- 0b. Which RLS policies exist? If there is an INSERT policy here that is not in
--     docs/supabase_schema.sql, that confirms the drift -- copy it into the repo file.
--
-- select policyname, cmd, roles, qual, with_check
-- from pg_policies where schemaname = 'public' and tablename = 'momo_profiles';

-- 0c. Who is premium right now, and how many are there? This is the blast radius:
--     every one of these accounts gets re-verified on first launch of build 17,
--     and any without a valid Apple subscription will lose Premium.
--
-- select p.is_premium, p.is_admin, u.email, u.last_sign_in_at, p.created_at
-- from momo_profiles p join auth.users u on u.id = p.id
-- where p.is_premium order by p.created_at desc;


-- ─────────────────────────────────────────────────────────────────────────────
-- Section 0 RESULTS -- recorded 2026-08-31 against production (wnumxvfvabqpgwenweff).
-- This is the rollback record section 0a asks for. All four assumptions held.
--
-- 0a. `authenticated` AND `anon` each hold TABLE-level INSERT, UPDATE, SELECT and
--     REFERENCES across all 5 columns (avatar_url, full_name, id, is_admin,
--     is_premium). No grant to PUBLIC exists, so revoking from those two roles in
--     section 2 is sufficient -- nothing leaks back in through a PUBLIC grant.
--
-- 0b. RLS is enabled (relrowsecurity = true), 5 policies, all on role {public}:
--       DELETE  "Users can delete their own profile"   using (auth.uid() = id)
--       INSERT  "Users can insert their own profile"   with check (auth.uid() = id)
--       SELECT  "profiles_select"                      using (auth.uid() = id)
--       UPDATE  "Users can update their own profile"   using (auth.uid() = id)
--       UPDATE  "profiles_update"                      using (auth.uid() = id)
--     The INSERT policy CONFIRMS the drift this file warned about -- it is absent
--     from docs/supabase_schema.sql. The INSERT grant in section 2 is therefore
--     required, exactly as written. (The two UPDATE policies are duplicates of each
--     other; harmless, worth tidying separately.)
--
-- 0c. Exactly two accounts hold is_premium: csk097020@gmail.com (Corinne, 138
--     memories, active) and reviewer@thunderpeak.net (Patrick). Only Corinne is
--     comped below -- Patrick's Premium is intentionally allowed to lapse.
--
-- Note: section 2 revokes INSERT and UPDATE only, never SELECT, so the new
-- premium_* columns stay readable by GetProfileAsync.
-- ─────────────────────────────────────────────────────────────────────────────


-- ─────────────────────────────────────────────────────────────────────────────
-- Section 1: entitlement columns (safe -- additive, no behavior change)
-- ─────────────────────────────────────────────────────────────────────────────

alter table momo_profiles
    add column if not exists premium_expires_at              timestamptz,
    add column if not exists premium_source                  text,
    add column if not exists premium_original_transaction_id text,
    add column if not exists premium_checked_at              timestamptz;

comment on column momo_profiles.premium_source is
    'apple | google | comp. ''comp'' is a permanent grant with no store subscription '
    'behind it (app owner, staff); verify-subscription returns early on it and never revokes it.';
comment on column momo_profiles.premium_expires_at is
    'End of the paid period as reported by the store. Null for comp grants.';

-- Existing premium rows predate this column. Mark them as store-sourced so they
-- are re-verified rather than silently treated as comps. Section 4 then promotes
-- the intended comp accounts.
update momo_profiles
set premium_source = 'apple'
where is_premium and premium_source is null;


-- ─────────────────────────────────────────────────────────────────────────────
-- Section 2: stop users writing their own entitlement
--
-- RLS filters rows, not columns, so the column-level GRANT is what closes this.
-- The existing "Users can update their own profile" policy stays as the row filter.
--
-- The INSERT grant is required, not optional: SupabaseService.UpdateFullNameAsync
-- and UpdateAvatarUrlAsync use POST upsert (Prefer: resolution=merge-duplicates),
-- and PostgREST upsert needs INSERT privilege. Drop it and profile saving breaks.
-- ─────────────────────────────────────────────────────────────────────────────

-- ############################################################################
-- NOT YET APPLIED -- deliberately held back. Sections 1, 3 and 4 ran 2026-08-31.
--
-- DO NOT RUN THIS UNTIL BUILD 17 IS LIVE ON THE APP STORE.
--
-- The build currently on the App Store still grants Premium client-side:
-- SubscriptionService.PurchaseSubscriptionAsync calls UpdatePremiumStatusAsync,
-- which PATCHes is_premium with the user's own JWT. This section revokes exactly
-- that privilege. Run it while the old build is in the wild and a paying customer
-- gets charged by Apple, then sees the paywall toast
--     "Purchase failed: Failed to update premium status: ..."
-- and stays on Free. Restore Purchases fails identically. It stays broken for that
-- user until they install build 17.
--
-- Holding this back costs nothing for revocation: verify-subscription writes with
-- the service role, which bypasses these grants entirely. Section 1 is all the
-- Edge Function needs. This section is a separate fix -- it closes the hole where
-- a user can grant themselves Premium by hand -- and that hole predates the app's
-- launch, so a few more weeks is the cheaper trade.
--
-- APPLY WHEN: build 17 is live on the App Store. Deliberately NOT "once old-build
-- installs have drained" -- exposure here is (old-build users x purchase rate), and
-- purchase rate rises as the app grows, so a straggler months from now is a likelier
-- and costlier event than anything today. Waiting past go-live buys nothing.
--
-- The damage is also self-healing: the failed purchase's receipt stays valid, so
-- installing build 17 grants Premium on the next launch, and Restore Purchases works.
-- The real reason to wait at all is that this failure is invisible from your side --
-- nothing alerts you, you would hear about it from a support email or not at all.
--
-- Section 3 already ran on 2026-08-31, so applying this is section 2 alone. Re-run
-- the section 5 checks afterwards -- especially (a) name save and (b) avatar upload,
-- which are what break if the INSERT grant is wrong.
-- ############################################################################

-- revoke insert, update on momo_profiles from authenticated;
-- revoke insert, update on momo_profiles from anon;
--
-- grant insert (id, full_name, avatar_url) on momo_profiles to authenticated;
-- grant update (full_name, avatar_url)     on momo_profiles to authenticated;

-- Rollback for section 2, if profile saving breaks:
--   grant insert, update on momo_profiles to authenticated;


-- ─────────────────────────────────────────────────────────────────────────────
-- Section 3: keep the admin panel working
--
-- AdminViewModel.ToggleAdminCommand -> SupabaseService.SetUserAdminAsync PATCHes
-- *another* user's is_admin with the calling admin's JWT. Section 2 revokes that,
-- so the write moves behind a definer function that does its own admin check.
-- Without this, section 2 silently breaks the admin panel.
-- ─────────────────────────────────────────────────────────────────────────────

create or replace function set_user_admin(target_id uuid, make_admin boolean)
returns void
language plpgsql
security definer
set search_path = public
as $$
begin
    if not exists (
        select 1 from momo_profiles where id = auth.uid() and is_admin
    ) then
        raise exception 'not authorized' using errcode = '42501';
    end if;

    update momo_profiles set is_admin = make_admin where id = target_id;
end;
$$;

revoke all on function set_user_admin(uuid, boolean) from public, anon;
grant execute on function set_user_admin(uuid, boolean) to authenticated;


-- ─────────────────────────────────────────────────────────────────────────────
-- Section 4: comp grants
--
-- Corinne owns the app and paid to have it built. A comp grant keeps her Premium
-- permanently with no App Store subscription behind it. Do NOT have her subscribe
-- for real: Apple takes its commission and pays the remainder back to her own
-- account weeks later, so she loses the spread on her own money, and promo/offer
-- codes only ever grant a finite free period.
--
-- Without this row, build 17 would strip her Premium on first launch, because she
-- has no Apple receipt to verify.
--
-- Corinne's login confirmed 2026-08-31: csk097020@gmail.com (138 memories, by far
-- the most active account; next highest is 17). Looked up by email rather than a
-- pasted UUID so a typo fails loudly instead of comping the wrong account.
-- ─────────────────────────────────────────────────────────────────────────────

update momo_profiles
set is_premium         = true,
    premium_source     = 'comp',
    premium_expires_at = null
where id = (
    select id from auth.users where lower(email) = lower('csk097020@gmail.com')
);

-- Verify the comp landed on exactly one row, and on the right person:
--
-- select u.email, p.is_premium, p.premium_source, p.premium_expires_at
-- from momo_profiles p join auth.users u on u.id = p.id
-- where p.premium_source = 'comp';


-- ─────────────────────────────────────────────────────────────────────────────
-- Section 5: post-migration checks (run from the app, not here)
-- ─────────────────────────────────────────────────────────────────────────────
--   a. Profile name save still works           (ProfilePage -> edit name -> save)
--   b. Avatar upload still works               (ProfilePage -> tap avatar)
--   c. Admin panel toggle-admin still works    (AdminPage -> Users -> toggle)
--   d. A direct PATCH of is_premium with a user JWT is now REJECTED
--   e. Corinne's row reads premium_source = 'comp' and stays Premium
