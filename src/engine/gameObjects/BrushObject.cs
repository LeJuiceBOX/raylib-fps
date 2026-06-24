using MapViewer;

namespace PhrawgEngine
{
    /// <summary>
    /// A <see cref="GameObject"/> that represents an entire .map file loaded into the scene.
    /// It holds a <see cref="BrushRenderer"/> for textured rendering and a
    /// <see cref="BrushCollider"/> for static physics collision.
    /// <para>
    /// Usage:
    /// <code>
    /// var map = workspace.AddGameObject&lt;BrushObject&gt;();
    /// map.LoadMap("levels/mymap.map");
    /// </code>
    /// </para>
    /// </summary>
    public class BrushObject : GameObject
    {
        private BrushRenderer? _renderer;
        private BrushCollider? _collider;

        /// <summary>
        /// Parses the .map file at <paramref name="mapPath"/>, builds all render meshes,
        /// and prepares the collision data for the physics system.
        /// </summary>
        /// <param name="mapPath">Path to the Valve 220 .map file.</param>
        /// <param name="textureRoot">
        /// Root directory used to resolve texture names to image files.
        /// Defaults to <c>"textures"</c> relative to the working directory.
        /// </param>
        public void LoadMap(string mapPath, string textureRoot = "textures")
        {
            var brushes = MapParser.Parse(mapPath);

            _renderer = AddComponent<BrushRenderer>();
            _renderer.Init(brushes, textureRoot);

            _collider = AddComponent<BrushCollider>();
            _collider.SetBrushes(brushes);
        }

        /// <inheritdoc/>
        public override void Draw3D() => _renderer?.Draw();
    }
}
