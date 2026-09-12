-- ─────────────────────────────────────────────────────────────────────────────
-- Migration 001 — Thumbnail URLs + AI-weighted memory metrics
-- Run once in the Supabase SQL editor.
-- ─────────────────────────────────────────────────────────────────────────────

-- 1. Add thumbnail_url column to momo_memories
--    Stores a Supabase Storage public URL for the extracted video frame.
--    NULL until a thumbnail is generated and uploaded by the mobile app.

ALTER TABLE momo_memories
    ADD COLUMN IF NOT EXISTS thumbnail_url text;

-- ─────────────────────────────────────────────────────────────────────────────
-- 2. momo_memory_metrics — per-user AI weighting for each memory
--
--    Matches the "memory_metrics" table used by the PWA (MemoryPlayback.tsx).
--    weight: default 1; +2 on favorite, -1 on "See Less" (min 0).
--    Memories with weight=0 are excluded from Surprise-Me selections.
-- ─────────────────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS momo_memory_metrics (
    id              uuid        PRIMARY KEY DEFAULT uuid_generate_v4(),
    memory_id       uuid        NOT NULL REFERENCES momo_memories (id) ON DELETE CASCADE,
    user_id         uuid        NOT NULL REFERENCES auth.users (id)    ON DELETE CASCADE,
    weight          integer     NOT NULL DEFAULT 1,
    favorite_count  integer     NOT NULL DEFAULT 0,
    skip_count      integer     NOT NULL DEFAULT 0,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (memory_id, user_id)
);

ALTER TABLE momo_memory_metrics ENABLE ROW LEVEL SECURITY;

-- Users can only read/write their own metrics
CREATE POLICY "Users manage their own metrics"
    ON momo_memory_metrics FOR ALL
    USING  (auth.uid() = user_id)
    WITH CHECK (auth.uid() = user_id);

-- Auto-update updated_at on row change
CREATE OR REPLACE FUNCTION update_memory_metrics_timestamp()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS set_memory_metrics_updated_at ON momo_memory_metrics;
CREATE TRIGGER set_memory_metrics_updated_at
    BEFORE UPDATE ON momo_memory_metrics
    FOR EACH ROW EXECUTE PROCEDURE update_memory_metrics_timestamp();
