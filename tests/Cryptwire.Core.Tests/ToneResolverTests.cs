using Cryptwire.Core.Notifications;
using Cryptwire.Core.Settings;
using Cryptwire.Core.Skins;

namespace Cryptwire.Core.Tests;

public class ToneResolverTests
{
    private const string WhatsApp = "https://web.whatsapp.com";
    private const string Tones = @"C:\tonos";
    private static readonly Theme Skin = new("relicario", "Relicario", @"C:\pieles\relicario");

    private static readonly IncomingNotification FromTrish = new(WhatsApp, "Trish", "Te dejo la pizza");
    private static readonly IncomingNotification CallFromTrish = new(WhatsApp, "Trish", "Llamada entrante", NotificationBridgeScript.CallTagPrefix + "1");

    private static string? Resolve(AppSettings settings, IncomingNotification notification, Theme? theme = null) =>
        new ToneResolver(() => settings, () => theme ?? Skin, Tones, _ => true).Resolve(notification);

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
        Assert.Null(new ToneResolver(() => new AppSettings(), () => null, Tones, _ => true).Resolve(FromTrish));
    }

    [Fact]
    public void ASkinWithoutCallToneRingsWithItsMessageTone()
    {
        var resolver = new ToneResolver(() => new AppSettings(), () => Skin, Tones, path => !path.EndsWith("llamada.wav", StringComparison.Ordinal));

        Assert.Equal(@"C:\pieles\relicario\tono.wav", resolver.Resolve(CallFromTrish));
    }

    [Fact]
    public void ASkinWithoutTonesStaysSilent()
    {
        Assert.Null(new ToneResolver(() => new AppSettings(), () => Skin, Tones, _ => false).Resolve(CallFromTrish));
    }

    [Fact]
    public void AToneThatIsNoLongerThereFallsBackToTheNextOne()
    {
        var settings = new AppSettings { MessageTone = ToneChoice.FromFile(@"C:\tonos\borrado.wav") }
            .WithToneRule("Trish", @"C:\tonos\tambien-borrado.wav", null);
        var resolver = new ToneResolver(() => settings, () => Skin, Tones, path => !path.Contains("borrado", StringComparison.Ordinal));

        Assert.Equal(@"C:\pieles\relicario\tono.wav", resolver.Resolve(FromTrish));
    }

    [Theory]
    [InlineData(@"\\servidor\tonos\trish.wav")]
    [InlineData(@"D:\otro\trish.wav")]
    [InlineData(@"C:\tonos\..\Windows\Media\tada.wav")]
    [InlineData(@"C:\tonos-otros\trish.wav")]
    [InlineData(@"tonos\trish.wav")]
    [InlineData("C:\\tonos\\tri\0sh.wav")]
    public void YourTonesOnlyCountFromTheToneFolderAndAreNeverLookedUpElsewhere(string path)
    {
        var looked = new List<string>();
        var settings = new AppSettings { CallTone = ToneChoice.FromFile(path) }.WithToneRule("Trish", path, path);
        var resolver = new ToneResolver(() => settings, () => Skin, Tones, p =>
        {
            looked.Add(p);
            return true;
        });

        Assert.Equal(@"C:\pieles\relicario\tono.wav", resolver.Resolve(FromTrish));
        Assert.Equal(@"C:\pieles\relicario\llamada.wav", resolver.Resolve(CallFromTrish));
        Assert.DoesNotContain(path, looked);
    }

    [Fact]
    public void SilentStillWinsOverAMissingFile()
    {
        var settings = new AppSettings { MessageTone = ToneChoice.Silent }.WithToneRule("Trish", @"C:\tonos\borrado.wav", null);
        var resolver = new ToneResolver(() => settings, () => Skin, Tones, path => !path.Contains("borrado", StringComparison.Ordinal));

        Assert.Null(resolver.Resolve(FromTrish));
    }

    [Fact]
    public void TheSettingsAreAskedOnEveryNotice()
    {
        var settings = new AppSettings();
        var resolver = new ToneResolver(() => settings, () => Skin, Tones, _ => true);

        var before = resolver.Resolve(FromTrish);
        settings = settings with { MessageTone = ToneChoice.FromFile(@"C:\tonos\nuevo.wav") };

        Assert.Equal(@"C:\pieles\relicario\tono.wav", before);
        Assert.Equal(@"C:\tonos\nuevo.wav", resolver.Resolve(FromTrish));
    }
}
