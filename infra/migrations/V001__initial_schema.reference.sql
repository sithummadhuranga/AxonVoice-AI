-- =============================================================================
-- REFERENCE ONLY — DO NOT EXECUTE DIRECTLY
-- =============================================================================
-- This file documents the intended database schema as plain SQL for human
-- reference. It is NOT used to initialise PostgreSQL.
--
-- Schema is managed exclusively by EF Core Code-First migrations.
-- Each service that owns tables has its own DbContext and migration history:
--
--   src/Services/AgentConfig/     → AgentConfigDbContext
--     Tables: tenants, agents, business_hours, closed_dates
--
--   src/Services/KnowledgeBase/   → KnowledgeBaseDbContext
--     Tables: knowledge_documents
--
--   src/Services/ConversationStore/ → ConversationDbContext
--     Tables: conversation_sessions, session_function_calls, webhook_deliveries
--
--   Booking tables (pending_bookings, confirmed_bookings) are owned by
--   AgentConfigDbContext because the availability query joins business_hours
--   and these tables atomically.
--
-- To apply migrations:
--   dotnet ef database update --project src/Services/AgentConfig
--   dotnet ef database update --project src/Services/KnowledgeBase
--   dotnet ef database update --project src/Services/ConversationStore
--
-- Or in production (via docker compose):
--   docker compose exec agent-config dotnet ef database update
--   docker compose exec knowledge-base dotnet ef database update
--   docker compose exec conversation-store dotnet ef database update
-- =============================================================================

