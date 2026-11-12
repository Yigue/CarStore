using Application.Abstractions.Tenancy;
using Application.AfterSales;
using Domain.Financial.Attributes;
using Domain.Sales;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.AfterSales;

/// <summary>sfc C2.3 / RN-049: a warranty is registered once, on a completed and delivered sale of the caller's dealer.</summary>
public class RegisterWarrantyCommandTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime Delivered = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static RegisterWarrantyCommandHandler Handler(TestApplicationDbContext context, Guid dealerId)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        tenant.SetupGet(t => t.DealerId).Returns(dealerId);
        return new RegisterWarrantyCommandHandler(context, tenant.Object);
    }

    [Fact]
    public async Task Registers_once_on_a_delivered_sale_and_refuses_undelivered_or_foreign_ones()
    {
        using var context = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var delivered = new Sale(DealerId, Guid.NewGuid(), Guid.NewGuid(), 1m, PaymentMethod.Cash, "C-1", "c", Delivered);
        delivered.Complete();
        delivered.SetLegalDocuments(null, null, null, Delivered);
        var undelivered = new Sale(DealerId, Guid.NewGuid(), Guid.NewGuid(), 1m, PaymentMethod.Cash, "C-2", "c", Delivered);
        undelivered.Complete();
        context.Sales.AddRange(delivered, undelivered);
        await context.SaveChangesAsync();

        var ok = await Handler(context, DealerId).Handle(new RegisterWarrantyCommand(delivered.Id, 12, "Motor"), CancellationToken.None);
        var twice = await Handler(context, DealerId).Handle(new RegisterWarrantyCommand(delivered.Id, 12, null), CancellationToken.None);
        var notDelivered = await Handler(context, DealerId).Handle(new RegisterWarrantyCommand(undelivered.Id, 12, null), CancellationToken.None);
        var foreign = await Handler(context, Guid.NewGuid()).Handle(new RegisterWarrantyCommand(delivered.Id, 12, null), CancellationToken.None);

        ok.IsSuccess.Should().BeTrue();
        (await context.WarrantyRecords.SingleAsync()).EndsOnUtc.Should().Be(Delivered.AddMonths(12));
        twice.Error.Code.Should().Be("Warranty.AlreadyRegistered");
        notDelivered.Error.Code.Should().Be("Warranty.SaleNotDelivered");
        foreign.Error.Type.Should().Be(SharedKernel.ErrorType.NotFound);
    }
}
