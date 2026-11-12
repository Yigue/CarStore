using Application.Abstractions.Data;
using Application.Abstractions.Integrations;
using Domain.Integrations;
using Domain.Leads;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Integrations.Meta;

public enum MetaLeadOutcome
{
    /// <summary>A brand-new Lead was created.</summary>
    Created,

    /// <summary>An existing open Lead for the same dealer/email got this as its first touch (or another follow-up).</summary>
    Appended,

    /// <summary>Same <c>leadgen_id</c> seen before (Meta retry) — no-op.</summary>
    Duplicate,

    /// <summary>No connected, credentialed <c>meta-leads</c> integration maps to this Page.</summary>
    DealerNotConnected,

    /// <summary>Graph API returned nothing usable (no field data, or no valid email).</summary>
    InvalidLeadData,
}

/// <summary>
/// sfc C1.4 / B13: turns a Meta Lead Ads webhook "leadgen" change into a <see cref="Lead"/>, with the same
/// create-or-append-by-open-lead shape as <c>Application.Quotes.CreateInquiry.CreateInquiryCommandHandler</c> (this
/// PR's allowed edit surface does not include Application/**, so the shape is mirrored here rather than reused).
/// </summary>
public sealed class MetaLeadsWebhookHandler
{
    /// <summary>
    /// sfc C1.1 / ADR C22: the <see cref="DealerIntegration"/> provider key for this connector.
    /// <see cref="DealerIntegration.Scopes"/> doubles, for this provider only, as the connected Facebook Page ID —
    /// <c>DealerIntegration</c> (Domain/Integrations) is outside this PR's allowed edit surface, so no dedicated
    /// "external account id" column was added; a follow-up should promote this to a real field.
    /// </summary>
    public const string Provider = "meta-leads";

    private static readonly LeadStatus[] ClosedStatuses = [LeadStatus.Ganado, LeadStatus.Perdido, LeadStatus.Archivado];

    private readonly IApplicationDbContext _context;
    private readonly ICredentialProtector _credentialProtector;
    private readonly IMetaGraphClient _graphClient;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger _logger;

    public MetaLeadsWebhookHandler(
        IApplicationDbContext context,
        ICredentialProtector credentialProtector,
        IMetaGraphClient graphClient,
        IDateTimeProvider clock,
        ILogger logger)
    {
        _context = context;
        _credentialProtector = credentialProtector;
        _graphClient = graphClient;
        _clock = clock;
        _logger = logger;
    }

    public async Task<MetaLeadOutcome> HandleLeadgenChangeAsync(string pageId, string leadgenId, CancellationToken cancellationToken)
    {
        var dedup = new ProcessedMetaLeadEventRepository(_context);

        DealerIntegration? integration = await _context.DealerIntegrations
            .Where(i => i.Provider == Provider
                        && i.Status == IntegrationStatus.Connected
                        && i.Scopes == pageId)
            .FirstOrDefaultAsync(cancellationToken);

        if (integration?.EncryptedCredentials is null)
        {
            _logger.LogWarning(
                "Meta Leads webhook: no connected {Provider} integration for page {PageId}; lead {LeadgenId} ignored.",
                Provider, pageId, leadgenId);
            return MetaLeadOutcome.DealerNotConnected;
        }

        bool isNew = await dedup.TryAddAsync(leadgenId, integration.DealerId, _clock.UtcNow, cancellationToken);
        if (!isNew)
        {
            _logger.LogInformation("Meta Leads webhook: leadgen_id {LeadgenId} already processed; retry ignored.", leadgenId);
            return MetaLeadOutcome.Duplicate;
        }

        string pageAccessToken = _credentialProtector.Unprotect(integration.EncryptedCredentials);
        MetaLeadFieldData? fields = await _graphClient.GetLeadAsync(leadgenId, pageAccessToken, cancellationToken);

        if (fields is null || string.IsNullOrWhiteSpace(fields.Email))
        {
            _logger.LogWarning("Meta Leads webhook: leadgen_id {LeadgenId} has no usable field data.", leadgenId);
            // Row stays committed so a Meta retry of the same unusable lead never reprocesses it.
            await _context.SaveChangesAsync(cancellationToken);
            return MetaLeadOutcome.InvalidLeadData;
        }

        Email email;
        try
        {
            email = new Email(fields.Email);
        }
        catch (DomainException)
        {
            _logger.LogWarning("Meta Leads webhook: leadgen_id {LeadgenId} has an invalid email.", leadgenId);
            await _context.SaveChangesAsync(cancellationToken);
            return MetaLeadOutcome.InvalidLeadData;
        }

        LeadSource source = string.Equals(fields.Platform, "ig", StringComparison.OrdinalIgnoreCase)
            ? LeadSource.Instagram
            : LeadSource.Facebook;

        LeadAttribution attribution = LeadAttribution.Create(
            utmSource: source == LeadSource.Instagram ? "instagram" : "facebook",
            utmMedium: "lead-ad",
            utmCampaign: fields.CampaignName ?? fields.CampaignId,
            utmTerm: fields.AdsetId,
            utmContent: fields.AdId,
            referrer: fields.FormId is null ? null : $"meta-form:{fields.FormId}",
            landingPath: null,
            gclid: null,
            fbclid: null,
            firstTouchAtUtc: _clock.UtcNow);

        Lead? existing = await _context.Leads
            .Where(l => l.DealerId == integration.DealerId && l.Email == email && !ClosedStatuses.Contains(l.Status))
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        MetaLeadOutcome outcome;
        Guid leadId;

        if (existing is not null)
        {
            // Same prospect reaching out again; one thread, not a second lead (CreateInquiryCommandHandler's rule).
            existing.SetFirstTouch(attribution);
            leadId = existing.Id;
            outcome = MetaLeadOutcome.Appended;
        }
        else
        {
            Lead lead = Lead.Create(
                integration.DealerId,
                string.IsNullOrWhiteSpace(fields.FullName) ? "Lead de Meta" : fields.FullName,
                fields.Email,
                fields.Phone ?? string.Empty,
                source,
                _clock.UtcNow);
            lead.SetFirstTouch(attribution);

            _context.Leads.Add(lead);
            leadId = lead.Id;
            outcome = MetaLeadOutcome.Created;
        }

        dedup.LinkLead(leadgenId, leadId);
        await _context.SaveChangesAsync(cancellationToken);
        return outcome;
    }
}
