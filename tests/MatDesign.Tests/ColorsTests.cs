using MatDesign.Core;

namespace MatDesign.Tests;

public class ColorsTests
{
    [Theory]
    [InlineData("#d6336c")]
    [InlineData("#000000")]
    [InlineData("#ffffff")]
    public void A_hex_colour_survives_a_round_trip(string hex)
        => Assert.Equal(hex, Rgb.Parse(hex).ToHex());

    [Fact]
    public void Short_hex_and_capitals_are_understood()
    {
        Assert.Equal("#aabbcc", Rgb.Parse("#ABC").ToHex());
        Assert.Equal("#1a56db", Rgb.Parse("1A56DB").ToHex());
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#12345")]
    [InlineData("")]
    public void A_text_that_is_not_a_colour_is_refused(string text)
        => Assert.Throws<FormatException>(() => Rgb.Parse(text));

    [Fact]
    public void Black_on_white_is_21_to_1()
        => Assert.Equal(21.0, Colors.Contrast(Rgb.Black, Rgb.White), 1);

    [Fact]
    public void Contrast_does_not_depend_on_which_colour_is_in_front()
    {
        var a = Rgb.Parse("#1a56db");
        var b = Rgb.Parse("#f3f5fb");
        Assert.Equal(Colors.Contrast(a, b), Colors.Contrast(b, a), 10);
    }

    [Fact]
    public void A_colour_is_at_no_distance_from_itself_and_farther_from_others()
    {
        var blue = Rgb.Parse("#1a56db");
        Assert.Equal(0, Colors.Distance(blue, blue), 10);
        Assert.True(Colors.Distance(blue, Rgb.Parse("#2563eb")) < Colors.Distance(blue, Rgb.Parse("#268538")));
    }

    [Fact]
    public void Mixing_returns_the_ends_at_the_ends()
    {
        var a = Rgb.Parse("#d6336c");
        var b = Rgb.Parse("#0b1020");
        Assert.Equal(a.ToHex(), Colors.Mix(a, b, 1).ToHex());
        Assert.Equal(b.ToHex(), Colors.Mix(a, b, 0).ToHex());
    }

    [Fact]
    public void Mixing_white_and_black_in_oklab_gives_the_grey_that_css_color_mix_gives()
    {
        // color-mix(in oklab, white 50%, black) is rgb(99, 99, 99): the mid point in OKLab is not the mid point in sRGB (128).
        var grey = Colors.Mix(Rgb.White, Rgb.Black, 0.5);
        Assert.InRange(grey.R, 98.5, 99.5);
        Assert.Equal(grey.R, grey.G, 3);
        Assert.Equal(grey.R, grey.B, 3);
    }

    [Theory]
    [InlineData("#d6336c")]
    [InlineData("#2f6feb")]
    [InlineData("#4fd1c5")]
    public void Oklab_and_back_returns_the_same_colour(string hex)
    {
        var c = Rgb.Parse(hex);
        Assert.Equal(hex, Colors.FromOklab(Colors.ToOklab(c)).ToHex());
    }

    [Fact]
    public void Lightness_in_oklab_follows_the_eye()
    {
        Assert.Equal(1.0, Colors.ToOklab(Rgb.White).L, 3);
        Assert.Equal(0.0, Colors.ToOklab(Rgb.Black).L, 3);
        Assert.True(Colors.ToOklab(Rgb.Parse("#4fd1c5")).L > Colors.ToOklab(Rgb.Parse("#1a56db")).L);
    }
}
