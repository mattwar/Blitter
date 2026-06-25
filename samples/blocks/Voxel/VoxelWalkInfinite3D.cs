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
using Blitter.Blocks;
using Blitter.Blocks3D;

const int ChunkVoxelsX = 16;
const int ChunkVoxelsY = 64;
const int ChunkVoxelsZ = 16;

// 1 + 2 * LoadRadius chunks across each horizontal axis. 4 keeps the
// active window at 9×9 chunks (≈144×144 cells) — plenty for a sample,
// small enough that the first-frame generation hitch is bearable.
const int LoadRadius = 4;

const float PlayerRadius = 0.45f;
const float WalkSpeed = 4.5f;
const float SprintMultiplier = 1.8f;
const float JumpSpeed = 6.0f;
// The eye view above the player position.
const float EyeOffsetY = 1.3f;

// Resolve loose asset files next to this source file.
Application.Current.SetCallerAssetFolder();

// ---- Atlas
// Slice the 16x16-tile sheet once into a grid; index tiles by [col, row].
var atlas = Bitmap.Load("Blocks.png");
var tiles = TextureCatalog.Tiles(atlas, 16, 16);
var grassTopTex  = tiles[4, 4];
var grassSideTex = tiles[1, 3];
var dirtTex      = tiles[4, 3];
var stoneTex     = tiles[1, 4];
var leavesTex    = tiles[0, 1];
var logTopTex    = tiles[0, 2];
var logSideTex   = tiles[0, 3];
var plankTex     = tiles[0, 0];
var stoneBrickTex = tiles[2, 4];
var sandTex      = tiles[0, 4];

var catalog = new VoxelCatalog();
var stone = catalog.Add(new VoxelType { Name = "stone", Shape = new CubeVoxelShape(stoneTex) });
var dirt  = catalog.Add(new VoxelType { Name = "dirt",  Shape = new CubeVoxelShape(dirtTex) });
var grass = catalog.Add(new VoxelType
{
    Name = "grass",
    Shape = new CubeVoxelShape(top: grassTopTex, sides: grassSideTex, bottom: dirtTex),
});
// Leaves: an alpha-cutout cube. IsOpaque = false so neighbors keep the
// faces they share with it (you can see through the gaps to the blocks
// behind), and the cutout shader discards the transparent texels.
var leaves = catalog.Add(new VoxelType
{
    Name = "leaves",
    IsOpaque = false,
    Shape = new CubeVoxelShape(leavesTex, TransparencyMode.Cutout),
});
// Tree trunk: log top/bottom and a separate bark side.
var log = catalog.Add(new VoxelType
{
    Name = "log",
    Shape = new CubeVoxelShape(topBottom: logTopTex, sides: logSideTex),
});
// Building blocks for the spawn hut.
var planks = catalog.Add(new VoxelType { Name = "planks", Shape = new CubeVoxelShape(plankTex) });
var stoneBrick = catalog.Add(new VoxelType { Name = "stonebrick", Shape = new CubeVoxelShape(stoneBrickTex) });
var sand = catalog.Add(new VoxelType { Name = "sand", Shape = new CubeVoxelShape(sandTex) });

// Glass: a tinted, see-through block
var glassTex = MakeGlassTile();
var glass = catalog.Add(new VoxelType
{
    Name = "glass",
    IsOpaque = false,
    Shape = new CubeVoxelShape(glassTex, TransparencyMode.Blend),
});

var generator = new TerrainGenerator(catalog);

var voxelWorld = new SparseVoxelWorld(catalog, generator, new ChunkSize(ChunkVoxelsX, ChunkVoxelsY, ChunkVoxelsZ));
var chunkSource = new VoxelChunkSource3D(voxelWorld, Vector3.One, ChunkVoxelsX, ChunkVoxelsY, ChunkVoxelsZ);

var window = new Window3D
{
    Title = "Voxel Walk Infinite 3D",
    BackgroundColor = new Color(140, 180, 220),
    FullScreen = true,
    RelativeMouseMode = true,
    CloseKey = Key.Escape,
};

// The sun's world direction, shared by the directional light and the
// sky's sun disc so the bright spot painted in the sky sits exactly
// where the surfaces are lit from. Direction points from a surface
// toward the light, which is also "toward the sun".
var sunDirection = Vector3.Normalize(new Vector3(0.4f, 0.8f, 0.5f));

window.Renderer.AmbientLight = new Color(140, 150, 165);
window.Renderer.DirectionalLight = new DirectionalLight(
    sunDirection,
    new Color(140, 135, 125));
