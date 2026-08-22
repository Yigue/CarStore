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

    /// <summary>
    /// True when the request carries a verified identity.
    /// </summary>
    /// <remarks>
    /// Gates fields that are private rather than commercially sensitive. Today
    /// that is <c>Car.Patente</c>: the licence plate identifies one physical
    /// vehicle out on the street, which is a different kind of secret from
    /// <see cref="IsAdmin"/>'s cost basis, and a different cut — a salesperson
    /// needs the plate, so the line is drawn at "signed in", not "admin".
    /// <para>
    /// Defaults to <c>false</c> for the same reason as <see cref="IsAdmin"/>:
    /// a context that cannot tell who is calling must hide the field rather
    /// than leak it, and a default keeps existing test doubles compiling
    /// instead of forcing a hurried choice of value.
    /// </para>
    /// </remarks>
    bool IsAuthenticated => false;
}
