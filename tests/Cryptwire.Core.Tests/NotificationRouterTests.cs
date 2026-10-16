using Cryptwire.Core.Notifications;
using Cryptwire.Core.Settings;
using Cryptwire.Core.Skins;

namespace Cryptwire.Core.Tests;

public class NotificationRouterTests
{
    private const string WhatsApp = "https://web.whatsapp.com";
    private const string SkinDir = @"C:\pieles\relicario";

    private readonly FakeSoundPlayer sound = new();
    private readonly FakeToastService toast = new();
    private readonly IncomingNotification message = new(WhatsApp, "Chat de prueba", "Hola");
    private readonly IncomingNotification call = new(WhatsApp, "Vergil", "Llamada entrante", NotificationBridgeScript.CallTagPrefix + "3EB0");

    private AppSettings settings = new();
    private Theme? theme = new("relicario", "Relicario", SkinDir);
    private DeliveryContext context = new(ToastsOn: true, WindowActive: false, DesktopState.Available);

    private NotificationRouter Router() =>
        new(sound, toast, new ToneResolver(() => settings, () => theme, @"C:\tonos", _ => true), () => context);

    [Fact]
    public void AMessagePlaysTheSkinToneOnceAndShowsTheNotice()
    {
        var key = Router().Handle(message);

        Assert.NotNull(key);
        Assert.Equal([Path.Combine(SkinDir, "tono.wav")], sound.Played);
        Assert.Empty(sound.Rings);
        var shown = Assert.Single(toast.Shown);
        Assert.Equal((key, NotificationKind.Message, "Chat de prueba", "Hola"), (shown.Key, shown.Kind, shown.Title, shown.Body));
    }

    [Fact]
    public void TheChatPhotoReachesTheNoticeOnlyIfItIsFromWhatsApp()
    {
        var router = Router();

        router.Handle(message with { IconUri = "https://pps.whatsapp.net/v/foto.jpg" });
        router.Handle(message with { IconUri = "https://otro-sitio.example/foto.jpg" });

        Assert.Equal("https://pps.whatsapp.net/v/foto.jpg", toast.Shown[0].IconUri);
        Assert.Null(toast.Shown[1].IconUri);
    }

    [Fact]
    public void ACallRingsWithTheCallToneUntilItIsAnswered()
    {
        var key = Router().Handle(call)!;

        Assert.Empty(sound.Played);
        Assert.Equal([(key, Path.Combine(SkinDir, "llamada.wav"), NotificationRouter.MaxRing)], sound.Rings);
        Assert.Equal(NotificationKind.Call, Assert.Single(toast.Shown).Kind);
    }

    [Fact]
    public void ClickingTheNoticeStopsTheRingAndTellsThePage()
    {
        var clicked = 0;
        var key = Router().Handle(call, clicked: () => clicked++)!;

        toast.Shown[0].Clicked!();

        Assert.Equal([key], sound.Stopped);
        Assert.Equal(1, clicked);
    }

    [Fact]
    public void DismissingTheNoticeStopsTheRingAndTellsThePage()
    {
        var dismissed = 0;
        var key = Router().Handle(call, dismissed: () => dismissed++)!;

        toast.Shown[0].Dismissed!();

        Assert.Equal([key], sound.Stopped);
        Assert.Equal(1, dismissed);
    }

    [Fact]
    public void WhenThePageClosesItTheRingStopsAndTheNoticeGoes()
    {
        var router = Router();
        var key = router.Handle(call)!;

        router.Close(key);

        Assert.Equal([key], sound.Stopped);
        Assert.Equal([key], toast.Closed);
    }

    [Fact]
    public void EachNoticeHasItsOwnKey()
    {
        var router = Router();

        var first = router.Handle(message);
        var second = router.Handle(message with { Body = "Otro" });

        Assert.NotEqual(first, second);
        Assert.Equal(2, sound.Played.Count);
        Assert.Equal(2, toast.Shown.Count);
    }

    [Fact]
    public void TheToneFollowsTheSkinThatIsActiveWhenTheNoticeArrives()
    {
        var router = Router();

        router.Handle(message);
        theme = new Theme("prueba", "Prueba", @"C:\pieles\prueba");
        router.Handle(message);

        Assert.Equal([Path.Combine(SkinDir, "tono.wav"), @"C:\pieles\prueba\tono.wav"], sound.Played);
    }

    [Fact]
    public void WithoutAnyToneItStaysSilentButStillNotifies()
    {
        theme = null;

        Assert.NotNull(Router().Handle(message));

        Assert.Empty(sound.Played);
        Assert.Single(toast.Shown);
    }

    [Fact]
    public void WhatTheDesktopAllowsDecidesToneAndNotice()
    {
        context = context with { Desktop = DesktopState.QuietTime };

        Assert.NotNull(Router().Handle(message));

        Assert.Empty(sound.Played);
        Assert.Empty(toast.Shown);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("https://web.whatsapp.com.evil.example")]
    [InlineData("http://web.whatsapp.com")]
    [InlineData("")]
    public void NoticesFromAnyoneElseAreDropped(string origin)
    {
        var router = Router();

        Assert.Null(router.Handle(call with { Origin = origin }));

        Assert.Empty(sound.Played);
        Assert.Empty(sound.Rings);
        Assert.Empty(toast.Shown);
        Assert.Empty(router.Recent.Names);
    }

    [Fact]
    public void ChatsThatNotifyAreRememberedForTheSettingsButNotTheTestNotice()
    {
        var router = Router();

        router.Handle(message);
        router.Handle(call);
        router.Handle(new IncomingNotification(WhatsApp, "WhatsApp", "Prueba", NotificationBridgeScript.WorkerTagPrefix + "verify-notification"));

        Assert.Equal(["Vergil", "Chat de prueba"], router.Recent.Names);
    }

    [Fact]
    public void RejectsANullNotification()
    {
        Assert.Throws<ArgumentNullException>(() => Router().Handle(null!));
        Assert.Empty(sound.Played);
    }

    private sealed class FakeSoundPlayer : ISoundPlayer
    {
        public List<string> Played { get; } = [];

        public List<(string Key, string Path, TimeSpan Max)> Rings { get; } = [];

        public List<string> Stopped { get; } = [];

        public void Play(string path) => Played.Add(path);

        public void Ring(string key, string path, TimeSpan max) => Rings.Add((key, path, max));

        public void StopRinging(string key) => Stopped.Add(key);
    }

    private sealed class FakeToastService : IToastService
    {
        public List<Toast> Shown { get; } = [];

        public List<string> Closed { get; } = [];

        public void Show(Toast toast) => Shown.Add(toast);

        public void Close(string key) => Closed.Add(key);
    }
}
