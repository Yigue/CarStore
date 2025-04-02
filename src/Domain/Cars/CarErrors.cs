using SharedKernel;

namespace Domain.Cars;

public static class CarErrors
{
    public static Error AlreadySold(Guid carId) => Error.Problem(
        "Cars.AlreadySold",
        $"The car with Id = '{carId}' is already sold.");

    public static Error NotFound(Guid carId) => Error.NotFound(
        "Cars.NotFound",
        $"The car with the Id = '{carId}' was not found");
    public static Error NotAllAtributes(Guid carId) => Error.NotFound(
        "Cars.NotAllAttributes",
        $"The car with the Id = '{carId}' was not found");
    public static Error AtributesInvalid() => Error.NotFound(
        "Cars.AtributesInvalid",
        $"Atributes are invalid");
        
    public static Error ImageNotFound(Guid imageId) => Error.NotFound(
        "Cars.ImageNotFound",
        $"The car image with the Id = '{imageId}' was not found");
}

