using Cryptwire.Core.Notifications;

namespace Cryptwire.Core.Tests;

public class DeliveryPolicyTests
{
    private static Delivery Decide(NotificationKind kind, DesktopState desktop, bool toastsOn = true, bool windowActive = false) =>
        DeliveryPolicy.Decide(kind, new DeliveryContext(toastsOn, windowActive, desktop));

    [Theory]
    [InlineData(NotificationKind.Message)]
    [InlineData(NotificationKind.Call)]
    public void WithTheDesktopFreeItSoundsAndShows(NotificationKind kind)
    {
        Assert.Equal(new Delivery(PlayTone: true, ShowToast: true), Decide(kind, DesktopState.Available));
    }

    [Theory]
    [InlineData(NotificationKind.Message)]
    [InlineData(NotificationKind.Call)]
    public void WithCryptwireInFrontThereIsNoNoticeButItStillSounds(NotificationKind kind)
    {
        Assert.Equal(new Delivery(PlayTone: true, ShowToast: false), Decide(kind, DesktopState.Available, windowActive: true));
    }

    [Fact]
    public void WithNoticesTurnedOffOnlyTheToneIsLeft()
    {
        Assert.Equal(new Delivery(PlayTone: true, ShowToast: false), Decide(NotificationKind.Message, DesktopState.Available, toastsOn: false));
    }

    [Theory]
    [InlineData(NotificationKind.Message, false)]
    [InlineData(NotificationKind.Call, true)]
    public void AtFullScreenOnlyCallsSound(NotificationKind kind, bool sounds)
    {
        Assert.Equal(new Delivery(sounds, ShowToast: false), Decide(kind, DesktopState.Busy));
    }

    [Theory]
    [InlineData(NotificationKind.Message)]
    [InlineData(NotificationKind.Call)]
    public void DoNotDisturbSilencesEverything(NotificationKind kind)
    {
        Assert.Equal(new Delivery(PlayTone: false, ShowToast: false), Decide(kind, DesktopState.QuietTime));
    }

    [Theory]
    [InlineData(NotificationKind.Message)]
    [InlineData(NotificationKind.Call)]
    public void WithTheScreenLockedItSoundsButShowsNothing(NotificationKind kind)
    {
        Assert.Equal(new Delivery(PlayTone: true, ShowToast: false), Decide(kind, DesktopState.Away));
    }
}
