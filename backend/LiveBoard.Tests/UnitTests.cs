using LiveBoard.Api.Hubs;
using LiveBoard.Api.Services;

namespace LiveBoard.Tests;

public class OrderingTests
{
    [Theory]
    [InlineData("C", 0, "CABD")]
    [InlineData("A", 3, "BCDA")]
    [InlineData("A", 99, "BCDA")] // clamped to the end
    [InlineData("D", -5, "DABC")] // clamped to the start
    [InlineData("B", 1, "ABCD")]  // no-op
    public void Move_reorders_within_a_list(string item, int toIndex, string expected)
    {
        var result = Ordering.Move("ABCD".Select(c => c.ToString()).ToList(), item, toIndex);

        Assert.Equal(expected, string.Concat(result));
    }

    [Fact]
    public void Move_rejects_an_item_that_is_not_in_the_list()
    {
        Assert.Throws<ArgumentException>(() => Ordering.Move(["A", "B"], "Z", 0));
    }

    [Theory]
    [InlineData(0, "XAB")]
    [InlineData(1, "AXB")]
    [InlineData(10, "ABX")]
    public void Insert_places_the_item_at_a_clamped_index(int toIndex, string expected)
    {
        Assert.Equal(expected, string.Concat(Ordering.Insert(["A", "B"], "X", toIndex)));
    }
}

public class PresenceTrackerTests
{
    private static readonly Guid Board = Guid.NewGuid();
    private static readonly Guid OtherBoard = Guid.NewGuid();
    private static readonly Guid Alice = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();

    [Fact]
    public void Two_tabs_of_one_user_show_once_with_a_connection_count()
    {
        var presence = new PresenceTracker();
        presence.Join("c1", Board, Alice, "Alice", false);
        var users = presence.Join("c2", Board, Alice, "Alice", false);

        var alice = Assert.Single(users);
        Assert.Equal(2, alice.Connections);
    }

    [Fact]
    public void Users_are_scoped_to_their_board_and_sorted_by_name()
    {
        var presence = new PresenceTracker();
        presence.Join("c1", Board, Bob, "bob", false);
        presence.Join("c2", Board, Alice, "Alice", true);
        presence.Join("c3", OtherBoard, Guid.NewGuid(), "Zed", false);

        Assert.Equal(["Alice", "bob"], presence.UsersOn(Board).Select(u => u.DisplayName));
    }

    [Fact]
    public void Leaving_returns_the_board_and_who_is_left()
    {
        var presence = new PresenceTracker();
        presence.Join("c1", Board, Alice, "Alice", false);
        presence.Join("c2", Board, Bob, "Bob", false);
        presence.SetEditing("c1", Guid.NewGuid());

        var result = presence.Leave("c1");

        Assert.NotNull(result);
        Assert.Equal(Board, result.Value.Left.BoardId);
        Assert.NotNull(result.Value.Left.EditingCardId);
        Assert.Equal(["Bob"], result.Value.Remaining.Select(u => u.DisplayName));
        Assert.Null(presence.Leave("c1")); // already gone
    }
}

public class DisplayNamesTests
{
    [Theory]
    [InlineData(null, "jane.doe@example.com", "Jane Doe")]
    [InlineData("", "sam_o-neil+test@x.io", "Sam O Neil Test")]
    [InlineData("  Tajveed  ", "t@x.io", "Tajveed")]
    public void Uses_the_requested_name_or_derives_one_from_the_email(string? requested, string email, string expected)
    {
        Assert.Equal(expected, DisplayNames.ForUser(requested, email));
    }

    [Fact]
    public void Guest_names_are_two_capitalised_words()
    {
        var parts = DisplayNames.ForGuest().Split(' ');

        Assert.Equal(2, parts.Length);
        Assert.All(parts, p => Assert.True(char.IsUpper(p[0])));
    }
}
