using JoltPhysicsSharp;
using Raylib_cs;
using System.Numerics;

namespace PhrawgEngine
{
    public class TestBlock : GameObject
    {
        private Transform? transform;
        private Rigidbody? rb;

        private Vector3 _size;

        public TestBlock()
        {
            
        }

        public override void Load()
        {
            transform = AddComponent<Transform>();
            transform.Position = new Vector3(0, 0, 0);

            rb = AddComponent<Rigidbody>();
            rb.ShapeSettings = new BoxShapeSettings(Vector3.Zero,1f);
            rb.Restitution    = 0.6f;
            rb.StartVelocity  = new Vector3(0,0,0);
        }

        public override void Draw3D()
        {
            Raylib.DrawSphere(transform!.Position, 0.5f, Color.Red);
        }

        public override void Draw2D() { }
    }
}
