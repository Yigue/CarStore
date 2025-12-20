using Application.Abstractions.Data;
using Domain.Financial.Attributes;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database.SeedData;

/// <summary>
/// Seeder para Categorías de Transacciones Financieras.
/// </summary>
internal static class TransactionCategoriesSeeder
{
    /// <summary>
    /// Seedea las categorías de transacciones según el roadmap.
    /// </summary>
    public static async Task SeedAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        // Verificar si ya hay categorías
        if (await context.TransactionCategories.AnyAsync(cancellationToken))
        {
            return;
        }

        var categories = new List<TransactionCategory>
        {
            // Ingresos
            new TransactionCategory(
                "Venta de Auto",
                "Ingresos por venta de vehículos",
                TransactionType.Income),
            
            new TransactionCategory(
                "Servicio Técnico",
                "Ingresos por servicios técnicos y reparaciones",
                TransactionType.Income),
            
            new TransactionCategory(
                "Garantía",
                "Ingresos por servicios de garantía",
                TransactionType.Income),

            // Egresos
            new TransactionCategory(
                "Compra de Auto",
                "Egresos por compra de vehículos",
                TransactionType.Expense),
            
            new TransactionCategory(
                "Gastos Operativos",
                "Gastos generales de operación del negocio",
                TransactionType.Expense),
            
            new TransactionCategory(
                "Mantenimiento",
                "Gastos de mantenimiento de vehículos e instalaciones",
                TransactionType.Expense),
            
            new TransactionCategory(
                "Publicidad",
                "Gastos en publicidad y marketing",
                TransactionType.Expense)
        };

        context.TransactionCategories.AddRange(categories);
        await context.SaveChangesAsync(cancellationToken);
    }
}

