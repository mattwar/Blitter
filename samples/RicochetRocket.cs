#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/RicochetRocket.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj

using Blitter;
using Blitter.Bits;
using Blitter.Blocks;

// Fixed design surface. The renderer letterboxes this into whatever
// the actual window size is, so the playfield stays a constant
// 1920x1080 regardless of monitor resolution or fullscreen toggles.
const int DesignW = 1920;
const int DesignH = 1080;

var window = new Window2D(DesignW, DesignH)
{
    Title = "Ricochet Rocket",
    BackgroundColor = new Color(0, 20, 0, 0),
    FullScreen = true,
    CloseKey = Key.Escape,
};

window.Renderer.SetLogicalSize(DesignW, DesignH, LogicalPresentation.Letterbox);

// create rocket sprite
var rocketImage = Bitmap.Load(Asset.GetPathRelativeToCaller("rocket.png"));
rocketImage.SetAlpha(0, rocketImage.GetPixel(0, 0)); // make the background transparent
var rocketCircle = rocketImage.ComputeOpaqueCircle();

// create meteor field - small static obstacles for the rocket to hit
var asteroidImage = Bitmap.Load(Asset.GetPathRelativeToCaller("asteroid.png"));
var asteroidCircle = asteroidImage.ComputeOpaqueCircle();
var asteroids = new List<Sprite2D>();
var rng = Random.Shared;
for (int i = 0; i < 14; i++)
{
    // keep meteors away from the rocket's starting position
    float x, y;
    do
    {
        x = rng.Next(80, DesignW - 80);
        y = rng.Next(80, DesignH - 80);
    }
    while (MathF.Abs(x - DesignW / 2f) < 200 && MathF.Abs(y - DesignH / 2f) < 200);
    var rotation = rng.Next(0, 360);
    var heading = rng.Next(0, 360);
    var scale = rng.Next(2, 10) / 10f;
    var speed = rng.Next(20, 100);

    asteroids.Add(new Sprite2D(asteroidImage, x, y, scale: scale)
    {
        HitRadius = asteroidCircle.Radius * scale,
        Rotation = rotation,
        Heading = heading,
        Speed = speed,
        Behaviors = {
            new Motion2D(),
            new Spin2D { DegreesPerSecond = rng.Next(-30, 31) },
            new BounceInBounds2D()
        },
    });
}

var rocket = new Sprite2D(
    rocketImage, 
    DesignW / 2, DesignH / 2, 
    0.1f)
{
    Speed = 600f,
    Heading = 45f,
    HitRadius = rocketCircle.Radius * 0.1f,
    Behaviors =
    {
        new RocketController(window.Input), // player input
        new Motion2D(),  // move with simple 2D physics
        new FaceHeading2D(),  // face the way we're moving
        new BounceInBounds2D  // bounce off the walls
        {
            OnBounce = s =>
            {
                s.Heading = (s.Heading + Random.Shared.Next(-10, 10) + 360f) % 360f;
                Audio.Play(Sounds.Boing, volume: .2f);
            },
        },
        new HitResponder2D  // smash through meteors
        {
            OnHit = (self, other) =>
            {
                // Only react to meteors. (No other collidable types in
                // this sample, but this keeps the handler honest if
                // anything is added later.)
                if (other is not Sprite2D big)
                    return;

                // Grace period: ignore freshly-spawned shards so they
                // can spread out before being hit again. Without this
                // the rocket sits inside the impact zone and devours
                // every shard on the very next frame.
                if (other.Age < TimeSpan.FromMilliseconds(400))
                    return;

                // Mark the meteor for removal; the scene reaps it
                // on its next update pass.
                other.IsAlive = false;
                self.Heading = (self.Heading + Random.Shared.Next(-15, 15) + 360f) % 360f;
                Audio.Play(Sounds.Explosion, volume: .3f);

                // Asteroids-style split: a hit meteor above a
                // minimum size breaks into a few smaller shards that
                // drift outward in random directions.
                if (big.Scale > 0.25f)
                {
                    var container = big.Container;
                    if (container == null)
                        return;

                    var newScale = big.Scale * 0.5f;
                    var newHitRadius = big.HitRadius * 0.5f;
                    var shardCount = Random.Shared.Next(2, 4);
                    for (int i = 0; i < shardCount; i++)
                    {
                        var shardSpin = Random.Shared.Next(120, 360);
                        if (Random.Shared.Next(2) == 0)
                            shardSpin = -shardSpin;
                        var shard = new Sprite2D(asteroidImage, big.CenterX, big.CenterY, scale: newScale)
                        {
                            HitRadius = newHitRadius,
                            Rotation = Random.Shared.Next(0, 360),
                            Heading = Random.Shared.Next(0, 360),
                            Speed = Random.Shared.Next(80, 180),
                            Behaviors =
                            {
                                new Motion2D(),
                                new Spin2D { DegreesPerSecond = shardSpin },
                                new BounceInBounds2D(),
                            },
                        };
                        container.Add(shard);
                    }
                }
            },
        },
    },
};

var scene = new Scene2D([
    new PlayField2D([
        ..asteroids,
        rocket
        ]),
    new CustomProp2D
    {
        OnRender = rd =>
        {
            rd.DrawColor = Color.White;
            rd.DrawDebugText(
                0, 10,
                $"heading: {rocket.Heading:#} speed: {rocket.Speed:#} rotation: {rocket.Rotation:#} x: {rocket.CenterX:#} y: {rocket.CenterY:#}",
                scale: 2f);
        }
    }
    ]);

await scene.RunAsync(window);


// Custom user input controller for the rocket.
// Rotates the sprite at a constant angular velocity (degrees per second).
sealed class Spin2D : SpriteBehavior2D
{
    public float DegreesPerSecond { get; set; }

    public override void Update(Sprite2D target, in UpdateContext2D context)
    {
        target.Rotation = (target.Rotation + DegreesPerSecond * (float)context.ElapsedSinceLastUpdate.TotalSeconds) % 360f;
    }
}

sealed class RocketController : SpriteBehavior2D
{
    private readonly FrameInput _input;

    public RocketController(FrameInput input) => _input = input;

    public override void Update(Sprite2D rocket, in UpdateContext2D context)
    {
        if (_input.IsDown(Key.Left))
            rocket.Heading = (rocket.Heading + 350f) % 360f;

        if (_input.IsDown(Key.Right))
            rocket.Heading = (rocket.Heading + 10f) % 360f;

        if (_input.WasJustPressed(Key.Up))
        {
            rocket.Speed = Math.Clamp(rocket.Speed + 50f, 0f, 1000f);
            Audio.Play(Sounds.RoarUp, volume: .25f);
        }
        if (_input.WasJustPressed(Key.Down))
        {
            rocket.Speed = Math.Clamp(rocket.Speed - 50f, 0f, 1000f);
            Audio.Play(Sounds.RoarDown, volume: .25f);
        }
    }
}