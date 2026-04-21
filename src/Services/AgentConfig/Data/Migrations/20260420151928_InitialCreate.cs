using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AxonVoiceAI.AgentConfig.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    api_key_encrypted = table.Column<string>(type: "text", nullable: false),
                    api_key_hint = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    default_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "si"),
                    webhook_url = table.Column<string>(type: "text", nullable: true),
                    webhook_secret = table.Column<string>(type: "text", nullable: true),
                    rate_limit_daily = table.Column<int>(type: "integer", nullable: false, defaultValue: 1000),
                    rate_limit_concurrent = table.Column<int>(type: "integer", nullable: false, defaultValue: 20),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    display_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    persona_prompt = table.Column<string>(type: "text", nullable: false),
                    supported_languages = table.Column<string[]>(type: "text[]", nullable: false),
                    primary_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "si"),
                    voice_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "Aoede"),
                    gemini_model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "gemini-2.0-flash-live-001"),
                    session_timeout_sec = table.Column<int>(type: "integer", nullable: false, defaultValue: 600),
                    silence_timeout_sec = table.Column<int>(type: "integer", nullable: false, defaultValue: 90),
                    tools_enabled = table.Column<string[]>(type: "text[]", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agents", x => x.id);
                    table.ForeignKey(
                        name: "FK_agents_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "business_hours",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<short>(type: "smallint", nullable: false),
                    open_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    close_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    slot_duration_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 90),
                    max_capacity_per_slot = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_hours", x => x.id);
                    table.ForeignKey(
                        name: "FK_business_hours_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "closed_dates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    closed_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_closed_dates", x => x.id);
                    table.ForeignKey(
                        name: "FK_closed_dates_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pending_bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    customer_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    customer_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    party_size = table.Column<short>(type: "smallint", nullable: false),
                    requested_datetime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    special_requests = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_bookings", x => x.id);
                    table.ForeignKey(
                        name: "FK_pending_bookings_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "confirmed_bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    promoted_from_pending = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    customer_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    customer_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    party_size = table.Column<short>(type: "smallint", nullable: false),
                    booking_datetime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    special_requests = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "confirmed"),
                    internal_notes = table.Column<string>(type: "text", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    confirmed_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_confirmed_bookings", x => x.id);
                    table.ForeignKey(
                        name: "FK_confirmed_bookings_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_confirmed_bookings_pending_bookings_promoted_from_pending",
                        column: x => x.promoted_from_pending,
                        principalTable: "pending_bookings",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_agents_tenant_id",
                table: "agents",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_business_hours_agent_id_day_of_week",
                table: "business_hours",
                columns: new[] { "agent_id", "day_of_week" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_closed_dates_agent_id_closed_date",
                table: "closed_dates",
                columns: new[] { "agent_id", "closed_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_confirmed_bookings_slot",
                table: "confirmed_bookings",
                columns: new[] { "agent_id", "booking_datetime", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_confirmed_bookings_promoted_from_pending",
                table: "confirmed_bookings",
                column: "promoted_from_pending");

            migrationBuilder.CreateIndex(
                name: "idx_pending_bookings_slot",
                table: "pending_bookings",
                columns: new[] { "agent_id", "requested_datetime", "status", "expires_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "business_hours");

            migrationBuilder.DropTable(
                name: "closed_dates");

            migrationBuilder.DropTable(
                name: "confirmed_bookings");

            migrationBuilder.DropTable(
                name: "pending_bookings");

            migrationBuilder.DropTable(
                name: "agents");

            migrationBuilder.DropTable(
                name: "tenants");
        }
    }
}