window.Renderer.TextureSampling = ImageSampling.Nearest;

// Procedural sky cubemap with a sun disc aimed along the light
// direction. The Skybox shader samples the cubemap with Z negated, so
// bake the sun at -Z to make the visible disc line up with where the
// directional light actually comes from.
var sky = Cubemaps.CreateSky(
    sunDirection: new Vector3(sunDirection.X, sunDirection.Y, -sunDirection.Z));

// DebugDraw (the HUD text overlay below) is a no-op unless a renderer
// opts in, so enable it here.
window.Renderer.DebugDrawEnabled = true;

const float spawnX = 0.5f;
const float spawnZ = 0.5f;
float groundY = TerrainGenerator.SurfaceHeight((int)spawnX, (int)spawnZ) + 1f;

var camera = new PerspectiveCamera
{
    Position = new Vector3(spawnX, groundY + EyeOffsetY, spawnZ),
    Target = new Vector3(spawnX, groundY + EyeOffsetY, spawnZ - 1f),
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
    [
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
    ]
};
chunkSource.AddSprite(player);

// Re-centers the chunk window on the player every frame and evicts
// anything that's drifted out. Runs as its own layer (before the
// playfield's update via scene entity ordering) so the playfield
// always iterates the just-recentered range.
var streamer = new ChunkStreamerLayer3D(chunkSource, playField, player, LoadRadius);

var hud = new VoxelHud3D(chunkSource, player);

// Draws the sky behind everything else each frame. The skybox shader
// strips camera translation (so the sky never moves) and writes the
// far depth, so it sits behind the opaque terrain regardless of order.
var skyLayer = new SkyLayer3D(sky);

var scene = new Scene3D
{
    Entities = [ skyLayer, streamer, playField, hud ]
};

await scene.RunAsync(window);

// ---- helpers ----------------------------------------------------------

// Builds a 16x16 colored-glass tile
static Bitmap MakeGlassTile()
{
    const int size = 16;
    var tile = Bitmap.Create(size, size);

    var body = new Color(150, 210, 235, 60);
    var border = new Color(190, 235, 250, 180);

    // Transparent clear, then draw. Opaque blend == "replace", so the
    // texture ends up with exactly these alpha values.
    tile.Render2D(new Color(0, 0, 0, 0), r =>
    {
        r.BlendMode = BlendMode.Opaque;
        r.DrawColor = border;
        r.DrawFillRect(new Rect(0, 0, size, size));
        r.DrawColor = body;
        r.DrawFillRect(new Rect(1, 1, size - 2, size - 2));
    });

    return tile;
}

// ---- types ------------------------------------------------------------

// Re-centers the chunk window on the player every frame and evicts
// anything that's drifted out. Update-only, so Draw is a no-op.
sealed class ChunkStreamerLayer3D(VoxelChunkSource3D chunkSource, ChunkedPlayField3D playField, Sprite3D player, int loadRadius) : Layer3D, IUpdatable
{
    public void Update(in EntityUpdateContext context)
    {
        var here = chunkSource.GetChunkCoords(player.Position);
        playField.MinChunk = new ChunkCoord(here.X - loadRadius, 0, here.Z - loadRadius);
        playField.MaxChunk = new ChunkCoord(here.X + loadRadius, 0, here.Z + loadRadius);
        chunkSource.TrimChunksOutside(playField.MinChunk, playField.MaxChunk);
    }

    protected override void DrawContent(Renderer3D renderer) { }
}

// HUD overlay: player position and chunk-pool statistics.
sealed class VoxelHud3D(VoxelChunkSource3D chunkSource, Sprite3D player) : Layer3D
{
    protected override void DrawContent(Renderer3D rd)
    {
        var pc = chunkSource.GetChunkCoords(player.Position);
        DebugDraw.DrawText("WASD walk   SHIFT sprint   SPACE jump   Mouse look   ESC quit", 18f, 16f);
        DebugDraw.DrawText($"pos ({player.Position.X:0.0}, {player.Position.Y:0.0}, {player.Position.Z:0.0}) chunk ({pc.X}, {pc.Z})", 18f, 48f);
        DebugDraw.DrawText($"chunks active: {chunkSource.ActiveChunkCount} pooled: {chunkSource.PooledChunkCount} allocated: {chunkSource.ChunksAllocated} reused: {chunkSource.ChunksReused}", 18f, 80f);
    }
}

// Draws the sky behind everything else each frame.
sealed class SkyLayer3D(Cubemap sky) : Layer3D
{
    protected override void DrawContent(Renderer3D rd) => rd.DrawSkybox(sky);
}

