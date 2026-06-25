using System.Diagnostics.CodeAnalysis;

namespace Blitter.Blocks2D;

/// <summary>
/// The 2D world layer: owns a flat entity list, updates and draws live
/// children, and runs 2D collision over those entities.
/// </summary>
public class PlayField2D : Layer2D, IContainerEntity
{
    private readonly List<IEntity> _entities = new();
    private readonly List<IEntity> _pendingAddEntities = new();
    private readonly HashSet<IEntity> _pendingRemoveEntities = new(ReferenceEqualityComparer.Instance);
    private bool _updating;

    private readonly Dictionary<IEntity, TimeSpan> _spawnedAt = new(ReferenceEqualityComparer.Instance);
    private readonly Bounds2D _bounds = new();
    private readonly Collider2D _collider;

    public PlayField2D()
    {
        AddTrait(_bounds);
        _collider = new Collider2D(IsLive);
    }

    public PlayField2D(IEnumerable<IEntity> entities)
        : this()
    {
        AdoptEntities(entities);
    }

    private static void SetParent(IEntity child, IContainerEntity? parent)
    {
        if (child is Entity entity)
        {
            entity.Parent = parent;
            return;
        }

        throw new InvalidOperationException($"PlayField2D can only contain {nameof(Entity)} instances.");
    }

    private void AdoptEntities(IEnumerable<IEntity> entities)
    {
        foreach (var entity in entities)
        {
            (entity.Parent as PlayField2D)?.RemoveImmediate(entity);
            SetParent(entity, this);
            _spawnedAt[entity] = Elapsed;
            _entities.Add(entity);
        }
    }

    /// <summary>
    /// The entities currently in this playfield. The <c>init</c> accessor
    /// adopts an initial set at construction.
    /// </summary>
    public IReadOnlyList<IEntity> Entities
    {
        get => _entities;
        init => AdoptEntities(value);
    }

    /// <summary>
    /// Tries to resolve the single entity assignable to <typeparamref name="T"/>
    /// in this playfield. Returns <c>false</c> if none. Throws if more than one
    /// matches.
    /// </summary>
    public bool TryGetEntity<T>([NotNullWhen(true)] out T? entity) where T : class, IEntity
    {
        T? match = null;
        foreach (var candidate in _entities)
        {
            if (candidate is not T typed)
                continue;
            if (match is not null)
                throw new InvalidOperationException($"More than one entity is a {typeof(T).Name}.");
            match = typed;
        }
        entity = match;
        return match is not null;
    }

    /// <summary>
    /// Resolves the single entity assignable to <typeparamref name="T"/> in
    /// this playfield. Throws if none exists or more than one matches.
    /// </summary>
    public T GetEntity<T>() where T : class, IEntity =>
        TryGetEntity<T>(out var entity) ? entity : throw new InvalidOperationException($"No entity of type {typeof(T).Name}.");

    /// <summary>
    /// Total time accumulated from <see cref="UpdateContext"/> deltas passed through this playfield's <see cref="Update"/>.
    /// </summary>
    public TimeSpan Elapsed { get; private set; }

    /// <summary>
    /// Optional world rectangle larger (or smaller) than the visible viewport.
    /// When set, behaviors can resolve this rectangle as their
    /// <see cref="Bounds2D"/> trait instead of the renderer's viewport.
    /// </summary>
    public Rect? WorldBounds { get; set; }

    /// <summary>
    /// When true and <see cref="WorldBounds"/> is set, the playfield draws the world boundary.
    /// </summary>
    public bool ShowWorldBounds { get; set; }

    /// <summary>
    /// Color used by <see cref="ShowWorldBounds"/> for the boundary outline.
    /// </summary>
    public Color WorldBoundsColor { get; set; } = new Color(0, 200, 255, 255);

    /// <inheritdoc/>
    public void AddEntity(IEntity child)
    {
        var existing = child.Parent as PlayField2D;
        if (existing is not null && existing != this)
            existing.RemoveImmediate(child);
        else if (existing == this)
        {
            _pendingRemoveEntities.Remove(child);
            if (IsEntityMember(child))
                return;
        }

        if (child.Parent != this)
            SetParent(child, this);
        _spawnedAt[child] = Elapsed;

        if (_updating)
            _pendingAddEntities.Add(child);
        else
            _entities.Add(child);
    }

    /// <inheritdoc/>
    public TimeSpan GetAge(IEntity child) =>
        _spawnedAt.TryGetValue(child, out var t) ? Elapsed - t : TimeSpan.Zero;

    /// <inheritdoc/>
    public void RemoveEntity(IEntity child)
    {
        if (child.Parent != this && !IsEntityMember(child))
            return;

        if (_updating)
        {
            var removedPendingAdd = _pendingAddEntities.Remove(child);
            if (_entities.Contains(child))
                _pendingRemoveEntities.Add(child);
            else if (removedPendingAdd)
                Detach(child);
        }
        else if (_entities.Remove(child))
        {
            Detach(child);
        }
    }

    /// <inheritdoc/>
    public Containment GetContainment(IEntity child)
    {
        if (_pendingRemoveEntities.Contains(child))
            return Containment.Removing;

        if (IsEntityMember(child))
        {
            if (!ReferenceEquals(child.Parent, this))
                return Containment.NotContained;
            return Containment.Contained;
        }

        return Containment.NotContained;
    }

