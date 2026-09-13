-- Add is_active column to tags table
ALTER TABLE tags ADD COLUMN IF NOT EXISTS is_active BOOLEAN DEFAULT true;

-- Set all existing tags as active by default
UPDATE tags SET is_active = true WHERE is_active IS NULL;
