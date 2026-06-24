using MapViewer;
using Raylib_cs;
using System.Numerics;

namespace PhrawgEngine
{
    /// <summary>
    /// Renders a set of map brushes as textured Raylib models.
    /// Geometry is batched by texture to minimise draw calls.
    /// Call <see cref="Init"/> once with the parsed brush list, then call
    /// <see cref="Draw"/> from the owning GameObject's Draw3D method.
    /// Call <see cref="Unload"/> when the object is destroyed to free GPU memory.
    /// </summary>
    public class BrushRenderer : Component
    {
        private readonly List<Model> _models = new();
        private readonly List<Texture2D> _ownedTextures = new();
        private Texture2D _fallback;
        private bool _initialized = false;

        /// <summary>
        /// Builds all Raylib meshes and loads textures for the provided brushes.
        /// Meshes are grouped by texture name to reduce draw calls.
        /// </summary>
        /// <param name="brushes">Parsed brush list from <see cref="MapParser"/>.</param>
        /// <param name="textureRoot">
        /// Directory to search for texture files. Each face texture name is resolved as
        /// <c>{textureRoot}/{name}.png</c>. Missing textures fall back to a flat magenta.
        /// </param>
        public void Init(List<MapBrush> brushes, string textureRoot = "textures")
        {
            _fallback = MakeFlatTexture(new Color(200, 60, 200, 255));

            var texSizeCache = new Dictionary<string, (int w, int h)>();
            var texCache = new Dictionary<string, Texture2D>();

            Texture2D LoadTex(string name)
            {
                if (texCache.TryGetValue(name, out var cached)) return cached;

                string path = Path.Combine(textureRoot, name + ".png");
                if (File.Exists(path))
                {
                    Image img = Raylib.LoadImage(path);
                    texSizeCache[name] = (img.Width, img.Height);
                    var tex = Raylib.LoadTextureFromImage(img);
                    Raylib.SetTextureFilter(tex, TextureFilter.Point);
                    Raylib.SetTextureWrap(tex, TextureWrap.Repeat);
                    Raylib.UnloadImage(img);
                    texCache[name] = tex;
                    _ownedTextures.Add(tex);
                    return tex;
                }

                texSizeCache[name] = (64, 64);
                texCache[name] = _fallback;
                return _fallback;
            }

            (int w, int h) SizeOf(string name)
            {
                if (texSizeCache.TryGetValue(name, out var s)) return s;
                LoadTex(name);
                return texSizeCache[name];
            }

            // Accumulate triangle soup per texture.
            var byTexture = new Dictionary<string, (List<Vector3> v, List<Vector3> n, List<Vector2> uv)>();

            foreach (var brush in brushes)
            foreach (var poly in BrushGeometry.BuildPolygons(brush, SizeOf))
            {
                if (!byTexture.TryGetValue(poly.Texture, out var buf))
                {
                    buf = (new List<Vector3>(), new List<Vector3>(), new List<Vector2>());
                    byTexture[poly.Texture] = buf;
                }

                // Fan-triangulate the convex polygon.
                for (int i = 1; i < poly.Vertices.Count - 1; i++)
                {
                    AddVert(buf, poly, 0);
                    AddVert(buf, poly, i);
                    AddVert(buf, poly, i + 1);
                }
            }

            foreach (var (texName, buf) in byTexture)
            {
                var mesh = BuildMesh(buf.v, buf.n, buf.uv);
                var model = Raylib.LoadModelFromMesh(mesh);
                var tex = LoadTex(texName);
                Raylib.SetMaterialTexture(ref model, 0, MaterialMapIndex.Albedo, ref tex);
                _models.Add(model);
            }

            _initialized = true;
        }

        /// <summary>Draws all textured brush meshes at the world origin.</summary>
        public void Draw()
        {
            if (!_initialized) return;
            foreach (var model in _models)
                Raylib.DrawModel(model, Vector3.Zero, 1f, Color.White);
        }

        /// <summary>Unloads all GPU resources. Call when the owning object is destroyed.</summary>
        public void Unload()
        {
            foreach (var model in _models) Raylib.UnloadModel(model);
            foreach (var tex in _ownedTextures) Raylib.UnloadTexture(tex);
            Raylib.UnloadTexture(_fallback);
            _models.Clear();
            _ownedTextures.Clear();
            _initialized = false;
        }

        private static void AddVert(
            (List<Vector3> v, List<Vector3> n, List<Vector2> uv) buf,
            Polygon p, int i)
        {
            buf.v.Add(p.Vertices[i]);
            buf.n.Add(p.Normal);
            buf.uv.Add(p.UVs[i]);
        }

        private static unsafe Mesh BuildMesh(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs)
        {
            int count = verts.Count;
            var mesh = new Mesh(count, count / 3);
            mesh.AllocVertices();
            mesh.AllocNormals();
            mesh.AllocTexCoords();

            Span<Vector3> mv = mesh.VerticesAs<Vector3>();
            Span<Vector3> mn = mesh.NormalsAs<Vector3>();
            Span<Vector2> mt = mesh.TexCoordsAs<Vector2>();

            for (int i = 0; i < count; i++)
            {
                mv[i] = verts[i];
                mn[i] = norms[i];
                mt[i] = uvs[i];
            }

            Raylib.UploadMesh(ref mesh, false);
            return mesh;
        }

        private static Texture2D MakeFlatTexture(Color c)
        {
            Image img = Raylib.GenImageColor(2, 2, c);
            var tex = Raylib.LoadTextureFromImage(img);
            Raylib.UnloadImage(img);
            return tex;
        }
    }
}
