namespace Blitter.Tests;

/// <summary>
/// Headless <see cref="Texture2D"/> test double — carries dimensions
/// only, never touches the GPU. Lets CPU-side catalog/animation logic
/// be exercised without a running Application.
/// </summary>
internal sealed class FakeTexture2D : Texture2D
{
    public override int Width => 16;
    public override int Height => 16;
    public override PixelFormat PixelFormat => default;
    public override int Version => 1;
    public override int LevelCount => 1;
    public override bool Mipmaps => false;
    public override bool IsDisposed => false;
    public override void Invalidate() { }
    public override void Dispose() { }
}
