-- Initialize PostgreSQL extensions required for AI and UUIDs
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE EXTENSION IF NOT EXISTS "vector";

-- Verify vector extension is loaded
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM pg_extension WHERE extname = 'vector'
    ) THEN
        RAISE NOTICE 'pgvector extension successfully initialized';
    ELSE
        RAISE EXCEPTION 'Failed to install pgvector extension';
    END IF;
END $$;
