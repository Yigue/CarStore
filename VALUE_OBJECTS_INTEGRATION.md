# Integración de Value Objects

Este documento describe cómo integrar los Value Objects creados en el dominio con EF Core.

## Value Objects Creados

1. **Money** (`Domain.Shared.ValueObjects.Money`)
   - Para: `Car.Price`, `Sale.FinalPrice`, `Quote.ProposedPrice`, `FinancialTransaction.Amount`
   
2. **Email** (`Domain.Shared.ValueObjects.Email`)
   - Para: `Client.Email`
   
3. **LicensePlate** (`Domain.Shared.ValueObjects.LicensePlate`)
   - Para: `Car.Patente`

## Pasos para Integración

### 1. Actualizar Configuraciones de Entidades

#### CarConfiguration.cs
```csharp
builder.Property(c => c.Price)
    .HasConversion(new MoneyValueConverter())
    .HasColumnName("price")
    .IsRequired();

builder.Property(c => c.Patente)
    .HasConversion(new LicensePlateValueConverter())
    .HasMaxLength(10)
    .IsRequired();
```

#### ClientConfiguration.cs
```csharp
builder.Property(c => c.Email)
    .HasConversion(new EmailValueConverter())
    .HasMaxLength(200)
    .IsRequired();
```

#### SaleConfiguration.cs
```csharp
builder.Property(s => s.FinalPrice)
    .HasConversion(new MoneyValueConverter())
    .HasColumnName("final_price")
    .IsRequired();
```

#### QuoteConfiguration.cs
```csharp
builder.Property(q => q.ProposedPrice)
    .HasConversion(new MoneyValueConverter())
    .HasColumnName("proposed_price")
    .IsRequired();
```

#### TransactionConfiguration.cs
```csharp
builder.Property(t => t.Amount)
    .HasConversion(new MoneyValueConverter())
    .HasColumnName("amount")
    .IsRequired();
```

### 2. Actualizar Entidades del Dominio

Cambiar las propiedades de `decimal`/`string` a los Value Objects correspondientes:

- `Car.Price`: `decimal` → `Money`
- `Car.Patente`: `string` → `LicensePlate`
- `Client.Email`: `string` → `Email`
- `Sale.FinalPrice`: `decimal` → `Money`
- `Quote.ProposedPrice`: `decimal` → `Money`
- `FinancialTransaction.Amount`: `decimal` → `Money`

### 3. Crear Migración

```bash
dotnet ef migrations add AddValueObjects --project src/Infrastructure --startup-project src/Web.Api
```

### 4. Actualizar Handlers y Commands

Los handlers necesitarán convertir entre `decimal`/`string` y Value Objects en los puntos de entrada/salida.

## Nota Importante

**NO activar estas configuraciones hasta que:**
1. Se haya coordinado con el Rol 3 (DevOps/Infrastructure)
2. Se haya creado un backup de la base de datos
3. Se haya probado en un entorno de desarrollo
4. Se haya creado y probado la migración

Los Value Objects están listos para usar, pero su integración requiere migraciones de base de datos.

