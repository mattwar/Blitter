using Blitter.Bits;

namespace Blitter.Tests;

public class GradientTests
{
    [Fact]
    public void Ctor_SortsStopsByPosition()
    {
        // Provide stops out of order; Sample should still treat 0->black, 1->white.
        var g = new Gradient(new (float, Color)[]
        {
            (1f, Color.White),
            (0f, Color.Black),
        });
        Assert.Equal(Color.Black, g.Sample(0f));
        Assert.Equal(Color.White, g.Sample(1f));
    }

    [Fact]
    public void Ctor_RequiresAtLeastTwoStops()
    {
        Assert.Throws<ArgumentException>(() =>
            new Gradient(new (float, Color)[] { (0f, Color.Red) }));
    }

    [Fact]
    public void Ctor_NullStops_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Gradient(null!));
    }

    [Fact]
    public void FromColors_EvenlySpacesStops()
    {
        var g = Gradient.FromColors(Color.Black, Color.Gray, Color.White);
        Assert.Equal(3, g.StopCount);
        // Middle stop sits at t=0.5 and equals the middle color exactly.
        Assert.Equal(Color.Gray, g.Sample(0.5f));
    }

    [Fact]
    public void FromColors_RequiresAtLeastTwoColors()
    {
        Assert.Throws<ArgumentException>(() => Gradient.FromColors(Color.Red));
    }

    [Fact]
    public void FromColors_NullColors_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Gradient.FromColors(null!));
    }

    [Fact]
    public void Sample_ClampsBelowFirstStop()
    {
        var g = Gradient.FromColors(Color.Black, Color.White);
        Assert.Equal(Color.Black, g.Sample(-5f));
    }

    [Fact]
    public void Sample_ClampsAboveLastStop()
    {
        var g = Gradient.FromColors(Color.Black, Color.White);
        Assert.Equal(Color.White, g.Sample(5f));
    }

    [Fact]
    public void Sample_InterpolatesMidpoint()
    {
        var g = Gradient.FromColors(Color.Black, Color.White);
        var mid = g.Sample(0.5f);
        // Halfway from black to white is mid-gray (~127 per channel).
        Assert.InRange(mid.R, 126, 128);
        Assert.InRange(mid.G, 126, 128);
        Assert.InRange(mid.B, 126, 128);
    }

    [Fact]
    public void Sample_InterpolatesWithinSegment()
    {
        // Two segments: black->red over [0,0.5], red->white over [0.5,1].
        var g = new Gradient(new (float, Color)[]
        {
            (0f, Color.Black),
            (0.5f, Color.Red),
            (1f, Color.White),
        });
        // Quarter point is halfway between black and red.
        var q = g.Sample(0.25f);
        Assert.InRange(q.R, 126, 128);
        Assert.Equal(0, q.G);
        Assert.Equal(0, q.B);
    }

    [Fact]
    public void StopCount_ReflectsNumberOfStops()
    {
        var g = Gradient.FromColors(Color.Red, Color.Green, Color.Blue, Color.White);
        Assert.Equal(4, g.StopCount);
    }
}
