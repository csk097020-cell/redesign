-- Migration 002: Subscription pricing model update
-- Adds paid_video_limit column and updates defaults to match new pricing:
--   Free tier:  0–100 memories (free)
--   Paid tier:  up to 500 memories at $4.99/month
--
-- Run in Supabase SQL editor: https://supabase.com/dashboard/project/wnumxvfvabqpgwenweff/sql

-- Add paid_video_limit column if it doesn't exist
alter table momo_subscription_config
    add column if not exists paid_video_limit integer not null default 500;

-- Update the live config row to the new pricing
update momo_subscription_config
set
    free_video_limit = 100,
    paid_video_limit = 500,
    monthly_price    = 4.99,
    annual_price     = 49.99
where id = 1;

-- If the row doesn't exist yet, insert it
insert into momo_subscription_config (id, free_video_limit, paid_video_limit, monthly_price, annual_price)
values (1, 100, 500, 4.99, 49.99)
on conflict (id) do nothing;
