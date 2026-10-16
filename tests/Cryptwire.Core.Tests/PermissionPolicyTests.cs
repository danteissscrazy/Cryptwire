using Cryptwire.Core.Permissions;

namespace Cryptwire.Core.Tests;

public class PermissionPolicyTests
{
    private readonly PermissionPolicy policy = new();

    [Fact]
    public void WhatsAppGetsNotificationsWithoutAsking() =>
        Assert.Equal(PermissionDecision.Allow, policy.Decide("https://web.whatsapp.com/", isNotifications: true));

    [Fact]
    public void WhatsAppAsksForEverythingElse() =>
        Assert.Equal(PermissionDecision.AskUser, policy.Decide("https://web.whatsapp.com/", isNotifications: false));

    [Theory]
    [InlineData("https://example.com/", true)]
    [InlineData("https://example.com/", false)]
    [InlineData("https://web.whatsapp.com.evil.example/", true)]
    [InlineData("http://web.whatsapp.com/", true)]
    [InlineData("https://www.youtube.com/", false)]
    [InlineData(null, true)]
    public void AnyoneElseIsDenied(string? origin, bool isNotifications) =>
        Assert.Equal(PermissionDecision.Deny, policy.Decide(origin, isNotifications));
}
