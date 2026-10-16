using Cryptwire.Core.Notifications;

namespace Cryptwire.Core.Tests;

public class ChatNameTests
{
    [Theory]
    [InlineData("Trish", "trish")]
    [InlineData("Trish", "  Trish  ")]
    [InlineData("Soporte · Turno de tarde", "SOPORTE · TURNO  DE TARDE")]
    [InlineData("+34 600 00 00 00", "\u202A+34 600 00 00 00\u202C")]
    [InlineData("Ángel", "A\u0301ngel")]
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
    [InlineData("\u200B", "\u200B")]
    public void DifferentOrEmptyNamesAreNotTheSameChat(string? a, string? b)
    {
        Assert.False(ChatName.Same(a, b));
    }

    [Fact]
    public void NormalizeTrimsAndComposesAccents()
    {
        Assert.Equal("Ángel", ChatName.Normalize("  A\u0301ngel "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\u202A\u202C")]
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
    public void HalfAnEmojiInTheNameBreaksNothing()
    {
        var half = "Trish " + '\uD83D';
        var recent = new RecentChats();

        recent.Add(half);

        Assert.Equal("Trish \uFFFD", ChatName.Normalize(half));
        Assert.True(ChatName.Same(half, "trish \uFFFD"));
        Assert.Equal(["Trish \uFFFD"], recent.Names);
    }

    [Fact]
    public void SafeTextKeepsWholeEmojisAndMendsHalves()
    {
        Assert.Equal("a👨‍👩‍👧b", SafeText.WithoutLoneSurrogates("a👨‍👩‍👧b"));
        Assert.Equal("\uFFFDx\uFFFD", SafeText.WithoutLoneSurrogates("\uDC00x\uD800"));
        Assert.Same("sin cambios", SafeText.WithoutLoneSurrogates("sin cambios"));
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
