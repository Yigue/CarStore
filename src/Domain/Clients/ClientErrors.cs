using SharedKernel;

namespace Domain.Clients;

public static class ClientErrors
{
    public static Error AlreadySold(Guid carId) => Error.Problem(
        "Clients.AlreadySold",
        $"The client with Id = '{carId}' is already sold.");

    public static Error NotFound(Guid carId) => Error.NotFound(
        "Clients.NotFound",
        $"The client with the Id = '{carId}' was not found");
    public static Error NotAllAtributes(Guid carId) => Error.NotFound(
        "Clients.NotAllAttributes",
        $"The client with the Id = '{carId}' was not found");
}

