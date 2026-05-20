#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run samples/RicochetRocket.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj

using System.Numerics;
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

// Font for floating score popups and HUD score readout.
var scoreFont = new Font(["Consolas", "Menlo"], 48, bold: true);

// Running score: each gold asteroid destroyed pops a value.
int score = 0;

// create rocket sprite
var rocketImage = Bitmap.Load(Asset.GetPathRelativeToCaller("rocket.png"));
 // make rocket's background transparent
rocketImage.SetAlpha(0, rocketImage.GetPixel(0, 0));

// create meteor field - small static obstacles for the rocket to hit
var asteroidImage = Bitmap.Load(Asset.GetPathRelativeToCaller("asteroid.png"));
var asteroids = CreateAsteroidField(14, asteroidImage);

var rocket = new Rocket(scoreFont, () => score, v => score = v)
{
    Image = rocketImage,
    Center = new Vector2(DesignW / 2, DesignH / 2),
    Scale = 0.1f,
    Speed = 600f,
    Heading = 45f,
    Behaviors = { new RocketController(window.Input) }
};

// The playfield includes the asteriods and the rocket.
var playField = new PlayField2D([..asteroids, rocket]);

// Heads-up display layer for debug info
var hud = new CustomLayer2D
{
    OnRender = rd =>
    {
        rd.DrawColor = Color.White;
        var asteriodCount = playField.Sprites.Count(s => s is Asteroid);
        rd.DrawDebugText(
            0, 10,
            $"heading: {rocket.Heading:#} speed: {rocket.Speed:#} rotation: {rocket.Rotation:#} x: {rocket.Center.X:#} y: {rocket.Center.Y:#} asteriods: {asteriodCount}",
            scale: 2f);
        scoreFont.DrawText(rd, $"SCORE {score}", new Color(255, 215, 0), 20, 60);
    }
};

var scene = new Scene2D(playField, hud)
{
    Behaviors =
    {
        new CustomSceneBehavior2D()
        {
            OnUpdate = (s, in ctx) =>
            {
                if (s.RunState != RunState.Running)
                    return;
                if (playField.Sprites.Count(s => s is Asteroid) == 0 || window.Input.WasJustPressed(Key.V))
                {
                    var task = Audio.PlayAsync(Melodies.LevelUp, volume: .3f);
                    s.ExitWithDelay((in _ctx) => task.IsCompleted);
                }
            }
        }
    },
};

// Run the scene until done
await scene.RunAsync(window);


//---------------------------------------------------------------------------------------------------------------

static List<Asteroid> CreateAsteroidField(int count, Bitmap image)
{
    var asteroids = new List<Asteroid>();
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

        var asteriod = new Asteroid
        {
            Image = image,
            Center = new Vector2(x, y),
            Scale = scale,
            Rotation = rotation,
            Heading = heading,
            Speed = speed,
            RotationSpeed = rng.Next(-30, 31),
        };

        asteroids.Add(asteriod);
    }
    return asteroids;
}

sealed class Rocket : Sprite2D
{
    public Rocket(Font scoreFont, Func<int> getScore, Action<int> setScore)
    {
        var goldTint = new Color(255, 215, 0);

        this.Behaviors.AddRange([
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
            new CustomSpriteBehavior2D  // smash through meteors
            {
                OnSpriteHit = (self, other) =>
                {
                    // Grace period: ignore freshly-spawned shards so they
                    // can spread out before being hit again. Without this
                    // the rocket sits inside the impact zone and devours
                    // every shard on the very next frame.
                    if (other.Age < TimeSpan.FromMilliseconds(400))
                        return;

                    if (other is not Asteroid asteroid)
                        return;

                    // Mark the meteor for removal; the playfield reaps it
                    // on its next update pass.
                    other.IsAlive = false;
                    self.Heading = (self.Heading + Random.Shared.Next(-15, 15) + 360f) % 360f;
                    Audio.Play(Sounds.Explosion, volume: .3f);

                    // Score & popup for gold ("valuable minerals") asteroids.
                    bool isGold = asteroid.Tint == goldTint;
                    if (isGold)
                    {
                        // Bigger rocks are worth more; round to tens.
                        int points = Math.Max(10, (int)Math.Round(asteroid.Scale * 200f / 10f) * 10);
                        setScore(getScore() + points);
                        asteroid.PlayField.AddSprite(new TextSprite2D
                        {
                            Font = scoreFont,
                            Text = $"+{points}",
                            Center = asteroid.Center,
                            Tint = goldTint,
                            Heading = 0f,        // up (toward smaller Y)
                            Speed = 90f,
                            Behaviors =
                            {
                                new Motion2D(),
                                new FadeAndExpire2D { Duration = TimeSpan.FromSeconds(1.2) },
                            },
                        });
                    }

                    // Asteroids-style split: a hit meteor above a
                    // minimum size breaks into a few smaller shards that
                    // drift outward in random directions.
                    if (asteroid.Scale > 0.25f)
                    {
                        var playfield = asteroid.PlayField;

                        // Gold (valuable-minerals) tint: if the parent
                        // already has one, all children inherit it.
                        // Otherwise each new shard has a small chance of
                        // exposing gold hidden inside.
                        bool parentIsGold = isGold;

                        var newScale = asteroid.Scale * 0.5f;
                        var shardCount = Random.Shared.Next(2, 4);
                        for (int i = 0; i < shardCount; i++)
                        {
                            var shardSpin = Random.Shared.Next(120, 360);
                            if (Random.Shared.Next(2) == 0)
                                shardSpin = -shardSpin;
                            var shardTint = parentIsGold || Random.Shared.NextDouble() < 0.15
                                ? goldTint
                                : Color.White;
                            var shard = new Asteroid
                            {
                                Image = asteroid.Image,
                                Center = asteroid.Center,
                                Scale = newScale,
                                Rotation = Random.Shared.Next(0, 360),
                                Heading = Random.Shared.Next(0, 360),
                                Speed = Random.Shared.Next(80, 180),
                                RotationSpeed = shardSpin,
                                Tint = shardTint,
                            };
                            playfield.AddSprite(shard);
                        }
                    }
                },
            }
        ]);
    }   
}

sealed class Asteroid : Sprite2D
{
    public Asteroid()
    {
        this.Behaviors.AddRange([
            new Motion2D(),
            new BounceInBounds2D()
        ]);
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