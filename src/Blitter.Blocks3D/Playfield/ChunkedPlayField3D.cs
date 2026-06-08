using System.Numerics;
using Blitter.Bits;

namespace Blitter.Blocks3D;

public class ChunkedPlayField3D : Layer3D
{
    // Reused frame-local scratch — populated each Update, holds every
    // alive sprite in the active range with its current chunk and the
    // sprite's index in iteration order (used to dedup sprite-vs-sprite
    // pairs across chunks).
    private readonly List<(Sprite3D Sprite, IChunk3D Chunk)> _frameSprites = 
        new();

    private readonly Dictionary<Sprite3D, int> _frameSpriteIndex =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// The source of chunked playfield data.
    /// </summary>
    public IChunkSource3D ChunkSource { get; }

    /// <summary>
    /// The mininum chunk coordinate to update and draw
    /// </summary>
    public ChunkCoord MinChunk { get; set; }

    /// <summary>
    /// The maximum chunk coordinate to update and draw (inclusive)
    /// </summary>
    public ChunkCoord MaxChunk { get; set; }
 
    public ChunkedPlayField3D(
        IChunkSource3D chunkSource, 
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
/// The read contract <see cref="ChunkedPlayField3D"/> depends on: query the
/// chunk grid and fetch the chunk for a coord/position. A source is also the
/// <see cref="ISpriteHost3D"/> for every sprite in any of its chunks. The
/// basic stateful implementation lives in <see cref="ChunkSource3D"/>.
/// </summary>
public interface IChunkSource3D : ISpriteHost3D
{
    /// <summary>The size of each chunk in world units.</summary>
    Vector3 ChunkSize { get; }

    /// <summary>Gets the chunk coordinates for the given world position.</summary>
    ChunkCoord GetChunkCoords(Vector3 position);

    /// <summary>Gets the world-space bounding box of the chunk at <paramref name="coord"/>.</summary>
    BoundingBox GetChunkBounds(in ChunkCoord coord);

    /// <summary>Gets the chunk at the given chunk coordinates, or null if absent.</summary>
    IChunk3D? GetChunk(in ChunkCoord coord);

    /// <summary>Gets the chunk at the given world position, or null if absent.</summary>
    IChunk3D? GetChunk(Vector3 position);

    /// <summary>Per-frame source tick (advances the host clock).</summary>
    void Update(in UpdateContext3D context);
}

/// <summary>
/// Basic <see cref="IChunkSource3D"/> implementation: lazily generates each
/// chunk on first access, caches it, and can evict chunks outside an active
/// range. Also acts as the <see cref="ISpriteHost3D"/> for every sprite in any
/// of its chunks. Subclasses supply <see cref="CreateChunk"/>.
/// </summary>
public abstract class ChunkSource3D : IChunkSource3D
{
    private readonly Dictionary<ChunkCoord, IChunk3D> _chunks = new();
    private readonly List<ChunkCoord> _trimScratch = new();
    private readonly Stack<Chunk3D> _pool = new();

    /// <summary>Number of chunks currently loaded (in the active window).</summary>
    public int ActiveChunkCount => _chunks.Count;

    /// <summary>Number of evicted chunks held in the reuse pool.</summary>
    public int PooledChunkCount => _pool.Count;

    /// <summary>
    /// The total number of chunk allocations. 
    /// </summary>
    public int ChunksAllocated { get; private set; }

    /// <summary>
    /// The total number of times a pooled chunk reused.
    /// </summary>
    public int ChunksReused { get; private set; }

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
    /// Gets the chunk at the given chunk coordinates, generating and
    /// caching it on first access. Returns null when
    /// <see cref="CreateChunk"/> declines to populate the coord.
    /// </summary>
    public IChunk3D? GetChunk(in ChunkCoord coord)
    {
        if (!_chunks.TryGetValue(coord, out var chunk))
        {
            chunk = CreateOrReuseChunk(coord);
            if (chunk != null)
                _chunks[coord] = chunk;
        }
        return chunk;
    }

    // Reuses a pooled chunk when one is available and the source opts into
    // pooling; otherwise generates a fresh chunk. A pooled chunk is reset
    // (sprites cleared, coord retargeted) before the source repopulates it
    // via ReinitializeChunk.
    private Chunk3D? CreateOrReuseChunk(in ChunkCoord coord)
    {
        if (PoolsChunks && _pool.Count > 0)
        {
            var pooled = _pool.Pop();
            pooled.ResetForReuse(coord);
            var reused = ReinitializeChunk(pooled, coord);
            if (reused != null)
            {
                ChunksReused++;
                return reused;
            }
            // Source declined to reuse this instance for this coord; drop
            // it and generate fresh.
        }
        var generated = CreateChunk(coord);
        if (generated != null)
            ChunksAllocated++;
        return generated;
    }

    /// <summary>
    /// Gets the chunk at the given world coordinates.
    /// If the chunk is out of bounds or not loaded, returns null.
    /// </summary>
    public IChunk3D? GetChunk(Vector3 position)
    {
        var coord = GetChunkCoords(position);
        return GetChunk(in coord);
    }

    /// <summary>
    /// Creates a chunk for the coordinates.
    /// Return <c>null</c> for coords this source doesn't populate.
    /// </summary>
    protected abstract Chunk3D? CreateChunk(in ChunkCoord coord);

    /// <summary>
    /// When <c>true</c>, chunks evicted by <see cref="TrimChunksOutside"/>
    /// are retained in a pool and offered back to
    /// <see cref="ReinitializeChunk"/> on a later load instead of being
    /// dropped. Defaults to <c>false</c>. Sources that implement
    /// <see cref="ReinitializeChunk"/> override this to opt in.
    /// </summary>
    protected virtual bool PoolsChunks => false;

    /// <summary>
    /// Repopulates a pooled <paramref name="chunk"/> for <paramref name="coord"/>,
    /// reusing its retained structures and buffers in place of a fresh
    /// <see cref="CreateChunk"/>. The chunk has already been reset (sprites
    /// cleared, coord retargeted) and keeps its barriers. Return the reused
    /// chunk, or <c>null</c> to decline (the instance is then dropped and a
    /// fresh chunk generated). Default returns <c>null</c>.
    /// </summary>
    protected virtual Chunk3D? ReinitializeChunk(Chunk3D chunk, in ChunkCoord coord) => null;
    /// <summary>
    /// Drops every loaded chunk whose coord falls outside the inclusive box
    /// <paramref name="min"/>..<paramref name="max"/>. Call this after updating
    /// <see cref="ChunkedPlayField3D.MinChunk"/> / <see cref="ChunkedPlayField3D.MaxChunk"/>
    /// each frame to bound memory while a viewer walks an unbounded world.
    /// </summary>
    public virtual void TrimChunksOutside(ChunkCoord min, ChunkCoord max)
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
            if (PoolsChunks && chunk is Chunk3D pooled)
                _pool.Push(pooled);
        }
    }

