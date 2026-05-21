using System.Collections;

namespace Blitter.Blocks;

/// <summary>
/// The layer list owned by a <see cref="Scene2D"/>. Designed for
/// collection-initializer syntax at scene construction; mutation
/// after the scene has started is allowed but rare. Adding a layer
/// already attached to another scene detaches it from that scene
/// first.
/// </summary>
public sealed class LayerCollection : IReadOnlyList<Layer2D>
{
    private readonly Scene2D _owner;
    private readonly List<Layer2D> _items = new();

    internal LayerCollection(Scene2D owner)
    {
        _owner = owner;
    }

    public int Count => _items.Count;

    public Layer2D this[int index] => _items[index];

    /// <summary>
    /// Adds <paramref name="layer"/> as the new topmost layer. If the
    /// layer is already attached to a different scene it is detached
    /// from that scene first.
    /// </summary>
    public void Add(Layer2D layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        var existing = layer._scene;
        if (existing == _owner)
            return;
        existing?.Layers.RemoveInternal(layer);
        _items.Add(layer);
        layer._scene = _owner;
    }

    /// <summary>
    /// Removes <paramref name="layer"/> from the scene. Returns
    /// <c>true</c> if it was present.
    /// </summary>
    public bool Remove(Layer2D layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        if (layer._scene != _owner)
            return false;
        if (!_items.Remove(layer))
            return false;
        layer._scene = null;
        return true;
    }

    // Used by Add when transferring a layer from another scene; skips the
    // owner check since the caller already verified the layer belongs.
    internal void RemoveInternal(Layer2D layer)
    {
        if (_items.Remove(layer) && layer._scene == _owner)
            layer._scene = null;
    }

    public List<Layer2D>.Enumerator GetEnumerator() => _items.GetEnumerator();

    IEnumerator<Layer2D> IEnumerable<Layer2D>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
