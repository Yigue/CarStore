using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Documents.Commands.UploadAndVerifyDocument;
using Application.Documents.DTOs;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SharedKernel;
using Web.Api.Endpoints.Documents;
using Xunit;

namespace Application.UnitTests.Documents;

public class UploadOcrTests
{
    private static IFormFile CreateMockFile(long lengthInBytes)
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.SetupGet(f => f.Length).Returns(lengthInBytes);
        fileMock.SetupGet(f => f.FileName).Returns("test_document.pdf");
        fileMock.SetupGet(f => f.ContentType).Returns("application/pdf");
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[Math.Min(lengthInBytes, 1024)]));
        return fileMock.Object;
    }

    [Fact]
    public async Task HandleUpload_WhenFileExceeds10MB_Returns413PayloadTooLarge()
    {
        // Arrange: 10 MB + 1 byte
        long oversized = 10 * 1024 * 1024 + 1;
        var file = CreateMockFile(oversized);
        var senderMock = new Mock<ISender>();

        // Act
        IResult result = await Upload.HandleUpload(file, null, null, senderMock.Object, CancellationToken.None);

        // Assert
        result.Should().BeAssignableTo<IStatusCodeHttpResult>();
        ((IStatusCodeHttpResult)result).StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
        senderMock.Verify(s => s.Send(It.IsAny<UploadAndVerifyDocumentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleUpload_WhenFileWithin10MB_SendsCommandAndReturnsCreated()
    {
        // Arrange: 5 MB file
        long validSize = 5 * 1024 * 1024;
        var file = CreateMockFile(validSize);
        var docId = Guid.NewGuid();
        var parsed = new ParsedDocumentDto("DNI", "12345678", "Juan", "Perez", null, null, null);
        var dto = new VerifyDocumentResultDto(docId, true, parsed, new List<string>());

        var senderMock = new Mock<ISender>();
        senderMock.Setup(s => s.Send(It.IsAny<UploadAndVerifyDocumentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        // Act
        IResult result = await Upload.HandleUpload(file, null, null, senderMock.Object, CancellationToken.None);

        // Assert
        result.Should().BeAssignableTo<IStatusCodeHttpResult>();
        ((IStatusCodeHttpResult)result).StatusCode.Should().Be(StatusCodes.Status201Created);
        senderMock.Verify(s => s.Send(It.IsAny<UploadAndVerifyDocumentCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void MapEndpoint_Should_EnforceDocumentsCreatePermission_And_RequestSizeLimit()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<ISender>(_ => Mock.Of<ISender>());
        var app = builder.Build();

        var endpoint = new Upload();
        endpoint.MapEndpoint(app);

        // Act: find the mapped endpoint for documents/ocr-upload
        var ocrEndpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .FirstOrDefault(e => e.RoutePattern.RawText?.Contains("documents/ocr-upload") == true);

        // Assert
        ocrEndpoint.Should().NotBeNull();

        // 1. Permission check: must require documents:create
        var authorizeData = ocrEndpoint!.Metadata.OfType<IAuthorizeData>().ToList();
        authorizeData.Should().NotBeEmpty();
        authorizeData.Should().Contain(a => a.Policy == "documents:create");

        // 2. RequestSizeLimit: must have limit of 10MB (10485760 bytes)
        var sizeLimitMetadata = ocrEndpoint.Metadata.OfType<IRequestSizeLimitMetadata>().FirstOrDefault();
        sizeLimitMetadata.Should().NotBeNull();
        sizeLimitMetadata!.MaxRequestBodySize.Should().Be(10 * 1024 * 1024);
    }
}
