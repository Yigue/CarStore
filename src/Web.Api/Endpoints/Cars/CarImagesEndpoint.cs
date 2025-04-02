using Application.Cars.Delete;
using Application.Cars.Update;
using Application.Cars.Upload;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Api.Endpoints.Cars;

[ApiController]
[Route("cars/{carId:guid}/images")]
// [Authorize]
[Tags("Car Images")]
public class CarImagesEndpoint : ControllerBase
{
    private readonly ISender _sender;

    public CarImagesEndpoint(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [DisableRequestSizeLimit]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadImage(
        [FromRoute] Guid carId,
        [FromForm] IFormFile file,
        [FromForm] bool isPrimary,
        [FromForm] int order)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded");
        }

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
            
        var command = new UploadImageCarCommand
        {
            CarId = carId,
            FileName = file.FileName,
            ImageData = memoryStream.ToArray(),
            IsPrimary = isPrimary,
            Order = order
        };

        var result = await _sender.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return Created($"/api/cars/{carId}/images/{result.Value}", result.Value);
    }

    [HttpDelete("{imageId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteImage(
        [FromRoute] Guid carId,
        [FromRoute] Guid imageId)
    {
        var command = new DeleteCarImageCommand { ImageId = imageId };
        var result = await _sender.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }

    [HttpPut("{imageId:guid}/make-primary")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MakePrimary(
        [FromRoute] Guid carId,
        [FromRoute] Guid imageId)
    {
        var command = new SetPrimaryCarImageCommand 
        { 
            CarId = carId,
            ImageId = imageId 
        };
        var result = await _sender.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }
} 