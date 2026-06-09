using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks3D;

/// <summary>
/// <see cref="HitShape3D"/> for a single voxel chunk. Each solid cell
/// contributes one box primitive; collision queries are clipped to the
/// cells overlapping the other shape's bounding sphere so cost scales
/// with the query, not the chunk size.
/// </summary>
internal sealed class VoxelHitShape3D : HitShape3D
{
    private readonly VoxelChunkGrid _grid;
    private readonly Vector3 _halfCell;
    private readonly Vector3 _localSize;
    private readonly BoundingSphere _localBoundary;

    // Tight Y band of solid cells: layers below _solidMinY and above
    // _solidMaxY are all air, so queries can skip them outright. Cached
    // against the chunk's change stamp; _bandVersion != _grid.Version
    // forces a rescan (an edit may have added or removed solid layers).
    private int _solidMinY;
    private int _solidMaxY;
    private int _bandVersion = -1;

    public VoxelHitShape3D(VoxelChunkGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        _grid = grid;
        _halfCell = grid.CellSize * 0.5f;
        _localSize = new Vector3(grid.CellsX, grid.CellsY, grid.CellsZ) * grid.CellSize;
        _localBoundary = new BoundingSphere(_localSize * 0.5f, _localSize.Length() * 0.5f);
    }

    /// <summary>The shared per-chunk data this shape reads from.</summary>
    public VoxelChunkGrid Grid => _grid;

    /// <summary>
    /// Drops the cached solid-Y band so it is rescanned on the next query.
    /// Used when the owning chunk is recycled onto a new coord (the grid's
    /// data changed wholesale) and the band can no longer be trusted.
    /// </summary>
    internal void Invalidate() => _bandVersion = -1;

    public override BoundingSphere LocalBoundary => _localBoundary;

    // Conservative upper bound: total cells in the chunk. Counting
    // only solid cells would require a full scan per call.
    public override int PrimitiveCount => _grid.CellsX * _grid.CellsY * _grid.CellsZ;

    public override bool TestHit(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester)
    {
        var range = ComputeCellRange(in mine, other.BoundingSphere);
        for (int y = range.MinY; y <= range.MaxY; y++)
        for (int z = range.MinZ; z <= range.MaxZ; z++)
        for (int x = range.MinX; x <= range.MaxX; x++)
        {
            if (!IsSolid(x, y, z))
                continue;
            var box = MakeBoxPrimitive(in mine, x, y, z);
            if (other.Shape.TestHit(in other.Pose, in box, tester))
                return true;
        }
        return false;
    }

    public override bool TestHit(in Pose3D mine, in HitPrimitive3D otherPrim, HitTester3D tester)
    {
        var range = ComputeCellRange(in mine, PrimBoundingSphere(in otherPrim));
        for (int y = range.MinY; y <= range.MaxY; y++)
        for (int z = range.MinZ; z <= range.MaxZ; z++)
        for (int x = range.MinX; x <= range.MaxX; x++)
        {
            if (!IsSolid(x, y, z))
                continue;
            var box = MakeBoxPrimitive(in mine, x, y, z);
            if (tester.TestHit(in box, in otherPrim))
                return true;
        }
        return false;
    }

    public override bool TryGetContact(in Pose3D mine, in PosedHitShape3D other, HitTester3D tester, out HitContact3D contact)
    {
        var range = ComputeCellRange(in mine, other.BoundingSphere);
        bool found = false;
        HitContact3D best = default;
        for (int y = range.MinY; y <= range.MaxY; y++)
        for (int z = range.MinZ; z <= range.MaxZ; z++)
        for (int x = range.MinX; x <= range.MaxX; x++)
        {
            if (!IsSolid(x, y, z))
                continue;
            var box = MakeBoxPrimitive(in mine, x, y, z);
            // other.TryGetContact(box) returns "from box → other"; flip
            // to "from other → me".
            if (other.Shape.TryGetContact(in other.Pose, in box, tester, out var c))
            {
                c = c.Flipped();
                if (!found || c.Penetration > best.Penetration)
                {
                    best = c;
                    found = true;
                }
            }
        }
        contact = best;
        return found;
    }

    public override bool TryGetContact(in Pose3D mine, in HitPrimitive3D otherPrim, HitTester3D tester, out HitContact3D contact)
    {
        var range = ComputeCellRange(in mine, PrimBoundingSphere(in otherPrim));
        bool found = false;
        HitContact3D best = default;
        for (int y = range.MinY; y <= range.MaxY; y++)
        for (int z = range.MinZ; z <= range.MaxZ; z++)
        for (int x = range.MinX; x <= range.MaxX; x++)
        {
            if (!IsSolid(x, y, z))
                continue;
            var box = MakeBoxPrimitive(in mine, x, y, z);
            // tester.TryGetContact(box, otherPrim) returns "from otherPrim → box"
            // = "from external → me". Receiver convention; no flip.
            if (tester.TryGetContact(in box, in otherPrim, out var c)
                && (!found || c.Penetration > best.Penetration))
            {
                best = c;
                found = true;
            }
        }
        contact = best;
        return found;
    }

    public override void Visit(in Pose3D mine, HitPrimitiveAction3D action)
    {
        for (int y = 0; y < _grid.CellsY; y++)
        for (int z = 0; z < _grid.CellsZ; z++)
        for (int x = 0; x < _grid.CellsX; x++)
        {
            if (!IsSolid(x, y, z))
                continue;
            var box = MakeBoxPrimitive(in mine, x, y, z);
            action(in box);
        }
    }

