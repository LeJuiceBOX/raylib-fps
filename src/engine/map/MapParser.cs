using System.Globalization;
using System.Numerics;

namespace MapViewer;

/// <summary>
/// A single plane-defined face in Valve 220 format.
/// Points are stored in the original Quake Z-up coordinate space so that
/// UV projection math stays correct. Use <see cref="ToYup"/> when converting
/// positions for rendering.
/// </summary>
public sealed class Face
{
    /// <summary>Three points that define the plane, in Quake Z-up space.</summary>
    public Vector3 P0, P1, P2;

    /// <summary>Name of the texture applied to this face.</summary>
    public string Texture = "";

    /// <summary>Valve 220 texture projection axes in Z-up space.</summary>
    public Vector3 UAxis, VAxis;

    /// <summary>Texture shift in texels along U and V.</summary>
    public float UOffset, VOffset;

    /// <summary>Rotation in degrees (already baked into axes by most editors).</summary>
    public float Rotation;

    /// <summary>Texture scale along U and V axes.</summary>
    public float ScaleX = 1f, ScaleY = 1f;

    /// <summary>
    /// Outward-facing plane normal in Z-up space.
    /// Map points are wound clockwise from the outside, so (P0-P1) x (P2-P1) is outward.
    /// </summary>
    public Vector3 Normal => Vector3.Normalize(Vector3.Cross(P0 - P1, P2 - P1));

    /// <summary>Signed distance from the origin along the plane normal.</summary>
    public float Dist => Vector3.Dot(Normal, P1);

    /// <summary>Converts a Quake Z-up vector to Raylib Y-up: (x, y, z) → (x, z, -y).</summary>
    public static Vector3 ToYup(Vector3 v) => new(v.X, v.Z, -v.Y);
}

/// <summary>
/// A convex solid defined by a set of half-space planes (<see cref="Face"/>s).
/// The interior of the brush is the intersection of all half-spaces.
/// </summary>
public sealed class MapBrush
{
    /// <summary>All bounding planes of this convex solid.</summary>
    public readonly List<Face> Faces = new();
}

/// <summary>
/// Parses Quake/GoldSrc .map files in Valve 220 format into a list of <see cref="MapBrush"/>es.
/// </summary>
public static class MapParser
{
    /// <summary>
    /// Reads the file at <paramref name="path"/> and returns every brush found across all entities.
    /// </summary>
    public static List<MapBrush> Parse(string path)
    {
        var brushes = new List<MapBrush>();
        var lines = File.ReadAllLines(path);

        MapBrush? current = null;
        int depth = 0; // 0 = top level, 1 = inside entity, 2 = inside brush

        foreach (var raw in lines)
        {
            var line = StripComment(raw).Trim();
            if (line.Length == 0) continue;

            if (line == "{")
            {
                depth++;
                if (depth == 2) current = new MapBrush();
                continue;
            }
            if (line == "}")
            {
                if (depth == 2 && current is { Faces.Count: > 0 })
                    brushes.Add(current);
                current = null;
                depth--;
                continue;
            }

            if (depth == 2 && current != null && line.StartsWith('('))
            {
                if (TryParseFace(line, out var face))
                    current.Faces.Add(face);
            }
        }

        return brushes;
    }

    private static string StripComment(string s)
    {
        int i = s.IndexOf("//", StringComparison.Ordinal);
        return i >= 0 ? s[..i] : s;
    }

    // Full Valve 220 face line:
    // ( x y z ) ( x y z ) ( x y z ) TEXNAME [ ux uy uz uoff ] [ vx vy vz voff ] rot sx sy
    private static bool TryParseFace(string line, out Face face)
    {
        face = new Face();
        int idx = 0;

        Span<Vector3> pts = stackalloc Vector3[3];
        for (int triple = 0; triple < 3; triple++)
        {
            int open = line.IndexOf('(', idx);
            int close = line.IndexOf(')', open + 1);
            if (open < 0 || close < 0) return false;

            var parts = line.Substring(open + 1, close - open - 1)
                            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return false;
            if (!F(parts[0], out var x) || !F(parts[1], out var y) || !F(parts[2], out var z))
                return false;

            pts[triple] = new Vector3(x, y, z);
            idx = close + 1;
        }
        face.P0 = pts[0]; face.P1 = pts[1]; face.P2 = pts[2];

        var rest = line[idx..].TrimStart();
        int sp = rest.IndexOf(' ');
        if (sp < 0) return false;
        face.Texture = rest[..sp];
        rest = rest[sp..];

        if (!ParseAxis(ref rest, out var uAxis, out var uOff)) return false;
        if (!ParseAxis(ref rest, out var vAxis, out var vOff)) return false;
        face.UAxis = uAxis; face.UOffset = uOff;
        face.VAxis = vAxis; face.VOffset = vOff;

        var tail = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tail.Length >= 1) F(tail[0], out face.Rotation);
        if (tail.Length >= 2) F(tail[1], out face.ScaleX);
        if (tail.Length >= 3) F(tail[2], out face.ScaleY);
        if (face.ScaleX == 0) face.ScaleX = 1f;
        if (face.ScaleY == 0) face.ScaleY = 1f;

        return true;
    }

    // Reads one "[ a b c d ]" group from the front of rest, advances rest past it.
    private static bool ParseAxis(ref string rest, out Vector3 axis, out float offset)
    {
        axis = default; offset = 0;
        int open = rest.IndexOf('[');
        int close = rest.IndexOf(']', open + 1);
        if (open < 0 || close < 0) return false;

        var p = rest.Substring(open + 1, close - open - 1)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (p.Length < 4) return false;
        if (!F(p[0], out var ax) || !F(p[1], out var ay) || !F(p[2], out var az) || !F(p[3], out offset))
            return false;

        axis = new Vector3(ax, ay, az);
        rest = rest[(close + 1)..];
        return true;
    }

    private static bool F(string s, out float v) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
}
