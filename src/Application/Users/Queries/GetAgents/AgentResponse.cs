namespace Application.Users.Queries.GetAgents;

public sealed record AgentResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string Role
);