    /// <summary>
    /// Hook invoked once per chunk evicted by <see cref="TrimChunksOutside"/>.
    /// Default does nothing; subclasses override to release any resources tied
    /// to the chunk (voxel storage, GPU buffers, etc.).
    /// </summary>
    protected virtual void OnChunkUnloaded(IChunk3D chunk) { }

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
/// A spatial bucket of sprites and barriers within a chunk grid. This is the
/// view <see cref="ChunkedPlayField3D"/> drives each frame; the basic stateful
/// implementation is <see cref="Chunk3D"/>.
/// </summary>
public interface IChunk3D
{
    /// <summary>The source that owns this chunk.</summary>
    IChunkSource3D Source { get; }

    /// <summary>This chunk's integer coordinates in the source's grid.</summary>
    ChunkCoord Coord { get; }

    /// <summary>Sprites currently bucketed in this chunk.</summary>
    IReadOnlyList<Sprite3D> Sprites { get; }

    /// <summary>Barriers currently bucketed in this chunk.</summary>
    IReadOnlyList<Barrier3D> Barriers { get; }

    /// <summary>Buckets <paramref name="sprite"/> into this chunk.</summary>
    void AddSprite(Sprite3D sprite);

    /// <summary>Removes <paramref name="sprite"/> from this chunk's bucket.</summary>
    void RemoveSprite(Sprite3D sprite);

    /// <summary>Buckets <paramref name="barrier"/> into this chunk.</summary>
    void AddBarrier(Barrier3D barrier);

    /// <summary>Removes <paramref name="barrier"/> from this chunk's bucket.</summary>
    void RemoveBarrier(Barrier3D barrier);

    /// <summary>Per-frame chunk update (barrier ticks plus any chunk-scoped work).</summary>
    void Update(in UpdateContext3D context);

    /// <summary>Draws the chunk's barriers and live sprites.</summary>
    void Draw(Renderer3D renderer);

    /// <summary>
    /// Signals that a frame has started and any Add/Remove calls should be deferred until the end of the frame.
    /// </summary>
    void BeginFrame();

    /// <summary>
    /// Signals that he frame has ended and any deferred Add/Remove calls should be flushed.
    /// </summary>
    void EndFrame();
}

/// <summary>
/// A spatial bucket of sprites and barriers owned by a <see cref="IChunkSource3D"/>. 
/// </summary>
public class Chunk3D : IChunk3D
{
    private readonly List<Sprite3D> _sprites = new();
    private readonly List<Barrier3D> _barriers = new();

    // Mutation during Update goes through these and is flushed at end of frame.
    private readonly List<Sprite3D> _pendingAddSprites = new();
    private readonly List<Sprite3D> _pendingRemoveSprites = new();
    private readonly List<Barrier3D> _pendingAddBarriers = new();
    private readonly List<Barrier3D> _pendingRemoveBarriers = new();
    private bool _updating;

    public Chunk3D(IChunkSource3D source, ChunkCoord coord)
    {
        Source = source;
        Coord = coord;
    }

    /// <summary>The source that owns this chunk.</summary>
    public IChunkSource3D Source { get; }

    /// <summary>This chunk's integer coordinates in the source's grid.</summary>
    public ChunkCoord Coord { get; private set; }

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

    /// <summary>
    /// Opens the frame. While open, Add/Remove calls land in pending
    /// lists and are applied by <see cref="EndFrame"/>. Lets the playfield
    /// (and collision callbacks) mutate the chunk safely while it is also
    /// iterating the sprite/barrier lists.
    /// </summary>
    public void BeginFrame() => _updating = true;

    /// <summary>
    /// Resets this chunk so it can be recycled onto <paramref name="coord"/>
    /// from a source's pool. Clears the sprite bucket and any deferred
    /// mutations (keeping the lists' capacity), and retargets
    /// <see cref="Coord"/>. Barriers are intentionally retained so the
    /// source can reuse the per-chunk barrier (and the mesh/collision
    /// buffers hanging off it) rather than rebuild it.
    /// </summary>
    internal void ResetForReuse(ChunkCoord coord)
    {
        Coord = coord;
        _updating = false;
        _sprites.Clear();
        _pendingAddSprites.Clear();
        _pendingRemoveSprites.Clear();
        _pendingAddBarriers.Clear();
        _pendingRemoveBarriers.Clear();
    }

    /// <summary>
    /// Closes the frame and flushes pending adds/removes. Called by the
    /// owning <see cref="ChunkedPlayField3D"/> after its collision pass.
    /// </summary>
    public void EndFrame()
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