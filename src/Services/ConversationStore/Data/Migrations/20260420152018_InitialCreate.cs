using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AxonVoiceAI.ConversationStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "conversation_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    caller_identifier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "en"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "active"),
                    summary = table.Column<string>(type: "text", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    duration_seconds = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversation_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "session_function_calls",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    function_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    arguments_json = table.Column<string>(type: "text", nullable: true),
                    result_json = table.Column<string>(type: "text", nullable: true),
                    succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    called_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    duration_ms = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_function_calls", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_function_calls_conversation_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "conversation_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "webhook_deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    webhook_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    last_http_status = table.Column<int>(type: "integer", nullable: true),
                    last_error_message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_retry_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "FK_webhook_deliveries_conversation_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "conversation_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_conversation_sessions_started_at",
                table: "conversation_sessions",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "IX_conversation_sessions_tenant_id_agent_id",
                table: "conversation_sessions",
                columns: new[] { "tenant_id", "agent_id" });

            migrationBuilder.CreateIndex(
                name: "IX_session_function_calls_session_id",
                table: "session_function_calls",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_session_id",
                table: "webhook_deliveries",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_status_next_retry_at",
                table: "webhook_deliveries",
                columns: new[] { "status", "next_retry_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_function_calls");

            migrationBuilder.DropTable(
                name: "webhook_deliveries");

            migrationBuilder.DropTable(
                name: "conversation_sessions");
        }
    }
}
