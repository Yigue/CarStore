using System.Linq;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Application.UnitTests.Catalog;

public class ModelosCreateTests
{
    [Fact]
    public void MapEndpoint_Should_RequirePlatformCatalogManagePermission()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<ISender>(_ => Mock.Of<ISender>());
        var app = builder.Build();

        var endpoint = new Web.Api.Endpoints.Modelos.Create();
        endpoint.MapEndpoint(app);

        // Act
        var modeloEndpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .FirstOrDefault(e => e.RoutePattern.RawText?.Contains("modelos") == true);

        // Assert
        modeloEndpoint.Should().NotBeNull();
        var authorizeData = modeloEndpoint!.Metadata.OfType<IAuthorizeData>().ToList();
        authorizeData.Should().NotBeEmpty();
        authorizeData.Should().Contain(a => a.Policy == "platform:catalog:manage",
            "creating a model requires platform:catalog:manage permission to return 403 for unauthorized users");
    }
}
