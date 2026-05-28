using System.Numerics;
using System.Runtime.InteropServices;

namespace Blitter;

/// <summary>
/// A rectangle defined by a position (top-left) and size.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Rect : IEquatable<Rect>
{
    /// <summary>
    /// The X coordinate of the rectangle's top-left corner.
    /// </summary>
    public readonly float X;

    /// <summary>
    /// The Y coordinate of the rectangle's top-left corner.
    /// </summary>
    public readonly float Y;

    /// <summary>
    /// The width of the rectangle.
    /// </summary>
    public readonly float Width;

    /// <summary>
    /// The height of the rectangle.
    /// </summary>
    public readonly float Height;

    /// <summary>
    /// Constructs a rectangle from its position and size.
    /// </summary>
    public Rect(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Constructs a rectangle from its position and size.
    /// </summary>
    public Rect(Vector2 position, float width, float height)
        : this(position.X, position.Y, width, height) { }

    /// <summary>
    /// The left edge of the rectangle.
    /// </summary>
    public float Left => X;

    /// <summary>
    /// The top edge of the rectangle.
    /// </summary>
    public float Top => Y;

    /// <summary>
    /// The right edge of the rectangle.
    /// </summary>
    public float Right => X + Width;

    /// <summary>
    /// The bottom edge of the rectangle.
    /// </summary>
    public float Bottom => Y + Height;

    /// <summary>
    /// The top-left corner of the rectangle.
    /// </summary>
    public Vector2 Position => new(X, Y);

    public static implicit operator SDL.FRect(Rect r) => new() { X = r.X, Y = r.Y, W = r.Width, H = r.Height };
    public static implicit operator Rect(SDL.FRect r) => new(r.X, r.Y, r.W, r.H);

    public static implicit operator SDL.Rect(Rect r) => new() { X = (int)r.X, Y = (int)r.Y, W = (int)r.Width, H = (int)r.Height };
    public static implicit operator Rect(SDL.Rect r) => new(r.X, r.Y, r.W, r.H);

    public bool Equals(Rect other) =>
        X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;        
    public override bool Equals(object? obj) => obj is Rect other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
    public static bool operator ==(Rect a, Rect b) => a.Equals(b);
    public static bool operator !=(Rect a, Rect b) => !a.Equals(b);

    public override string ToString() => $"Rect(X={X}, Y={Y}, W={Width}, H={Height})";
}
