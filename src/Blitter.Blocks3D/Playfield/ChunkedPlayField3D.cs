using System.Numerics;
using Blitter.Bits;

namespace Blitter.Blocks3D;

public class ChunkedPlayField3D : Layer3D
{
    // Reused frame-local scratch — populated each Update, holds every
    // alive sprite in the active range with its current chunk and the
    // sprite's index in iteration order (used to dedup sprite-vs-sprite
    // pairs across chunks).
    private readonly List<(Sprite3D Sprite, Chunk3D Chunk)> _frameSprites = 
        new();

    private readonly Dictionary<Sprite3D, int> _frameSpriteIndex =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// The source of chunked playfield data.
    /// </summary>
    public ChunkSource3D ChunkSource { get; }

    /// <summary>
    /// The mininum chunk coordinate to update and draw
    /// </summary>
    public ChunkCoord MinChunk { get; set; }

    /// <summary>
    /// The maximum chunk coordinate to update and draw (inclusive)
    /// </summary>
    public ChunkCoord MaxChunk { get; set; }
 
    public ChunkedPlayField3D(
        ChunkSource3D chunkSource, 
        ChunkCoord minChunk, 
        ChunkCoord maxChunk)
    {
        ChunkSource = chunkSource;
        MinChunk = minChunk;
        MaxChunk = maxChunk;
    }

    public override void Update(in UpdateContext3D context)
    {
        ChunkSource.Update(context);

        // Phase 0: open the frame on every active chunk so any add/remove
        // (rebucket, reap, collision-spawned items) is deferred to end of frame.
        for (int y = MinChunk.Y; y <= MaxChunk.Y; y++)
        for (int z = MinChunk.Z; z <= MaxChunk.Z; z++)
        for (int x = MinChunk.X; x <= MaxChunk.X; x++)
        {
            var coord = new ChunkCoord(x, y, z);
            ChunkSource.GetChunk(in coord)?.BeginFrame();
        }

        // Phase 1: per-chunk update (barrier ticks, plus subclass hooks like
        // voxel remeshing). Runs before sprites so sprite-vs-barrier sees
        // this frame's barrier geometry.
        for (int y = MinChunk.Y; y <= MaxChunk.Y; y++)
        for (int z = MinChunk.Z; z <= MaxChunk.Z; z++)
        for (int x = MinChunk.X; x <= MaxChunk.X; x++)
        {
            var coord = new ChunkCoord(x, y, z);
            ChunkSource.GetChunk(in coord)?.Update(context);
        }

        // Snapshot alive sprites in the active range with a stable order
        // so cross-chunk pair dedup works.
        _frameSprites.Clear();
        _frameSpriteIndex.Clear();
        for (int y = MinChunk.Y; y <= MaxChunk.Y; y++)
        for (int z = MinChunk.Z; z <= MaxChunk.Z; z++)
        for (int x = MinChunk.X; x <= MaxChunk.X; x++)
        {
            var coord = new ChunkCoord(x, y, z);
            var chunk = ChunkSource.GetChunk(in coord);
            if (chunk == null)
                continue;
            var sprites = chunk.Sprites;
            for (int i = 0; i < sprites.Count; i++)
            {
                var s = sprites[i];
                if (!s.IsAlive)
                    continue;
                _frameSpriteIndex[s] = _frameSprites.Count;
                _frameSprites.Add((s, chunk));
            }
        }

        // Phase 2: sprite ticks. Behaviors may move the sprite, kill it,
        // or spawn new sprites (those go into pending and arrive next frame).
        for (int i = 0; i < _frameSprites.Count; i++)
        {
            var s = _frameSprites[i].Sprite;
            if (s.IsAlive)
                s.Update(context);
        }

        // Phase 3: rebucket sprites that crossed chunk boundaries. Host
        // doesn't change — both chunks belong to the same source. The
        // add lands in the destination chunk's pending list, so the
        // sprite is not double-processed this frame.
        for (int i = 0; i < _frameSprites.Count; i++)
        {
            var (s, chunk) = _frameSprites[i];
            if (!s.IsAlive)
                continue;
            var newCoord = ChunkSource.GetChunkCoords(s.Position);
            if (newCoord.Equals(chunk.Coord))
                continue;
            var newChunk = ChunkSource.GetChunk(in newCoord);
            if (newChunk == null || newChunk == chunk)
                continue;
            chunk.RemoveSprite(s);
            newChunk.AddSprite(s);
        }

        // Phase 4: collision.
        RunCollisionPass(context);

        // Phase 5: reap dead sprites from whichever chunk currently buckets them.
        for (int i = 0; i < _frameSprites.Count; i++)
        {
            var (s, chunk) = _frameSprites[i];
            if (!s.IsAlive)
                chunk.RemoveSprite(s);
        }

        // Phase 6: flush pending on every chunk we touched.
        for (int y = MinChunk.Y; y <= MaxChunk.Y; y++)
        for (int z = MinChunk.Z; z <= MaxChunk.Z; z++)
        for (int x = MinChunk.X; x <= MaxChunk.X; x++)
        {
            var coord = new ChunkCoord(x, y, z);
            ChunkSource.GetChunk(in coord)?.EndFrame();
        }
    }

