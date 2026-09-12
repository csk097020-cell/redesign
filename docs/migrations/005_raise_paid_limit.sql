-- Migration 005: raise the paid-tier memory cap from 500 to 1,000.
--
-- The paywall, the upgrade toasts and the premium status card have always advertised
-- "1,000 momentos", while migration 002 set paid_video_limit = 500. Advertising a benefit the app
-- does not deliver is an App Review 2.3.1 risk, so 1,000 is the intended figure everywhere.
--
-- APPLIED 2026-08-17 against the live project. Checked first: the config row had already been
-- moved to 1000 by hand at some point (probably via the in-app admin page), so the update was a
-- no-op — but the *column default* was still 500, which would have handed 500 to any freshly
-- inserted config row. The alter is the part that actually did something.
--
-- Run in Supabase SQL editor: https://supabase.com/dashboard/project/wnumxvfvabqpgwenweff/sql

alter table momo_subscription_config
    alter column paid_video_limit set default 1000;

update momo_subscription_config
set paid_video_limit = 1000
where id = 1;