    private bool IsSolid(int x, int y, int z) =>
        _grid.GetVoxel(x, y, z).Type.Shape.FillsVoxel;

    private HitPrimitive3D MakeBoxPrimitive(in Pose3D mine, int x, int y, int z)
    {
        var local = new Vector3(
            (x + 0.5f) * _grid.CellSize.X,
            (y + 0.5f) * _grid.CellSize.Y,
            (z + 0.5f) * _grid.CellSize.Z);
        return HitPrimitive3D.Box(
            mine.Transform(local),
            _halfCell * mine.Scale,
            mine.Rotation);
    }

    private CellRange ComputeCellRange(in Pose3D mine, in BoundingSphere worldSphere)
    {
        // Bring the world-space query sphere into chunk-local space.
        var delta = worldSphere.Center - mine.Position;
        var localCenter = Vector3.Transform(delta, Quaternion.Inverse(mine.Rotation)) / mine.Scale;
        var localRadius = worldSphere.Radius / mine.Scale;
        var r = new Vector3(localRadius);
        var minLocal = localCenter - r;
        var maxLocal = localCenter + r;

        int minX = Math.Max(0, (int)MathF.Floor(minLocal.X / _grid.CellSize.X));
        int minY = Math.Max(0, (int)MathF.Floor(minLocal.Y / _grid.CellSize.Y));
        int minZ = Math.Max(0, (int)MathF.Floor(minLocal.Z / _grid.CellSize.Z));
        int maxX = Math.Min(_grid.CellsX - 1, (int)MathF.Floor(maxLocal.X / _grid.CellSize.X));
        int maxY = Math.Min(_grid.CellsY - 1, (int)MathF.Floor(maxLocal.Y / _grid.CellSize.Y));
        int maxZ = Math.Min(_grid.CellsZ - 1, (int)MathF.Floor(maxLocal.Z / _grid.CellSize.Z));

        // Tighten Y to the cached solid-layer band so empty top/bottom
        // slabs cost nothing per query.
        EnsureSolidYBand(out var bandMin, out var bandMax);
        if (bandMin > bandMax || maxY < bandMin || minY > bandMax)
            return new CellRange(0, 0, 0, -1, -1, -1);
        if (minY < bandMin) minY = bandMin;
        if (maxY > bandMax) maxY = bandMax;

        return new CellRange(minX, minY, minZ, maxX, maxY, maxZ);
    }

    private void EnsureSolidYBand(out int minY, out int maxY)
    {
        var version = _grid.Version;
        if (_bandVersion == version)
        {
            minY = _solidMinY;
            maxY = _solidMaxY;
            return;
        }

        int foundMin = int.MaxValue;
        int foundMax = -1;
        for (int y = 0; y < _grid.CellsY; y++)
        {
            if (!LayerHasSolid(y))
                continue;
            if (y < foundMin)
                foundMin = y;
            foundMax = y;
        }
        // Empty chunk: encode an empty band (min > max) so callers skip
        // the loops entirely without rescanning every frame.
        _solidMinY = foundMax < 0 ? _grid.CellsY : foundMin;
        _solidMaxY = foundMax;
        _bandVersion = version;
        minY = _solidMinY;
        maxY = _solidMaxY;
    }

    private bool LayerHasSolid(int y)
    {
        for (int z = 0; z < _grid.CellsZ; z++)
        for (int x = 0; x < _grid.CellsX; x++)
        {
            if (IsSolid(x, y, z))
                return true;
        }
        return false;
    }

    private static BoundingSphere PrimBoundingSphere(in HitPrimitive3D p) => p.Kind switch
    {
        HitKind3D.Sphere => new BoundingSphere(p.P0, p.R),
        HitKind3D.Capsule => new BoundingSphere((p.P0 + p.P1) * 0.5f, (p.P0 - p.P1).Length() * 0.5f + p.R),
        HitKind3D.Cylinder => new BoundingSphere((p.P0 + p.P1) * 0.5f, (p.P0 - p.P1).Length() * 0.5f + p.R),
        HitKind3D.Box => new BoundingSphere(p.P0, p.P1.Length()),
        HitKind3D.Wall => new BoundingSphere(p.P0, new Vector2(p.P1.X, p.P1.Y).Length()),
        HitKind3D.Triangle => TriangleBoundingSphere(p.P0, p.P1, new Vector3(p.Q.X, p.Q.Y, p.Q.Z)),
        _ => new BoundingSphere(p.P0, 0f),
    };

    private static BoundingSphere TriangleBoundingSphere(Vector3 v0, Vector3 v1, Vector3 v2)
    {
        var c = (v0 + v1 + v2) / 3f;
        var r2 = Math.Max(
            (v0 - c).LengthSquared(),
            Math.Max((v1 - c).LengthSquared(), (v2 - c).LengthSquared()));
        return new BoundingSphere(c, MathF.Sqrt(r2));
    }

    private readonly struct CellRange
    {
        public readonly int MinX, MinY, MinZ, MaxX, MaxY, MaxZ;
        public CellRange(int minX, int minY, int minZ, int maxX, int maxY, int maxZ)
        {
            MinX = minX; MinY = minY; MinZ = minZ;
            MaxX = maxX; MaxY = maxY; MaxZ = maxZ;
        }
    }
}
