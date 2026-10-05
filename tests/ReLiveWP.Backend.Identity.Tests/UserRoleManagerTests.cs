using Grpc.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ReLiveWP.Backend.Identity.Data;
using ReLiveWP.Backend.Identity.Services;

namespace ReLiveWP.Backend.Identity.Tests;

public class UserRoleManagerTests : IDisposable
{
    private const string Reviewer = "marketplace.reviewer";
    private const string Admin = "marketplace.admin";

    private readonly SqliteConnection _connection;
    private readonly LiveDbContext _db;

    public UserRoleManagerTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _db = new LiveDbContext(
            new DbContextOptionsBuilder<LiveDbContext>().UseSqlite(_connection).Options);

        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private UserRoleManager Manager(Dictionary<string, string?>? extraConfig = null, string[]? knownRoles = null)
    {
        var config = new Dictionary<string, string?>();

        var known = knownRoles ?? [Reviewer, Admin];
        for (var i = 0; i < known.Length; i++)
            config[$"Roles:Known:{i}"] = known[i];

        foreach (var (key, value) in extraConfig ?? [])
            config[key] = value;

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(config).Build();
        var normalizer = new UpperInvariantLookupNormalizer();

        var userManager = new UserManager<LiveUser>(
            new UserStore<LiveUser, LiveRole, LiveDbContext, Guid>(_db),
            null!, new PasswordHasher<LiveUser>(), [], [],
            normalizer, new IdentityErrorDescriber(), null!,
            NullLogger<UserManager<LiveUser>>.Instance);

        var roleManager = new RoleManager<LiveRole>(
            new RoleStore<LiveRole, LiveDbContext, Guid>(_db),
            [new RoleValidator<LiveRole>()],
            normalizer, new IdentityErrorDescriber(),
            NullLogger<RoleManager<LiveRole>>.Instance);

        return new UserRoleManager(userManager, roleManager, configuration, NullLogger<UserRoleManager>.Instance);
    }

    private async Task<LiveUser> AddUserAsync(string username, LiveUserType type = LiveUserType.User)
    {
        var user = new LiveUser()
        {
            Id = Guid.NewGuid(),
            Cid = Guid.NewGuid().ToString("N")[..16],
            Puid = Random.Shared.NextInt64(),
            UserName = username,
            NormalizedUserName = username.ToUpperInvariant(),
            Email = $"{username}@relivewp.net",
            NormalizedEmail = $"{username}@relivewp.net".ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
            Type = type,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<UserRoleManager> ProvisionedAsync(Dictionary<string, string?>? extraConfig = null)
    {
        var manager = Manager(extraConfig);
        await manager.ProvisionAsync();
        return manager;
    }

    [Fact]
    public async Task Provisioning_creates_every_known_role_and_is_safe_to_repeat()
    {
        await (await ProvisionedAsync()).ProvisionAsync();

        var roles = await _db.Roles.AsNoTracking().Select(r => r.Name).OrderBy(n => n).ToListAsync();
        Assert.Equal([Admin, Reviewer], roles);
    }

    [Fact]
    public async Task Bootstrap_grants_listed_users_and_skips_the_rest()
    {
        var owner = await AddUserAsync("owner");
        var device = await AddUserAsync("phone", LiveUserType.Device);

        var manager = await ProvisionedAsync(new()
        {
            [$"Roles:Bootstrap:{Admin}:0"] = "owner",
            [$"Roles:Bootstrap:{Admin}:1"] = "phone",
            [$"Roles:Bootstrap:{Admin}:2"] = "nobody",
            ["Roles:Bootstrap:made.up:0"] = "owner",
        });

        Assert.Equal([Admin], await manager.GetRolesAsync(owner.Id.ToString()));
        Assert.Empty(await manager.GetRolesAsync(device.Id.ToString()));
        Assert.DoesNotContain(await _db.Roles.AsNoTracking().ToListAsync(), r => r.Name == "made.up");
    }

    [Fact]
    public async Task Adding_a_role_twice_leaves_one_grant()
    {
        var user = await AddUserAsync("someone");
        var manager = await ProvisionedAsync();

        await manager.AddRoleAsync(user.Id.ToString(), Reviewer);
        var roles = await manager.AddRoleAsync(user.Id.ToString(), Reviewer);

        Assert.Equal([Reviewer], roles);
        Assert.Equal(1, await _db.UserRoles.CountAsync(r => r.UserId == user.Id));
    }

    [Fact]
    public async Task Unknown_roles_are_refused_and_never_created()
    {
        var user = await AddUserAsync("someone");
        var manager = await ProvisionedAsync();

        var ex = await Assert.ThrowsAsync<RpcException>(() => manager.AddRoleAsync(user.Id.ToString(), "made.up"));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
        Assert.DoesNotContain(await _db.Roles.AsNoTracking().ToListAsync(), r => r.Name == "made.up");
    }

    [Fact]
    public async Task Device_accounts_cannot_hold_roles()
    {
        var device = await AddUserAsync("phone", LiveUserType.Device);
        var manager = await ProvisionedAsync();

        var ex = await Assert.ThrowsAsync<RpcException>(() => manager.AddRoleAsync(device.Id.ToString(), Admin));

        Assert.Equal(StatusCode.FailedPrecondition, ex.StatusCode);
    }

    [Fact]
    public async Task Missing_users_are_not_found()
    {
        var manager = await ProvisionedAsync();

        var ex = await Assert.ThrowsAsync<RpcException>(() => manager.AddRoleAsync(Guid.NewGuid().ToString(), Admin));

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task Removing_a_role_drops_it_and_removing_again_is_fine()
    {
        var user = await AddUserAsync("someone");
        var manager = await ProvisionedAsync();
        await manager.AddRoleAsync(user.Id.ToString(), Reviewer);
        await manager.AddRoleAsync(user.Id.ToString(), Admin);

        await manager.RemoveRoleAsync(user.Id.ToString(), Admin);
        var roles = await manager.RemoveRoleAsync(user.Id.ToString(), Admin);

        Assert.Equal([Reviewer], roles);
    }

    [Fact]
    public async Task Roles_dropped_from_config_stop_being_reported()
    {
        var user = await AddUserAsync("someone");
        var manager = await ProvisionedAsync();
        await manager.AddRoleAsync(user.Id.ToString(), Reviewer);
        await manager.AddRoleAsync(user.Id.ToString(), Admin);

        var narrowed = Manager(knownRoles: [Reviewer]);

        Assert.Equal([Reviewer], await narrowed.GetRolesAsync(user.Id.ToString()));
    }

    [Fact]
    public async Task Users_in_role_lists_only_holders()
    {
        var first = await AddUserAsync("bravo");
        var second = await AddUserAsync("alpha");
        await AddUserAsync("charlie");
        var manager = await ProvisionedAsync();
        await manager.AddRoleAsync(first.Id.ToString(), Admin);
        await manager.AddRoleAsync(second.Id.ToString(), Admin);

        var holders = await manager.GetUsersInRoleAsync(Admin);

        Assert.Equal(["alpha", "bravo"], holders.Select(u => u.UserName));
    }
}