-- Enable the pgcrypto extension for gen_random_uuid().
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- =============================================================================
-- TENANTS
-- =============================================================================
CREATE TABLE IF NOT EXISTS tenants (
    id                      UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    name                    VARCHAR(255) NOT NULL,
    api_key_encrypted       TEXT        NOT NULL,
    api_key_hint            VARCHAR(8),
    default_language        VARCHAR(10) NOT NULL DEFAULT 'si'
                                CHECK (default_language IN ('si', 'ta', 'en')),
    webhook_url             TEXT,
    webhook_secret          TEXT,
    rate_limit_daily        INTEGER     NOT NULL DEFAULT 1000,
    rate_limit_concurrent   INTEGER     NOT NULL DEFAULT 20,
    is_active               BOOLEAN     NOT NULL DEFAULT TRUE,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- =============================================================================
-- AGENTS
-- =============================================================================
CREATE TABLE IF NOT EXISTS agents (
    id                  UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID        NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    name                VARCHAR(255) NOT NULL,
    display_name        VARCHAR(255) NOT NULL,
    persona_prompt      TEXT        NOT NULL,
    supported_languages TEXT[]      NOT NULL DEFAULT '{si,ta,en}',
    primary_language    VARCHAR(10) NOT NULL DEFAULT 'si'
                            CHECK (primary_language IN ('si', 'ta', 'en')),
    voice_name          VARCHAR(100) NOT NULL DEFAULT 'Aoede',
    gemini_model        VARCHAR(100) NOT NULL DEFAULT 'gemini-2.0-flash-live-001',
    gemini_cache_name   VARCHAR(500),
    session_timeout_sec INTEGER     NOT NULL DEFAULT 600,
    silence_timeout_sec INTEGER     NOT NULL DEFAULT 90,
    tools_enabled       TEXT[]      NOT NULL DEFAULT '{check_availability,create_booking}',
    is_active           BOOLEAN     NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_agents_tenant_id
    ON agents (tenant_id);

-- =============================================================================
-- BUSINESS HOURS
-- =============================================================================
CREATE TABLE IF NOT EXISTS business_hours (
    id                      UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_id                UUID        NOT NULL REFERENCES agents(id) ON DELETE CASCADE,
    tenant_id               UUID        NOT NULL,
    day_of_week             SMALLINT    NOT NULL CHECK (day_of_week BETWEEN 0 AND 6),
    open_time               TIME        NOT NULL,
    close_time              TIME        NOT NULL,
    slot_duration_minutes   INTEGER     NOT NULL DEFAULT 90,
    max_capacity_per_slot   INTEGER     NOT NULL DEFAULT 30,
    is_active               BOOLEAN     NOT NULL DEFAULT TRUE,
    UNIQUE (agent_id, day_of_week)
);

-- =============================================================================
-- CLOSED DATES
-- =============================================================================
CREATE TABLE IF NOT EXISTS closed_dates (
    id          UUID    PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_id    UUID    NOT NULL REFERENCES agents(id) ON DELETE CASCADE,
    tenant_id   UUID    NOT NULL,
    closed_date DATE    NOT NULL,
    reason      VARCHAR(255),
    UNIQUE (agent_id, closed_date)
);

-- =============================================================================
-- PENDING BOOKINGS  (AI-collected holds)
-- =============================================================================
CREATE TABLE IF NOT EXISTS pending_bookings (
    id                  UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_id            UUID        NOT NULL REFERENCES agents(id),
    tenant_id           UUID        NOT NULL,
    session_id          UUID        NOT NULL,
    customer_name       VARCHAR(255) NOT NULL,
    customer_phone      VARCHAR(50)  NOT NULL,
    customer_language   VARCHAR(10)  NOT NULL,
    party_size          SMALLINT    NOT NULL CHECK (party_size > 0),
    requested_datetime  TIMESTAMPTZ NOT NULL,
    special_requests    TEXT,
    status              VARCHAR(20)  NOT NULL DEFAULT 'pending'
                            CHECK (status IN ('pending', 'approved', 'rejected', 'expired')),
    expires_at          TIMESTAMPTZ NOT NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Critical for the dual-table availability query performance.
CREATE INDEX IF NOT EXISTS idx_pending_bookings_slot
    ON pending_bookings (agent_id, requested_datetime, status, expires_at);

-- =============================================================================
-- CONFIRMED BOOKINGS  (admin-approved, authoritative)
-- =============================================================================
CREATE TABLE IF NOT EXISTS confirmed_bookings (
    id                      UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_id                UUID        NOT NULL REFERENCES agents(id),
    tenant_id               UUID        NOT NULL,
    promoted_from_pending   UUID        REFERENCES pending_bookings(id),
    customer_name           VARCHAR(255) NOT NULL,
    customer_phone          VARCHAR(50)  NOT NULL,
    customer_language       VARCHAR(10)  NOT NULL,
    party_size              SMALLINT    NOT NULL,
    booking_datetime        TIMESTAMPTZ NOT NULL,
    special_requests        TEXT,
    status                  VARCHAR(20)  NOT NULL DEFAULT 'confirmed'
                                CHECK (status IN ('confirmed', 'cancelled', 'no_show', 'completed')),
    internal_notes          TEXT,
    confirmed_at            TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    confirmed_by            VARCHAR(255),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Critical for the dual-table availability query performance.
CREATE INDEX IF NOT EXISTS idx_confirmed_bookings_slot
    ON confirmed_bookings (agent_id, booking_datetime, status);

-- =============================================================================
-- KNOWLEDGE DOCUMENTS
-- Chunk text and vectors live in Qdrant. This table tracks document metadata.
-- =============================================================================
CREATE TABLE IF NOT EXISTS knowledge_documents (
    id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_id        UUID        NOT NULL REFERENCES agents(id) ON DELETE CASCADE,
    tenant_id       UUID        NOT NULL,
    filename        VARCHAR(500) NOT NULL,
    file_size_bytes BIGINT,
    mime_type       VARCHAR(100),
    content_sha256  VARCHAR(64),
    status          VARCHAR(20)  NOT NULL DEFAULT 'uploading'
                        CHECK (status IN ('uploading', 'extracting', 'embedding', 'ready', 'error')),
    error_message   TEXT,
    chunk_count     INTEGER,
    language        VARCHAR(10),
    uploaded_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    processed_at    TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_knowledge_documents_agent_id
    ON knowledge_documents (agent_id);

CREATE INDEX IF NOT EXISTS idx_knowledge_documents_sha256
    ON knowledge_documents (content_sha256)
    WHERE content_sha256 IS NOT NULL;

-- =============================================================================
-- CONVERSATION SESSIONS
-- =============================================================================
CREATE TABLE IF NOT EXISTS conversation_sessions (
    id                  UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_id            UUID        NOT NULL REFERENCES agents(id),
    tenant_id           UUID        NOT NULL,
    channel             VARCHAR(20)  NOT NULL CHECK (channel IN ('web', 'whatsapp', 'phone')),
    detected_language   VARCHAR(10),
    started_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ended_at            TIMESTAMPTZ,
    duration_seconds    INTEGER,
    close_reason        VARCHAR(50)
                            CHECK (close_reason IN
                                ('user_disconnect', 'timeout', 'silence', 'error', 'server_close')),
    ai_summary          TEXT,
    gemini_session_id   VARCHAR(500)
);

CREATE INDEX IF NOT EXISTS idx_conversation_sessions_tenant_agent
    ON conversation_sessions (tenant_id, agent_id, started_at DESC);

-- =============================================================================
-- SESSION FUNCTION CALLS
-- =============================================================================
CREATE TABLE IF NOT EXISTS session_function_calls (
    id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id      UUID        NOT NULL REFERENCES conversation_sessions(id),
    tenant_id       UUID        NOT NULL,
    function_name   VARCHAR(100) NOT NULL,
    arguments_json  JSONB,
    result_json     JSONB,
    latency_ms      INTEGER,
    called_at       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_session_function_calls_session_id
    ON session_function_calls (session_id);

-- =============================================================================
-- WEBHOOK DELIVERY LOG
-- =============================================================================
CREATE TABLE IF NOT EXISTS webhook_deliveries (
    id              UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID        NOT NULL,
    event_type      VARCHAR(100) NOT NULL,
    payload_json    JSONB       NOT NULL,
    target_url      TEXT        NOT NULL,
    attempt_count   SMALLINT    NOT NULL DEFAULT 0,
    last_status     SMALLINT,
    last_response   TEXT,
    delivered_at    TIMESTAMPTZ,
    next_retry_at   TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_webhook_deliveries_tenant_id
    ON webhook_deliveries (tenant_id, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_webhook_deliveries_next_retry
    ON webhook_deliveries (next_retry_at)
    WHERE delivered_at IS NULL AND attempt_count < 3;

-- =============================================================================
-- ROW-LEVEL SECURITY
-- These policies are a safety net. Application code must still filter by
-- tenant_id explicitly — never rely solely on RLS.
-- =============================================================================

-- The application sets this at the start of every database connection:
--   SET LOCAL app.current_tenant_id = '<uuid>';

ALTER TABLE agents               ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_hours       ENABLE ROW LEVEL SECURITY;
ALTER TABLE closed_dates         ENABLE ROW LEVEL SECURITY;
ALTER TABLE pending_bookings     ENABLE ROW LEVEL SECURITY;
ALTER TABLE confirmed_bookings   ENABLE ROW LEVEL SECURITY;
ALTER TABLE knowledge_documents  ENABLE ROW LEVEL SECURITY;
ALTER TABLE conversation_sessions ENABLE ROW LEVEL SECURITY;
ALTER TABLE session_function_calls ENABLE ROW LEVEL SECURITY;
ALTER TABLE webhook_deliveries   ENABLE ROW LEVEL SECURITY;

-- Policy template — applied to every tenant-scoped table:
CREATE POLICY tenant_isolation ON agents
    USING (tenant_id = current_setting('app.current_tenant_id', TRUE)::UUID);

CREATE POLICY tenant_isolation ON business_hours
    USING (tenant_id = current_setting('app.current_tenant_id', TRUE)::UUID);

CREATE POLICY tenant_isolation ON closed_dates
    USING (tenant_id = current_setting('app.current_tenant_id', TRUE)::UUID);

CREATE POLICY tenant_isolation ON pending_bookings
    USING (tenant_id = current_setting('app.current_tenant_id', TRUE)::UUID);

CREATE POLICY tenant_isolation ON confirmed_bookings
    USING (tenant_id = current_setting('app.current_tenant_id', TRUE)::UUID);

CREATE POLICY tenant_isolation ON knowledge_documents
    USING (tenant_id = current_setting('app.current_tenant_id', TRUE)::UUID);

CREATE POLICY tenant_isolation ON conversation_sessions
    USING (tenant_id = current_setting('app.current_tenant_id', TRUE)::UUID);

CREATE POLICY tenant_isolation ON session_function_calls
    USING (tenant_id = current_setting('app.current_tenant_id', TRUE)::UUID);

CREATE POLICY tenant_isolation ON webhook_deliveries
    USING (tenant_id = current_setting('app.current_tenant_id', TRUE)::UUID);
