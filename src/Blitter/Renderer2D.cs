using System.Diagnostics;
using System.Numerics;

namespace Blitter;

/// <summary>
/// A renderer that renders 2D graphics to a target.
/// </summary>
public abstract class Renderer2D
{
    // Snapshots pushed by PushState() and popped on scope dispose. The
    // stack is allocated once per renderer and grown lazily, so push/pop
    // is allocation-free in steady state.
    private readonly Stack<RendererState> _stateStack = new();

    private readonly long _startTs = Stopwatch.GetTimestamp();
    private long _lastRenderTs = Stopwatch.GetTimestamp();

    /// <summary>
    /// Elapsed wall-clock time since this renderer was created.
    /// </summary>
    public TimeSpan ElapsedSinceStart => Stopwatch.GetElapsedTime(_startTs);

    /// <summary>
    /// Elapsed wall-clock time since the last call to <c>Render()</c>
    /// (or since renderer creation if no frame has been rendered yet),
    /// clamped by <see cref="MaxFrameDelta"/>.
    /// </summary>
    public TimeSpan ElapsedSinceLastRender
    {
        get
        {
            var elapsed = Stopwatch.GetElapsedTime(_lastRenderTs);
            return elapsed > MaxFrameDelta ? MaxFrameDelta : elapsed;
        }
    }

    /// <summary>
    /// <see cref="ElapsedSinceStart"/> as <c>float</c> seconds. Convenient
    /// for shader uniforms and animation phase math.
    /// </summary>
    public float ElapsedSecondsSinceStart =>
        (float)ElapsedSinceStart.TotalSeconds;

    /// <summary>
    /// <see cref="ElapsedSinceLastRender"/> as <c>float</c> seconds.
    /// Convenient as a per-frame <c>dt</c> for time-integrated state.
    /// </summary>
    public float ElapsedSecondsSinceLastRender =>
        (float)ElapsedSinceLastRender.TotalSeconds;

