using BuildingBlocks.Hosting;
using System.Security.Claims;

namespace AutoHub.BuildingBlocks.Tests;

public sealed class OwnerScopeExtensionsTests
{
    [Fact]
    public void TryGetOwnerScope_Client_ReturnsOwnerFilter()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "client1"),
            new Claim(ClaimTypes.Role, "Client")
        ], "test"));

        var ok = OwnerScopeExtensions.TryGetOwnerScope(principal, out var deny, out var filter);
        Assert.True(ok);
        Assert.Null(deny);
        Assert.Equal("client1", filter);
    }

    [Fact]
    public void CanAccessByOwner_Client_AllowsOwnRecordOnly()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "client1"),
            new Claim(ClaimTypes.Role, "Client")
        ], "test"));

        Assert.True(OwnerScopeExtensions.CanAccessByOwner(principal, "client1"));
        Assert.False(OwnerScopeExtensions.CanAccessByOwner(principal, "other"));
    }
}
