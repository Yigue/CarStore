namespace Domain.Services;

public static class DecimalMath
{
    /// <summary>Raises <paramref name="value"/> to a non-negative integer power using exponentiation by squaring.</summary>
    public static decimal PowInt(decimal value, int exponent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(exponent);

        decimal result = 1m;
        decimal factor = value;
        int remaining = exponent;

        while (remaining > 0)
        {
            if ((remaining & 1) == 1)
            {
                result *= factor;
            }

            remaining >>= 1;
            if (remaining > 0)
            {
                factor *= factor;
            }
        }

        return result;
    }
}
