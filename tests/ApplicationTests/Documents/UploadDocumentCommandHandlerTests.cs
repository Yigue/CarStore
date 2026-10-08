using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Application.Documents.Commands.UploadDocument;
using Domain.Documents;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;

namespace Application.UnitTests.Documents;

public class UploadDocumentCommandHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task Handle_NoTenantContext_ReturnsForbidden()
    {
        // API-005: Missing tenant context must return Forbidden
        using var context = CreateContext();
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.SetupGet(t => t.HasTenant).Returns(false);

        var storageMock = new Mock<IStorageService>();

        var handler = new UploadDocumentCommandHandler(context, tenantMock.Object, storageMock.Object);
        var command = new UploadDocumentCommand(
            Guid.NewGuid(),
            DocumentType.DNI,
            Convert.ToBase64String(new byte[] { 1, 2, 3 }),
            "document.pdf",
            "application/pdf");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }
}
