using FluentValidation;

namespace Application.Appointments.Commands.CreateAppointment;

public sealed class CreateAppointmentCommandValidator : AbstractValidator<CreateAppointmentCommand>
{
    public CreateAppointmentCommandValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.AgentId).NotEmpty();
        RuleFor(x => x.End)
            .GreaterThan(x => x.Start)
            .WithMessage("La hora de fin debe ser posterior a la hora de inicio.");
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Notes).MaximumLength(2000).When(x => x.Notes is not null);
    }
}
