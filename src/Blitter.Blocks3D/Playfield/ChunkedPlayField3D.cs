using System.Numerics;
using Blitter.Bits;

namespace Blitter.Blocks3D;

public class ChunkedPlayField3D : Layer3D
{
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
        for (int y = this.MinChunk.Y; y <= this.MaxChunk.Y; y++)
        {
            for (int z = this.MinChunk.Z; z <= this.MaxChunk.Z; z++)
            {
                for (int x = this.MinChunk.X; x <= this.MaxChunk.X; x++)
                {
                    var coord = new ChunkCoord(x, y, z);
                    var chunk = ChunkSource.GetChunk(in coord);
                    chunk?.Update(context);
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

public struct ChunkCoord
{
    public int X { get; }
    public int Y { get; }
    public int Z { get; }

    public ChunkCoord(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }
}

/// <summary>
/// A source of chunked playfield data.
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
    public virtual BoundingBox GetChunkBounds(int x, int y, int z)
    {
        var min = new Vector3(x * ChunkSize.X, y * ChunkSize.Y, z * ChunkSize.Z);
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
        return GetChunk(coord);
    }

    public virtual void Update(in UpdateContext3D context)
    {
        this.Elapsed += context.ElapsedSinceLastUpdate;
    }

#region ISpriteHost3D
    public TimeSpan Elapsed { get; private set;}

    public virtual void AddSprite(Sprite3D sprite) => throw new NotImplementedException();
    public virtual void RemoveSprite(Sprite3D sprite) => throw new NotImplementedException();
    public virtual void AddBarrier(Barrier3D barrier) => throw new NotImplementedException();
    public virtual void RemoveBarrier(Barrier3D barrier) => throw new NotImplementedException();
#endregion

    public virtual void SetChunk(Sprite3D sprite, Chunk3D chunk) {}
    public virtual void SetChunk(Barrier3D barrier, Chunk3D chunk) {}
}

public abstract class Chunk3D
{
    public abstract ChunkSource3D Source { get; }

    public abstract IReadOnlyList<Sprite3D> Sprites { get; }
    public abstract IReadOnlyList<Barrier3D> Barriers { get; }

    public virtual void Update(in UpdateContext3D context)
    {
        foreach (var barrier in this.Barriers)
        {
            barrier.Update(context);
        }

        foreach (var sprite in this.Sprites)
        {
            sprite.Update(context);

            // sprites can move between chunks.
            var newChunk = this.Source.GetChunk(sprite.Position);
            if (newChunk != this && newChunk is {} c)
            {
                // move sprite to new chunk
                this.Source.SetChunk(sprite, c);
            }
        }
    }

    public virtual void Draw(Renderer3D renderer)
    {
        foreach (var barrier in this.Barriers)
        {
            barrier.Draw(renderer);
        }

        foreach (var sprite in this.Sprites)
        {
            sprite.Draw(renderer);
        }
    }

    private readonly List<Sprite3D> _pendingAddSprites = new();
    private readonly List<Sprite3D> _pendingRemoveSprites = new();

    public void AddPending(Sprite3D sprite)
    {
        _pendingAddSprites.Add(sprite);
    }

    public void RemovePending(Sprite3D sprite)
    {
        _pendingRemoveSprites.Add(sprite);
    }
}


public abstract class GeneratedChunkSource3D : ChunkSource3D
{
    private readonly Dictionary<ChunkCoord, Chunk3D> _chunks = new();

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
}