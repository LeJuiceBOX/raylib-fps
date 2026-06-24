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

            var shape = AddComponent<RectangleShape>();
            shape.Size = new Vector3(4f,1f,1f);

            rb = AddComponent<Rigidbody>();
            rb.Restitution    = 0.6f;
            rb.StartVelocity  = new Vector3(0,0,0);
        }

        public override void Draw3D()
        {
            GetComponent<Shape>()!.DebugDraw();
        }

        public override void Draw2D() { }
    }
}
