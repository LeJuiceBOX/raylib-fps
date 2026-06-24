using JoltPhysicsSharp;
using Raylib_cs;

namespace PhrawgEngine
{
    public class SphereShape : Shape
    {
        public float Radius = 0.5f;

        public override ShapeSettings GetShapeSettings() => new SphereShapeSettings(Radius);

        public override void DebugDraw()
        {
            var transform = Owner?.GetComponent<Transform>();
            if (transform is null) return;

            Raylib.DrawSphereWires(transform.Position, Radius, 8, 8, Color.Green);
        }
    }
}
