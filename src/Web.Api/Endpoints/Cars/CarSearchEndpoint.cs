using Application.Cars.Search;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Api.Endpoints.Cars;

[ApiController]
[Route("api/cars")]
public class CarSearchEndpoint : ControllerBase
{
    private readonly ISender _sender;

    public CarSearchEndpoint(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("search")]
    [AllowAnonymous] // Búsqueda pública, sin autenticación
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SearchCars([FromQuery] SearchCarsQuery query)
    {
        var result = await _sender.Send(query);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }
} 