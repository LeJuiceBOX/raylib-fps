using System.Numerics;

namespace MapViewer;

/// <summary>
/// A single renderable, textured convex face ready for upload to the GPU.
/// Positions and normals are in Raylib Y-up space.
/// </summary>
public sealed class Polygon
{
    /// <summary>Face normal in Y-up space.</summary>
    public Vector3 Normal;

    /// <summary>Name of the texture applied to this polygon.</summary>
    public string Texture = "";

    /// <summary>Vertex positions in Y-up space, wound counter-clockwise.</summary>
    public readonly List<Vector3> Vertices = new();

    /// <summary>Texture coordinates matching each vertex.</summary>
    public readonly List<Vector2> UVs = new();
}

/// <summary>
/// Converts <see cref="MapBrush"/> plane definitions into renderable <see cref="Polygon"/>s
/// and collision hull vertices via plane-intersection geometry.
/// </summary>
public static class BrushGeometry
{
    private const float Epsilon = 0.01f;

    /// <summary>
    /// Builds a list of textured <see cref="Polygon"/>s for a single brush.
    /// <paramref name="sizeLookup"/> must return the pixel dimensions of a texture by name
    /// so that Valve 220 UV offsets can be normalized correctly.
    /// </summary>
    public static List<Polygon> BuildPolygons(MapBrush brush, Func<string, (int w, int h)> sizeLookup)
    {
        var faces = brush.Faces;
        int n = faces.Count;
        var rings = new List<List<Vector3>>(n);
        for (int i = 0; i < n; i++) rings.Add(new List<Vector3>());

        // Intersect every triple of planes; keep only points that lie on the brush surface.
        for (int i = 0; i < n; i++)
        for (int j = i + 1; j < n; j++)
        for (int k = j + 1; k < n; k++)
        {
            if (!IntersectPlanes(faces[i], faces[j], faces[k], out var p)) continue;
            if (PointOutsideBrush(faces, p)) continue;

            for (int f = 0; f < n; f++)
                if (MathF.Abs(Vector3.Dot(faces[f].Normal, p) - faces[f].Dist) <= Epsilon)
                    AddUnique(rings[f], p);
        }

        var polys = new List<Polygon>(n);
        for (int i = 0; i < n; i++)
        {
            if (rings[i].Count < 3) continue;

            var face = faces[i];
            var orderedZup = WindCcw(rings[i], face.Normal);
            var (tw, th) = sizeLookup(face.Texture);

            var poly = new Polygon
            {
                Texture = face.Texture,
                Normal  = Vector3.Normalize(Face.ToYup(face.Normal))
            };

            foreach (var vZ in orderedZup)
            {
                // Valve 220 UV: project the original Z-up vertex onto the texture axes.
                float u = (Vector3.Dot(vZ, face.UAxis) / face.ScaleX + face.UOffset) / tw;
                float v = (Vector3.Dot(vZ, face.VAxis) / face.ScaleY + face.VOffset) / th;

                poly.Vertices.Add(Face.ToYup(vZ));
                poly.UVs.Add(new Vector2(u, v));
            }
            polys.Add(poly);
        }
        return polys;
    }

    /// <summary>
    /// Returns the unique surface vertices of a brush in Y-up space.
    /// These are the same points used for rendering and are suitable for
    /// feeding directly into a convex hull collision shape.
    /// </summary>
    public static List<Vector3> GetHullVertices(MapBrush brush)
    {
        var faces = brush.Faces;
        int n = faces.Count;
        var verts = new List<Vector3>();

        for (int i = 0; i < n; i++)
        for (int j = i + 1; j < n; j++)
        for (int k = j + 1; k < n; k++)
        {
            if (!IntersectPlanes(faces[i], faces[j], faces[k], out var p)) continue;
            if (PointOutsideBrush(faces, p)) continue;
            AddUnique(verts, Face.ToYup(p));
        }

        return verts;
    }

    private static bool IntersectPlanes(Face a, Face b, Face c, out Vector3 point)
    {
        point = default;
        Vector3 n1 = a.Normal, n2 = b.Normal, n3 = c.Normal;
        Vector3 cross23 = Vector3.Cross(n2, n3);
        float denom = Vector3.Dot(n1, cross23);
        if (MathF.Abs(denom) < 1e-6f) return false;

        point = (a.Dist * cross23
               + b.Dist * Vector3.Cross(n3, n1)
               + c.Dist * Vector3.Cross(n1, n2)) / denom;
        return true;
    }

    private static bool PointOutsideBrush(List<Face> faces, Vector3 p)
    {
        foreach (var f in faces)
            if (Vector3.Dot(f.Normal, p) - f.Dist > Epsilon) return true;
        return false;
    }

    private static void AddUnique(List<Vector3> list, Vector3 p)
    {
        foreach (var q in list)
            if (Vector3.DistanceSquared(p, q) < Epsilon * Epsilon) return;
        list.Add(p);
    }

    private static List<Vector3> WindCcw(List<Vector3> verts, Vector3 normal)
    {
        var center = Vector3.Zero;
        foreach (var v in verts) center += v;
        center /= verts.Count;

        Vector3 u = Vector3.Normalize(verts[0] - center);
        Vector3 w = Vector3.Normalize(Vector3.Cross(normal, u));

        verts.Sort((a, b) =>
        {
            Vector3 da = a - center, db = b - center;
            float angA = MathF.Atan2(Vector3.Dot(da, w), Vector3.Dot(da, u));
            float angB = MathF.Atan2(Vector3.Dot(db, w), Vector3.Dot(db, u));
            return angA.CompareTo(angB);
        });
        return verts;
    }
}
