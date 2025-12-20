using System.Linq.Expressions;
using Domain.Cars;
using Domain.Cars.Atribbutes;

namespace Domain.Shared.Specifications;

public sealed class AvailableCarsSpecification : ISpecification<Car>
{
    public Expression<Func<Car, bool>> ToExpression()
    {
        return car => car.ServiceCar == statusServiceCar.Disponible;
    }
    
    public bool IsSatisfiedBy(Car car)
    {
        return car.ServiceCar == statusServiceCar.Disponible;
    }
}

