using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Storage;
using Application.Documents.DTOs;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services;

public class AzureDocumentIntelligenceOcrService : IOcrService
{
    private readonly DocumentAnalysisClient _client;

    public AzureDocumentIntelligenceOcrService(IConfiguration configuration)
    {
        string endpoint = configuration["AzureDocumentIntelligence:Endpoint"]
            ?? throw new ArgumentNullException("AzureDocumentIntelligence:Endpoint");
        string apiKey = configuration["AzureDocumentIntelligence:ApiKey"]
            ?? throw new ArgumentNullException("AzureDocumentIntelligence:ApiKey");

        _client = new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
    }

    public async Task<ParsedDocumentDto> ParseAsync(Stream fileStream, string contentType, CancellationToken ct)
    {
        var operation = await _client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-document", fileStream, cancellationToken: ct);
        var result = operation.Value;

        // Simplify extraction by looking for key-value pairs or content heuristics (demo implementation)
        string? docType = null;
        string? docNumber = null;
        string? firstName = null;
        string? lastName = null;
        string? licensePlate = null;
        string? chassisNumber = null;
        string? year = null;

        foreach (var kvp in result.KeyValuePairs)
        {
            var key = kvp.Key.Content.ToLowerInvariant();
            var value = kvp.Value?.Content;

            if (key.Contains("type") || key.Contains("tipo")) docType = value;
            else if (key.Contains("number") || key.Contains("número") || key.Contains("dni")) docNumber = value;
            else if (key.Contains("first name") || key.Contains("nombre")) firstName = value;
            else if (key.Contains("last name") || key.Contains("apellido")) lastName = value;
            else if (key.Contains("plate") || key.Contains("patente") || key.Contains("dominio")) licensePlate = value;
            else if (key.Contains("chassis") || key.Contains("chasis")) chassisNumber = value;
            else if (key.Contains("year") || key.Contains("año")) year = value;
        }

        return new ParsedDocumentDto(
            DocumentType: docType,
            DocumentNumber: docNumber,
            FirstName: firstName,
            LastName: lastName,
            LicensePlate: licensePlate,
            ChassisNumber: chassisNumber,
            Year: year
        );
    }
}
