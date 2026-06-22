namespace Blitter.Blocks2D.Tests;

using System.Numerics;

using Blitter.Bits;

public class ParallaxBackground2DTests
{
    [Fact]
    public void TerseInitializer_AddsHorizontalPlatesInOrder()
    {
        var background = new ParallaxBackground2D
        {
            BottomY = 540f,
            Plates =
            {
                { "far.png", 0.15f },
                { "near.png", 0.60f },
            },
        };

        Assert.Equal(2, background.Plates.Count);
        Assert.Equal(new Vector2(0.15f, 0f), background.Plates[0].Parallax);
        Assert.Equal(new Vector2(0.60f, 0f), background.Plates[1].Parallax);
        // Terse plates carry no own BottomY, so they inherit the layer's.
        Assert.Null(background.Plates[0].BottomY);
    }

    [Fact]
    public void MixedInitializer_AllowsFullPlateObjects()
    {
        var background = new ParallaxBackground2D
        {
            Plates =
            {
                new() { Image = "sky.png", Parallax = Vector2.Zero, RepeatX = false },
                { "ground.png", 1.0f },
            },
        };

        Assert.Equal(2, background.Plates.Count);
        Assert.False(background.Plates[0].RepeatX);
        Assert.Equal(Vector2.Zero, background.Plates[0].Parallax);
        Assert.True(background.Plates[1].RepeatX);
    }

    [Fact]
    public void Plate_DefaultsToForegroundParallax()
    {
        var plate = new ParallaxPlate2D();
        Assert.Equal(Vector2.One, plate.Parallax);
        Assert.True(plate.RepeatX);
    }

    [Fact]
    public void Plate_ImageAcceptsImplicitPath()
    {
        var plate = new ParallaxPlate2D { Image = "mountains.png" };
        Assert.Equal("mountains.png", plate.Image.FilePath);
    }

    [Fact]
    public void PlateCollection_RejectsNulls()
    {
        var plates = new ParallaxBackground2D.PlateCollection();
        Assert.Throws<ArgumentNullException>(() => plates.Add((ParallaxPlate2D)null!));
        Assert.Throws<ArgumentNullException>(() => plates.Add((ImageSource)null!, 0.5f));
    }
}
