using Application.Abstractions.Messaging;

namespace Application.Cars.Delete;

public sealed class DeleteCarImageCommand : ICommand
{
    public Guid ImageId { get; set; }
} 