    private void RunCollisionPass(in UpdateContext3D context)
    {
        for (int i = 0; i < _frameSprites.Count; i++)
        {
            var sprite = _frameSprites[i].Sprite;
            if (!sprite.IsAlive || !sprite.CanBeHit)
                continue;

            var shape = sprite.HitShape;
            var bs = shape.BoundingSphere;
            if (bs.Radius <= 0f)
                continue;

            // Chunk range overlapped by the sprite's bounding sphere, clamped to the active range.
            // Anything outside is treated as not loaded for collision purposes.
            var r = new Vector3(bs.Radius);
            var qMin = ChunkSource.GetChunkCoords(bs.Center - r);
            var qMax = ChunkSource.GetChunkCoords(bs.Center + r);
            var minX = Math.Max(qMin.X, MinChunk.X);
            var minY = Math.Max(qMin.Y, MinChunk.Y);
            var minZ = Math.Max(qMin.Z, MinChunk.Z);
            var maxX = Math.Min(qMax.X, MaxChunk.X);
            var maxY = Math.Min(qMax.Y, MaxChunk.Y);
            var maxZ = Math.Min(qMax.Z, MaxChunk.Z);

            for (int qy = minY; qy <= maxY && sprite.IsAlive; qy++)
            for (int qz = minZ; qz <= maxZ && sprite.IsAlive; qz++)
            for (int qx = minX; qx <= maxX && sprite.IsAlive; qx++)
            {
                var qcoord = new ChunkCoord(qx, qy, qz);
                var qchunk = ChunkSource.GetChunk(in qcoord);
                if (qchunk == null)
                    continue;

                // sprite-vs-sprite: each pair tested once via index order.
                var qSprites = qchunk.Sprites;
                for (int k = 0; k < qSprites.Count && sprite.IsAlive; k++)
                {
                    var other = qSprites[k];
                    if (other == sprite || !other.IsAlive || !other.CanBeHit)
                        continue;
                    if (!_frameSpriteIndex.TryGetValue(other, out var j) || j <= i)
                        continue;
                    var otherShape = other.HitShape;
                    if (otherShape.BoundingSphere.Radius <= 0f)
                        continue;
                    if (!shape.TestHit(otherShape))
                        continue;
                    sprite.OnHitSprite(other, context);
                    if (sprite.IsAlive && other.IsAlive)
                        other.OnHitSprite(sprite, context);
                }

                // sprite-vs-barrier: no dedup; barriers don't pair with each other.
                var qBarriers = qchunk.Barriers;
                for (int k = 0; k < qBarriers.Count && sprite.IsAlive; k++)
                {
                    var barrier = qBarriers[k];
                    if (!shape.TestHit(barrier.HitShape))
                        continue;
                    sprite.OnHitBarrier(barrier, context);
                    if (sprite.IsAlive)
                        barrier.OnHitSprite(sprite, context);
                }
            }
        }
    }
    
    public override void Draw(Renderer3D renderer)
    {
        for (int y = this.MinChunk.Y; y <= this.MaxChunk.Y; y++)
        {
            for (int z = this.MinChunk.Z; z <= this.MaxChunk.Z; z++)
            {
                for (int x = this.MinChunk.X; x <= this.MaxChunk.X; x++)
                {
                    var coord = new ChunkCoord(x, y, z);
                    var chunk = ChunkSource.GetChunk(in coord);
                    chunk?.Draw(renderer);
                }
            }
        }       
    }
}

public readonly record struct ChunkCoord(int X, int Y, int Z);

/// <summary>
/// A source of chunked playfield data. 
/// Acts as the <see cref="ISpriteHost3D"/> for every sprite in any of its chunks.
/// </summary>
public abstract class ChunkSource3D : ISpriteHost3D
{
    /// <summary>
    /// The size of each chunk in world units.
    /// </summary>
    public abstract Vector3 ChunkSize { get; }

    /// <summary>
    /// Gets the chunk coordinates for the given world position.
    /// </summary>
    public virtual ChunkCoord GetChunkCoords(Vector3 position)
    {
        return new ChunkCoord(
                (int)Math.Floor(position.X / ChunkSize.X),
                (int)Math.Floor(position.Y / ChunkSize.Y),
                (int)Math.Floor(position.Z / ChunkSize.Z)
                );
    }

