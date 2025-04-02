using Application.Abstractions.Messaging;

namespace Application.Cars.Update;

public sealed class SetPrimaryCarImageCommand : ICommand
{
    public Guid CarId { get; set; }
    public Guid ImageId { get; set; }
} 