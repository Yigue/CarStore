using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

/// <summary>
/// Migración para documentar la integración de Value Objects en las configuraciones de EF Core.
/// Esta migración no modifica la estructura de la base de datos, ya que los ValueConverters
/// solo cambian cómo se mapean los valores entre el dominio y la base de datos.
/// 
/// Value Objects integrados:
/// - Money: Para Car.Price, Sale.FinalPrice, Quote.ProposedPrice, FinancialTransaction.Amount
/// - Email: Para Client.Email
/// - LicensePlate: Para Car.Patente
/// 
/// Los ValueConverters están configurados en:
/// - CarConfiguration.cs
/// - ClientConfiguration.cs
/// - SaleConfiguration.cs
/// - QuoteConfiguration.cs
/// - TransactionConfiguration.cs
/// </summary>
public partial class AddValueObjects : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Esta migración es principalmente documental.
        // Los ValueConverters no requieren cambios en la estructura de la base de datos,
        // ya que solo cambian cómo se mapean los valores entre el dominio y la BD.
        // 
        // Las columnas en la base de datos permanecen como:
        // - price, final_price, proposed_price, amount: decimal/numeric
        // - email: string/varchar
        // - patente: string/varchar
        //
        // Los ValueConverters se aplican automáticamente en tiempo de ejecución
        // cuando EF Core lee/escribe estos valores.
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // No hay cambios que revertir ya que no se modificó la estructura de la BD
    }
}

