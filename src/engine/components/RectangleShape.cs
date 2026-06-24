using JoltPhysicsSharp;
using Raylib_cs;
using System.Numerics;

namespace PhrawgEngine
{
    public class RectangleShape : Shape
    {
        public Vector3 Size = Vector3.One;

        public override ShapeSettings GetShapeSettings() => new BoxShapeSettings(Size / 2f);

        public override void DebugDraw()
        {
            var transform = Owner?.GetComponent<Transform>();
            if (transform is null) return;

            Raylib.DrawCubeWires(transform.Position, Size.X, Size.Y, Size.Z, Color.Green);
        }
    }
}