    /// <summary>
    /// Gets the bounding box for the chunk at the given chunk coordinates.
    /// </summary>
    public virtual BoundingBox GetChunkBounds(in ChunkCoord coord)
    {
        var min = new Vector3(coord.X * ChunkSize.X, coord.Y * ChunkSize.Y, coord.Z * ChunkSize.Z);
        var max = min + ChunkSize;
        return new BoundingBox(min, max);
    }

    /// <summary>
    /// Gets the chunk at the given chunk coordinates.
    /// If the chunk is out of bounds or not loaded, returns null.
    /// </summary>
    public abstract Chunk3D? GetChunk(in ChunkCoord coord);

    /// <summary>
    /// Gets the chunk at the given world coordinates.
    /// If the chunk is out of bounds or not loaded, returns null.
    /// </summary>
    public Chunk3D? GetChunk(Vector3 position)
    {
        var coord = GetChunkCoords(position);
        return GetChunk(in coord);
    }

    public virtual void Update(in UpdateContext3D context)
    {
        this.Elapsed += context.ElapsedSinceLastUpdate;
    }

    #region ISpriteHost3D

    /// <inheritdoc/>
    public TimeSpan Elapsed { get; private set; }

    /// <summary>
    /// Adds <paramref name="sprite"/> to the chunk that contains its current <see cref="Sprite3D.Position"/>.
    /// </summary>
    public virtual void AddSprite(Sprite3D sprite)
    {
        var chunk = GetChunk(sprite.Position);
        chunk?.AddSprite(sprite);
    }

    /// <summary>
    /// Removes <paramref name="sprite"/> from the chunk that contains its current <see cref="Sprite3D.Position"/>.
    /// </summary>
    public virtual void RemoveSprite(Sprite3D sprite)
    {
        var chunk = GetChunk(sprite.Position);
        chunk?.RemoveSprite(sprite);
    }

    /// <summary>
    /// Adds <paramref name="barrier"/> to the chunk that contains its current <see cref="Barrier3D.Position"/>.
    /// </summary>
    public virtual void AddBarrier(Barrier3D barrier)
    {
        var chunk = GetChunk(barrier.Position);
        chunk?.AddBarrier(barrier);
    }

    /// <summary>
    /// Removes <paramref name="barrier"/> from the chunk that contains its current <see cref="Barrier3D.Position"/>.
    /// </summary>
    public virtual void RemoveBarrier(Barrier3D barrier)
    {
        var chunk = GetChunk(barrier.Position);
        chunk?.RemoveBarrier(barrier);
    }

    #endregion
}

/// <summary>
/// A spatial bucket of sprites and barriers owned by a <see cref="ChunkSource3D"/>. 
/// </summary>
public class Chunk3D
{
    private readonly List<Sprite3D> _sprites = new();
    private readonly List<Barrier3D> _barriers = new();

    // Mutation during Update goes through these and is flushed at end of frame.
    private readonly List<Sprite3D> _pendingAddSprites = new();
    private readonly List<Sprite3D> _pendingRemoveSprites = new();
    private readonly List<Barrier3D> _pendingAddBarriers = new();
    private readonly List<Barrier3D> _pendingRemoveBarriers = new();
    private bool _updating;

    public Chunk3D(ChunkSource3D source, ChunkCoord coord)
    {
        Source = source;
        Coord = coord;
    }

    /// <summary>The source that owns this chunk.</summary>
    public ChunkSource3D Source { get; }

    /// <summary>This chunk's integer coordinates in the source's grid.</summary>
    public ChunkCoord Coord { get; }

    /// <summary>Sprites currently bucketed in this chunk.</summary>
    public IReadOnlyList<Sprite3D> Sprites => _sprites;

    /// <summary>Barriers currently bucketed in this chunk.</summary>
    public IReadOnlyList<Barrier3D> Barriers => _barriers;

    /// <summary>
    /// Buckets <paramref name="sprite"/> into this chunk. Safe to call
    /// during <see cref="Update"/>; the change is applied at end of frame.
    /// </summary>
    public void AddSprite(Sprite3D sprite)
    {
        if (_updating)
        {
            _pendingRemoveSprites.Remove(sprite);
            _pendingAddSprites.Add(sprite);
        }
        else
        {
            _sprites.Add(sprite);
        }
    }

    /// <summary>
    /// Removes <paramref name="sprite"/> from this chunk's bucket. Safe
    /// to call during <see cref="Update"/>.
    /// </summary>
    public void RemoveSprite(Sprite3D sprite)
    {
        if (_updating)
        {
            _pendingAddSprites.Remove(sprite);
            _pendingRemoveSprites.Add(sprite);
        }
        else
        {
            _sprites.Remove(sprite);
        }
    }

    /// <summary>
    /// Buckets <paramref name="barrier"/> into this chunk. Safe to call
    /// during <see cref="Update"/>.
    /// </summary>
    public void AddBarrier(Barrier3D barrier)
    {
        if (_updating)
        {
            _pendingRemoveBarriers.Remove(barrier);
            _pendingAddBarriers.Add(barrier);
        }
        else
        {
            _barriers.Add(barrier);
        }
    }

