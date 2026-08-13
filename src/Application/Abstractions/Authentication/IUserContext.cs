namespace Application.Abstractions.Authentication;

public interface IUserContext
{
    Guid UserId { get; }

    /// <summary>
    /// True when the caller is a dealership administrator.
    /// </summary>
    /// <remarks>
    /// Gates fields that are commercially sensitive rather than merely private —
    /// today that is <c>Car.PurchaseCost</c>, the cost basis behind every margin.
    /// <para>
    /// The default is <c>false</c> on purpose, and it is a default rather than a
    /// required member for the same reason: adding a required member would break
    /// every existing test double, and whoever fixed them in a hurry would be
    /// choosing a value under pressure. Failing closed means a context that does
    /// not know the caller's role hides the cost instead of leaking it.
    /// </para>
    /// </remarks>
    bool IsAdmin => false;
}
