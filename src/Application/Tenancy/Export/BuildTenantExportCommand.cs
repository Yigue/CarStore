using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Tenancy.Export;

/// <summary>RN-014 (A7): builds the ZIP of a Requested export (CSV per aggregate, attachments, manifest) and publishes it.</summary>
public sealed record BuildTenantExportCommand(Guid ExportId) : ICommand;

internal sealed class BuildTenantExportCommandHandler(
    IApplicationDbContext context,
    IStorageService storage,
    IEmailService emailService,
    IDateTimeProvider clock,
    IConfiguration configuration,
    ILogger<BuildTenantExportCommandHandler> logger) : ICommandHandler<BuildTenantExportCommand>
{
    public const int SchemaVersion = 1;

    private sealed record FileEntry(string Name, int Rows, string Sha256, long Bytes);

    private sealed record AttachmentResult(int Included, List<string> Missing, int SkippedLegacy);

    public async Task<Result> Handle(BuildTenantExportCommand request, CancellationToken cancellationToken)
    {
        // The job runs with no tenant, so the export is located by id and every query below
        // constrains DealerId explicitly.
        TenantExport? export = await context.TenantExports
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == request.ExportId, cancellationToken);

        if (export is null)
            return Result.Failure(TenantExportErrors.NotFound);

        if (export.Status != TenantExportStatus.Requested)
            return Result.Failure(new Error(
                "TenantExport.InvalidState", $"La exportación está en estado {export.Status}.", ErrorType.Conflict));

        export.MarkRunning(clock.UtcNow);
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            await using FileStream zipFile = new(
                Path.Combine(Path.GetTempPath(), $"carstore-export-{export.Id:N}.zip"),
                FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920,
                FileOptions.DeleteOnClose | FileOptions.Asynchronous);

            await BuildZipAsync(zipFile, export, cancellationToken);

            zipFile.Position = 0;
            string sha256 = Convert.ToHexString(await SHA256.HashDataAsync(zipFile, cancellationToken)).ToLowerInvariant();
            long size = zipFile.Length;

            zipFile.Position = 0;
            string key = $"exports/{export.DealerId}/{export.Id}.zip";
            await storage.UploadFileAsync(zipFile, key, "application/zip", size, cancellationToken);

            export.MarkReady(key, sha256, size, clock.UtcNow);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Tenant export {ExportId} failed", export.Id);
            export.Fail(ex.Message, clock.UtcNow);
            await context.SaveChangesAsync(CancellationToken.None);
            return Result.Failure(new Error("TenantExport.BuildFailed", "No se pudo generar la exportación.", ErrorType.Failure));
        }

        await NotifyAsync(export, cancellationToken);
        return Result.Success();
    }

    private async Task BuildZipAsync(Stream destination, TenantExport export, CancellationToken ct)
    {
        Guid dealerId = export.DealerId;
        using var zip = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        var files = new List<FileEntry>();

        // IgnoreQueryFilters keeps soft-deleted / archived rows: the dealer asked for ALL its data.
        files.Add(await AddCsvAsync(zip, "vehiculos", context.Cars.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.DealerId == dealerId).OrderBy(x => x.Id).AsAsyncEnumerable(), ct));
        files.Add(await AddCsvAsync(zip, "clientes", context.Clients.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.DealerId == dealerId).OrderBy(x => x.Id).AsAsyncEnumerable(), ct));
        files.Add(await AddCsvAsync(zip, "leads", context.Leads.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.DealerId == dealerId).OrderBy(x => x.Id).AsAsyncEnumerable(), ct));
        files.Add(await AddCsvAsync(zip, "cotizaciones", context.Quotes.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.DealerId == dealerId).OrderBy(x => x.Id).AsAsyncEnumerable(), ct));
        files.Add(await AddCsvAsync(zip, "ventas", context.Sales.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.DealerId == dealerId).OrderBy(x => x.Id).AsAsyncEnumerable(), ct));
        files.Add(await AddCsvAsync(zip, "turnos", context.Appointments.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.DealerId == dealerId).OrderBy(x => x.Id).AsAsyncEnumerable(), ct));
        files.Add(await AddCsvAsync(zip, "transacciones", context.Transactions.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.DealerId == dealerId).OrderBy(x => x.Id).AsAsyncEnumerable(), ct));

        AttachmentResult attachments = await AddAttachmentsAsync(zip, dealerId, ct);

        await WriteManifestAsync(zip, export, files, attachments, ct);
    }

    private static async Task<FileEntry> AddCsvAsync<T>(
        ZipArchive zip, string aggregate, IAsyncEnumerable<T> rows, CancellationToken ct) where T : class
    {
        string name = aggregate + ".csv";
        ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        await using Stream stream = entry.Open();
        (int count, string sha256, long bytes) = await TenantExportCsv.WriteAsync(stream, rows, ct);
        return new FileEntry(name, count, sha256, bytes);
    }

    private async Task<AttachmentResult> AddAttachmentsAsync(ZipArchive zip, Guid dealerId, CancellationToken ct)
    {
        var missing = new List<string>();
        int included = 0;

        var documents = await context.Documents.IgnoreQueryFilters().AsNoTracking()
            .Where(d => d.DealerId == dealerId)
            .Select(d => new { d.Id, d.BlobName, d.FileName })
            .ToListAsync(ct);

        foreach (var document in documents)
        {
            string entryName = $"adjuntos/documentos/{document.Id}-{SafeFileName(document.FileName)}";
            if (await CopyBlobAsync(zip, document.BlobName, entryName, ct))
                included++;
            else
                missing.Add(document.BlobName);
        }

        var images = await (
                from image in context.CarImages.AsNoTracking()
                join car in context.Cars.IgnoreQueryFilters() on image.CarId equals car.Id
                where car.DealerId == dealerId
                select new { image.Id, image.CarId, image.ObjectKey })
            .ToListAsync(ct);

        int skippedLegacy = 0;
        foreach (var image in images)
        {
            if (image.ObjectKey is null)
            {
                // Legacy images live behind a direct URL, not in our storage.
                skippedLegacy++;
                continue;
            }

            string entryName = $"adjuntos/vehiculos/{image.CarId}/{image.Id}{Path.GetExtension(image.ObjectKey)}";
            if (await CopyBlobAsync(zip, image.ObjectKey, entryName, ct))
                included++;
            else
                missing.Add(image.ObjectKey);
        }

        return new AttachmentResult(included, missing, skippedLegacy);
    }

    private async Task<bool> CopyBlobAsync(ZipArchive zip, string objectKey, string entryName, CancellationToken ct)
    {
        await using Stream? source = await storage.OpenReadAsync(objectKey, ct);
        if (source is null)
            return false;

        ZipArchiveEntry entry = zip.CreateEntry(entryName, CompressionLevel.Fastest);
        await using Stream target = entry.Open();
        await source.CopyToAsync(target, ct);
        return true;
    }

    private async Task WriteManifestAsync(
        ZipArchive zip, TenantExport export, List<FileEntry> files, AttachmentResult attachments, CancellationToken ct)
    {
        var counts = files.ToDictionary(f => Path.GetFileNameWithoutExtension(f.Name), f => f.Rows);
        // Seña (deposit) has no persistence yet (sfc B1); the key is kept so the manifest schema is stable.
        counts["senas"] = 0;

        var manifest = new
        {
            schemaVersion = SchemaVersion,
            exportId = export.Id,
            dealerId = export.DealerId,
            generatedAtUtc = clock.UtcNow,
            counts,
            files = files.Select(f => new { name = f.Name, rows = f.Rows, sha256 = f.Sha256, bytes = f.Bytes }),
            attachments = new { included = attachments.Included, missing = attachments.Missing, skippedLegacy = attachments.SkippedLegacy },
            notIncluded = new[] { "senas" }
        };

        ZipArchiveEntry entry = zip.CreateEntry("manifest.json", CompressionLevel.Optimal);
        await using Stream stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, manifest, new JsonSerializerOptions { WriteIndented = true }, ct);
    }

    private async Task NotifyAsync(TenantExport export, CancellationToken ct)
    {
        try
        {
            Domain.Shared.ValueObjects.Email? requesterEmail = await context.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(u => u.Id == export.RequestedBy && u.DealerId == export.DealerId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync(ct);

            if (requesterEmail is null)
                return;

            string recipient = requesterEmail.Value;

            string baseUrl = (configuration["Frontend:BaseUrl"] ?? "https://carstore-app.com").TrimEnd('/');
            string link = $"{baseUrl}/api/v1/dealer/exports/{export.Id}/download";
            string body = $"""
                Hola,

                La exportación de los datos de tu concesionaria ya está lista.
                Descargala desde el siguiente enlace (requiere iniciar sesión; disponible por {TenantExport.RetentionDays} días):
                {link}

                Saludos,
                El equipo de CarStore
                """;

            await emailService.SendEmailAsync(recipient, "Tu exportación de datos está lista - CarStore", body, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The ZIP is already published; a mail outage must not turn a good export into a failed one.
            logger.LogWarning(ex, "Export {ExportId} is ready but the notification email failed", export.Id);
        }
    }

    private static string SafeFileName(string fileName)
    {
        string name = Path.GetFileName(fileName ?? string.Empty);
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "archivo" : name;
    }
}
