using Application.Abstractions;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Cars;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Leads.RegisterWhatsAppClickIntent;

/// <summary>
/// sfc C0.3 / B14: creates a minimal, visible "WhatsApp click intent" Lead.
/// <para>
/// There is no returning-visitor identity to key off here (no email, no phone — see the command's
/// doc comment), unlike <c>CreateInquiryCommandHandler</c>'s find-or-create. Each click is recorded
/// as its own Lead with a synthetic placeholder email (<see cref="Domain.Shared.ValueObjects.Email"/>
/// is a non-nullable value object) so it satisfies the aggregate's invariant without inventing a
/// false identity for the visitor. This is a deliberate judgment call: a real identity only exists
/// once the visitor actually sends the WhatsApp message, at which point the inbound webhook
/// (<c>RegisterWhatsAppInboundMessageCommand</c>) finds/creates the real Lead by phone number and
/// the sales team sees the earlier click note already sitting in the car's interest trail.
/// </para>
/// </summary>
internal sealed class RegisterWhatsAppClickIntentCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    ICurrentTenantService tenantService)
    : ICommandHandler<RegisterWhatsAppClickIntentCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RegisterWhatsAppClickIntentCommand command, CancellationToken cancellationToken)
    {
        Car? car = null;
        Guid dealerId;

        if (command.CarId.HasValue)
        {
            car = await context.Cars.SingleOrDefaultAsync(c => c.Id == command.CarId, cancellationToken);
            if (car is null)
            {
                return Result.Failure<Guid>(
                    Error.NotFound("Car.NotFound", $"Vehículo con ID {command.CarId} no encontrado."));
            }

            dealerId = car.DealerId;
        }
        else
        {
            // Same tenant-resolution rule as CreateInquiryCommandHandler: this endpoint is
            // AllowAnonymous, so there is no dealer fallback — resolve through the host, or fail.
            if (!tenantService.HasTenant || tenantService.DealerId == Guid.Empty)
            {
                return Result.Failure<Guid>(Error.Failure(
                    "Dealer.NotResolved", "No se pudo determinar la concesionaria para esta consulta."));
            }

            dealerId = tenantService.DealerId;
        }

        string placeholderEmail = $"whatsapp.intent+{Guid.NewGuid():N}@leads.carstore.internal";
        var lead = Lead.Create(
            dealerId,
            "Interesado por WhatsApp",
            placeholderEmail,
            string.Empty,
            LeadSource.WhatsApp,
            dateTimeProvider.UtcNow,
            car?.Id);

        context.Leads.Add(lead);

        string description = car is not null
            ? $"Hizo clic para chatear por WhatsApp sobre el vehículo (referencia #L{command.Code})."
            : $"Hizo clic para chatear por WhatsApp (referencia #L{command.Code}).";

        context.LeadActivities.Add(LeadActivity.Record(
            dealerId,
            lead.Id,
            LeadActivityType.NoteAdded,
            description,
            dateTimeProvider.UtcNow,
            relatedEntityId: car?.Id,
            relatedEntityType: car is not null ? "Car" : null));

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(lead.Id);
    }
}