    private bool IsLive(IEntity entity) => !_pendingRemoveEntities.Contains(entity);

    private bool IsEntityMember(IEntity entity) =>
        _entities.Contains(entity) || _pendingAddEntities.Contains(entity);

    private void RemoveImmediate(IEntity child)
    {
        var removed = _pendingAddEntities.Remove(child);
        _pendingRemoveEntities.Remove(child);
        if (_entities.Remove(child))
            removed = true;
        if (removed)
            Detach(child);
    }

    private void Detach(IEntity child)
    {
        _spawnedAt.Remove(child);
        if (child.Parent == this)
            SetParent(child, null);
    }

    /// <summary>
    /// Maximum number of physics substeps per frame. When the fastest
    /// collider would move more than half its hit radius in one frame,
    /// the playfield runs the per-frame update loop multiple times
    /// with proportionally smaller deltas.
    /// </summary>
    public int MaxSubsteps { get; set; } = 8;

    /// <inheritdoc/>
    public override void Update(in UpdateContext context)
    {
        Elapsed += context.ElapsedSinceLastUpdate;

        _bounds.Rect = WorldBounds
            ?? (this.Parent as Scene2D)?.RendererOrNull?.LogicalBounds
            ?? _bounds.Rect;

        var dt = (float)context.ElapsedSinceLastUpdate.TotalSeconds;
        int substeps = ComputeSubstepCount(dt);
        var subContext = substeps > 1
            ? context with { ElapsedSinceLastUpdate = context.ElapsedSinceLastUpdate / substeps }
            : context;

        _updating = true;
        try
        {
            for (int s = 0; s < substeps; s++)
                RunOneStep(subContext);
        }
        finally
        {
            _updating = false;
        }

        ApplyPendingChanges();
    }

    private int ComputeSubstepCount(float dt)
    {
        if (dt <= 0f || MaxSubsteps <= 1)
            return 1;

        float maxStep = 0f;
        float minRadius = float.PositiveInfinity;
        for (int i = 0; i < _entities.Count; i++)
        {
            var entity = _entities[i];
            if (!IsLive(entity) || entity is IColliderBarrier2D)
                continue;
            if (!Collider2D.TryGetHitShape(entity, out var posed))
                continue;
            var r = posed.BoundingCircle.Radius;
            if (r <= 0f)
                continue;
            if (r < minRadius)
                minRadius = r;
            if (!entity.TryGetTrait<Velocity2D>(out var velocity))
                continue;
            var step = MathF.Abs(velocity.Speed) * dt;
            if (step > maxStep)
                maxStep = step;
        }

        if (!float.IsFinite(minRadius) || minRadius <= 0f)
            return 1;
        var budget = 0.5f * minRadius;
        if (maxStep <= budget)
            return 1;
        int n = (int)MathF.Ceiling(maxStep / budget);
        return Math.Clamp(n, 1, MaxSubsteps);
    }

    private void RunOneStep(in UpdateContext context)
    {
        for (int i = 0; i < _entities.Count; i++)
        {
            var entity = _entities[i];
            if (IsLive(entity))
                entity.Update(context);
        }

        _collider.Collide(_entities);
    }

    private void ApplyPendingChanges()
    {
        if (_pendingRemoveEntities.Count > 0)
        {
            for (int i = _entities.Count - 1; i >= 0; i--)
            {
                var entity = _entities[i];
                if (!_pendingRemoveEntities.Contains(entity)) continue;
                _entities.RemoveAt(i);
                Detach(entity);
            }
            _pendingRemoveEntities.Clear();
        }

        if (_pendingAddEntities.Count > 0)
        {
            _entities.AddRange(_pendingAddEntities);
            _pendingAddEntities.Clear();
        }
    }

    protected override void DrawContent(Renderer2D renderer)
    {
        DrawBackground(renderer);

        for (int i = 0; i < _entities.Count; i++)
        {
            var entity = _entities[i];
            if (IsLive(entity) && entity is IDrawable2D drawable)
                drawable.Draw(renderer);
        }

        if (ShowWorldBounds && WorldBounds is not null)
            DrawWorldBoundsOutline(renderer);

        DrawForeground(renderer);
    }

    /// <summary>
    /// Draws the <see cref="WorldBounds"/> overlay. Override to customize
    /// the style (thicker lines, dashed, animated, etc.). Only called
    /// when <see cref="WorldBounds"/> is non-null.
    /// </summary>
    protected virtual void DrawWorldBoundsOutline(Renderer2D renderer)
    {
        if (WorldBounds is not Rect wb)
            return;
        using var _ = renderer.PushState();
        renderer.DrawColor = WorldBoundsColor;
        var inset = 1f / (renderer.Camera?.Zoom ?? 1f);
        var x0 = wb.X;
        var y0 = wb.Y;
        var x1 = wb.X + wb.Width - inset;
        var y1 = wb.Y + wb.Height - inset;
        renderer.DrawLine(x0, y0, x1, y0);
        renderer.DrawLine(x1, y0, x1, y1);
        renderer.DrawLine(x1, y1, x0, y1);
        renderer.DrawLine(x0, y1, x0, y0);
    }

    /// <summary>Hook to draw before the entity pass.</summary>
    protected virtual void DrawBackground(Renderer2D renderer)
    {
    }

    /// <summary>Hook to draw after the entity pass.</summary>
    protected virtual void DrawForeground(Renderer2D renderer)
    {
    }
}