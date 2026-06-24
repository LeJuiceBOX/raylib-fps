using System.Numerics;

namespace PhrawgEngine
{
    public class Transform: Component
    {
        public Vector3 Position { get; set; } = Vector3.Zero;
        public Quaternion Rotation { get; set; } = Quaternion.Identity;
        public Vector3 Scale { get; set; } = Vector3.One;

        public Vector3 Forward =>
            Vector3.Transform(-Vector3.UnitZ, Rotation);

        public Vector3 Right =>
            Vector3.Transform(Vector3.UnitX, Rotation);

        public Vector3 Up =>
            Vector3.Transform(Vector3.UnitY, Rotation);

        public Matrix4x4 WorldMatrix =>
            Matrix4x4.CreateScale(Scale)
            * Matrix4x4.CreateFromQuaternion(Rotation)
            * Matrix4x4.CreateTranslation(Position);
    }
}
