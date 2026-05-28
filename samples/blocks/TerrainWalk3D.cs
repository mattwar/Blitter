#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run TerrainWalk3D.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj

// Walk around a rolling procedural terrain in first person.
//
// Showcases:
//   * MeshBarrier3D + MeshHitShape3D (triangle-soup collision against
//     a non-flat surface)
//   * Gravity3D pulling the player onto the ground
//   * BarrierBounce3D with Restitution=0 doing the "slide along the
//     slope" surface response
//   * A walk-style controller behavior that drives a Sprite3D and the
//     scene camera together
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

const float TerrainSize = 40f;
const int   TerrainSubdivisions = 80;

const float PlayerRadius = 0.5f;
const float WalkSpeed = 4.5f;
const float SprintMultiplier = 1.8f;
const float JumpSpeed = 6.0f;
// Eye sits a bit above the sphere's center so the camera clears the
// avatar when the player drops into a dip.
const float EyeOffsetY = 0.6f;

var terrainMesh = BuildTerrainMesh(TerrainSize, TerrainSubdivisions, HeightAt, NormalAt);
var terrain = new TerrainBarrier3D(terrainMesh);

var window = new Window3D
{
    Title = "Terrain Walk 3D",
    BackgroundColor = new Color(140, 180, 220),
    FullScreen = true,
    RelativeMouseMode = true,
    CloseKey = Key.Escape,
};

window.Renderer.AmbientLight = new Color(95, 110, 130);
window.Renderer.DirectionalLight = new DirectionalLight(
    Vector3.Normalize(new Vector3(-0.4f, -0.8f, -0.5f)),
    new Color(255, 246, 230));

var camera = new PerspectiveCamera
{
    Position = new Vector3(0f, HeightAt(0f, 0f) + EyeOffsetY + 4f, 0f),
    Target = new Vector3(0f, HeightAt(0f, 0f) + EyeOffsetY + 4f, -1f),
    FieldOfView = MathF.PI / 2.5f,
    NearPlane = 0.05f,
    FarPlane = 200f,
};
window.Renderer.Camera = camera;

var playField = new PlayField3D();
playField.AddBarrier(terrain);

var walkController = new WalkController3D(window, camera)
{
    EyeOffsetY = EyeOffsetY,
    MoveSpeed = WalkSpeed,
    SprintMultiplier = SprintMultiplier,
    JumpSpeed = JumpSpeed,
};

