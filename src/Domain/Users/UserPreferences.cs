using SharedKernel;

namespace Domain.Users;

/// <summary>
/// sfc B5.3 / ADR C21: one row per user with a versioned JSON document, so the UI can add preferences without a column
/// per setting. <see cref="SchemaVersion"/> names the shape of <see cref="Preferences"/>.
/// </summary>
public sealed class UserPreferences
{
    public const int CurrentSchemaVersion = 1;

    public Guid UserId { get; private set; }
    public int SchemaVersion { get; private set; }
    public PreferencesV1 Preferences { get; private set; } = PreferencesV1.Default;

    private UserPreferences() { }

    public static UserPreferences Create(Guid userId, PreferencesV1 preferences) =>
        new() { UserId = userId, SchemaVersion = CurrentSchemaVersion, Preferences = preferences };

    public void Update(PreferencesV1 preferences)
    {
        Preferences = preferences;
        SchemaVersion = CurrentSchemaVersion;
    }
}

/// <summary>Version 1 of the preferences document: catalogue defaults and notification opt-ins.</summary>
public sealed record PreferencesV1(
    string DefaultSort,
    string DefaultView,
    int ResultsPerPage,
    bool EmailNewMessages,
    bool EmailPriceDrops,
    bool EmailNewsletter)
{
    public static readonly IReadOnlyList<string> SortOptions = ["relevance", "price_asc", "price_desc", "year_desc", "year_asc"];
    public static readonly IReadOnlyList<string> ViewOptions = ["grid", "list"];
    public static readonly IReadOnlyList<int> ResultsPerPageOptions = [12, 24, 48];

    public static PreferencesV1 Default { get; } = new("price_asc", "grid", 12, true, false, true);

    /// <summary>Accepts exactly the options the UI offers.</summary>
    public static Result<PreferencesV1> Create(
        string defaultSort, string defaultView, int resultsPerPage,
        bool emailNewMessages, bool emailPriceDrops, bool emailNewsletter)
    {
        if (!SortOptions.Contains(defaultSort) || !ViewOptions.Contains(defaultView) || !ResultsPerPageOptions.Contains(resultsPerPage))
            return Result.Failure<PreferencesV1>(Error.Validation(
                "Preferences.Invalid", "One or more preferences are not among the supported options."));

        return Result.Success(new PreferencesV1(defaultSort, defaultView, resultsPerPage, emailNewMessages, emailPriceDrops, emailNewsletter));
    }
}
