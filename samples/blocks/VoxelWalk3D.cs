#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run VoxelWalk3D.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj

// Walk around a small voxel world built from grass / dirt / stone
// blocks.
//
// Showcases:
//   * ArrayVoxelWorld + VoxelPalette + VoxelType
//   * VoxelChunkBarrier3D (per-cell collision via VoxelHitShape3D and
//     greedy face-meshed visual via VoxelChunkVisual3D)
//   * Sharing a single Bitmap atlas across voxel types using
//     TextureRegion2D — the mesher buckets faces by source texture so
//     the whole chunk draws in a single mesh.
//   * Same WalkController3D + gravity + bounce loop as TerrainWalk3D.
//
// Controls:
//   WASD ....................... walk on the horizontal plane
//   SHIFT ...................... sprint
//   SPACE ...................... jump (only while grounded)
//   Mouse ...................... look around
//   ESC ........................ quit

using System.Numerics;

using Blitter;
using Blitter.Bits;
using Blitter.Blocks3D;

const int WorldWidth  = 32;
const int WorldHeight = 8;
const int WorldDepth  = 32;

// Sphere body (not capsule) because HitPrimitive3D currently only
// has closed-form contact for Sphere-vs-Box, and each voxel cell is a
// Box primitive; a capsule player would TestHit but TryGetContact
// would return false and the player would fall straight through.
const float PlayerRadius = 0.45f;
const float WalkSpeed = 4.5f;
const float SprintMultiplier = 1.8f;
const float JumpSpeed = 6.0f;
const float EyeOffsetY = 0.35f;

// ---- Atlas: three solid-color tiles packed side-by-side. The mesher
// only samples one UV rect per voxel type, so the per-tile resolution
// can be tiny; 8 px gives a comfortable margin away from tile edges
// for any bilinear filtering.
const int Tile = 8;
var atlas = Bitmap.Create(Tile * 3, Tile);
FillTile(atlas, 0, Color.Green);    // grass
FillTile(atlas, 1, Color.Brown);    // dirt
FillTile(atlas, 2, Color.Gray);   // stone

var grassTex = new TextureRegion2D(atlas, new Rect(0,        0, Tile, Tile));
var dirtTex  = new TextureRegion2D(atlas, new Rect(Tile,     0, Tile, Tile));
var stoneTex = new TextureRegion2D(atlas, new Rect(Tile * 2, 0, Tile, Tile));

var palette = new VoxelPalette();
var stone = palette.Add(new VoxelType { Id = 1, Name = "stone", Texture = stoneTex });
var dirt  = palette.Add(new VoxelType { Id = 2, Name = "dirt",  Texture = dirtTex  });
var grass = palette.Add(new VoxelType { Id = 3, Name = "grass", Texture = grassTex });

var world = new ArrayVoxelWorld(WorldWidth, WorldHeight, WorldDepth, palette);

// Layered terrain with a couple of low rolling bumps so the surface
// isn't perfectly flat. The top voxel is mostly grass but occasionally
// breaks out into dirt or stone splotches, driven by a deterministic
// per-cell hash so the world looks the same on every run.
for (int z = 0; z < WorldDepth; z++)
{
    for (int x = 0; x < WorldWidth; x++)
    {
        int top = SurfaceHeight(x, z);
        world.SetVoxel(x, 0, z, stone.Id);
        for (int y = 1; y < top; y++)
            world.SetVoxel(x, y, z, dirt.Id);
        world.SetVoxel(x, top, z, SurfaceVoxel(x, z, grass.Id, dirt.Id, stone.Id));
    }
}

var grid = new VoxelChunkGrid(
    world,
    new ChunkCoord(0, 0, 0),
    WorldWidth, WorldHeight, WorldDepth,
    Vector3.One);
var chunk = new VoxelChunkBarrier3D(grid);

var window = new Window3D
{
    Title = "Voxel Walk 3D",
    BackgroundColor = new Color(140, 180, 220),
    FullScreen = true,
    RelativeMouseMode = true,
    CloseKey = Key.Escape,
};