    /// <summary>
    /// Removes <paramref name="barrier"/> from this chunk's bucket. Safe
    /// to call during <see cref="Update"/>.
    /// </summary>
    public void RemoveBarrier(Barrier3D barrier)
    {
        if (_updating)
        {
            _pendingAddBarriers.Remove(barrier);
            _pendingRemoveBarriers.Add(barrier);
        }
        else
        {
            _barriers.Remove(barrier);
        }
    }

    /// <summary>
    /// Per-frame chunk update hook. Default ticks every barrier in the
    /// chunk. Subclasses override for chunk-scoped work like voxel
    /// remeshing. Sprite ticks, rebucketing, collision, and dead-sprite
    /// reaping are orchestrated by <see cref="ChunkedPlayField3D"/>.
    /// </summary>
    public virtual void Update(in UpdateContext3D context)
    {
        for (int i = 0; i < _barriers.Count; i++)
            _barriers[i].Update(context);
    }

    // Called by ChunkedPlayField3D at the start of its Update. While
    // open, Add/Remove calls land in pending lists and are applied by
    // EndFrame. Lets the playfield (and collision callbacks) mutate the
    // chunk safely while it's also iterating the sprite/barrier lists.
    internal void BeginFrame() => _updating = true;

    // Called by ChunkedPlayField3D after the collision pass. Flushes
    // pending adds/removes and closes the frame.
    internal void EndFrame()
    {
        _updating = false;
        ApplyPendingChanges();
    }

    public virtual void Draw(Renderer3D renderer)
    {
        for (int i = 0; i < _barriers.Count; i++)
            _barriers[i].Draw(renderer);

        for (int i = 0; i < _sprites.Count; i++)
        {
            var s = _sprites[i];
            if (s.IsAlive)
                s.Draw(renderer);
        }
    }

    private void ApplyPendingChanges()
    {
        if (_pendingRemoveSprites.Count > 0)
        {
            foreach (var s in _pendingRemoveSprites)
                _sprites.Remove(s);
            _pendingRemoveSprites.Clear();
        }
        if (_pendingAddSprites.Count > 0)
        {
            _sprites.AddRange(_pendingAddSprites);
            _pendingAddSprites.Clear();
        }
        if (_pendingRemoveBarriers.Count > 0)
        {
            foreach (var b in _pendingRemoveBarriers)
                _barriers.Remove(b);
            _pendingRemoveBarriers.Clear();
        }
        if (_pendingAddBarriers.Count > 0)
        {
            _barriers.AddRange(_pendingAddBarriers);
            _pendingAddBarriers.Clear();
        }
    }
}


public abstract class GeneratedChunkSource3D : ChunkSource3D
{
    private readonly Dictionary<ChunkCoord, Chunk3D> _chunks = new();
    private readonly List<ChunkCoord> _trimScratch = new();

    public override Chunk3D? GetChunk(in ChunkCoord coord)
    {
        if (!_chunks.TryGetValue(coord, out var chunk))
        {
            chunk = GenerateChunk(coord);
            if (chunk != null)
            {
                _chunks[coord] = chunk;
            }
        }

        return chunk;
    }

    protected abstract Chunk3D? GenerateChunk(in ChunkCoord coord);

    /// <summary>
    /// Drops every loaded chunk whose coord falls outside the inclusive box <paramref name="min"/>..<paramref name="max"/>. 
    /// Call this/ after updating <see cref="ChunkedPlayField3D.MinChunk"/> / <see cref="ChunkedPlayField3D.MaxChunk"/> each frame to bound
    /// memory while a viewer walks an unbounded world.
    /// </summary>
    public void TrimChunksOutside(ChunkCoord min, ChunkCoord max)
    {
        if (_chunks.Count == 0)
            return;
        _trimScratch.Clear();
        foreach (var key in _chunks.Keys)
        {
            if (key.X < min.X || key.X > max.X ||
                key.Y < min.Y || key.Y > max.Y ||
                key.Z < min.Z || key.Z > max.Z)
            {
                _trimScratch.Add(key);
            }
        }
        for (int i = 0; i < _trimScratch.Count; i++)
        {
            var key = _trimScratch[i];
            var chunk = _chunks[key];
            _chunks.Remove(key);
            OnChunkUnloaded(chunk);
        }
    }

    /// <summary>
    /// Hook invoked once per chunk evicted by
    /// <see cref="TrimChunksOutside"/>. Default does nothing; subclasses
    /// override to release any resources tied to the chunk (voxel
    /// storage, GPU buffers, etc.).
    /// </summary>
    protected virtual void OnChunkUnloaded(Chunk3D chunk) { }
}