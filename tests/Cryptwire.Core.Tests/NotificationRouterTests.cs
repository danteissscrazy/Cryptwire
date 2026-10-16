using Cryptwire.Core.Notifications;

namespace Cryptwire.Core.Tests;

public class NotificationRouterTests
{
    private readonly FakeSoundPlayer sound = new();
    private readonly FakeToastService toast = new();
    private readonly IncomingNotification message = new("Chat de prueba", "Hola");

    [Fact]
    public void PlaysTheSkinToneAndShowsTheNotice()
    {
        var router = new NotificationRouter(sound, toast, @"C:\cryptwire\tono.wav");

        router.Handle(message);

        Assert.Equal([@"C:\cryptwire\tono.wav"], sound.Played);
        Assert.Equal([message], toast.Shown);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void WithoutToneItStaysSilentButStillNotifies(string? tonePath)
    {
        var router = new NotificationRouter(sound, toast, tonePath);

        router.Handle(message);

        Assert.Empty(sound.Played);
        Assert.Equal([message], toast.Shown);
    }

    [Fact]
    public void EachNotificationSoundsOnce()
    {
        var router = new NotificationRouter(sound, toast, "tono.wav");

        router.Handle(message);
        router.Handle(message with { Body = "Otro" });

        Assert.Equal(2, sound.Played.Count);
        Assert.Equal(2, toast.Shown.Count);
    }

    [Fact]
    public void RejectsANullNotification()
    {
        var router = new NotificationRouter(sound, toast, "tono.wav");

        Assert.Throws<ArgumentNullException>(() => router.Handle(null!));
        Assert.Empty(sound.Played);
    }

    private sealed class FakeSoundPlayer : ISoundPlayer
    {
        public List<string> Played { get; } = [];

        public void Play(string path) => Played.Add(path);
    }

    private sealed class FakeToastService : IToastService
    {
        public List<IncomingNotification> Shown { get; } = [];

        public void Show(IncomingNotification notification) => Shown.Add(notification);
    }
}