// Two-octave value-noise heightmap with a per-cell splotch noise that
// occasionally substitutes dirt or stone for grass on the surface.
// Pure functions of (x, z) so chunks can be regenerated identically
// after eviction.
sealed class TerrainGenerator : IVoxelGenerator
{
    private readonly VoxelType _grass;
    private readonly VoxelType _dirt;
    private readonly VoxelType _stone;
    private readonly VoxelType _leaves;
    private readonly VoxelType _log;
    private readonly VoxelType _planks;
    private readonly VoxelType _stoneBrick;
    private readonly VoxelType _sand;
    private readonly VoxelType _glass;

    public TerrainGenerator(VoxelCatalog catalog)
    {
        _grass = catalog["grass"];
        _dirt = catalog["dirt"];
        _stone = catalog["stone"];
        _leaves = catalog["leaves"];
        _log = catalog["log"];
        _planks = catalog["planks"];
        _stoneBrick = catalog["stonebrick"];
        _sand = catalog["sand"];
        _glass = catalog["glass"];
    }

    public void Generate(in VoxelBuffer voxels)
    {
        var bounds = voxels.Bounds;

        // Reused each column: the (up to 9) trees from the 3x3 grid
        // cells around the column that could reach it.
        Span<int> treeX = stackalloc int[9];
        Span<int> treeZ = stackalloc int[9];
        Span<int> treeBase = stackalloc int[9];

        for (int wz = bounds.Min.Z; wz <= bounds.Max.Z; wz++)
        {
            for (int wx = bounds.Min.X; wx <= bounds.Max.X; wx++)
            {
                int top = SurfaceHeight(wx, wz);
                VoxelType surface = SurfaceVoxel(wx, wz);

                // Gather trees whose trunk/leaves could reach this
                // column. Trees are placed on a world grid (pure
                // functions of world coords), so a tree centered in a
                // neighboring chunk still stamps its blocks here and the
                // two halves line up across the chunk seam.
                int treeCount = 0;
                int cellX = FloorDiv(wx, TreeCell);
                int cellZ = FloorDiv(wz, TreeCell);
                for (int dcz = -1; dcz <= 1; dcz++)
                    for (int dcx = -1; dcx <= 1; dcx++)
                    {
                        if (!TryTreeAt(cellX + dcx, cellZ + dcz, out int tx, out int tz))
                            continue;
                        // Trees only grow on grass, and never inside the
                        // spawn hut footprint (so leaves don't poke in).
                        if (SurfaceVoxel(tx, tz) != _grass)
                            continue;
                        if (InHut(tx, tz))
                            continue;
                        treeX[treeCount] = tx;
                        treeZ[treeCount] = tz;
                        treeBase[treeCount] = SurfaceHeight(tx, tz);
                        treeCount++;
                    }

                for (int wy = bounds.Min.Y; wy <= bounds.Max.Y; wy++)
                {
                    VoxelType type;
                    if (wy == 0)
                        type = _stone;
                    else if (wy < top)
                        type = _dirt;
                    else if (wy == top)
                        type = surface;
                    else
                    {
                        // Above the surface: see if any nearby tree
                        // puts a log or leaf at this voxel.
                        type = VoxelType.Air;
                        for (int t = 0; t < treeCount; t++)
                        {
                            var tv = TreeVoxel(
                                wx - treeX[t], wz - treeZ[t], wy - treeBase[t]);
                            if (!tv.IsAir)
                            {
                                type = tv;
                                break;
                            }
                        }
                    }

                    // The spawn hut overrides terrain and trees.
                    var structure = StructureVoxel(wx, wy, wz);
                    if (structure is not null)
                        type = structure;
                    voxels[wx, wy, wz] = type;
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

    public VoxelType SurfaceVoxel(int x, int z)
    {
        float n = ValueNoise(x * 0.45f, z * 0.45f) * 0.65f
                + ValueNoise(x * 0.15f, z * 0.15f) * 0.35f;
        if (n > 0.78f) return _stone;
        if (n > 0.58f) return _dirt;
        return _grass;
    }

    // ---- Trees -------------------------------------------------------
    // Tree placement grid: at most one tree per TreeCell x TreeCell
    // block of world columns, jittered within the cell. Spacing keeps
    // neighboring crowns from merging into a solid canopy.
    const int TreeCell = 6;

    // Deterministic per-cell tree placement. Returns the trunk column
    // (tx, tz) for grid cell (cellX, cellZ), or false if that cell has
    // no tree. Pure function of the cell coords so every chunk that
    // touches the tree agrees on where it is.
    static bool TryTreeAt(int cellX, int cellZ, out int tx, out int tz)
    {
        uint h = HashU(cellX, cellZ);
        // ~40% of cells grow a tree.
        if ((h & 0xFFFF) / 65535f > 0.40f)
        {
            tx = tz = 0;
            return false;
        }
        // Jitter the trunk to an inner 2..4 offset so trees in adjacent
        // cells stay at least a few columns apart.
        int ox = 2 + (int)((h >> 16) % 3u);
        int oz = 2 + (int)((h >> 20) % 3u);
        tx = cellX * TreeCell + ox;
        tz = cellZ * TreeCell + oz;
        return true;
    }

    // The blocky "small oak" shape, evaluated relative to the trunk
    // base. dx/dz are the column offset from the trunk; rel is the
    // height above the surface the trunk sits on. Returns the log type,
    // the leaf type, or air for empty.
    VoxelType TreeVoxel(int dx, int dz, int rel)
    {
        const int trunkHeight = 4;

        // Trunk: a single column of logs.
        if (dx == 0 && dz == 0 && rel >= 1 && rel <= trunkHeight)
            return _log;

        int reach = Math.Max(Math.Abs(dx), Math.Abs(dz));
        // Two wide leaf layers around the top, corners trimmed.
        if (rel == trunkHeight - 1 || rel == trunkHeight)
        {
            if (reach <= 2 && !(Math.Abs(dx) == 2 && Math.Abs(dz) == 2))
                return _leaves;
        }
        // A 3x3 layer above them.
        else if (rel == trunkHeight + 1)
        {
            if (reach <= 1)
                return _leaves;
        }
        // A small plus-shaped cap.
        else if (rel == trunkHeight + 2)
        {
            if (Math.Abs(dx) + Math.Abs(dz) <= 1)
                return _leaves;
        }
        return VoxelType.Air;
    }

    // Floor division (C# integer division truncates toward zero, which
    // would mis-bucket negative world coords into the wrong cell).
    static int FloorDiv(int a, int b)
        => a >= 0 ? a / b : -((-a + b - 1) / b);

    // ---- Spawn hut ---------------------------------------------------
    // One fixed 5x5 hut a few blocks in front of the spawn point
    // (camera looks toward -Z). Stone-brick walls, plank floor and
    // roof, a sand foundation, a doorway facing the player, and three
    // empty window slots that the glass step will fill.
    const int HutMinX = -2;
    const int HutMinZ = -9;
    const int HutSize = 5;

    static bool InHut(int x, int z)
        => x >= HutMinX && x < HutMinX + HutSize
        && z >= HutMinZ && z < HutMinZ + HutSize;

    // Returns the hut's voxel at (wx, wy, wz): a block type,
    // <see cref="VoxelType.Air"/> to force air (interior, doorway,
    // window slots), or <c>null</c> for "no opinion" so the terrain
    // shows through.
    VoxelType? StructureVoxel(int wx, int wy, int wz)
    {
        if (!InHut(wx, wz))
            return null;

        int lx = wx - HutMinX;
        int lz = wz - HutMinZ;
        int baseY = SurfaceHeight(HutMinX + 2, HutMinZ + 2);
        int rel = wy - baseY;

        if (rel < -2) return null;          // deep ground unchanged
        if (rel < 0) return _sand;          // foundation pad
        if (rel == 0) return _planks;       // floor
        if (rel == 4) return _planks;       // roof
        if (rel > 4) return null;           // open sky above

        // Wall band (rel 1..3).
        bool perimeter = lx == 0 || lx == HutSize - 1 || lz == 0 || lz == HutSize - 1;
        if (!perimeter)
            return VoxelType.Air;           // hollow interior

        // Doorway: front wall (max Z, facing spawn), center column, two
        // voxels tall.
        bool frontWall = lz == HutSize - 1;
        if (frontWall && lx == 2 && (rel == 1 || rel == 2))
            return VoxelType.Air;

        // Window slots at eye height on the back wall and the two sides.
        bool backWall = lz == 0;
        bool sideWall = lx == 0 || lx == HutSize - 1;
        bool windowSlot =
            (backWall && lx == 2) ||
            (sideWall && lz == 2);
        if (rel == 2 && windowSlot)
            return _glass;                  // tinted pane

        return _stoneBrick;
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

    // Integer hash used for tree placement (same mix as Hash01, but
    // returns the raw bits so callers can pull several values out).
    static uint HashU(int x, int z)
    {
        uint h = (uint)(x * 374761393) ^ (uint)(z * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        return h;
    }
}