window.Renderer.AmbientLight = new Color(95, 110, 130);
window.Renderer.DirectionalLight = new DirectionalLight(
    Vector3.Normalize(new Vector3(-0.4f, -0.8f, -0.5f)),
    new Color(255, 246, 230));

float spawnX = WorldWidth * 0.5f;
float spawnZ = WorldDepth * 0.5f;
float groundY = SurfaceHeight((int)spawnX, (int)spawnZ) + 1f;

var camera = new PerspectiveCamera
{
    Position = new Vector3(spawnX, groundY + EyeOffsetY + 4f, spawnZ),
    Target = new Vector3(spawnX, groundY + EyeOffsetY + 4f, spawnZ - 1f),
    FieldOfView = MathF.PI / 2.5f,
    NearPlane = 0.05f,
    FarPlane = 200f,
};
window.Renderer.Camera = camera;

var playField = new PlayField3D();
playField.AddBarrier(chunk);

var walkController = new WalkController3D(window, camera)
{
    EyeOffsetY = EyeOffsetY,
    MoveSpeed = WalkSpeed,
    SprintMultiplier = SprintMultiplier,
    JumpSpeed = JumpSpeed,
};

var player = new Sprite3D
{
    Visual = MeshVisual3D.Sphere(
        color: new Color(220, 130, 90, 0),
        radius: PlayerRadius),
    Position = new Vector3(spawnX, groundY + 4f, spawnZ),
    Behaviors =
    {
        walkController,
        new Gravity3D
        {
            Acceleration = new Vector3(0f, -20f, 0f),
            MaxFallSpeed = 30f,
        },
        new BarrierBounce3D
        {
            Restitution = 0f,
            TangentialDamping = 1f,
        },
        new Motion3D(),
    },
};
playField.AddSprite(player);

var hud = new CustomLayer3D
{
    OnRender = rd =>
    {
        DebugDraw.DrawText("WASD walk   SHIFT sprint   SPACE jump   Mouse look   ESC quit",
            18f, 16f);
        DebugDraw.DrawText(
            $"pos ({player.Position.X,6:0.0}, {player.Position.Y,6:0.0}, {player.Position.Z,6:0.0})    " +
            $"v ({player.Velocity.X,6:0.0}, {player.Velocity.Y,6:0.0}, {player.Velocity.Z,6:0.0})",
            18f, 48f);
    },
};

var scene = new Scene3D
{
    Layers = { playField, hud },
};

await scene.RunAsync(window);

// ---- helpers ----------------------------------------------------------

static int SurfaceHeight(int x, int z)
{
    // Two soft bumps over a base layer; result is always in
    // [baseTop, baseTop + 2].
    const int baseTop = 2;
    float h = MathF.Sin(x * 0.35f) * MathF.Cos(z * 0.30f)
            + 0.5f * MathF.Sin(z * 0.7f + 0.6f);
    return baseTop + Math.Clamp((int)MathF.Round(h + 1f), 0, 2);
}

// Picks the top-layer voxel kind: mostly grass, with occasional dirt
// and rarer stone splotches. Two scales of value-noise are summed so
// the patches cluster instead of looking like salt-and-pepper.
static int SurfaceVoxel(int x, int z, int grassId, int dirtId, int stoneId)
{
    float n = ValueNoise(x * 0.45f, z * 0.45f) * 0.65f
            + ValueNoise(x * 0.15f, z * 0.15f) * 0.35f;
    if (n > 0.78f) return stoneId;
    if (n > 0.58f) return dirtId;
    return grassId;
}

// Bilinearly-interpolated hash noise in [0, 1]. Deterministic per (x, z).
static float ValueNoise(float x, float z)
{
    int xi = (int)MathF.Floor(x);
    int zi = (int)MathF.Floor(z);
    float fx = x - xi;
    float fz = z - zi;
    float a = Hash01(xi,     zi);
    float b = Hash01(xi + 1, zi);
    float c = Hash01(xi,     zi + 1);
    float d = Hash01(xi + 1, zi + 1);
    // Smoothstep on each axis for softer transitions between cells.
    float sx = fx * fx * (3f - 2f * fx);
    float sz = fz * fz * (3f - 2f * fz);
    return (a + (b - a) * sx) * (1f - sz) + (c + (d - c) * sx) * sz;
}