    /// <summary>
    /// Upper bound on <see cref="ElapsedSinceLastRender"/>. Set to
    /// <see cref="TimeSpan.MaxValue"/> to disable clamping.
    /// </summary>
    public TimeSpan MaxFrameDelta { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Color used to clear the render target before the first draw of
    /// each frame when <see cref="AutoClear"/> is true. Set by the
    /// owning window; not user-mutable through the renderer.
    /// </summary>
    public Color BackgroundColor { get; internal set; }

    /// <summary>
    /// How frames are scheduled against the display's vertical blank.
    /// Treated as a hint: unsupported modes fall back to the next-best
    /// supported mode. Defaults to <see cref="SyncMode.WaitForSync"/>.
    /// </summary>
    public virtual SyncMode SyncMode { get; set; } = SyncMode.WaitForSync;

    /// <summary>
    /// When true (the default), the renderer clears the target to
    /// <see cref="BackgroundColor"/> before the first draw of each
    /// frame. Set to false for additive or persistence-of-pixels
    /// rendering.
    /// </summary>
    internal bool AutoClear { get; set; } = true;

    /// <summary>
    /// Optional 2D camera applied to world-space draws. When set,
    /// <c>DrawImage</c>, <c>DrawImageRotated</c>, <c>DrawFillRect(s)</c>,
    /// <c>DrawLine(s)</c>, <c>DrawPoint(s)</c>, and <c>DrawGeometry</c>
    /// interpret their coordinates as world-space and map them through
    /// the camera. When <c>null</c> (the default), draws use viewport
    /// coordinates directly. <c>DrawDebugText</c> always uses viewport
    /// coordinates regardless of camera.
    /// </summary>
    public Camera2D? Camera { get; set; }

    /// <summary>
    /// Resets the <see cref="ElapsedSinceLastRender"/> clock. Concrete
    /// renderers call this from their <c>Render()</c> implementation
    /// after the frame has been submitted.
    /// </summary>
    private protected void AdvanceFrameClock()
    {
        _lastRenderTs = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Builds a per-frame <see cref="UpdateContext2D"/> snapshotting this
    /// renderer's clock and target bounds. Convenience for the common
    /// case where one loop drives both update and render; standalone
    /// simulations should build their own context from their own clock.
    /// </summary>
    public UpdateContext2D GetUpdateContext()
    {
        // Prefer the logical surface size so update logic that consults
        // Bounds (layout, edge-bounce, hit tests) stays in the same
        // coordinate space the renderer is drawing in.
        var (w, h) = LogicalSize;
        if (w == 0 || h == 0)
            (w, h) = OutputSize;

        return new UpdateContext2D
        {
            ElapsedSinceStart = ElapsedSinceStart,
            ElapsedSinceLastUpdate = ElapsedSinceLastRender,
            Bounds = new Rect(0, 0, w, h),
        };
    }

    #region State

    /// <summary>
    /// Clipping rectangle for subsequent draws. <c>null</c> disables
    /// clipping (draws use the full target).
    /// </summary>
    public abstract Rect? ClipRect { get; set; }

    /// <summary>Per-channel color scale applied to draw colors.</summary>
    public abstract float ColorScale { get; set; }

    /// <summary>
    /// The blend mode used for drawing. 
    /// Defaults to <see cref="Blitter.BlendMode.Alpha"/>, allowing transparency with the color alpha channel.
    /// </summary>
    public abstract BlendMode BlendMode { get; set; }

    /// <summary>The current draw color.</summary>
    public abstract Color DrawColor { get; set; }

    /// <summary>Logical presentation rectangle for the current presentation mode.</summary>
    public abstract Rect LogicalRepresentationRect { get; }

    /// <summary>The output size in pixels.</summary>
    public abstract (int Width, int Height) OutputSize { get; }

    /// <summary>
    /// The logical drawing surface size, if one was configured via
    /// <see cref="SetLogicalSize"/>. Returns <c>(0, 0)</c> when logical
    /// presentation is disabled (i.e. draws use raw output pixels).
    /// </summary>
    public abstract (int Width, int Height) LogicalSize { get; }

    /// <summary>
    /// Output width / height as a single <c>float</c>. Reads
    /// <see cref="OutputSize"/> on every call so resizing is picked up
    /// automatically.
    /// </summary>
    public float AspectRatio
    {
        get
        {
            var (w, h) = OutputSize;
            return h == 0 ? 0f : (float)w / h;
        }
    }

    /// <summary>The rendering scale factors.</summary>
    public abstract (float ScaleX, float ScaleY) Scale { get; set; }

    /// <summary>
    /// The portion of the rendering target where draws are performed.
    /// <c>null</c> means "the entire target" (the renderer's default).
    /// </summary>
    public abstract Rect? ViewPort { get; set; }

    /// <summary>
    /// Configures a fixed logical drawing surface that the renderer
    /// scales to fit the actual output. After calling this, all draws
    /// use coordinates in the (<paramref name="width"/>,
    /// <paramref name="height"/>) space and the renderer handles
    /// scaling, centering, and letterbox bars automatically.
    /// Pass <see cref="LogicalPresentation.Disabled"/> with any size
    /// to turn it off.
    /// </summary>
    public abstract void SetLogicalSize(int width, int height, LogicalPresentation mode);

    #endregion

    #region Frame

    /// <summary>Fills the current draw target with <see cref="DrawColor"/>.</summary>
    public abstract void Clear();

    /// <summary>
    /// When true, calls to <see cref="Render"/> become no-ops. Used by
    /// <see cref="Window2D"/> to suppress stray <c>Render()</c> calls
    /// from inside a <c>Rendering</c> event handler so the window itself
    /// can own the single per-event flush.
    /// </summary>
    internal bool RenderSuppressed { get; set; }

    /// <summary>
    /// Renders the entire frame to the output target.
    /// Call this to manually render at any time. 
    /// This is unnecessary when rendering within Rendering event handlers.
    /// </summary>
    public void Render()
    {
        if (RenderSuppressed)
            return;
        // Marshal to the application thread so callers can invoke Render()
        // from any thread; Send is a no-op when already on the app thread.
        Application.Current.Send(_ => RenderOnApplicationThread());
    }

    /// <summary>
    /// Performs the actual frame rendering. Always invoked on the
    /// application thread by <see cref="Render"/>.
    /// </summary>
    protected abstract void RenderOnApplicationThread();

    #endregion

    #region Drawing

    /// <summary>Draws debug text at the given location.</summary>
    public abstract bool DrawDebugText(int x, int y, string text, float scale = 0f);

    /// <summary>Draws a portion of <paramref name="image"/> to a destination rectangle.</summary>
    public abstract bool DrawImage(Texture2D image, Rect source, Rect destination);

    /// <summary>Draws a portion of <paramref name="image"/> to a destination rectangle,
    /// multiplied by <paramref name="tint"/> (per-channel). Use <see cref="Color.White"/>
    /// for untinted output.</summary>
    public abstract bool DrawImage(Texture2D image, Rect source, Rect destination, Color tint);

    /// <summary>Draws the entire <paramref name="image"/> to a destination rectangle.</summary>
    public bool DrawImage(Texture2D image, Rect destination)
    {
        ArgumentNullException.ThrowIfNull(image);
        var (w, h) = image.Size;
        return DrawImage(image, new Rect(0, 0, w, h), destination);
    }

    /// <summary>Draws the entire <paramref name="image"/> at a position with optional uniform scale.</summary>
    public bool DrawImage(Texture2D image, float x, float y, float scale = 1.0f)
    {
        ArgumentNullException.ThrowIfNull(image);
        var (w, h) = image.Size;
        var source = new Rect(0, 0, w, h);
        var destination = new Rect(x, y, w * scale, h * scale);
        return DrawImage(image, source, destination);
    }

    /// <summary>Draws a portion of <paramref name="image"/> rotated about <paramref name="center"/>.</summary>
    public abstract bool DrawImageRotated(Texture2D image, Rect source, Rect destination, float angle, Vector2 center, FlipMode flip = FlipMode.None);

    /// <summary>Draws a portion of <paramref name="image"/> rotated about <paramref name="center"/>,
    /// multiplied by <paramref name="tint"/> (per-channel). Use <see cref="Color.White"/>
    /// for untinted output.</summary>
    public abstract bool DrawImageRotated(Texture2D image, Rect source, Rect destination, float angle, Vector2 center, FlipMode flip, Color tint);

    /// <summary>Draws the entire <paramref name="image"/> rotated about <paramref name="center"/>.</summary>
    public bool DrawImageRotated(Texture2D image, Rect destination, float angle, Vector2 center, FlipMode flip = FlipMode.None)
    {
        ArgumentNullException.ThrowIfNull(image);
        var (w, h) = image.Size;
        return DrawImageRotated(image, new Rect(0, 0, w, h), destination, angle, center, flip);
    }

    /// <summary>Draws the entire <paramref name="image"/> at a position rotated about a center.</summary>
    public bool DrawImageRotated(Texture2D image, float x, float y, float angle, float centerX, float centerY, float scale = 1.0f, FlipMode flip = FlipMode.None)
    {
        ArgumentNullException.ThrowIfNull(image);
        var (w, h) = image.Size;
        var source = new Rect(0, 0, w, h);
        var destination = new Rect(x, y, w * scale, h * scale);
        var center = new Vector2(centerX * scale, centerY * scale);
        return DrawImageRotated(image, source, destination, angle, center, flip);
    }

    /// <summary>Fills <paramref name="rect"/> with <see cref="DrawColor"/>.</summary>
    public abstract bool DrawFillRect(Rect rect);

    /// <summary>Fills each rectangle in <paramref name="rects"/> with <see cref="DrawColor"/>.</summary>
    public abstract bool DrawFillRects(ReadOnlySpan<Rect> rects);

    /// <summary>
    /// Draws an indexed triangle list, optionally sampling from
    /// <paramref name="image"/>.
    /// </summary>
    public abstract bool DrawGeometry(ReadOnlySpan<Vertex2D> vertices, ReadOnlySpan<int> indices, Texture2D? image = null);

    /// <summary>Draws a line between two points.</summary>
    public abstract bool DrawLine(float x1, float y1, float x2, float y2);

    /// <summary>Draws a connected polyline through <paramref name="points"/>.</summary>
    public abstract bool DrawLines(ReadOnlySpan<Vector2> points);

    /// <summary>Draws a single point.</summary>
    public abstract bool DrawPoint(float x, float y);

    /// <summary>Draws a set of points.</summary>
    public abstract bool DrawPoints(ReadOnlySpan<Vector2> points);

    #endregion

    #region State stack

    /// <summary>
    /// Saves the current renderer state and returns a scope whose
    /// disposal restores it. Intended for use with a <c>using</c>
    /// statement so callers can change state for a sub-region of drawing
    /// without having to remember and reset every property by hand.
    /// </summary>
    public StateScope PushState()
    {
        _stateStack.Push(new RendererState(Camera, DrawColor, ClipRect, ColorScale, Scale, ViewPort, BlendMode));
        return new StateScope(this);
    }

    private void PopState()
    {
        var s = _stateStack.Pop();
        Camera = s.Camera;
        DrawColor = s.DrawColor;
        ClipRect = s.ClipRect;
        ColorScale = s.ColorScale;
        Scale = s.Scale;
        ViewPort = s.ViewPort;
        BlendMode = s.BlendMode;
    }

    // Snapshot of every property a PushState/PopState cycle has to save
    // and restore. Add a field here whenever a new mutable knob is added
    // to Renderer2D so existing callers that already use PushState don't
    // have to change.
    private readonly record struct RendererState(
        Camera2D? Camera,
        Color DrawColor,
        Rect? ClipRect,
        float ColorScale,
        (float ScaleX, float ScaleY) Scale,
        Rect? ViewPort,
        BlendMode BlendMode);

    /// <summary>
    /// A disposable scope returned from <see cref="PushState"/>. Disposing
    /// it restores the renderer state captured at the matching push. As a
    /// <c>ref struct</c> it cannot be stored in fields, captured by
    /// closures, or moved across <c>await</c> boundaries -- the only valid
    /// use is in a <c>using</c> statement on the same call frame as the
    /// push.
    /// </summary>
    public ref struct StateScope
    {
        private Renderer2D? _renderer;

        internal StateScope(Renderer2D renderer)
        {
            _renderer = renderer;
        }

        public void Dispose()
        {
            var r = _renderer;
            _renderer = null;
            r?.PopState();
        }
    }

    #endregion
}
