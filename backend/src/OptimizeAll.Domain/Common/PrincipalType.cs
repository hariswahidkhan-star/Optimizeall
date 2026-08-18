namespace OptimizeAll.Domain.Common;

/// <summary>
/// What kind of actor performed something. Distinguishing agents from users is load-bearing:
/// several rules in the platform ("an agent may never approve", "the requester may not decide")
/// depend on knowing the actor is not human.
/// </summary>
public enum PrincipalType
{
    User = 1,
    Agent = 2,
    ServiceClient = 3,
    System = 4,
}

/// <summary>An actor identity, independent of which table the identity happens to live in.</summary>
public readonly record struct PrincipalRef(PrincipalType Type, Guid Id)
{
    public static PrincipalRef ForUser(UserId userId) => new(PrincipalType.User, userId.Value);

    public static PrincipalRef ForAgent(AgentDefinitionId agentId) => new(PrincipalType.Agent, agentId.Value);

    public static PrincipalRef System { get; } = new(PrincipalType.System, Guid.Empty);

    public bool IsHuman => Type == PrincipalType.User;

    public override string ToString() => $"{Type}:{Id}";
}
