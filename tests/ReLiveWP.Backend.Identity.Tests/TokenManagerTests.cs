using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ReLiveWP.Backend.Identity.Data;
using ReLiveWP.Backend.Identity.Services;
using ReLiveWP.Passport;
using ReLiveWP.Services.Grpc;

namespace ReLiveWP.Backend.Identity.Tests;

public class TokenManagerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LiveDbContext _db;

    public TokenManagerTests()
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

    private const string StsKey = "dW5pdCB0ZXN0IHN0cyBrZXkgMjRieXRl";

    private TokenManager Manager()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT:Secret"] = "a-test-signing-secret-that-is-long-enough-for-hmac-sha256",
                ["Passport:StsKey"] = StsKey,
            })
            .Build();

        return new TokenManager(configuration, NullLogger<TokenManager>.Instance, UserManager(), _db);
    }

    private UserManager<LiveUser> UserManager()
        => new(new UserStore<LiveUser, LiveRole, LiveDbContext, Guid>(_db),
            null!, new PasswordHasher<LiveUser>(), [], [],
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!,
            NullLogger<UserManager<LiveUser>>.Instance);

    private static LiveUser User() => new()
    {
        Id = Guid.NewGuid(),
        Cid = "0123456789abcdef",
        Puid = 0x99B2C15D02A28,
        UserName = "someone",
        Email = "someone@relivewp.net",
        Type = LiveUserType.User,
    };

    [Fact]
    public void Jwt_with_no_lifetime_lasts_a_day()
    {
        var issued = Manager().IssueJwtAsync(User(), "relivewp.net");

        var hours = (issued.Expires - issued.Created).TotalHours;
        Assert.InRange(hours, 23.9, 24.1);
    }

    [Fact]
    public async Task Device_auth_token_comes_back_as_its_user_with_the_same_session_key()
    {
        var manager = Manager();
        var user = User();
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var issued = await manager.IssueDeviceAuthTokenAsync(user);
        var authenticated = await manager.GetUserForSecurityTokenAsync(new SecurityTokensRequest { DeviceAuthToken = issued.Token });

        Assert.NotNull(authenticated);
        Assert.Equal(user.Id, authenticated!.User.Id);
        Assert.Equal(issued.ProofKey, authenticated.SessionKey);
        Assert.InRange((issued.Expires - issued.Created).TotalDays, 29.9, 30.1);
    }

    [Fact]
    public async Task Revoked_device_auth_token_is_refused()
    {
        var manager = Manager();
        var user = User();
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var issued = await manager.IssueDeviceAuthTokenAsync(user);
        await _db.LiveRefreshTokens
            .Where(t => t.ServiceTarget == TokenManager.DeviceAuthAudience)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow));

        Assert.Null(await manager.GetUserForSecurityTokenAsync(new SecurityTokensRequest { DeviceAuthToken = issued.Token }));
    }

    [Fact]
    public async Task Device_auth_token_is_not_a_bearer_token_for_services()
    {
        var manager = Manager();
        var issued = await manager.IssueDeviceAuthTokenAsync(User());
        var jwt = Encoding.UTF8.GetString(Convert.FromBase64String(issued.Token));

        var asServiceToken = await manager.ValidateJwtAsync(jwt, ["http://Passport.NET/tb", "relivewp.net"]);

        Assert.False(asServiceToken.IsValid);
    }

    [Fact]
    public async Task Sealed_device_auth_token_from_before_the_jwt_form_is_still_accepted()
    {
        var manager = Manager();
        var user = User();
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var sessionKey = DaToken.GenerateSessionKey();
        var sealedToken = DaToken.Seal(Convert.FromBase64String(StsKey), new DaTokenPayload(
            user.Puid, user.Cid, user.Email!, sessionKey, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1)));

        var authenticated = await manager.GetUserForSecurityTokenAsync(new SecurityTokensRequest { DeviceAuthToken = sealedToken });

        Assert.Equal(user.Id, authenticated?.User.Id);
        Assert.Equal(sessionKey, authenticated?.SessionKey);
    }

    [Fact]
    public async Task Sealed_device_auth_token_whose_iv_looks_like_a_jwt_is_still_accepted()
    {
        var manager = Manager();
        var user = User();
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var sessionKey = DaToken.GenerateSessionKey();
        var payload = new DaTokenPayload(
            user.Puid, user.Cid, user.Email!, sessionKey, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
        var sealedToken = SealWithIv(payload, [.. "eyJ"u8, 0, 0, 0, 0, 0]);

        var authenticated = await manager.GetUserForSecurityTokenAsync(new SecurityTokensRequest { DeviceAuthToken = sealedToken });

        Assert.Equal(user.Id, authenticated?.User.Id);
        Assert.Equal(sessionKey, authenticated?.SessionKey);
    }

    private static string SealWithIv(DaTokenPayload payload, byte[] iv)
    {
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        using var des = TripleDES.Create();
        des.Key = Convert.FromBase64String(StsKey);
        var ciphertext = des.EncryptCbc(plaintext, iv, PaddingMode.PKCS7);

        return Convert.ToBase64String([.. iv, .. ciphertext]);
    }

    [Fact]
    public void Service_proof_key_is_fixed_by_the_token_id()
    {
        var manager = Manager();

        Assert.Equal(
            manager.DeriveTokenKey(TokenKeyPurpose.ServiceProof, "jti-1"),
            manager.DeriveTokenKey(TokenKeyPurpose.ServiceProof, "jti-1"));
        Assert.NotEqual(
            manager.DeriveTokenKey(TokenKeyPurpose.ServiceProof, "jti-1"),
            manager.DeriveTokenKey(TokenKeyPurpose.DeviceSession, "jti-1"));
    }

    [Fact]
    public void Jwt_honours_an_explicit_lifetime()
    {
        var issued = Manager().IssueJwtAsync(User(), "relivewp.net", TimeSpan.FromHours(1));

        var minutes = (issued.Expires - issued.Created).TotalMinutes;
        Assert.InRange(minutes, 59, 61);
    }

    [Fact]
    public void Jwt_carries_sid_only_when_a_session_is_supplied()
    {
        var manager = Manager();
        var sessionId = Guid.NewGuid();
        var handler = new JwtSecurityTokenHandler();

        var withSession = handler.ReadJwtToken(
            manager.IssueJwtAsync(User(), "relivewp.net", null, sessionId).Token);
        var withoutSession = handler.ReadJwtToken(
            manager.IssueJwtAsync(User(), "relivewp.net").Token);

        Assert.Equal(sessionId.ToString(), withSession.Claims.Single(c => c.Type == "sid").Value);
        Assert.DoesNotContain(withoutSession.Claims, c => c.Type == "sid");
    }

    [Fact]
    public void Jwt_carries_one_role_claim_per_role()
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(
            Manager().IssueJwtAsync(User(), "relivewp.net", null, Guid.NewGuid(), ["marketplace.admin", "marketplace.reviewer"]).Token);

        var roles = token.Claims.Where(c => c.Type == TokenManager.RoleClaimType).Select(c => c.Value);
        Assert.Equal(["marketplace.admin", "marketplace.reviewer"], roles);
    }

    [Fact]
    public void Jwt_without_roles_carries_no_role_claims()
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(
            Manager().IssueJwtAsync(User(), "relivewp.net").Token);

        Assert.DoesNotContain(token.Claims, c => c.Type == TokenManager.RoleClaimType);
    }

    [Fact]
    public async Task Role_claims_survive_validation_as_identity_roles()
    {
        var manager = Manager();
        var issued = manager.IssueJwtAsync(User(), "relivewp.net", null, Guid.NewGuid(), ["marketplace.reviewer"]);

        var result = await manager.ValidateJwtAsync(issued.Token, ["relivewp.net"]);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(result.ClaimsIdentity.Claims, "LiveID"));

        Assert.True(result.IsValid);
        Assert.True(principal.IsInRole("marketplace.reviewer"));
        Assert.False(principal.IsInRole("marketplace.admin"));
    }

    [Fact]
    public async Task Replaying_a_rotated_refresh_token_revokes_the_whole_session()
    {
        var manager = Manager();
        var user = User();
        var sessionId = Guid.NewGuid();

        var first = await manager.IssueRefreshTokenAsync(user, "relivewp.net", null, sessionId);
        var second = await manager.IssueRefreshTokenAsync(user, "sync.relivewp.net", null, sessionId);

        // simulate the first having already been redeemed and rotated away
        var stored = await _db.LiveRefreshTokens.SingleAsync(t => t.ServiceTarget == "relivewp.net");
        stored.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        Assert.Null(await manager.RedeemRefreshTokenAsync(first.Token));

        var sibling = await _db.LiveRefreshTokens.AsNoTracking()
            .SingleAsync(t => t.ServiceTarget == "sync.relivewp.net");
        Assert.NotNull(sibling.RevokedAt);
        Assert.NotEqual(default, second.Expires);
    }

    [Fact]
    public async Task Revoking_a_session_leaves_other_sessions_alone()
    {
        var manager = Manager();
        var user = User();
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();

        await manager.IssueRefreshTokenAsync(user, "relivewp.net", null, mine);
        await manager.IssueRefreshTokenAsync(user, "relivewp.net", null, theirs);

        await manager.RevokeTokensForSessionAsync(mine);

        var tokens = await _db.LiveRefreshTokens.AsNoTracking().ToListAsync();
        Assert.NotNull(tokens.Single(t => t.SsoSessionId == mine).RevokedAt);
        Assert.Null(tokens.Single(t => t.SsoSessionId == theirs).RevokedAt);
    }

    private async Task<LiveUser> SignedInAsync(Guid sessionId, DateTimeOffset absoluteExpiry)
    {
        var user = User();
        _db.Users.Add(user);
        _db.LiveSsoSessions.Add(new LiveSsoSession()
        {
            Id = sessionId,
            UserId = user.Id,
            TokenHash = sessionId.ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
            ExpiresAt = absoluteExpiry,
            AbsoluteExpiresAt = absoluteExpiry,
        });
        await _db.SaveChangesAsync();

        return user;
    }

    [Fact]
    public async Task Session_bound_refresh_token_rotates_while_its_session_lives()
    {
        var manager = Manager();
        var sessionId = Guid.NewGuid();
        var user = await SignedInAsync(sessionId, DateTimeOffset.UtcNow.AddDays(30));

        var issued = await manager.IssueRefreshTokenAsync(user, "relivewp.net", null, sessionId);

        var redemption = await manager.RedeemRefreshTokenAsync(issued.Token);

        Assert.NotNull(redemption);
        Assert.Equal(sessionId, redemption!.SsoSessionId);
    }

    [Fact]
    public async Task Session_bound_refresh_token_stops_once_its_session_lapses()
    {
        var manager = Manager();
        var sessionId = Guid.NewGuid();
        var user = await SignedInAsync(sessionId, DateTimeOffset.UtcNow.AddHours(12));

        // the refresh token outlives a transient session, so the session is what has to end it
        var issued = await manager.IssueRefreshTokenAsync(user, "relivewp.net", TimeSpan.FromDays(14), sessionId);

        var session = await _db.LiveSsoSessions.SingleAsync(s => s.Id == sessionId);
        session.AbsoluteExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await _db.SaveChangesAsync();

        Assert.Null(await manager.RedeemRefreshTokenAsync(issued.Token));
    }

    [Fact]
    public async Task Session_bound_refresh_token_stops_once_its_session_is_revoked()
    {
        var manager = Manager();
        var sessionId = Guid.NewGuid();
        var user = await SignedInAsync(sessionId, DateTimeOffset.UtcNow.AddDays(30));

        var issued = await manager.IssueRefreshTokenAsync(user, "relivewp.net", null, sessionId);

        var session = await _db.LiveSsoSessions.SingleAsync(s => s.Id == sessionId);
        session.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        Assert.Null(await manager.RedeemRefreshTokenAsync(issued.Token));
    }

    [Fact]
    public async Task Replaying_a_rotated_refresh_token_revokes_its_session_too()
    {
        var manager = Manager();
        var sessionId = Guid.NewGuid();
        var user = await SignedInAsync(sessionId, DateTimeOffset.UtcNow.AddDays(30));

        var issued = await manager.IssueRefreshTokenAsync(user, "relivewp.net", null, sessionId);

        Assert.NotNull(await manager.RedeemRefreshTokenAsync(issued.Token));
        Assert.Null(await manager.RedeemRefreshTokenAsync(issued.Token));

        var session = await _db.LiveSsoSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);
        Assert.NotNull(session.RevokedAt);
    }

    [Fact]
    public async Task Web_refresh_tokens_are_shorter_lived_than_device_ones()
    {
        var manager = Manager();
        var user = User();

        var device = await manager.IssueRefreshTokenAsync(user, "relivewp.net");
        var web = await manager.IssueRefreshTokenAsync(user, "sync.relivewp.net", manager.WebRefreshTokenLifetime);

        Assert.True(web.Expires < device.Expires);
    }
}
