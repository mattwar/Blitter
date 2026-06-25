#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run FallingBlocks.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj

// Minimal showcase of Blitter.Blocks2D.Spawner2D: a steady rain of
// colored squares fall from above and disappear off the bottom.
// Press Escape to exit, Space to pause/resume the spawner.

using System.Numerics;
using Blitter;
using Blitter.Blocks;
using Blitter.Blocks2D;

const int W = 800, H = 600;
var rng = new Random(42);

var window = new Window2D(W, H)
{
    Title = "Falling Blocks (Spawner2D)",
    BackgroundColor = new Color(15, 18, 28),
    CloseKey = Key.Escape,
};

var scene = new Scene2D
{
    Layers = 
    [ 
        new PlayField2D { WorldBounds = new Rect(0, 0, W, H) }, 
        new FallingBlocksHud() 
    ],
    Behaviors =
    [
        new BlockSpawner
        {
            Interval = TimeSpan.FromMilliseconds(180),
            Jitter = TimeSpan.FromMilliseconds(120),
            MaxAlive = 60,
            Random = rng,
            WorldWidth = W,
        },
        new PauseOnSpace2D(),
    ],
};

await scene.RunAsync(window);

// HUD overlay: live sprite/spawn counts and the pause hint.
sealed class FallingBlocksHud : Layer2D
{
    protected override void DrawContent(Renderer2D rd)
    {
        var playField = Scene.GetLayer<PlayField2D>();
        var spawner = Scene.GetBehavior<IFallingBlocksSpawner>();

        using var _ = rd.PushState();
        rd.Camera = null;
        rd.DrawDebugText(10, 10,
            $"alive: {playField.Entities.Count(e => e is not IColliderBarrier2D)}  spawned: {spawner.SpawnedCount}" +
            $"  {(spawner.Paused ? "[PAUSED — Space to resume]" : "[Space to pause]")}",
            scale: 2f);
    }
}

// Toggles a spawner's paused state when Space is pressed.
sealed class PauseOnSpace2D : Behavior, IUpdatable
{
    private IFallingBlocksSpawner? _spawner;
    private bool _spaceWasDown;

    protected override void OnAttach(IEntity entity)
    {
        _spawner = entity.GetBehavior<IFallingBlocksSpawner>();
    }

    public void Update(in UpdateContext context)
    {
        var spaceIsDown = Keyboard.IsDown(Key.Space);
        if (spaceIsDown && !_spaceWasDown && _spawner is { } spawner)
            spawner.Paused = !spawner.Paused;
        _spaceWasDown = spaceIsDown;
    }
}

interface IFallingBlocksSpawner
{
    bool Paused { get; set; }
    int SpawnedCount { get; }
}

// Spawns falling blocks into the playfield.
sealed class BlockSpawner : Spawner2D, IFallingBlocksSpawner
{
    public required int WorldWidth { get; init; }

    protected override IEntity CreateSprite() => new Block(Random, WorldWidth);
}

// Self-removes a sprite once its top edge clears the bottom of the world bounds.
sealed class RemoveBelowBounds2D : Behavior, IUpdatable
{
    public float Margin { get; set; }

    public void Update(in UpdateContext context)
    {
        if (Entity is Sprite2D sprite
            && sprite.TryFindTrait<Bounds2D>(out var bounds)
            && sprite.Center.Y - Margin > bounds.Rect.Height)
            sprite.RemoveFromContainer();
    }
}

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
        GetOrAddBehavior<Motion2D>();
        GetOrAddBehavior<RemoveBelowBounds2D>().Margin = Size;
    }

    public override void Draw(Renderer2D renderer)
    {
        using var _ = renderer.PushState();
        renderer.DrawColor = _color;
        renderer.DrawFillRect(new Rect(Center.X - Size / 2f, Center.Y - Size / 2f, Size, Size));
    }
}
