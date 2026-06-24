using JoltPhysicsSharp;
using MapViewer;
using System.Numerics;

namespace PhrawgEngine
{
    /// <summary>
    /// Creates static Jolt Physics collision for a set of map brushes.
    /// Each brush is a convex solid, so it maps directly to a
    /// <see cref="ConvexHullShapeSettings"/>. All hulls are combined into a
    /// single <see cref="StaticCompoundShapeSettings"/> and registered as one
    /// immovable body in the physics world.
    /// <para>
    /// Call <see cref="SetBrushes"/> before the first physics update, then let
    /// the engine call <see cref="Init_IfNeeded"/> automatically via
    /// <see cref="GameObject.Update"/>.
    /// </para>
    /// </summary>
    public class BrushCollider : Component
    {
        /// <summary>Physics layer assigned to the static body. Defaults to <see cref="PhysicsServer.LayerStatic"/>.</summary>
        public byte Layer = PhysicsServer.LayerStatic;

        private List<MapBrush> _brushes = new();
        private BodyInterface _bodyInterface;
        private BodyID _bodyID;
        private bool _initialized = false;

        /// <summary>
        /// Provides the brush data to collide against.
        /// Must be called before the first physics step.
        /// </summary>
        public void SetBrushes(List<MapBrush> brushes) => _brushes = brushes;

        /// <summary>
        /// Builds the compound convex-hull shape and registers a static body
        /// with the physics system. Safe to call multiple times — no-op after
        /// the first successful call.
        /// </summary>
        public void Init(PhysicsServer physics)
        {
            _bodyInterface = physics.BodyInterface;

            var compound = new StaticCompoundShapeSettings();

            foreach (var brush in _brushes)
            {
                var verts = BrushGeometry.GetHullVertices(brush);
                if (verts.Count < 4) continue; // degenerate brush — skip

                var hull = new ConvexHullShapeSettings(verts.ToArray());
                compound.AddShape(Vector3.Zero, Quaternion.Identity, hull);
            }

            var bodySettings = new BodyCreationSettings(
                compound,
                Vector3.Zero,
                Quaternion.Identity,
                MotionType.Static,
                Layer
            );

            _bodyID = _bodyInterface.CreateAndAddBody(bodySettings, Activation.DontActivate);
            _initialized = true;
        }

        /// <summary>Called by <see cref="GameObject.Update"/> — initialises on the first physics tick.</summary>
        internal void Init_IfNeeded(PhysicsServer physics)
        {
            if (!_initialized) Init(physics);
        }

        /// <summary>Removes and destroys the static body from the physics world.</summary>
        public void Destroy()
        {
            if (!_initialized) return;
            _bodyInterface.RemoveBody(_bodyID);
            _bodyInterface.DestroyBody(_bodyID);
            _initialized = false;
        }
    }
}
