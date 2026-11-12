using System.Security.Cryptography;
using Application.Abstractions.Data;
using Application.Abstractions.Documents;
using Application.Abstractions.Email;
using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Application.Common;
using Application.Leads.Activity;
using Domain.Documents;
using Domain.Leads;
using Domain.Quotes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace Application.Quotes.Send;

/// <summary>
/// sfc B4.3 / B10 / RN-018: builds the quote's proposal PDF, uploads it, emails
/// <see cref="EmailTemplate.QuoteSent"/> to the quote's client and records a
/// <c>LeadActivity</c> entry when a lead can be resolved for this quote.
/// <para>
/// <b>Why the CLIENT's email, not the lead's.</b> Both <c>Client.Email</c> and <c>Lead.Email</c>
/// are the same non-nullable, validated <c>Email</c> value object — an actually blank email
/// cannot exist on either once constructed. This handler still gates on
/// <c>quote.Client is null</c> (a lead-only quote is rejected) rather than falling back to the
/// lead's email: the spec's "send to the lead/client contact" is satisfied by requiring the
/// quote to have a <c>Client</c> party at all — the same party <see cref="GenerateDocument.GenerateSaleDocumentCommandHandler"/>
/// requires for a sale's legal documents. A lead that has not been converted yet has no address
/// a dealer can reconcile later against a real transaction; converting it to a client (or
/// attaching the quote to one) is the deliberate gate before a priced proposal goes out.
/// </para>
/// <para>
/// <b>"PDF adjunto" in a codebase with no MIME-attachment capability.</b> Every transactional
/// email path (<see cref="ITransactionalEmailSender.SendAsync"/>,
/// <see cref="IBackgroundEmailQueue.TryEnqueue"/>) takes only a template name and a
/// string/string token model — there is no byte-payload/attachment parameter anywhere in this
/// email transport, and <see cref="EmailTemplate.QuoteSent"/>'s existing token set
/// (<c>TransactionalEmailTokens</c>) was already scoped with <c>QuoteUrl</c>, not an attachment
/// slot. This handler renders the PDF via <see cref="IDocumentRenderer"/>, uploads it through
/// the same <see cref="IStorageService"/>/SHA-256-on-the-<c>Document</c>-row pattern
/// <see cref="GenerateDocument.GenerateSaleDocumentCommandHandler"/> uses for sale documents, and
/// puts a short-lived presigned download URL into <c>QuoteUrl</c>. That is "the PDF is attached"
/// in the way this codebase's email architecture actually supports attachment-like delivery.
/// Adding true MIME attachments to the email transport layer is explicitly out of scope here.
/// </para>
/// <para>
/// <b>Direct send, not the background queue.</b> <see cref="IBackgroundEmailQueue"/> exists so an
/// ANONYMOUS, public-facing endpoint's response time cannot be timed to tell "recipient exists"
/// apart from "recipient does not exist" (sfc B5.1 newsletter enumeration). <c>POST
/// quotes/{id}/send</c> is an explicit, authenticated staff action on a quote staff already knows
/// exists — there is no enumeration surface to protect — so this sends synchronously via
/// <see cref="ITransactionalEmailSender"/> and lets a transport failure surface to the caller
/// instead of silently dropping an action the user just asked for.
/// </para>
/// </summary>
internal sealed class SendQuoteCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService,
    IDateTimeProvider dateTimeProvider,
    IStorageService storageService,
    IDocumentRenderer renderer,
    ITransactionalEmailSender emailSender,
    LeadActivityRecorder leadActivityRecorder)
    : ICommandHandler<SendQuoteCommand, SendQuoteResult>
{
    // sfc B4.3: long enough to survive the recipient opening the email days later — unlike the
    // 15-minute SAS used for an immediately-rendered download link (GetDocumentDownloadUrlQueryHandler).
    private static readonly TimeSpan QuoteUrlTtl = TimeSpan.FromDays(7);

    public async Task<Result<SendQuoteResult>> Handle(SendQuoteCommand command, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
        {
            return Result.Failure<SendQuoteResult>(guard.Error);
        }

        Guid dealerId = tenantService.DealerId;

        Quote? quote = await context.Quotes
            .Include(q => q.Car).ThenInclude(c => c.Marca)
            .Include(q => q.Car).ThenInclude(c => c.Modelo)
            .Include(q => q.Client)
            .SingleOrDefaultAsync(q => q.Id == command.QuoteId && q.DealerId == dealerId, cancellationToken);

        if (quote is null)
        {
            return Result.Failure<SendQuoteResult>(QuoteErrors.NotFound(command.QuoteId));
        }

        // Gate BEFORE the renderer, storage or email are ever touched — same discipline as
        // RN-028's InvoiceNumber/TransferFormNumber gate in GenerateSaleDocumentCommandHandler.
        // The blank-email half is provably unreachable in this codebase today (ClientConfiguration's
        // EmailValueConverter re-validates via Email's constructor on every read, not just on
        // write — confirmed empirically in SendQuoteCommandHandlerTests' class remarks), kept
        // anyway because it costs nothing and documents the invariant QuoteErrors.MissingContactEmail
        // actually names.
        if (quote.Client is null || string.IsNullOrWhiteSpace(quote.Client.Email.Value))
        {
            return Result.Failure<SendQuoteResult>(QuoteErrors.MissingContactEmail(quote.Id));
        }

        if (quote.Number is null)
        {
            return Result.Failure<SendQuoteResult>(QuoteErrors.NotNumbered(quote.Id));
        }

        DealerSettingsEntity? dealerSettings = await context.DealerSettings
            .SingleOrDefaultAsync(d => d.DealerId == dealerId, cancellationToken);

        string dealerName = dealerSettings?.DealerName ?? "—";
        string? dealerAddress = dealerSettings?.Address;
        string? dealerPhoneNumber = dealerSettings?.PhoneNumber;
        string clientFullName = $"{quote.Client.FirstName} {quote.Client.LastName}";

        var model = new QuoteProposalDocument(
            QuoteNumber: quote.Number,
            CarBrand: quote.Car.Marca.Nombre,
            CarModel: quote.Car.Modelo.Nombre,
            CarYear: quote.Car.Anio,
            ProposedPrice: quote.ProposedPrice.Amount,
            Currency: quote.ProposedPrice.Currency,
            PaymentMethod: quote.PaymentMethod.ToString(),
            ValidUntil: quote.ValidUntil,
            ClientFullName: clientFullName,
            Comments: quote.Comments,
            DealerName: dealerName,
            DealerAddress: dealerAddress,
            DealerPhoneNumber: dealerPhoneNumber);

        byte[] pdfBytes = await renderer.RenderAsync(DocumentTemplate.QuoteProposal, model, cancellationToken);

        int existingVersions = await context.Documents
            .Where(d => d.QuoteId == quote.Id && d.Type == DocumentType.QuoteProposal)
            .CountAsync(cancellationToken);
        int version = existingVersions + 1;

        string objectKey = $"documents/{dealerId}/quotes/{quote.Id}/propuesta-v{version}.pdf";

        using var stream = new MemoryStream(pdfBytes);
        await storageService.UploadFileAsync(stream, objectKey, "application/pdf", pdfBytes.Length, cancellationToken);

        string sha256Hash = Convert.ToHexString(SHA256.HashData(pdfBytes));

        Document document = Document.Create(
            clientId: quote.ClientId,
            type: DocumentType.QuoteProposal,
            blobName: objectKey,
            fileName: $"propuesta-v{version}.pdf",
            contentType: "application/pdf",
            dealerId: dealerId,
            saleId: null,
            quoteId: quote.Id,
            sha256Hash: sha256Hash);

        context.Documents.Add(document);

        Uri quoteUrl = await storageService.GetPresignedUrlAsync(objectKey, QuoteUrlTtl, cancellationToken);

        // Persist the document row BEFORE notifying anyone — a failed save here must not leave
        // an email in the client's inbox pointing at a Document that was never actually stored.
        await context.SaveChangesAsync(cancellationToken);

        await emailSender.SendAsync(
            EmailTemplate.QuoteSent,
            quote.Client.Email.Value,
            new Dictionary<string, string>
            {
                ["Name"] = clientFullName,
                ["DealerName"] = dealerName,
                ["QuoteNumber"] = quote.Number,
                ["QuoteUrl"] = quoteUrl.ToString(),
            },
            cancellationToken);

        // Lead timeline — resolves directly (quote.LeadId) or through the client's origin lead
        // (LeadActivityRecorder.FindLeadForQuoteAsync), same path RecordQuoteActivityOnLeadHandler
        // uses for Created/Accepted/Rejected. A quote whose client traces back to no lead at all
        // (a walk-in with no prior enquiry) legitimately resolves to null — no activity, no error.
        Lead? lead = await leadActivityRecorder.FindLeadForQuoteAsync(quote.Id, cancellationToken);
        if (lead is not null)
        {
            bool recorded = await leadActivityRecorder.RecordAsync(
                lead,
                LeadActivityType.QuoteSent,
                $"Cotización {quote.Number} enviada por email a {clientFullName}",
                dateTimeProvider.UtcNow,
                cancellationToken,
                relatedEntityId: quote.Id,
                relatedEntityType: "Quote");

            if (recorded)
            {
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        return Result.Success(new SendQuoteResult(document.Id, quoteUrl.ToString()));
    }
}
