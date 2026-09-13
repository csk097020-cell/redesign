-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 002 — Memento captured-date + caption
-- Run once in the Supabase SQL editor. Idempotent.
--
-- Adds two optional columns to momo_memories:
--   caption       — optional free-text caption entered by the user at capture.
--   date_captured — the date the user says the moment was actually filmed
--                   (a calendar day; created_at remains the auto upload timestamp).
--
-- Both are nullable with no default, so every existing memento across all users
-- stays valid and simply reads back NULL until edited (editing existing rows is
-- out of scope — these are populated at capture time only).
-- ─────────────────────────────────────────────────────────────────────────────

ALTER TABLE momo_memories
    ADD COLUMN IF NOT EXISTS caption       text;

ALTER TABLE momo_memories
    ADD COLUMN IF NOT EXISTS date_captured date;
