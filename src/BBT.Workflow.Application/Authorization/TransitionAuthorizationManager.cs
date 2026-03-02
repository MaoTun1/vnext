using BBT.Aether.Users;
using BBT.Workflow.Definitions;
using BBT.Workflow.Instances;
using WorkflowDefinition = BBT.Workflow.Definitions.Workflow;

namespace BBT.Workflow.Authorization;

/// <summary>
/// Evaluates transition-level role grants (static + $InstanceStarter / $PreviousUser).
/// DENY always wins; if no DENY match, any ALLOW match yields allowed.
/// </summary>
public sealed class TransitionAuthorizationManager(
    ICurrentUser currentUser,
    IInstanceTransitionRepository instanceTransitionRepository) : ITransitionAuthorizationManager
{
    /// <inheritdoc />
    public async Task<bool> IsTransitionAllowedForRoleAsync(
        WorkflowDefinition workflow,
        Transition transition,
        Instance? instance,
        string role,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;

        var roleGrants = transition.Roles;
        if (roleGrants.Count == 0)
            return true; // No roles defined → allow

        if (instance != null)
            return await EvaluateRolesWithPredefinedAsync(role, roleGrants, instance, cancellationToken);

        return EvaluateRoles(role, roleGrants);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> FilterAuthorizedTransitionKeysAsync(
        WorkflowDefinition workflow,
        State currentState,
        Instance? instance,
        IReadOnlyList<string> transitionKeys,
        string role,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(role) || transitionKeys.Count == 0)
            return transitionKeys;

        var result = new List<string>();
        foreach (var key in transitionKeys)
        {
            var transition = workflow.FindTransitionInContext(key);
            if (transition == null)
                continue;
            var allowed = await IsTransitionAllowedForRoleAsync(workflow, transition, instance, role, cancellationToken);
            if (allowed)
                result.Add(key);
        }
        return result;
    }

    /// <inheritdoc />
    public async Task<bool> IsRoleAllowedForGrantsAsync(
        string role,
        IReadOnlyCollection<RoleGrant> roleGrants,
        Instance? instance,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;
        if (roleGrants.Count == 0)
            return true; // No roles defined → allow
        if (instance != null)
            return await EvaluateRolesWithPredefinedAsync(role, roleGrants, instance, cancellationToken);
        return EvaluateRoles(role, roleGrants);
    }

    /// <summary>
    /// Evaluates role grants with resolution of predefined instance roles ($InstanceStarter, $PreviousUser).
    /// </summary>
    private async Task<bool> EvaluateRolesWithPredefinedAsync(
        string role,
        IReadOnlyCollection<RoleGrant> roleGrants,
        Instance instance,
        CancellationToken cancellationToken)
    {
        var normalizedRole = role.Trim();
        var currentActorUserName = currentUser.ActorUserName?.Trim();

        string? previousUserCreatedBy = null;
        if (roleGrants.Any(g => string.Equals(g.Role, PredefinedInstanceRoles.PreviousUser, StringComparison.Ordinal)))
            previousUserCreatedBy = (await instanceTransitionRepository.GetLastCompletedManualTransitionAsync(instance.Id, cancellationToken))?.CreatedBy;

        bool IsMatch(RoleGrant g)
        {
            var resolvedValue = ResolvePredefinedRole(g.Role, instance, previousUserCreatedBy);
            if (resolvedValue != null)
                return !string.IsNullOrEmpty(currentActorUserName) && string.Equals(currentActorUserName, resolvedValue, StringComparison.Ordinal);
            return string.Equals(g.Role, normalizedRole, StringComparison.OrdinalIgnoreCase);
        }

        if (roleGrants.Any(g => g.IsDeny && IsMatch(g)))
            return false;

        if (roleGrants.Any(g => g.IsAllow && IsMatch(g)))
            return true;

        return false;
    }

    /// <summary>
    /// Resolves predefined role to the effective user identifier for comparison with CurrentUser.ActorUserName.
    /// </summary>
    private static string? ResolvePredefinedRole(string? grantRole, Instance instance, string? previousUserCreatedBy)
    {
        if (string.IsNullOrWhiteSpace(grantRole))
            return null;
        if (string.Equals(grantRole, PredefinedInstanceRoles.InstanceStarter, StringComparison.Ordinal))
            return instance.CreatedBy;
        if (string.Equals(grantRole, PredefinedInstanceRoles.PreviousUser, StringComparison.Ordinal))
            return previousUserCreatedBy;
        return null;
    }

    /// <summary>
    /// Evaluates role against role grants (static only). DENY always wins; else any ALLOW match → true.
    /// </summary>
    private static bool EvaluateRoles(string role, IReadOnlyCollection<RoleGrant> roleGrants)
    {
        if (roleGrants.Count == 0)
            return true; // No roles defined → allow
        var normalizedRole = role.Trim();
        foreach (var g in roleGrants)
        {
            if (string.Equals(g.Role, normalizedRole, StringComparison.OrdinalIgnoreCase) && g.IsDeny)
                return false;
        }
        foreach (var g in roleGrants)
        {
            if (string.Equals(g.Role, normalizedRole, StringComparison.OrdinalIgnoreCase) && g.IsAllow)
                return true;
        }
        return false;
    }
}
