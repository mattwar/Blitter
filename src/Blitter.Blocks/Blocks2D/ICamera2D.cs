namespace Blitter.Blocks2D;

/// <summary>
/// Provides a 2D camera for entity operations.
/// </summary>
public interface ICamera2D : ICapability
{
    Camera2D Camera { get; }
}