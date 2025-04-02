#pragma warning disable IDE0161
#pragma warning disable IDE0053
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "clients",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    dni = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clients", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "marca",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_marca", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "transaction_categories",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transaction_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "modelo",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    marca_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modelo", x => x.id);
                    table.ForeignKey(
                        name: "fk_modelo_marca_marca_id",
                        column: x => x.marca_id,
                        principalSchema: "public",
                        principalTable: "marca",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cars",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    marca_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modelo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    color = table.Column<int>(type: "integer", nullable: false),
                    car_type = table.Column<string>(type: "text", nullable: false),
                    car_status = table.Column<string>(type: "text", nullable: false),
                    service_car = table.Column<string>(type: "text", nullable: false),
                    fuel_type = table.Column<string>(type: "text", nullable: false),
                    cantidad_puertas = table.Column<int>(type: "integer", nullable: false),
                    cantidad_asientos = table.Column<int>(type: "integer", nullable: false),
                    cilindrada = table.Column<int>(type: "integer", nullable: false),
                    kilometraje = table.Column<int>(type: "integer", nullable: false),
                    año = table.Column<int>(type: "integer", nullable: false),
                    patente = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    price = table.Column<decimal>(type: "numeric", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cars", x => x.id);
                    table.ForeignKey(
                        name: "fk_cars_marca_marca_id",
                        column: x => x.marca_id,
                        principalSchema: "public",
                        principalTable: "marca",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_cars_modelo_modelo_id",
                        column: x => x.modelo_id,
                        principalSchema: "public",
                        principalTable: "modelo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quotes",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    car_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposed_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    valid_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    comments = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quotes", x => x.id);
                    table.ForeignKey(
                        name: "fk_quotes_cars_car_id",
                        column: x => x.car_id,
                        principalSchema: "public",
                        principalTable: "cars",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quotes_clients_client_id",
                        column: x => x.client_id,
                        principalSchema: "public",
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    car_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    final_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    payment_method = table.Column<string>(type: "text", nullable: false),
                    contract_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sale_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    comments = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales", x => x.id);
                    table.ForeignKey(
                        name: "fk_sales_cars_car_id",
                        column: x => x.car_id,
                        principalSchema: "public",
                        principalTable: "cars",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_clients_client_id",
                        column: x => x.client_id,
                        principalSchema: "public",
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    payment_method = table.Column<int>(type: "integer", nullable: false),
                    reference_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    transaction_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    car_id = table.Column<Guid>(type: "uuid", nullable: true),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sale_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_transactions_cars_car_id",
                        column: x => x.car_id,
                        principalSchema: "public",
                        principalTable: "cars",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transactions_clients_client_id",
                        column: x => x.client_id,
                        principalSchema: "public",
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transactions_sales_sale_id",
                        column: x => x.sale_id,
                        principalSchema: "public",
                        principalTable: "sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transactions_transaction_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "public",
                        principalTable: "transaction_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cars_marca_id",
                schema: "public",
                table: "cars",
                column: "marca_id");

            migrationBuilder.CreateIndex(
                name: "ix_cars_modelo_id",
                schema: "public",
                table: "cars",
                column: "modelo_id");

            migrationBuilder.CreateIndex(
                name: "ix_cars_patente",
                schema: "public",
                table: "cars",
                column: "patente",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_clients_dni",
                schema: "public",
                table: "clients",
                column: "dni",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_modelo_marca_id",
                schema: "public",
                table: "modelo",
                column: "marca_id");

            migrationBuilder.CreateIndex(
                name: "ix_quotes_car_id",
                schema: "public",
                table: "quotes",
                column: "car_id");

            migrationBuilder.CreateIndex(
                name: "ix_quotes_client_id",
                schema: "public",
                table: "quotes",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_car_id",
                schema: "public",
                table: "sales",
                column: "car_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_client_id",
                schema: "public",
                table: "sales",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_car_id",
                schema: "public",
                table: "transactions",
                column: "car_id");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_category_id",
                schema: "public",
                table: "transactions",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_client_id",
                schema: "public",
                table: "transactions",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_sale_id",
                schema: "public",
                table: "transactions",
                column: "sale_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                schema: "public",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quotes",
                schema: "public");

            migrationBuilder.DropTable(
                name: "transactions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "users",
                schema: "public");

            migrationBuilder.DropTable(
                name: "sales",
                schema: "public");

            migrationBuilder.DropTable(
                name: "transaction_categories",
                schema: "public");

            migrationBuilder.DropTable(
                name: "cars",
                schema: "public");

            migrationBuilder.DropTable(
                name: "clients",
                schema: "public");

            migrationBuilder.DropTable(
                name: "modelo",
                schema: "public");

            migrationBuilder.DropTable(
                name: "marca",
                schema: "public");
        }
    }

