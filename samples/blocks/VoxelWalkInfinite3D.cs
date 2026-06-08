#:package Blitter@*-*

// Run this file directly with .NET 10 or later:
//
//     dotnet run VoxelWalkInfinite3D.cs
//
// While Blitter is unpublished, build a local copy first:
//
//     dotnet build src/Blitter.Package/Blitter.Package.csproj

// Walk an unbounded voxel world. Chunks are generated on demand
// around the player and evicted as they fall outside the load
// radius, so memory + GPU cost stay bounded no matter how far you
// roam.
//
// Showcases:
//   * SparseVoxelWorld + IVoxelGenerator (deterministic terrain).
//   * VoxelChunkSource3D bridging the voxel layer into the playfield
//     chunked-streaming infrastructure.
//   * ChunkedPlayField3D driven by the player's chunk coordinate every
//     frame, with TrimChunksOutside reaping anything outside the
//     active range.
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

const int ChunkCellsX = 16;
const int ChunkCellsY = 64;
const int ChunkCellsZ = 16;

// 1 + 2 * LoadRadius chunks across each horizontal axis. 4 keeps the
// active window at 9×9 chunks (≈144×144 cells) — plenty for a sample,
// small enough that the first-frame generation hitch is bearable.
const int LoadRadius = 4;

const float PlayerRadius = 0.45f;
const float WalkSpeed = 4.5f;
const float SprintMultiplier = 1.8f;
const float JumpSpeed = 6.0f;
const float EyeOffsetY = 1.55f;

// ---- Atlas
var atlas = Bitmap.Load(Asset.GetPathRelativeToCaller("Blocks.png"));
var grassTopTex  = AtlasTile(atlas, col: 4, row: 4);
var grassSideTex = AtlasTile(atlas, col: 1, row: 3);
var dirtTex      = AtlasTile(atlas, col: 4, row: 3);
var stoneTex     = AtlasTile(atlas, col: 1, row: 4);

var palette = new VoxelPalette();
var stone = palette.Add(new VoxelType { Id = 1, Name = "stone", Texture = stoneTex });
var dirt  = palette.Add(new VoxelType { Id = 2, Name = "dirt",  Texture = dirtTex  });
var grass = palette.Add(new VoxelType
{
    Id = 3,
    Name = "grass",
    TopTexture    = grassTopTex,
    SideTexture   = grassSideTex,
    BottomTexture = dirtTex,
});

var generator = new TerrainGenerator(grassId: grass.Id, dirtId: dirt.Id, stoneId: stone.Id);
var voxelWorld = new SparseVoxelWorld(palette, generator, ChunkCellsX, ChunkCellsY, ChunkCellsZ);
var chunkSource = new VoxelChunkSource3D(voxelWorld, Vector3.One, ChunkCellsX, ChunkCellsY, ChunkCellsZ);

var window = new Window3D
{
    Title = "Voxel Walk Infinite 3D",
    BackgroundColor = new Color(140, 180, 220),
    FullScreen = true,
    RelativeMouseMode = true,
    CloseKey = Key.Escape,
};

window.Renderer.AmbientLight = new Color(140, 150, 165);
window.Renderer.DirectionalLight = new DirectionalLight(
    Vector3.Normalize(new Vector3(-0.4f, -0.8f, -0.5f)),
    new Color(140, 135, 125));
window.Renderer.TextureSampling = ImageSampling.Nearest;

// DebugDraw (the HUD text overlay below) is a no-op unless a renderer
// opts in, so enable it here.
window.Renderer.DebugDrawEnabled = true;

const float spawnX = 0.5f;
const float spawnZ = 0.5f;
float groundY = TerrainGenerator.SurfaceHeight((int)spawnX, (int)spawnZ) + 1f;

var camera = new PerspectiveCamera
{
    Position = new Vector3(spawnX, groundY + EyeOffsetY + 4f, spawnZ),
    Target = new Vector3(spawnX, groundY + EyeOffsetY + 4f, spawnZ - 1f),
    FieldOfView = MathF.PI / 2.5f,
    NearPlane = 0.05f,
    FarPlane = 400f,
};
window.Renderer.Camera = camera;

var initialMin = new ChunkCoord(-LoadRadius, 0, -LoadRadius);
var initialMax = new ChunkCoord( LoadRadius, 0,  LoadRadius);
var playField = new ChunkedPlayField3D(chunkSource, initialMin, initialMax);

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
    // Visual is kept only as a collision proxy — first-person view
    // doesn't want to look at the inside of its own sphere.
    Visible = false,
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
chunkSource.AddSprite(player);

