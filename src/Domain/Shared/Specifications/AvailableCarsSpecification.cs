using System.Linq.Expressions;
using Domain.Cars;
using Domain.Cars.Attributes;

namespace Domain.Shared.Specifications;

public sealed class AvailableCarsSpecification : ISpecification<Car>
{
    public Expression<Func<Car, bool>> ToExpression()
    {
        return car => car.ServiceCar == StatusServiceCar.Disponible;
    }
    
    public bool IsSatisfiedBy(Car car)
    {
        return car.ServiceCar == StatusServiceCar.Disponible;
    }
}

