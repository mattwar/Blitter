using System.Collections;
using System.Numerics;

using Blitter.Bits;

namespace Blitter.Blocks2D;

/// <summary>
/// A single <see cref="Layer2D"/> that contains a colleciton of stacked plates
/// each with a different parallax factor, so they scroll at different rates to create
/// a sense of depth.
/// </summary>
public sealed class ParallaxBackground2D : Layer2D
{
    /// <summary>
    /// The individual plates including images and parallax factors.
    /// </summary>
    public PlateCollection Plates { get; } = new();

    /// <summary>
    /// Default world-space Y for the bottom edge of every plate, 
    /// used by any plate that does not set its own <see cref="ParallaxPlate2D.BottomY"/>.
    /// </summary>
    public float BottomY { get; set; }

    /// <inheritdoc/>
    protected override void DrawContent(Renderer2D renderer)
    {
        var main = renderer.Camera;
        foreach (var plate in Plates)
        {
            float bottomY = plate.BottomY ?? BottomY;
            if (main is not null && plate.Parallax != Vector2.One)
            {
                using var _ = renderer.PushState();
                renderer.Camera = new Camera2D
                {
                    Position = main.Position * plate.Parallax,
                    Zoom = main.Zoom,
                };
                DrawPlate(renderer, plate, bottomY);
            }
            else
            {
                DrawPlate(renderer, plate, bottomY);
            }
        }
    }

    private static void DrawPlate(Renderer2D renderer, ParallaxPlate2D plate, float bottomY)
    {
        var image = plate.Texture;
        float tileW = image.Width;
        float tileH = image.Height;
        float topY = bottomY - tileH;

        var cam = renderer.Camera;
        if (!plate.RepeatX || tileW <= 0f || cam is null)
        {
            // A non-repeating plate is a single backdrop centred horizontally
            // on the (parallax-adjusted) camera, with OffsetX as a nudge.
            float singleX = (cam?.Position.X ?? 0f) - tileW * 0.5f + plate.OffsetX;
            DrawAt(renderer, image, plate, singleX, topY, tileW, tileH);
            return;
        }

        var (vw, _) = renderer.LogicalSize;
        if (vw <= 0)
            (vw, _) = renderer.OutputSize;
        float zoom = cam.Zoom > 0f ? cam.Zoom : 1f;
        float halfViewW = (vw / zoom) * 0.5f;
        float viewLeft = cam.Position.X - halfViewW;
        float viewRight = cam.Position.X + halfViewW;

        int first = (int)MathF.Floor((viewLeft - plate.OffsetX) / tileW);
        int last = (int)MathF.Ceiling((viewRight - plate.OffsetX) / tileW);
        for (int i = first; i <= last; i++)
            DrawAt(renderer, image, plate, plate.OffsetX + i * tileW, topY, tileW, tileH);
    }

    private static void DrawAt(
        Renderer2D renderer, Texture2D image, ParallaxPlate2D plate,
        float x, float y, float w, float h)
    {
        var dst = new Rect(x, y, w, h);
        if (plate.Tint == Color.White)
            renderer.DrawImage(image, dst);
        else
            renderer.DrawImage(image, new Rect(0, 0, image.Width, image.Height), dst, plate.Tint);
    }

    /// <summary>
    /// The plate list for <see cref="ParallaxBackground2D.Plates"/>.
    /// </summary>
    public sealed class PlateCollection : IEnumerable<ParallaxPlate2D>
    {
        private readonly List<ParallaxPlate2D> _plates = new();

        /// <summary>Number of plates.</summary>
        public int Count => _plates.Count;

        /// <summary>Gets the plate at <paramref name="index"/>.</summary>
        public ParallaxPlate2D this[int index] => _plates[index];

        /// <summary>Adds a fully-specified plate.</summary>
        public void Add(ParallaxPlate2D plate)
        {
            ArgumentNullException.ThrowIfNull(plate);
            _plates.Add(plate);
        }

        /// <summary>
        /// Adds a plate.
        /// </summary>
        public void Add(ImageSource image, float parallaxX)
        {
            ArgumentNullException.ThrowIfNull(image);
            _plates.Add(new ParallaxPlate2D { Image = image, Parallax = new Vector2(parallaxX, 0f) });
        }

        /// <inheritdoc/>
        public IEnumerator<ParallaxPlate2D> GetEnumerator() => _plates.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
