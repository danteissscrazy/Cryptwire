using Cryptwire.Core.Notifications;
using Cryptwire.Core.Settings;
using Cryptwire.Core.Widgets;

namespace Cryptwire.Core.Tests;

public sealed class WidgetsTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 20, 30, 0, TimeSpan.FromHours(2));

    private readonly TempDir dir = new();

    public void Dispose() => dir.Dispose();

    [Fact]
    public void ByDefaultTheWidgetsGoInTheBar()
    {
        var settings = new AppSettings();

        Assert.Equal(WidgetMode.Bar, settings.Widgets);
        Assert.True(settings.PanelToBarWhenNarrow);
        Assert.Null(settings.SilencedUntil);
        Assert.False(settings.Veil);
    }

    [Theory]
    [InlineData(WidgetMode.Bar, WidgetMode.Panel)]
    [InlineData(WidgetMode.Panel, WidgetMode.None)]
    [InlineData(WidgetMode.None, WidgetMode.Bar)]
    public void F9GoesBarPanelNoneAndRoundAgain(WidgetMode from, WidgetMode to)
    {
        Assert.Equal(to, WidgetLayout.Next(from));
    }

    [Theory]
    [InlineData(WidgetMode.Panel, 1600, true, WidgetMode.Panel)]
    [InlineData(WidgetMode.Panel, 1099, true, WidgetMode.Bar)]
    [InlineData(WidgetMode.Panel, 1099, false, WidgetMode.Panel)]
    [InlineData(WidgetMode.Bar, 800, true, WidgetMode.Bar)]
    [InlineData(WidgetMode.None, 800, true, WidgetMode.None)]
    [InlineData(WidgetMode.Panel, 1100, true, WidgetMode.Panel)]
    public void APanelInANarrowWindowBecomesABar(WidgetMode chosen, double width, bool panelToBar, WidgetMode shown)
    {
        Assert.Equal(shown, WidgetLayout.Effective(chosen, width, panelToBar));
    }

    [Theory]
    [InlineData(WidgetMode.Bar, "barra")]
    [InlineData(WidgetMode.Panel, "panel")]
    [InlineData(WidgetMode.None, "ninguno")]
    public void EachModeHasItsName(WidgetMode mode, string name)
    {
        Assert.Equal(name, WidgetLayout.Name(mode));
    }

    [Fact]
    public void StealthLastsAnHourAndCountsDown()
    {
        var until = Now + Stealth.Length;

        Assert.True(Stealth.IsOn(until, Now));
        Assert.Equal(1, Stealth.Progress(until, Now));
        Assert.Equal("quedan 60 min", Stealth.Label(until, Now));
        Assert.Equal("quedan 45 min", Stealth.Label(until, Now.AddMinutes(15)));
        Assert.Equal(0.25, Stealth.Progress(until, Now.AddMinutes(45)), precision: 6);
    }

    [Fact]
    public void TheLastSecondsStillCountAsAMinute()
    {
        var until = Now.AddSeconds(30);

        Assert.Equal("queda 1 min", Stealth.Label(until, Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    [InlineData(0)]
    public void StealthInThePastOrUnsetIsOff(int? minutesFromNow)
    {
        DateTimeOffset? until = minutesFromNow is { } m ? Now.AddMinutes(m) : null;

        Assert.False(Stealth.IsOn(until, Now));
        Assert.Equal(TimeSpan.Zero, Stealth.Remaining(until, Now));
        Assert.Equal(0, Stealth.Progress(until, Now));
        Assert.Equal("apagado", Stealth.Label(until, Now));
    }

    [Theory]
    [InlineData(NotificationKind.Message)]
    [InlineData(NotificationKind.Call)]
    public void StealthSilencesEvenCalls(NotificationKind kind)
    {
        var silenced = new DeliveryContext(ToastsOn: true, WindowActive: false, DesktopState.Available, Silenced: true);

        Assert.Equal(new Delivery(PlayTone: false, ShowToast: false), DeliveryPolicy.Decide(kind, silenced));
    }

    [Fact]
    public void ThePageGetsSkinThenWallpaperThenVeilEachOnItsOwn()
    {
        var layers = PageCss.Compose("/*piel*/", "/*fondo*/", veil: true);

        Assert.Equal(["/*piel*/", "/*fondo*/", VeilCss.Css], layers);
    }

    [Theory]
    [InlineData(null, null, false, null)]
    [InlineData(null, null, true, "velo")]
    [InlineData("/*piel*/", null, false, "/*piel*/")]
    [InlineData(null, "/*fondo*/", false, "/*fondo*/")]
    [InlineData("", "", false, null)]
    public void WithNothingToPutThePageStaysAsItIs(string? skin, string? wallpaper, bool veil, string? expected)
    {
        var layers = PageCss.Compose(skin, wallpaper, veil);

        if (expected is null)
        {
            Assert.Null(layers);
        }
        else
        {
            Assert.Equal([expected == "velo" ? VeilCss.Css : expected], layers);
        }
    }

    [Fact]
    public void TheVeilBlursUntilHover()
    {
        Assert.Contains("filter: blur(", VeilCss.Css, StringComparison.Ordinal);
        Assert.Contains(":hover", VeilCss.Css, StringComparison.Ordinal);
        Assert.Contains("filter: none !important", VeilCss.Css, StringComparison.Ordinal);
    }

    [Fact]
    public void WidgetsStealthAndVeilSurviveASaveAndALoad()
    {
        var path = Path.Combine(dir.Path, "settings.json");
        var store = new JsonSettingsStore(path);
        var settings = new AppSettings { Widgets = WidgetMode.Panel, PanelToBarWhenNarrow = false, SilencedUntil = Now, Veil = true };

        store.Save(settings);

        Assert.Equivalent(settings, store.Load(), strict: true);
        Assert.Contains("\"widgets\": \"Panel\"", File.ReadAllText(path), StringComparison.Ordinal);
    }
}