var player = new Sprite3D
{
    // Sphere visual + SphereHitShape3D. Mesh collision currently only
    // implements sphere×triangle contact, so a sphere player is the
    // shape that actually responds to the terrain.
    Visual = MeshVisual3D.Sphere(
        color: new Color(220, 130, 90, 0),  // 0 alpha to be invisible
        radius: PlayerRadius),
    // Drop in from a few units up so the player settles onto the
    // surface via gravity + bounce.
    Position = new Vector3(0f, HeightAt(0f, 0f) + 4f, 0f),
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
            // Restitution=0: stick to the slope (don't bounce off it).
            // TangentialDamping=1: keep tangential speed (no surface
            // friction beyond what the barrier's PhysicsMaterial adds).
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

// ---- terrain helpers --------------------------------------------------

// Rolling-hills heightmap: small enough that the analytic gradient
// (NormalAt) stays well-behaved across the whole footprint.
static float HeightAt(float x, float z) =>
    1.0f * MathF.Sin(0.35f * x) * MathF.Cos(0.30f * z)
    + 0.35f * MathF.Sin(0.7f * z + 0.6f);

// Outward unit normal of y = HeightAt(x, z): (-df/dx, 1, -df/dz)
// normalised.
static Vector3 NormalAt(float x, float z)
{
    float dfx =  0.35f * MathF.Cos(0.35f * x) * MathF.Cos(0.30f * z);
    float dfz = -0.30f * MathF.Sin(0.35f * x) * MathF.Sin(0.30f * z)
              +  0.35f * 0.7f * MathF.Cos(0.7f * z + 0.6f);
    return Vector3.Normalize(new Vector3(-dfx, 1f, -dfz));
}

static Mesh<LitVertex3D> BuildTerrainMesh(
    float size, int subdivisions,
    Func<float, float, float> height,
    Func<float, float, Vector3> normal)
{
    if (subdivisions < 1) throw new ArgumentOutOfRangeException(nameof(subdivisions));

    int n = subdivisions + 1;
    var verts = new LitVertex3D[n * n];
    for (int z = 0; z < n; z++)
    {
        for (int x = 0; x < n; x++)
        {
            float fx = (x / (float)subdivisions - 0.5f) * size;
            float fz = (z / (float)subdivisions - 0.5f) * size;
            float fy = height(fx, fz);

            // Color by elevation: lows lean green, highs lean sandy.
            float t = Math.Clamp((fy + 1.4f) / 2.8f, 0f, 1f);
            var c = new Color(
                (byte)(110 + 110 * t),
                (byte)(160 -  30 * t),
                (byte)(90  -  40 * t));
            verts[z * n + x] = new LitVertex3D(
                new Vector3(fx, fy, fz), normal(fx, fz), c);
        }
    }
    var indices = new uint[subdivisions * subdivisions * 6];
    int k = 0;
    for (int z = 0; z < subdivisions; z++)
    {
        for (int x = 0; x < subdivisions; x++)
        {
            uint a = (uint)(z * n + x);
            uint b = a + 1;
            uint c = (uint)((z + 1) * n + x);
            uint d = c + 1;
            indices[k++] = a; indices[k++] = c; indices[k++] = b;
            indices[k++] = b; indices[k++] = c; indices[k++] = d;
        }
    }
    return Mesh.Create<LitVertex3D>(verts, indices);
}

// ---- types ------------------------------------------------------------

// Thin wrapper around MeshBarrier3D that also draws the source mesh as
// a lit visual. Collision goes through the cached MeshHitShape3D the
// base class already owns; rendering reuses the same mesh.
sealed class TerrainBarrier3D : MeshBarrier3D<LitVertex3D>
{
    private readonly MeshVisual3D _visual;

    public TerrainBarrier3D(Mesh<LitVertex3D> mesh)
        : base(mesh)
    {
        _visual = new MeshVisual3D(mesh, LitTextureMaterial.Default);
    }

    public override void Draw(Renderer3D renderer)
    {
        _visual.Draw(renderer, Pose3D.Identity, Color.White, TimeSpan.Zero);
    }
}

// Drives the host sprite's horizontal Velocity from WASD, fires a jump
// impulse on Space if recently grounded, and slaves the supplied
// camera to the sprite's eye every frame. The vertical channel of
// Velocity is left to Gravity3D and BarrierBounce3D.
sealed class WalkController3D : SpriteBehavior3D
{
    private readonly Window _window;
    private readonly PerspectiveCamera _camera;
    private TimeSpan _elapsed;
    private TimeSpan _lastGroundedAt = TimeSpan.MinValue;

    // Window after a ground contact during which Space still counts as
    // a jump — covers single-frame lift-offs and helps the input feel
    // forgiving over uneven terrain.
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

        // ---- Mouse look. With Window.RelativeMouseMode on, the
        // cursor is hidden + locked and Input.MouseDelta delivers
        // unclamped per-frame motion in pixels.
        var delta = _window.Input.MouseDelta;
        if (delta != Vector2.Zero)
        {
            Yaw -= delta.X * LookSpeed;
            Pitch = Math.Clamp(Pitch - delta.Y * LookSpeed, -MaxPitch, MaxPitch);
        }

        // ---- Horizontal movement basis: yaw-only, so looking down
        // doesn't shorten the step.
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

        // ---- Jump: edge-triggered, gated by coyote window.
        if (_window.Input.WasJustPressed(Key.Space)
            && (_elapsed - _lastGroundedAt).TotalSeconds <= CoyoteWindowSeconds)
        {
            v.Y = JumpSpeed;
            _lastGroundedAt = TimeSpan.MinValue;   // consume
        }
        target.Velocity = v;

        // ---- Camera follow.
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
        // Normal convention: contact.Normal points from the barrier
        // surface toward the sprite. A sufficiently upward normal means
        // we're on ground (not bumping a near-vertical wall).
        if (contact.Normal.Y > 0.5f)
            _lastGroundedAt = _elapsed;
    }
}
