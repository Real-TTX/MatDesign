namespace MatDesktop.Tests;

/// <summary>DESK-05: the first start takes the instance, a second start hands its arguments to it and ends.</summary>
public sealed class SingleInstanceTests
{
    [Fact]
    public void A_second_start_hands_its_arguments_to_the_first_and_is_told_to_end()
    {
        var product = "MatDesktopTest" + Guid.NewGuid().ToString("N")[..8];
        using var first = new MatSingleInstance(product);
        using var arrived = new ManualResetEventSlim();
        string[]? received = null;
        first.SecondStart += (_, args) =>
        {
            received = args;
            arrived.Set();
        };
        Assert.True(first.TryClaim([]));
        Assert.True(first.IsFirst);

        using var second = new MatSingleInstance(product);
        Assert.False(second.TryClaim(["--show", "a file.txt"], TimeSpan.FromSeconds(5)));
        Assert.False(second.IsFirst);
        Assert.True(arrived.Wait(TimeSpan.FromSeconds(10)), "the first instance heard nothing");
        Assert.NotNull(received);
        Assert.Equal(["--show", "a file.txt"], received);
    }

    [Fact]
    public void The_names_carry_the_product_and_the_user_and_a_scope_keeps_two_copies_apart()
    {
        using var plain = new MatSingleInstance("Example Agent");
        using var scoped = new MatSingleInstance("Example Agent", "C:/dev/data");
        Assert.StartsWith("Example_Agent.ShowWindow.", plain.PipeName, StringComparison.Ordinal);
        Assert.NotEqual(plain.PipeName, scoped.PipeName);
        Assert.DoesNotContain(' ', plain.PipeName);
    }
}
