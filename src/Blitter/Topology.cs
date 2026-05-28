namespace Blitter;

/// <summary>
/// How consecutive vertices in a mesh are grouped into rendered shapes. 
/// The same vertex buffer means very different pictures depending on the topology.
/// For example, six vertices is two triangles under <see cref="TriangleList"/>, 
/// or three line segments under <see cref="LineList"/>, 
/// or four triangles under <see cref="TriangleStrip"/>.
/// </summary>
public enum Topology
{
    /// <summary>
    /// Vertices in groups of three become independent triangles. 
    /// </summary>
    TriangleList,

    /// <summary>
    /// Each new vertex forms a triangle with the previous two.
    /// </summary>
    TriangleStrip,

    /// <summary>
    /// Vertices in pairs become independent line segments.
    /// </summary>
    LineList,

    /// <summary>
    /// Vertices form a continuous polyline.
    /// </summary>
    LineStrip,

    /// <summary>
    /// Each vertex is rendered as a single point.
    /// </summary>
    PointList,
}
