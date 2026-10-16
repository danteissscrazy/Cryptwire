using Cryptwire.Core.Notifications;
using Cryptwire.Core.Startup;

namespace Cryptwire.Core.Tests;

public class StartupAndUnreadTests
{
    private const string Exe = @"C:\Program Files\Cryptwire\Cryptwire.exe";

    [Fact]
    public void TheStartupCommandQuotesThePathAndStartsInTheTray()
    {
        Assert.Equal("\"C:\\Program Files\\Cryptwire\\Cryptwire.exe\" --bandeja", StartupCommand.Build(Exe));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Cryptwire.exe")]
    [InlineData("C:\\a\"b\\Cryptwire.exe")]
    public void TheStartupCommandNeedsAnAbsolutePathWithoutQuotes(string exe)
    {
        Assert.ThrowsAny<ArgumentException>(() => StartupCommand.Build(exe));
    }

    [Theory]
    [InlineData("\"C:\\Program Files\\Cryptwire\\Cryptwire.exe\" --bandeja")]
    [InlineData("\"c:\\program files\\cryptwire\\CRYPTWIRE.EXE\"")]
    [InlineData("  \"C:\\Program Files\\Cryptwire\\..\\Cryptwire\\Cryptwire.exe\" --bandeja")]
    public void ACommandThatStartsThisExecutableCounts(string command)
    {
        Assert.True(StartupCommand.PointsTo(command, Exe));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\"D:\\Viejo\\Cryptwire.exe\" --bandeja")]
    [InlineData("\"C:\\Program Files\\Cryptwire\\Cryptwire.exe --bandeja")]
    [InlineData("C:\\Program Files\\Cryptwire\\Cryptwire.exe --bandeja")]
    [InlineData("Cryptwire.exe")]
    public void ACommandForAnotherOrBrokenPathDoesNotCount(string? command)
    {
        Assert.False(StartupCommand.PointsTo(command, Exe));
    }

    [Fact]
    public void ACommandWithoutQuotesAndWithoutSpacesCounts()
    {
        Assert.True(StartupCommand.PointsTo(@"C:\Apps\Cryptwire.exe --bandeja", @"C:\Apps\Cryptwire.exe"));
    }

    [Theory]
    [InlineData("WhatsApp", 0)]
    [InlineData("(3) WhatsApp", 3)]
    [InlineData("(128) WhatsApp", 128)]
    [InlineData("(99+) WhatsApp", 99)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    [InlineData("() WhatsApp", 0)]
    [InlineData("(tres) WhatsApp", 0)]
    [InlineData("(-3) WhatsApp", 0)]
    [InlineData("(3 WhatsApp", 0)]
    [InlineData("(99999999999) WhatsApp", 0)]
    [InlineData("WhatsApp (3)", 0)]
    public void TheUnreadCountComesFromTheStartOfTheTitle(string? title, int expected)
    {
        Assert.Equal(expected, UnreadCount.FromTitle(title));
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(-1, "")]
    [InlineData(7, "7")]
    [InlineData(99, "99")]
    [InlineData(100, "99+")]
    public void TheUnreadLabelIsShort(int count, string label)
    {
        Assert.Equal(label, UnreadCount.Label(count));
    }
}