// Re-centers the chunk window on the player every frame and evicts
// anything that's drifted out. Runs as its own layer (before the
// playfield's update via Scene3D.Layers ordering) so the playfield
// always iterates the just-recentered range.
var streamer = new CustomLayer3D
{
    OnUpdate = ctx =>
    {
        var here = chunkSource.GetChunkCoords(player.Position);
        playField.MinChunk = new ChunkCoord(here.X - LoadRadius, 0, here.Z - LoadRadius);
        playField.MaxChunk = new ChunkCoord(here.X + LoadRadius, 0, here.Z + LoadRadius);
        chunkSource.TrimChunksOutside(playField.MinChunk, playField.MaxChunk);
    },
};

var hud = new CustomLayer3D
{
    OnRender = rd =>
    {
        var pc = chunkSource.GetChunkCoords(player.Position);
        DebugDraw.DrawText("WASD walk   SHIFT sprint   SPACE jump   Mouse look   ESC quit", 18f, 16f);
        DebugDraw.DrawText($"pos ({player.Position.X:0.0}, {player.Position.Y:0.0}, {player.Position.Z:0.0}) chunk ({pc.X}, {pc.Z})", 18f, 48f);
        DebugDraw.DrawText($"chunks active: {chunkSource.ActiveChunkCount} pooled: {chunkSource.PooledChunkCount} allocated: {chunkSource.ChunksAllocated} reused: {chunkSource.ChunksReused}", 18f, 80f);
    },
};

var scene = new Scene3D
{
    Layers = { streamer, playField, hud },
};

await scene.RunAsync(window);

// ---- helpers ----------------------------------------------------------

static TextureRegion2D AtlasTile(Bitmap atlas, int col, int row)
    => new(atlas, new Rect(col * 16, row * 16, 16, 16));

// ---- types ------------------------------------------------------------

// Two-octave value-noise heightmap with a per-cell splotch noise that
// occasionally substitutes dirt or stone for grass on the surface.
// Pure functions of (x, z) so chunks can be regenerated identically
// after eviction.
sealed class TerrainGenerator : IVoxelGenerator
{
    private readonly int _grassId;
    private readonly int _dirtId;
    private readonly int _stoneId;

    public TerrainGenerator(int grassId, int dirtId, int stoneId)
    {
        _grassId = grassId;
        _dirtId = dirtId;
        _stoneId = stoneId;
    }

    public void Generate(ChunkCoord coord, int cellsX, int cellsY, int cellsZ, int[] cells)
    {
        int originX = coord.X * cellsX;
        int originY = coord.Y * cellsY;
        int originZ = coord.Z * cellsZ;
        for (int lz = 0; lz < cellsZ; lz++)
        {
            int wz = originZ + lz;
            for (int lx = 0; lx < cellsX; lx++)
            {
                int wx = originX + lx;
                int top = SurfaceHeight(wx, wz);
                int surfaceId = SurfaceVoxel(wx, wz, _grassId, _dirtId, _stoneId);
                for (int ly = 0; ly < cellsY; ly++)
                {
                    int wy = originY + ly;
                    int id;
                    if (wy == 0)
                        id = _stoneId;
                    else if (wy < top)
                        id = _dirtId;
                    else if (wy == top)
                        id = surfaceId;
                    else
                        id = 0;
                    cells[(lz * cellsY + ly) * cellsX + lx] = id;
                }
            }
        }
    }

    public static int SurfaceHeight(int x, int z)
    {
        const int baseTop = 2;
        float h = MathF.Sin(x * 0.35f) * MathF.Cos(z * 0.30f)
                + 0.5f * MathF.Sin(z * 0.7f + 0.6f);
        return baseTop + Math.Clamp((int)MathF.Round(h + 1f), 0, 2);
    }

    public static int SurfaceVoxel(int x, int z, int grassId, int dirtId, int stoneId)
    {
        float n = ValueNoise(x * 0.45f, z * 0.45f) * 0.65f
                + ValueNoise(x * 0.15f, z * 0.15f) * 0.35f;
        if (n > 0.78f) return stoneId;
        if (n > 0.58f) return dirtId;
        return grassId;
    }

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
}
