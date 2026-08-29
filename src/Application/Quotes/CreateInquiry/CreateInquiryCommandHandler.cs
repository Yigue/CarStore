using Application.Abstractions;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Cars;
using Domain.Leads;
using Domain.Quotes;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Quotes.CreateInquiry;

/// <summary>
/// A public enquiry produces a <see cref="Lead"/>, not a <see cref="Domain.Clients.Client"/>.
/// <para>
/// This handler used to create a Client and anchor the Quote to it. That put a stranger who had
/// merely asked about a vehicle into the system as a consolidated customer, and it disabled the
/// automation that already exists: <c>CreateClientFromLeadOnNegociacionHandler</c>
/// (REQ-CRM-PROSPECT-001) creates the Client when a Lead reaches Negociación, which never fired
/// because the Client already existed.
/// </para>
/// <para>
/// The Client now appears on its own at Negociación, as designed.
/// </para>
/// </summary>
internal sealed class CreateInquiryCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    ICurrentTenantService tenantService,
    IRoundRobinLeadAllocator allocator)
    : ICommandHandler<CreateInquiryCommand, Guid>
{
    /// <summary>Stages where the lead is closed and a fresh enquiry deserves a fresh lead.</summary>
    private static readonly LeadStatus[] ClosedStatuses =
        [LeadStatus.Ganado, LeadStatus.Perdido, LeadStatus.Archivado];

    public async Task<Result<Guid>> Handle(CreateInquiryCommand command, CancellationToken cancellationToken)
    {
        Car? car = null;
        Guid dealerId;

        if (command.CarId.HasValue)
        {
            car = await context.Cars
                .SingleOrDefaultAsync(c => c.Id == command.CarId, cancellationToken);

            if (car is null)
            {
                return Result.Failure<Guid>(
                    Error.NotFound("Car.NotFound", $"Vehículo con ID {command.CarId} no encontrado."));
            }

            dealerId = car.DealerId;
        }
        else
        {
            // A general enquiry used to take `DealerSettings.FirstOrDefaultAsync()` — "the first
            // configured dealer". CurrentTenantService documents that this exact fallback was
            // removed from tenant resolution because it leaked cross-tenant data in production
            // (saas-custom-domains ADR-1); it must not survive here. The tenant service resolves
            // anonymous requests through X-Tenant-Host → Origin → Host, so ask it, and fail
            // loudly rather than guessing a dealership.
            if (!tenantService.HasTenant || tenantService.DealerId == Guid.Empty)
            {
                return Result.Failure<Guid>(Error.Failure(
                    "Dealer.NotResolved",
                    "No se pudo determinar la concesionaria para esta consulta."));
            }

            dealerId = tenantService.DealerId;
        }

        // Validated upstream by CreateInquiryCommandValidator; the value object is the last gate.
        Email inquiryEmail;
        try
        {
            inquiryEmail = new Email(command.Email);
        }
        catch (DomainException ex)
        {
            return Result.Failure<Guid>(Error.Validation("Email.Invalid", ex.Message));
        }

        Lead lead = await FindOrCreateLeadAsync(command, dealerId, inquiryEmail, car, cancellationToken);

        if (car is not null)
        {
            // The Quote's invariant is "either a Client or a Lead, never both". Anchoring it to
            // the lead is what makes the enquiry visible in the CRM pipeline.
            context.Quotes.Add(new Quote(
                dealerId,
                car,
                client: null,
                lead: lead,
                car.Price.Amount,
                Domain.Quotes.Attributes.PaymentMethod.Contado,
                dateTimeProvider.UtcNow.AddDays(30),
                command.Comments,
                dateTimeProvider.UtcNow));
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(lead.Id);
    }

    private async Task<Lead> FindOrCreateLeadAsync(
        CreateInquiryCommand command,
        Guid dealerId,
        Email inquiryEmail,
        Car? car,
        CancellationToken cancellationToken)
    {
        // Scope by DealerId explicitly: this endpoint is AllowAnonymous, and when host resolution
        // misses, HasTenant is false and the global filter on Lead is disabled for the whole
        // request — an unscoped lookup would reach into every other dealership.
        Lead? existing = await context.Leads
            .Where(l => l.DealerId == dealerId
                        && l.Email == inquiryEmail
                        && !ClosedStatuses.Contains(l.Status))
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            // Someone enquiring twice is one prospect, not two. Append rather than duplicate, so
            // the agent sees one thread instead of a pipeline full of the same person.
            AppendComments(existing, command.Comments, dateTimeProvider.UtcNow);

            if (existing.InterestedVehicleId is null && car is not null)
            {
                existing.LinkVehicle(car.Id);
            }

            return existing;
        }

        var lead = Lead.Create(
            dealerId,
            $"{command.FirstName} {command.LastName}".Trim(),
            command.Email,
            command.Phone,
            LeadSource.Web,
            dateTimeProvider.UtcNow,
            car?.Id);

        if (!string.IsNullOrWhiteSpace(command.Comments))
        {
            lead.UpdateNotes(command.Comments);
        }

        // Same allocator the dashboard uses. It takes dealerId explicitly, which is why it works
        // here — CreateLeadCommandHandler does not, because it reads the dealer off the tenant
        // service and this request may be anonymous.
        Guid? agentId = await allocator.AllocateAsync(dealerId, cancellationToken);
        if (agentId.HasValue)
        {
            lead.AssignAgent(agentId.Value);
        }

        context.Leads.Add(lead);
        return lead;
    }

    private static void AppendComments(Lead lead, string comments, DateTime occurredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(comments))
        {
            return;
        }

        string entry = $"[{occurredAtUtc:yyyy-MM-dd HH:mm}] {comments}";

        lead.UpdateNotes(string.IsNullOrWhiteSpace(lead.Notes)
            ? entry
            : $"{lead.Notes}\n{entry}");
    }
}