static float Hash01(int x, int z)
{
    uint h = (uint)(x * 374761393) ^ (uint)(z * 668265263);
    h = (h ^ (h >> 13)) * 1274126177u;
    h ^= h >> 16;
    return (h & 0xFFFFFF) / (float)0x1000000;
}

static void FillTile(Bitmap atlas, int tileX, Color color)
{
    int x0 = tileX * 8;
    for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
            atlas.SetPixel(x0 + x, y, color);
}

// ---- types ------------------------------------------------------------

// Same controller as TerrainWalk3D — drives the host sprite's
// horizontal velocity from WASD, jumps on Space when recently
// grounded, and slaves the camera to the sprite each frame.
sealed class WalkController3D : SpriteBehavior3D
{
    private readonly Window _window;
    private readonly PerspectiveCamera _camera;
    private TimeSpan _elapsed;
    private TimeSpan? _lastGroundedAt;

    private const float CoyoteWindowSeconds = 0.12f;

    public float Yaw { get; set; }
    public float Pitch { get; set; }
    public float MaxPitch { get; set; } = MathF.PI / 2f - 0.05f;

    public float MoveSpeed { get; set; } = 4.5f;
    public float SprintMultiplier { get; set; } = 1.8f;
    public float JumpSpeed { get; set; } = 6.0f;
    public float EyeOffsetY { get; set; } = 0.6f;
    public float LookSpeed { get; set; } = 0.005f;

    public WalkController3D(Window window, PerspectiveCamera camera)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(camera);
        _window = window;
        _camera = camera;
    }

    public override void Apply(Sprite3D target, in UpdateContext3D context)
    {
        _elapsed += context.ElapsedSinceLastUpdate;
        var dt = (float)context.ElapsedSinceLastUpdate.TotalSeconds;
        if (dt <= 0f)
            return;

        var delta = _window.Input.MouseDelta;
        if (delta != Vector2.Zero)
        {
            Yaw -= delta.X * LookSpeed;
            Pitch = Math.Clamp(Pitch - delta.Y * LookSpeed, -MaxPitch, MaxPitch);
        }

        var forwardFlat = new Vector3(-MathF.Sin(Yaw), 0f, -MathF.Cos(Yaw));
        var rightFlat = Vector3.Normalize(Vector3.Cross(forwardFlat, Vector3.UnitY));

        var move = Vector3.Zero;
        if (Keyboard.IsDown(Key.W)) move += forwardFlat;
        if (Keyboard.IsDown(Key.S)) move -= forwardFlat;
        if (Keyboard.IsDown(Key.D)) move += rightFlat;
        if (Keyboard.IsDown(Key.A)) move -= rightFlat;

        float speed = MoveSpeed;
        if (Keyboard.IsDown(Key.LShift) || Keyboard.IsDown(Key.RShift))
            speed *= SprintMultiplier;

        var horiz = move == Vector3.Zero
            ? Vector3.Zero
            : Vector3.Normalize(move) * speed;

        var v = target.Velocity;
        v.X = horiz.X;
        v.Z = horiz.Z;

        if (_window.Input.WasJustPressed(Key.Space)
            && _lastGroundedAt is { } groundedAt
            && (_elapsed - groundedAt).TotalSeconds <= CoyoteWindowSeconds)
        {
            v.Y = JumpSpeed;
            _lastGroundedAt = null;
        }
        target.Velocity = v;

        var eye = target.Position + new Vector3(0f, EyeOffsetY, 0f);
        var cosP = MathF.Cos(Pitch);
        var look = new Vector3(
            -cosP * MathF.Sin(Yaw),
             MathF.Sin(Pitch),
            -cosP * MathF.Cos(Yaw));
        _camera.Position = eye;
        _camera.Target = eye + look;
        _camera.Up = Vector3.UnitY;
    }

    public override void OnHitBarrier(Sprite3D self, Barrier3D barrier, in UpdateContext3D context)
    {
        if (!self.HitShape.TryGetContact(barrier.HitShape, out var contact))
            return;
        if (contact.Normal.Y > 0.5f)
            _lastGroundedAt = _elapsed;
    }
}
