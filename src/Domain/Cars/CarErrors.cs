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

    public static Error ImageLimitReached(int max) => Error.Validation(
        "CarImage.LimitReached",
        $"A car cannot have more than {max} images.");

    public static Error ImageNotFoundInCar(Guid imageId, Guid carId) => Error.Validation(
        "Image.NotFoundInCar",
        $"Image '{imageId}' does not belong to car '{carId}'.");

    public static readonly Error BlobDeleteFailed = Error.Problem(
        "CarImage.BlobDeleteFailed",
        "Failed to delete the image blob from object storage.");

    public static readonly Error CarBlobDeleteFailed = Error.Problem(
        "Car.BlobDeleteFailed",
        "Failed to delete one or more car image blobs from object storage; the delete was rolled back.");

    public static Error ReorderMismatch() => Error.Validation(
        "Image.ReorderMismatch",
        "The provided image ids do not match the car's images.");
    
    public static Error PatenteAlreadyExists(string patente) => Error.Conflict(
        "Cars.PatenteAlreadyExists",
        $"A car with license plate '{patente}' already exists");
}

