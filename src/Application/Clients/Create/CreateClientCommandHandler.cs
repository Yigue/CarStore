using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Clients;
using SharedKernel;

namespace Application.Clients.Create;

internal sealed class CreateClientCommandHandler(
    IApplicationDbContext context)
    : ICommandHandler<CreateClientCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateClientCommand command, CancellationToken cancellationToken)
    {
        var client = new Client(
            command.FirstName,
            command.LastName,
            command.DNI,
            command.Email,
            command.Phone,
            command.Address);

        context.Clients.Add(client);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(client.Id);
    }
}
