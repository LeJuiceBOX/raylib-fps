using Raylib_cs;
using System.Numerics;

namespace PhrawgEngine
{
    /// <summary>
    /// Manages first-person mouse look and drives <see cref="Game.camera"/> each frame.
    /// Owns yaw, pitch, eye height, and sensitivity. Add to any GameObject that
    /// should control the player camera.
    /// </summary>
    public class FirstPersonCamera : Component
    {
        /// <summary>Eye height above the owning Transform's Position in world units.</summary>
        public float EyeHeight = 64f;

        /// <summary>Mouse sensitivity in radians per pixel.</summary>
        public float MouseSensitivity = 0.003f;

        public float Yaw   { get; private set; }
        public float Pitch { get; private set; }

        /// <summary>
        /// Forward vector on the horizontal plane only (Y = 0), derived from current yaw.
        /// Use this for movement direction input.
        /// </summary>
        public Vector3 HorizontalForward =>
            new(MathF.Cos(Yaw), 0f, MathF.Sin(Yaw));

        /// <summary>
        /// Right vector on the horizontal plane, derived from current yaw.
        /// </summary>
        public Vector3 HorizontalRight =>
            new(MathF.Sin(Yaw), 0f, -MathF.Cos(Yaw));

        /// <summary>
        /// Initialise yaw/pitch from wherever the camera is already pointing.
        /// Call this from the owning GameObject's Load().
        /// </summary>
        public void InitFromCamera()
        {
            Vector3 dir = Vector3.Normalize(Game.camera.Target - Game.camera.Position);
            Yaw   = MathF.Atan2(dir.Z, dir.X);
            Pitch = MathF.Asin(Math.Clamp(dir.Y, -1f, 1f));
        }

        public override void Update(float dt)
        {
            Vector2 mouse = Raylib.GetMouseDelta();
            Yaw   += mouse.X * MouseSensitivity;
            Pitch -= mouse.Y * MouseSensitivity;
            Pitch  = Math.Clamp(Pitch, -1.55f, 1.55f);

            var transform = Owner?.GetComponent<Transform>();
            if (transform is null) return;

            Vector3 camFwd = new(
                MathF.Cos(Pitch) * MathF.Cos(Yaw),
                MathF.Sin(Pitch),
                MathF.Cos(Pitch) * MathF.Sin(Yaw));

            Vector3 eye = transform.Position + Vector3.UnitY * EyeHeight;
            Game.camera.Position = eye;
            Game.camera.Target   = eye + camFwd;
        }
    }
}
