using Grpc.Core;
using Microsoft.AspNetCore.Identity;
using ReLiveWP.Backend.Identity.Data;

namespace ReLiveWP.Backend.Identity.Services;

public class UserRoleManager(
    UserManager<LiveUser> userManager,
    RoleManager<LiveRole> roleManager,
    IConfiguration configuration,
    ILogger<UserRoleManager> logger)
{
    public IReadOnlyList<string> KnownRoles =>
        configuration.GetSection("Roles:Known").Get<string[]>() ?? [];

    public bool IsKnown(string role) =>
        KnownRoles.Contains(role, StringComparer.Ordinal);

    public async Task ProvisionAsync()
    {
        foreach (var role in KnownRoles)
        {
            if (await roleManager.RoleExistsAsync(role))
                continue;

            var created = await roleManager.CreateAsync(new LiveRole() { Name = role });
            if (!created.Succeeded)
                logger.LogError("Could not create role {Role}: {Errors}", role, Describe(created));
        }

        foreach (var bootstrap in configuration.GetSection("Roles:Bootstrap").GetChildren())
        {
            var role = bootstrap.Key;
            if (!IsKnown(role))
            {
                logger.LogWarning("Ignoring bootstrap entries for unknown role {Role}", role);
                continue;
            }

            foreach (var username in bootstrap.Get<string[]>() ?? [])
                await BootstrapUserAsync(username, role);
        }
    }

    private async Task BootstrapUserAsync(string username, string role)
    {
        var user = await userManager.FindByNameAsync(username);
        if (user?.Type != LiveUserType.User)
        {
            logger.LogWarning("Cannot bootstrap {User} into {Role}, no such user account", username, role);
            return;
        }

        if (await userManager.IsInRoleAsync(user, role))
            return;

        var added = await userManager.AddToRoleAsync(user, role);
        if (added.Succeeded)
            logger.LogInformation("Bootstrapped {User} into {Role}", username, role);
        else
            logger.LogError("Could not bootstrap {User} into {Role}: {Errors}", username, role, Describe(added));
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(LiveUser user)
    {
        var assigned = await userManager.GetRolesAsync(user);
        return [.. assigned.Where(IsKnown).Order(StringComparer.Ordinal)];
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(string userId)
    {
        var user = await FindUserAsync(userId);
        return await GetRolesAsync(user);
    }

    public async Task<IReadOnlyList<string>> AddRoleAsync(string userId, string role)
    {
        RequireKnown(role);

        var user = await FindUserAsync(userId);
        if (user.Type != LiveUserType.User)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Only user accounts can hold roles."));

        if (!await userManager.IsInRoleAsync(user, role))
        {
            var added = await userManager.AddToRoleAsync(user, role);
            if (!added.Succeeded)
                throw new RpcException(new Status(StatusCode.Internal, Describe(added)));

            logger.LogInformation("Granted {Role} to {User}", role, user.Id);
        }

        return await GetRolesAsync(user);
    }

    public async Task<IReadOnlyList<string>> RemoveRoleAsync(string userId, string role)
    {
        RequireKnown(role);

        var user = await FindUserAsync(userId);
        if (await userManager.IsInRoleAsync(user, role))
        {
            var removed = await userManager.RemoveFromRoleAsync(user, role);
            if (!removed.Succeeded)
                throw new RpcException(new Status(StatusCode.Internal, Describe(removed)));

            logger.LogInformation("Revoked {Role} from {User}", role, user.Id);
        }

        return await GetRolesAsync(user);
    }

    public async Task<IReadOnlyList<LiveUser>> GetUsersInRoleAsync(string role)
    {
        RequireKnown(role);

        var users = await userManager.GetUsersInRoleAsync(role);
        return [.. users.OrderBy(u => u.UserName, StringComparer.OrdinalIgnoreCase)];
    }

    private void RequireKnown(string role)
    {
        if (!IsKnown(role))
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Unknown role '{role}'."));
    }

    private async Task<LiveUser> FindUserAsync(string userId) =>
        await userManager.FindByIdAsync(userId)
            ?? throw new RpcException(new Status(StatusCode.NotFound, "User does not exist."));

    private static string Describe(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(e => e.Description));
}
