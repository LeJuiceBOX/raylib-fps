using Raylib_cs;
using System.Numerics;

namespace PhrawgEngine
{
    public class TestObject : GameObject
    {
        private Transform? transform;

        public override void Load()
        {
            transform = AddComponent<Transform>();
            transform.Position = new Vector3(0, 10, 0);

            var shape = AddComponent<SphereShape>();
            shape.Radius = 0.5f;

            var rb = AddComponent<Rigidbody>();
            rb.Restitution   = 0.6f;
            rb.StartVelocity = new Vector3(5f, 10f, 0f);
        }

        public override void Draw3D()
        {
            GetComponent<Shape>()!.DebugDraw();
        }

        public override void Draw2D() { }
    }
}
