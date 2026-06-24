using JoltPhysicsSharp;
using Raylib_cs;
using System.Numerics;

namespace PhrawgEngine
{
    public class TestObject : GameObject
    {
        private RigidbodyComponent? rb;

        public override void Load()
        {
            rb = AddComponent<RigidbodyComponent>();
            rb.StartPosition = new Vector3(0, 10, 0);   // drop from y=10
            rb.ShapeSettings = new SphereShapeSettings(0.5f);
            rb.Restitution   = 0.6f;
            rb.StartVelocity = new Vector3(5f,10f,0f);
            // rb.Init() is called automatically on first Update
        }

        public override void Draw3D()
        {
            // Mirror Jolt position straight into Raylib
            Raylib.DrawSphere(rb.Position, 0.5f, Color.Red);
        }

        public override void Draw2D() { }
    }
}