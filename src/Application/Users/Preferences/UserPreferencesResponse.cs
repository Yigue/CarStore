namespace Application.Users.Preferences;

public sealed record UserPreferencesResponse(
    string DefaultSort,
    string DefaultView,
    int ResultsPerPage,
    bool EmailNewMessages,
    bool EmailPriceDrops,
    bool EmailNewsletter);
