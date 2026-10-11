using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Application.Tenancy.Export;
using Domain.Clients;
using Domain.Documents;
using Domain.Leads;
using Domain.Tenancy;
using Domain.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SharedKernel;
using Xunit;

namespace Application.UnitTests.Tenancy;

/// <summary>RN-014 (A7): the ZIP holds one RFC 4180 CSV per aggregate, the attachments and a manifest with counts.</summary>
public class BuildTenantExportCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
    private readonly Guid _dealerId = Guid.NewGuid();

    private sealed class Rig
    {
        public required TestApplicationDbContext Context { get; init; }
        public required Mock<IStorageService> Storage { get; init; }
        public required Mock<IEmailService> Email { get; init; }
        public required BuildTenantExportCommandHandler Handler { get; init; }
        public byte[]? UploadedZip { get; set; }
        public string? UploadedKey { get; set; }
        public string? UploadedContentType { get; set; }
    }

    private Rig CreateRig(Dictionary<string, byte[]>? blobs = null)
    {
        var context = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options, _dealerId);

        var storage = new Mock<IStorageService>();
        var email = new Mock<IEmailService>();
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.UtcNow).Returns(Now);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:BaseUrl"] = "https://app.test" })
            .Build();

        var handler = new BuildTenantExportCommandHandler(
            context, storage.Object, email.Object, clock.Object, config,
            NullLogger<BuildTenantExportCommandHandler>.Instance);
        var rig = new Rig { Context = context, Storage = storage, Email = email, Handler = handler };

        storage
            .Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .Returns(async (Stream stream, string key, string contentType, long? _, CancellationToken ct) =>
            {
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, ct);
                rig.UploadedZip = ms.ToArray();
                rig.UploadedKey = key;
                rig.UploadedContentType = contentType;
                return key;
            });
        storage
            .Setup(s => s.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) =>
                blobs is not null && blobs.TryGetValue(key, out var bytes) ? new MemoryStream(bytes) : null);

        return rig;
    }

    private TenantExport SeedRequestedExport(TestApplicationDbContext context)
    {
        var requester = new User(_dealerId, "owner@dealer.com", "Ana", "Owner", "hash", Guid.NewGuid());
        context.Users.Add(requester);
        var export = TenantExport.Request(_dealerId, requester.Id, Now.AddMinutes(-1));
        context.TenantExports.Add(export);
        context.SaveChanges();
        return export;
    }

    private static Client NewClient(Guid dealerId, string first, string last, string dni) =>
        new(dealerId, first, last, dni, $"client{dni}@test.com", "111", "Street 1", Now);

    private static ZipArchive Open(byte[] zip) => new(new MemoryStream(zip), ZipArchiveMode.Read);

    private static string ReadEntry(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name);
        entry.Should().NotBeNull($"the ZIP must contain {name}");
        using var reader = new StreamReader(entry!.Open(), new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false);
        return reader.ReadToEnd();
    }

    [Fact]
    public async Task Handle_BuildsZipWithManifestCountsPerAggregate_OnlyForTheTenant()
    {
        var rig = CreateRig();
        var export = SeedRequestedExport(rig.Context);
        rig.Context.Clients.AddRange(
            NewClient(_dealerId, "Alice", "Smith", "11111111"),
            NewClient(_dealerId, "Bob", "Jones", "22222222"),
            NewClient(Guid.NewGuid(), "Eve", "Other", "33333333"));
        rig.Context.Leads.Add(Lead.Create(_dealerId, "Jane Doe", "jane@test.com", "555", LeadSource.Web, Now));
        rig.Context.SaveChanges();

        var result = await rig.Handler.Handle(new BuildTenantExportCommand(export.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        rig.UploadedZip.Should().NotBeNull();
        using var zip = Open(rig.UploadedZip!);
        using var manifest = JsonDocument.Parse(ReadEntry(zip, "manifest.json"));
        var counts = manifest.RootElement.GetProperty("counts");
        counts.GetProperty("clientes").GetInt32().Should().Be(2);
        counts.GetProperty("leads").GetInt32().Should().Be(1);
        foreach (var aggregate in new[] { "vehiculos", "cotizaciones", "senas", "ventas", "turnos", "transacciones" })
            counts.GetProperty(aggregate).GetInt32().Should().Be(0, aggregate);
        manifest.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        manifest.RootElement.GetProperty("exportId").GetGuid().Should().Be(export.Id);

        foreach (var file in new[]
                 { "vehiculos.csv", "clientes.csv", "leads.csv", "cotizaciones.csv", "ventas.csv", "turnos.csv", "transacciones.csv" })
            zip.GetEntry(file).Should().NotBeNull(file);
    }

    [Fact]
    public async Task Handle_WritesCsvAsRfc4180_Utf8WithBom_QuotingSpecialCharacters()
    {
        var rig = CreateRig();
        var export = SeedRequestedExport(rig.Context);
        rig.Context.Clients.Add(NewClient(_dealerId, "Ana, \"la\" Jefa", "Pérez", "44444444"));
        rig.Context.SaveChanges();

        await rig.Handler.Handle(new BuildTenantExportCommand(export.Id), CancellationToken.None);

        using var zip = Open(rig.UploadedZip!);
        using var stream = zip.GetEntry("clientes.csv")!.Open();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var bytes = ms.ToArray();
        bytes.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
        var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        text.Should().Contain("\"Ana, \"\"la\"\" Jefa\"");
        text.Should().Contain("Pérez");
        text.Should().Contain("\r\n");
        text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(2, "header + one row");
    }

    [Fact]
    public async Task Handle_UploadsToTenantScopedKey_AndMarksReadyWithHashOfTheZip()
    {
        var rig = CreateRig();
        var export = SeedRequestedExport(rig.Context);

        var result = await rig.Handler.Handle(new BuildTenantExportCommand(export.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        rig.UploadedKey.Should().Be($"exports/{_dealerId}/{export.Id}.zip");
        rig.UploadedContentType.Should().Be("application/zip");
        var stored = await rig.Context.TenantExports.SingleAsync();
        stored.Status.Should().Be(TenantExportStatus.Ready);
        stored.ObjectKey.Should().Be(rig.UploadedKey);
        stored.Sha256.Should().Be(Convert.ToHexString(SHA256.HashData(rig.UploadedZip!)).ToLowerInvariant());
        stored.SizeBytes.Should().Be(rig.UploadedZip!.Length);
        stored.ExpiresAt.Should().Be(Now.AddDays(7));
    }

    [Fact]
    public async Task Handle_IncludesDocumentAttachmentsReadFromStorage_AndListsMissingOnes()
    {
        var blobKey = $"documents/{_dealerId}/dni.png";
        var rig = CreateRig(new Dictionary<string, byte[]> { [blobKey] = [1, 2, 3, 4] });
        var export = SeedRequestedExport(rig.Context);
        rig.Context.Documents.Add(Document.Create(null, DocumentType.DNI, blobKey, "dni.png", "image/png", _dealerId));
        rig.Context.Documents.Add(Document.Create(null, DocumentType.DNI, "gone/key.png", "gone.png", "image/png", _dealerId));
        rig.Context.SaveChanges();

        var result = await rig.Handler.Handle(new BuildTenantExportCommand(export.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        using var zip = Open(rig.UploadedZip!);
        var attachment = zip.Entries.Should().ContainSingle(e => e.FullName.StartsWith("adjuntos/documentos/")).Subject;
        attachment.FullName.Should().EndWith("dni.png");
        attachment.Length.Should().Be(4);
        using var manifest = JsonDocument.Parse(ReadEntry(zip, "manifest.json"));
        var attachments = manifest.RootElement.GetProperty("attachments");
        attachments.GetProperty("included").GetInt32().Should().Be(1);
        attachments.GetProperty("missing").GetArrayLength().Should().Be(1);
    }

    [Theory]
    [InlineData("..\\..\\evil.exe")]
    [InlineData("../../evil.exe")]
    [InlineData("C:\\Windows\\evil.exe")]
    [InlineData("....//evil.exe")]
    [InlineData("..")]
    [InlineData("...")]
    [InlineData("a..b\\..\\c.exe")]
    public async Task Handle_HostileDocumentFileName_ProducesSafeEntryName(string hostileName)
    {
        var blobKey = $"documents/{_dealerId}/x.bin";
        var rig = CreateRig(new Dictionary<string, byte[]> { [blobKey] = [1] });
        var export = SeedRequestedExport(rig.Context);
        rig.Context.Documents.Add(Document.Create(null, DocumentType.DNI, blobKey, hostileName, "application/octet-stream", _dealerId));
        rig.Context.SaveChanges();

        await rig.Handler.Handle(new BuildTenantExportCommand(export.Id), CancellationToken.None);

        using var zip = Open(rig.UploadedZip!);
        var attachment = zip.Entries.Should().ContainSingle(e => e.FullName.StartsWith("adjuntos/documentos/")).Subject;
        attachment.FullName.Should().NotContain("\\");
        attachment.FullName.Should().NotContain("..");
        attachment.FullName.Should().NotContain(":");
        attachment.FullName.Should().NotStartWith("/");
        attachment.FullName.Split('/').Should().NotContain("..").And.OnlyContain(segment => segment.Length > 0);
        attachment.Name.Should().NotStartWith(".");
    }

    [Fact]
    public async Task Handle_ManifestHashesMatchTheCsvFiles()
    {
        var rig = CreateRig();
        var export = SeedRequestedExport(rig.Context);
        rig.Context.Clients.Add(NewClient(_dealerId, "Alice", "Smith", "11111111"));
        rig.Context.SaveChanges();

        await rig.Handler.Handle(new BuildTenantExportCommand(export.Id), CancellationToken.None);

        using var zip = Open(rig.UploadedZip!);
        using var manifest = JsonDocument.Parse(ReadEntry(zip, "manifest.json"));
        var clientsFile = manifest.RootElement.GetProperty("files").EnumerateArray()
            .Single(f => f.GetProperty("name").GetString() == "clientes.csv");
        using var entryStream = zip.GetEntry("clientes.csv")!.Open();
        using var ms = new MemoryStream();
        entryStream.CopyTo(ms);
        clientsFile.GetProperty("sha256").GetString()
            .Should().Be(Convert.ToHexString(SHA256.HashData(ms.ToArray())).ToLowerInvariant());
    }

    [Fact]
    public async Task Handle_SendsReadyEmailToTheRequesterWithDownloadLink()
    {
        var rig = CreateRig();
        var export = SeedRequestedExport(rig.Context);

        await rig.Handler.Handle(new BuildTenantExportCommand(export.Id), CancellationToken.None);

        rig.Email.Verify(e => e.SendEmailAsync(
            "owner@dealer.com",
            It.IsAny<string>(),
            It.Is<string>(body => body.Contains($"https://app.test/api/v1/dealer/exports/{export.Id}/download")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEmailFails_ExportIsStillReady()
    {
        var rig = CreateRig();
        var export = SeedRequestedExport(rig.Context);
        rig.Email
            .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp down"));

        var result = await rig.Handler.Handle(new BuildTenantExportCommand(export.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await rig.Context.TenantExports.SingleAsync()).Status.Should().Be(TenantExportStatus.Ready);
    }

    [Fact]
    public async Task Handle_WhenUploadFails_MarksExportFailed()
    {
        var rig = CreateRig();
        var export = SeedRequestedExport(rig.Context);
        rig.Storage
            .Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("minio down"));

        var result = await rig.Handler.Handle(new BuildTenantExportCommand(export.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        var stored = await rig.Context.TenantExports.SingleAsync();
        stored.Status.Should().Be(TenantExportStatus.Failed);
        stored.FailureReason.Should().Contain("minio down");
    }

    [Fact]
    public async Task Handle_WhenExportIsNotRequested_DoesNothing()
    {
        var rig = CreateRig();
        var export = SeedRequestedExport(rig.Context);
        export.MarkRunning(Now);
        rig.Context.SaveChanges();

        var result = await rig.Handler.Handle(new BuildTenantExportCommand(export.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        rig.UploadedZip.Should().BeNull();
    }

    [Fact]
    public async Task Handle_UnknownExport_ReturnsNotFound()
    {
        var rig = CreateRig();

        var result = await rig.Handler.Handle(new BuildTenantExportCommand(Guid.NewGuid()), CancellationToken.None);

        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}
