using Application.Abstractions.Messaging;

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

public class CarDto
{
    public Guid Id { get; set; }
    public string Marca { get; set; }
    public string Modelo { get; set; }
    public int Año { get; set; }
    public decimal Precio { get; set; }
    public string Descripcion { get; set; }
    public string ImagenPrincipal { get; set; }
    public int CantidadPuertas { get; set; }
    public int Kilometraje { get; set; }
}

public class SearchCarsResult
{
    public IEnumerable<CarDto> Cars { get; set; }
    public int TotalResults { get; set; }
    public int TotalPages { get; set; }
    public int CurrentPage { get; set; }
} 