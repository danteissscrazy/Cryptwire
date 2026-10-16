using Cryptwire.Core.Notifications;
using Cryptwire.Core.Settings;
using Cryptwire.Core.Skins;

namespace Cryptwire.Core.Tests;

public class ToneResolverTests
{
    private const string WhatsApp = "https://web.whatsapp.com";
    private static readonly Theme Skin = new("relicario", "Relicario", @"C:\pieles\relicario");

    private static readonly IncomingNotification FromTrish = new(WhatsApp, "Trish", "Te dejo la pizza");
    private static readonly IncomingNotification CallFromTrish = new(WhatsApp, "Trish", "Llamada entrante", NotificationBridgeScript.CallTagPrefix + "1");

    private static string? Resolve(AppSettings settings, IncomingNotification notification, Theme? theme = null) =>
        new ToneResolver(() => settings, () => theme ?? Skin).Resolve(notification);

    [Fact]
    public void ByDefaultTheSkinTonesSound()
    {
        Assert.Equal(@"C:\pieles\relicario\tono.wav", Resolve(new AppSettings(), FromTrish));
        Assert.Equal(@"C:\pieles\relicario\llamada.wav", Resolve(new AppSettings(), CallFromTrish));
    }

    [Fact]
    public void YourGeneralTonesReplaceTheSkinOnes()
    {
        var settings = new AppSettings { MessageTone = ToneChoice.FromFile(@"C:\tonos\acero.wav"), CallTone = ToneChoice.FromFile(@"C:\tonos\campana.mp3") };

        Assert.Equal(@"C:\tonos\acero.wav", Resolve(settings, FromTrish));
        Assert.Equal(@"C:\tonos\campana.mp3", Resolve(settings, CallFromTrish));
    }

    [Fact]
    public void SilentMeansNoTone()
    {
        var settings = new AppSettings { MessageTone = ToneChoice.Silent, CallTone = ToneChoice.Silent };

        Assert.Null(Resolve(settings, FromTrish));
        Assert.Null(Resolve(settings, CallFromTrish));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void AFileChoiceWithoutAFileFallsBackToTheSkin(string? file)
    {
        var settings = new AppSettings { MessageTone = new ToneChoice(ToneSource.File, file) };

        Assert.Equal(@"C:\pieles\relicario\tono.wav", Resolve(settings, FromTrish));
    }

    [Fact]
    public void AChatWithItsOwnToneUsesIt()
    {
        var settings = new AppSettings { MessageTone = ToneChoice.Silent }
            .WithToneRule("Trish", @"C:\tonos\trish.wav", @"C:\tonos\trish-llama.wav");

        Assert.Equal(@"C:\tonos\trish.wav", Resolve(settings, FromTrish));
        Assert.Equal(@"C:\tonos\trish-llama.wav", Resolve(settings, CallFromTrish));
        Assert.Null(Resolve(settings, FromTrish with { Title = "Lady" }));
    }

    [Fact]
    public void AChatWithOnlyAMessageToneRingsWithTheGeneralCallTone()
    {
        var settings = new AppSettings().WithToneRule("Trish", @"C:\tonos\trish.wav", callTone: null);

        Assert.Equal(@"C:\tonos\trish.wav", Resolve(settings, FromTrish));
        Assert.Equal(@"C:\pieles\relicario\llamada.wav", Resolve(settings, CallFromTrish));
    }

    [Theory]
    [InlineData("trish")]
    [InlineData("  TRISH ")]
    [InlineData("\u202ATrish\u202C")]
    public void TheChatIsRecognisedHoweverWhatsAppWritesItsName(string title)
    {
        var settings = new AppSettings().WithToneRule("Trish", @"C:\tonos\trish.wav", null);

        Assert.Equal(@"C:\tonos\trish.wav", Resolve(settings, FromTrish with { Title = title }));
    }

    [Fact]
    public void WithoutASkinAndWithoutYourTonesNothingSounds()
    {
        Assert.Null(new ToneResolver(() => new AppSettings(), () => null).Resolve(FromTrish));
    }

    [Fact]
    public void TheSettingsAreAskedOnEveryNotice()
    {
        var settings = new AppSettings();
        var resolver = new ToneResolver(() => settings, () => Skin);

        var before = resolver.Resolve(FromTrish);
        settings = settings with { MessageTone = ToneChoice.FromFile(@"C:\tonos\nuevo.wav") };

        Assert.Equal(@"C:\pieles\relicario\tono.wav", before);
        Assert.Equal(@"C:\tonos\nuevo.wav", resolver.Resolve(FromTrish));
    }
}
