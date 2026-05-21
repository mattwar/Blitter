#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/FallingBlocks.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj

// Minimal showcase of Blitter.Blocks.Spawner2D: a steady rain of
// colored squares fall from above and disappear off the bottom.
// Press Escape to exit, Space to pause/resume the spawner.

using System.Numerics;
using Blitter;
using Blitter.Blocks;

const int W = 800, H = 600;
var rng = new Random(42);

var window = new Window2D(W, H)
{
    Title = "Falling Blocks (Spawner2D)",
    BackgroundColor = new Color(15, 18, 28),
    CloseKey = Key.Escape,
};

var playField = new PlayField2D { WorldBounds = new Rect(0, 0, W, H) };

var spawner = new Spawner2D
{
    Target   = playField,
    Interval = TimeSpan.FromMilliseconds(180),
    Jitter   = TimeSpan.FromMilliseconds(120),
    MaxAlive = 60,
    Factory  = () => new Block(rng, W),
};

var hud = new CustomLayer2D
{
    OnRender = rd =>
    {
        using var _ = rd.PushState();
        rd.Camera = null;
        rd.DrawDebugText(10, 10,
            $"alive: {playField.Sprites.Count}  spawned: {spawner.SpawnedCount}" +
            $"  {(spawner.Paused ? "[PAUSED — Space to resume]" : "[Space to pause]")}",
            scale: 2f);
    }
};

var scene = new Scene2D
{
    Layers = { playField, hud },
    Behaviors =
    {
        spawner,
        new CustomSceneBehavior2D
        {
            OnApply = (s, in ctx) =>
            {
                if (window.Input.WasJustPressed(Key.Space))
                    spawner.Paused = !spawner.Paused;
            }
        },
    },
};

await scene.RunAsync(window);

// A 40×40 colored square that drifts straight down. Self-removes
// once its top edge clears the bottom of the playfield.
sealed class Block : Sprite2D
{
    private const float Size = 40f;
    private readonly Color _color;

    public Block(Random rng, int worldWidth)
    {
        _color = new Color(
            (byte)rng.Next(80, 256),
            (byte)rng.Next(80, 256),
            (byte)rng.Next(80, 256)
            );
        Center = new Vector2(rng.Next((int)Size, worldWidth - (int)Size), -Size);
        Speed  = rng.Next(120, 320);
        Heading = 180f; // straight down (0 = up)
        Behaviors.Add(new Motion2D());
        Behaviors.Add(new CustomSpriteBehavior2D
        {
            OnApply = (sprite, in ctx) =>
            {
                if (sprite.Center.Y - Size > ctx.Bounds.Height)
                    sprite.IsAlive = false;
            }
        });
        CanBeHit = false;
    }

    public override void Draw(Renderer2D renderer)
    {
        using var _ = renderer.PushState();
        renderer.DrawColor = _color;
        renderer.DrawFillRect(new Rect(Center.X - Size / 2f, Center.Y - Size / 2f, Size, Size));
    }
}
