using Application.Abstractions.Messaging;
using Domain.Cars.Attributes;

namespace Application.Cars.Search;

public class SearchCarsQuery : IQuery<SearchCarsResult>
{
    public string? SearchTerm { get; set; }
    public Guid? MarcaId { get; set; }
    public Guid? ModeloId { get; set; }
    public int? YearFrom { get; set; }
    public int? YearTo { get; set; }
    public decimal? PriceFrom { get; set; }
    public decimal? PriceTo { get; set; }
    public List<int>? ColorIds { get; set; }
    public List<int>? CarTypeIds { get; set; }
    public int? DoorsFrom { get; set; }
    public int? DoorsTo { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

/// <summary>
/// A search result row.
///
/// <para>
/// Status, ServiceStatus, Featured, FuelType, Transmission, Color and CarType
/// are here because the catalog needs them and the aggregate has always carried
/// them. Without them the frontend adapter had nothing to read and filled the
/// gaps with constants (<c>Featured=false</c>, <c>ServiceStatus=Disponible</c>),
/// which silently broke everything built on top: the featured strip could never
/// match a vehicle, and inventory counters derived from search results reported
/// every car as available. A field the UI needs belongs on the payload, not in a
/// default on the other side of the wire.
/// </para>
/// </summary>
public class CarDto
{
    public Guid Id { get; set; }
    public string Marca { get; set; }
    public string Modelo { get; set; }
    public int Anio { get; set; }
    public decimal Precio { get; set; }
    public string Descripcion { get; set; }
    public string ImagenPrincipal { get; set; }
    public int CantidadPuertas { get; set; }
    public int Kilometraje { get; set; }
    public int CantidadAsientos { get; set; }
    public int Cilindrada { get; set; }
    public Color Color { get; set; }
    public TypeCar CarType { get; set; }
    public StatusCar Status { get; set; }
    public StatusServiceCar ServiceStatus { get; set; }
    public FuelType FuelType { get; set; }
    public Transmission Transmission { get; set; }
    public bool Featured { get; set; }
    public string Patente { get; set; }
}

public class SearchCarsResult
{
    public IEnumerable<CarDto> Cars { get; set; }
    public int TotalResults { get; set; }
    public int TotalPages { get; set; }
    public int CurrentPage { get; set; }
} 