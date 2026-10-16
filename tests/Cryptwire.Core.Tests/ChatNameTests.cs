using Cryptwire.Core.Notifications;

namespace Cryptwire.Core.Tests;

public class ChatNameTests
{
    [Theory]
    [InlineData("Trish", "trish")]
    [InlineData("Trish", "  Trish  ")]
    [InlineData("Soporte · Turno de tarde", "SOPORTE · TURNO  DE TARDE")]
    [InlineData("+34 600 00 00 00", "‪+34 600 00 00 00‬")]
    [InlineData("Ángel", "Ángel")]
    [InlineData("Familia 👨‍👩‍👧", "familia 👨‍👩‍👧")]
    public void TheSameChatWrittenDifferently(string a, string b)
    {
        Assert.True(ChatName.Same(a, b));
    }

    [Theory]
    [InlineData("Trish", "Lady")]
    [InlineData("Trish", "Trish 2")]
    [InlineData("", "")]
    [InlineData(null, null)]
    [InlineData("​", "​")]
    public void DifferentOrEmptyNamesAreNotTheSameChat(string? a, string? b)
    {
        Assert.False(ChatName.Same(a, b));
    }

    [Fact]
    public void NormalizeTrimsAndComposesAccents()
    {
        Assert.Equal("Ángel", ChatName.Normalize("  Ángel "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("‪‬")]
    public void ANameWithNothingVisibleIsNoName(string? name)
    {
        Assert.Null(ChatName.Normalize(name));
    }

    [Fact]
    public void AVeryLongNameIsCutWithoutSplittingAnEmoji()
    {
        var name = new string('a', ChatName.MaxLength - 1) + "👨‍👩‍👧" + "bbb";

        var normalized = ChatName.Normalize(name)!;

        Assert.EndsWith("👨‍👩‍👧", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public void RecentChatsKeepsTheNewestFirstWithoutRepeats()
    {
        var recent = new RecentChats();

        recent.Add("Trish");
        recent.Add("Lady");
        recent.Add(" trish ");
        recent.Add(null);
        recent.Add("  ");

        Assert.Equal(["trish", "Lady"], recent.Names);
    }

    [Fact]
    public void RecentChatsForgetsTheOldestPastItsCapacity()
    {
        var recent = new RecentChats();

        for (var i = 0; i < RecentChats.Capacity + 5; i++)
        {
            recent.Add($"Chat {i}");
        }

        Assert.Equal(RecentChats.Capacity, recent.Names.Count);
        Assert.Equal($"Chat {RecentChats.Capacity + 4}", recent.Names[0]);
        Assert.DoesNotContain("Chat 0", recent.Names);
    }
}
