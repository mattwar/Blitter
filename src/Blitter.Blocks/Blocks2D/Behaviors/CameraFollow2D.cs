namespace Blitter.Blocks2D;

using System.Numerics;


/// <summary>
/// Scrolls a <see cref="Camera2D"/> to keep a target sprite inside a
/// configurable margin of the viewport. The camera holds still while
/// the target stays within the central dead zone; once the target
/// enters the margin, the camera shifts just enough to push the
/// target back to the dead-zone edge. Optionally clamped so the
/// viewport never extends outside <see cref="WorldBounds"/>.
/// </summary>
public class CameraFollow2D : Behavior, IUpdatable
{
    /// <summary>
    /// Optional name of the <see cref="ICamera2D"/> capability to drive. When
    /// unset, the behavior resolves the nearest <see cref="ICamera2D"/> in
    /// the entity's ancestor chain.
    /// </summary>
    public string? CameraName
    {
        get => _cameraName;
        set
        {
            if (_cameraName == value)
                return;
            _cameraName = value;
            _camera = null;
        }
    }

    /// <summary>
    /// Viewport size in viewport pixels (typically the renderer's
    /// logical size). Divided by <see cref="Camera2D.Zoom"/> to get
    /// the visible region in world units.
    /// </summary>
    public Vector2 ViewportSize { get; set; }

    /// <summary>
    /// Fraction of the viewport on each edge (per axis) inside which
    /// the target triggers scrolling. <c>0.3</c> means the target may
    /// roam freely in the central 40% of the viewport; once it enters
    /// the outer 30% on either side, the camera scrolls to keep it on
    /// the dead-zone edge. Clamped to <c>[0, 0.5]</c>; <c>0.5</c>
    /// locks the target dead-center.
    /// </summary>
    public float MarginFraction { get; set; } = 0.3f;

    /// <summary>
    /// Optional world rectangle the visible viewport is clamped
    /// inside. When set, the camera will not scroll past the world
    /// edges; if an axis of the world is smaller than the viewport,
    /// the camera centers on that axis.
    /// </summary>
    public Rect? WorldBounds { get; set; }

    /// <summary>When false, the camera's X is left untouched.</summary>
    public bool FollowX { get; set; } = true;

    /// <summary>When false, the camera's Y is left untouched. Useful
    /// for side-scrollers where a moving target shouldn't drag the
    /// camera vertically.</summary>
    public bool FollowY { get; set; } = true;

    private IEntity _entity = null!;
    private Transform2D _target = null!;
    private ICamera2D? _camera;
    private string? _cameraName;

    protected override void OnAttach(IEntity entity)
    {
        _entity = entity;
        _target = entity.GetOrAddTrait<Transform2D>();
    }

    public void Update(in EntityUpdateContext context)
    {
        // The camera may live on this entity or any containing scope. Resolve
        // opportunistically once the entity is part of a tree; retry each tick
        // until it succeeds.
        if (_camera is null)
            ResolveCamera();

        var cam = _camera?.Camera;
        if (cam is null)
            return;

        var zoom = cam.Zoom > 0f ? cam.Zoom : 1f;
        var viewWorld = ViewportSize / zoom;
        if (viewWorld.X <= 0f || viewWorld.Y <= 0f)
            return;

        var margin = Math.Clamp(MarginFraction, 0f, 0.5f);
        var halfDead = viewWorld * (0.5f - margin);

        var t = _target.Position;
        var p = cam.Position;

        // Push the camera just enough to put the target back on the
        // dead-zone edge — no easing in v1.
        if (FollowX)
        {
            if (t.X < p.X - halfDead.X) p.X = t.X + halfDead.X;
            else if (t.X > p.X + halfDead.X) p.X = t.X - halfDead.X;
        }
        if (FollowY)
        {
            if (t.Y < p.Y - halfDead.Y) p.Y = t.Y + halfDead.Y;
            else if (t.Y > p.Y + halfDead.Y) p.Y = t.Y - halfDead.Y;
        }

        // Clamp so the viewport never shows outside the world. If an
        // axis of the world is smaller than the viewport, center on it
        // instead of clamping (would otherwise produce an empty range).
        if (WorldBounds is Rect wb)
        {
            var halfView = viewWorld * 0.5f;
            if (FollowX)
            {
                p.X = wb.Width <= viewWorld.X
                    ? wb.X + wb.Width * 0.5f
                    : Math.Clamp(p.X, wb.X + halfView.X, wb.X + wb.Width - halfView.X);
            }
            if (FollowY)
            {
                p.Y = wb.Height <= viewWorld.Y
                    ? wb.Y + wb.Height * 0.5f
                    : Math.Clamp(p.Y, wb.Y + halfView.Y, wb.Y + wb.Height - halfView.Y);
            }
        }

        cam.Position = p;
    }

    /// <summary>
    /// Resolves the camera from an <see cref="ICamera2D"/> when
    /// it was not assigned explicitly: by <see cref="CameraName"/> if set,
    /// otherwise the nearest containing scope's single camera behavior. A no-op
    /// until the entity is part of a running entity tree.
    /// </summary>
    private void ResolveCamera()
    {
        ICamera2D? camera;
        if (_cameraName is not null)
        {
            if (!_entity.TryFindCapability(_cameraName, out camera))
                return;
        }
        else if (!_entity.TryFindCapability(out camera))
        {
            return;
        }

        _camera = camera;
    }
}
