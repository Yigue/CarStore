namespace Domain.Cars.Atribbutes;
public class ImagenesComparer : IEqualityComparer<string>
{
    public bool Equals(string? x, string? y)
    {
        return x == y;
    }

    public int GetHashCode(string obj)
    {
        return obj.GetHashCode();
    }

   
}