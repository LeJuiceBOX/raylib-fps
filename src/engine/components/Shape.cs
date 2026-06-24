using JoltPhysicsSharp;

namespace PhrawgEngine
{
    public abstract class Shape : Component
    {
        public abstract ShapeSettings GetShapeSettings();
        public abstract void DebugDraw();
    }
}
