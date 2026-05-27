using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadsAndDealerSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "last_name",
                schema: "public",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "first_name",
                schema: "public",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "role",
                schema: "public",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Cliente");

            migrationBuilder.CreateTable(
                name: "dealer_settings",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dealer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contact_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    notifications_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    host_name = table.Column<string>(type: "text", nullable: true),
                    custom_domain = table.Column<string>(type: "text", nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    facebook_url = table.Column<string>(type: "text", nullable: true),
                    instagram_url = table.Column<string>(type: "text", nullable: true),
                    twitter_url = table.Column<string>(type: "text", nullable: true),
                    interest_rate_tna = table.Column<decimal>(type: "numeric", nullable: true),
                    last_assigned_agent_index = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    dealer_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dealer_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    ocr_status = table.Column<string>(type: "text", nullable: false),
                    blob_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ocr_raw_json = table.Column<string>(type: "jsonb", nullable: true),
                    uploaded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    verified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    discrepancy_notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    dealer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parsed_data = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_documents_clients_client_id",
                        column: x => x.client_id,
                        principalSchema: "public",
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "leads",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Nuevo"),
                    assigned_agent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    dealer_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leads", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dealer_settings_dealer_id",
                schema: "public",
                table: "dealer_settings",
                column: "dealer_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_documents_client_id",
                schema: "public",
                table: "documents",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_dealer_id",
                schema: "public",
                table: "documents",
                column: "dealer_id");

            migrationBuilder.CreateIndex(
                name: "ix_leads_dealer_agent",
                schema: "public",
                table: "leads",
                columns: new[] { "dealer_id", "assigned_agent_id" });

            migrationBuilder.CreateIndex(
                name: "ix_leads_dealer_status",
                schema: "public",
                table: "leads",
                columns: new[] { "dealer_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dealer_settings",
                schema: "public");

            migrationBuilder.DropTable(
                name: "documents",
                schema: "public");

            migrationBuilder.DropTable(
                name: "leads",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "role",
                schema: "public",
                table: "users");

            migrationBuilder.AlterColumn<string>(
                name: "last_name",
                schema: "public",
                table: "users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "first_name",
                schema: "public",
                table: "users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);
        }
    }
}
