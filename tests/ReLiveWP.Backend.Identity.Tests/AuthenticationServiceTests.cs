using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ReLiveWP.Backend.Identity.Data;
using ReLiveWP.Backend.Identity.Grpc;
using ReLiveWP.Backend.Identity.Services;
using ReLiveWP.Services.Grpc;

namespace ReLiveWP.Backend.Identity.Tests;

public class AuthenticationServiceTests
{
    private static TokenManager Manager()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT:Secret"] = "a-test-signing-secret-that-is-long-enough-for-hmac-sha256",
            })
            .Build();

        return new TokenManager(configuration, NullLogger<TokenManager>.Instance, null!, null!);
    }

    private static AuthenticationService Service(TokenManager tokenManager)
        => new(tokenManager, null!, null!, null!, null!, null!, null!, NullLogger<AuthenticationService>.Instance);

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
    public async Task Verify_returns_every_role_a_token_carries()
    {
        var manager = Manager();
        var issued = manager.IssueJwtAsync(User(), "relivewp.net", null, Guid.NewGuid(), ["marketplace.admin", "marketplace.reviewer"]);
        var request = new VerifyTokenRequest { Token = issued.Token, TokenType = "JWT", ServiceTargets = { "relivewp.net" } };

        var reply = await Service(manager).VerifySecurityToken(request, null!);

        var roles = reply.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value);
        Assert.Equal(["marketplace.admin", "marketplace.reviewer"], roles);
    }

    [Fact]
    public async Task Verify_still_returns_single_valued_claims_once()
    {
        var manager = Manager();
        var user = User();
        var issued = manager.IssueJwtAsync(user, "relivewp.net");
        var request = new VerifyTokenRequest { Token = issued.Token, TokenType = "JWT", ServiceTargets = { "relivewp.net" } };

        var reply = await Service(manager).VerifySecurityToken(request, null!);

        var email = Assert.Single(reply.Claims, c => c.Type == ClaimTypes.Email);
        Assert.Equal(user.Email, email.Value);
        Assert.Equal(user.Id.ToString(), reply.Id);
    }
}
