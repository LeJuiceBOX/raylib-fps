using System.Numerics;
using Raylib_cs;

namespace PhrawgEngine
{
    public class TestObject : GameObject
    {
        public override void Load()
        {
            
        }

        public override void Update(float dt)
        {
            
        }

        public override void Draw2D()
        {
            
        }

        public override void Draw3D()
        {
            Raylib.DrawSphere(new Vector3(0,0,0),5f,Color.Blue);
        }
    }
}