using JoltPhysicsSharp;
using Raylib_cs;
using System.Numerics;

namespace PhrawgEngine
{
    /// <summary>
    /// A first-person player controller that replicates Source 2 ground movement:
    /// velocity-based acceleration with per-frame friction, reduced air control,
    /// and instant-response directional changes on the ground.
    /// <para>
    /// All speed values are in world units per second. If your map was built in
    /// Quake/Hammer units (1 unit ≈ 1 inch), the defaults match Source's
    /// <c>sv_maxspeed 320</c> and <c>sv_gravity 800</c> tuning.
    /// </para>
    /// </summary>
    public class Player : GameObject
    {
        // ---- Spawn ----

        /// <summary>
        /// World position the player spawns at. Can be set after
        /// <see cref="Workspace.AddGameObject{T}"/> — the Rigidbody is created on
        /// the first physics tick, so setting this before then is always safe.
        /// </summary>
        public Vector3 SpawnPosition
        {
            get => _spawnPosition;
            set
            {
                _spawnPosition = value;
                // Keep the Transform in sync if Load() has already run.
                if (_transform != null) _transform.Position = value;
            }
        }
        private Vector3 _spawnPosition = new Vector3(0, 128f, 0);

        // ---- Movement ----

        /// <summary>Maximum horizontal walk speed (Source: sv_maxspeed 320).</summary>
        public float MaxSpeed = 320f;

        /// <summary>Maximum horizontal sprint speed while Shift is held.</summary>
        public float SprintSpeed = 520f;

        /// <summary>Ground acceleration (Source: sv_accelerate 10).</summary>
        public float Acceleration = 10f;

        /// <summary>
        /// Air-strafing acceleration. Kept low to preserve momentum but allow
        /// steering (Source CS2: sv_airaccelerate 10; classic HL2: 1).
        /// </summary>
        public float AirAcceleration = 1f;

        /// <summary>Friction applied when grounded (Source: sv_friction 4; raised to 6 for snappier feel).</summary>
        public float Friction = 6f;

        /// <summary>
        /// Minimum speed used when computing friction deceleration.
        /// Lower values stop the player faster at low speeds (Source: sv_stopspeed 100; lowered to 75).
        /// </summary>
        public float StopSpeed = 75f;

        /// <summary>Upward velocity applied on jump (Source classic: 301.993).</summary>
        public float JumpSpeed = 301.993f;

        /// <summary>
        /// Downward gravity acceleration in world units/s² applied manually.
        /// Independent of the PhysicsServer gravity so the two do not stack
        /// (Jolt gravity is disabled for the player body via GravityFactor = 0).
        /// Source default: 800 HU/s².
        /// </summary>
        public float Gravity = 800f;

        /// <summary>
        /// Maximum distance below the box bottom that counts as grounded.
        /// Increase slightly if the player loses ground contact on bumpy geometry.
        /// </summary>
        public float GroundCheckDistance = 4f;

        // ---- Camera ----

        /// <summary>Eye height above the body origin in world units (~72 HU).</summary>
        public float EyeHeight = 64f;

        /// <summary>Mouse look sensitivity in radians per pixel.</summary>
        public float MouseSensitivity = 0.003f;

        // ---- Collision shape ----

        /// <summary>Size of the box collider in world units (Source player hull ≈ 32x72x32 HU).</summary>
        public Vector3 ColliderSize = new Vector3(32f, 72f, 32f);

        // ---- Private state ----

        private Rigidbody? _rb;
        private Transform? _transform;

        private float _yaw;
        private float _pitch;

        private float _vx, _vz;  // Horizontal velocity — managed by Source math.
        private float _vy;        // Vertical velocity — driven manually (gravity + jump).
        private bool  _isGrounded;

        public override void Load()
        {
            _transform = AddComponent<Transform>();
            _transform.Position = _spawnPosition;

            var shape = AddComponent<RectangleShape>();
            shape.Size = ColliderSize;

            _rb = AddComponent<Rigidbody>();
            _rb.GravityFactor = 0f;  // We apply gravity ourselves.
            _rb.Restitution   = 0f;
            _rb.Layer         = PhysicsServer.LayerMoving;

            Raylib.DisableCursor();

            // Derive initial look direction from wherever the camera already points.
            Vector3 dir = Vector3.Normalize(Game.camera.Target - Game.camera.Position);
            _yaw   = MathF.Atan2(dir.Z, dir.X);
            _pitch = MathF.Asin(Math.Clamp(dir.Y, -1f, 1f));
        }

        public override void Update(float dt, PhysicsServer? physics = null)
        {
            // Initialises Rigidbody on the first tick via base.
            base.Update(dt, physics);

            if (_rb == null || _transform == null) return;

            // ----------------------------------------------------------------
            // 0. Inherit Jolt's post-collision horizontal velocity
            // We set velocity at the end of every frame, but Jolt modifies it
            // during the physics step when the player hits a wall (zeroing the
            // component into the surface). Reading it back here means friction
            // and acceleration run on top of the collision-corrected velocity,
            // so walls feel solid instead of the player slowly grinding through.
            // _vy is excluded because we manage gravity/jump ourselves and have
            // disabled Jolt gravity on this body.
            // ----------------------------------------------------------------
            Vector3 postCollision = _rb.GetVelocity();
            _vx = postCollision.X;
            _vz = postCollision.Z;

            // ----------------------------------------------------------------
            // 1. Mouse look
            // ----------------------------------------------------------------
            Vector2 mouse = Raylib.GetMouseDelta();
            _yaw   += mouse.X * MouseSensitivity;
            _pitch -= mouse.Y * MouseSensitivity;
            _pitch  = Math.Clamp(_pitch, -1.55f, 1.55f);

            // ----------------------------------------------------------------
            // 2. Ground detection via downward raycast
            // Ray starts just below the box bottom (avoiding self-hit) and
            // travels downward. Fraction on the result is distance in world
            // units along the normalised ray, so we compare it to
            // GroundCheckDistance directly.
            // ----------------------------------------------------------------
            if (physics != null)
            {
                _isGrounded = CastGroundRay(physics);
                if (_isGrounded && _vy < 0f) _vy = 0f;
            }

            // ----------------------------------------------------------------
            // 3. Wish direction (horizontal, yaw only — pitch doesn't affect movement)
            // ----------------------------------------------------------------
            Vector3 fwd   = new(MathF.Cos(_yaw), 0f, MathF.Sin(_yaw));
            Vector3 right = new(MathF.Sin(_yaw), 0f, -MathF.Cos(_yaw));

            Vector3 wish = Vector3.Zero;
            if (Raylib.IsKeyDown(KeyboardKey.W)) wish += fwd;
            if (Raylib.IsKeyDown(KeyboardKey.S)) wish -= fwd;
            if (Raylib.IsKeyDown(KeyboardKey.A)) wish += right;
            if (Raylib.IsKeyDown(KeyboardKey.D)) wish -= right;

            float wishLen = wish.Length();
            Vector3 wishDir = wishLen > 0f ? wish / wishLen : Vector3.Zero;
            float maxSpeed  = Raylib.IsKeyDown(KeyboardKey.LeftShift) ? SprintSpeed : MaxSpeed;

            // ----------------------------------------------------------------
            // 4. Ground movement
            // ----------------------------------------------------------------
            if (_isGrounded)
            {
                if (Raylib.IsKeyDown(KeyboardKey.Space))
                {
                    // Jump this frame: skip friction entirely so horizontal speed
                    // is fully preserved — this is what makes bhop work in Source.
                    _vy = JumpSpeed;
                    _isGrounded = false;
                    Accelerate(wishDir, maxSpeed, Acceleration, dt);
                }
                else
                {
                    // Normal ground frame: friction first, then accelerate.
                    ApplyFriction(dt);
                    Accelerate(wishDir, maxSpeed, Acceleration, dt);
                }
            }
            // ----------------------------------------------------------------
            // 5. Air movement
            // ----------------------------------------------------------------
            else
            {
                Accelerate(wishDir, maxSpeed, AirAcceleration, dt);
                _vy -= Gravity * dt;
            }

            // ----------------------------------------------------------------
            // 6. Push combined velocity into Jolt and lock rotation
            // ----------------------------------------------------------------
            _rb.SetVelocity(new Vector3(_vx, _vy, _vz));
            _rb.SetAngularVelocity(Vector3.Zero);

            // ----------------------------------------------------------------
            // 7. Update camera
            // ----------------------------------------------------------------
            Vector3 camFwd = new(
                MathF.Cos(_pitch) * MathF.Cos(_yaw),
                MathF.Sin(_pitch),
                MathF.Cos(_pitch) * MathF.Sin(_yaw));

            Vector3 eye = _transform.Position + Vector3.UnitY * EyeHeight;
            Game.camera.Position = eye;
            Game.camera.Target   = eye + camFwd;
        }

        // ---- Ground detection -----------------------------------------------

        /// <summary>
        /// Casts a ray straight down from just below the box bottom.
        /// <para>
        /// <see cref="Ray"/> takes a normalised direction; <see cref="RayCastResult.Fraction"/>
        /// is the hit distance in world units along that direction, so we compare it
        /// directly to <see cref="GroundCheckDistance"/>.
        /// </para>
        /// </summary>
        private bool CastGroundRay(PhysicsServer physics)
        {
            if (_transform == null) return false;

            // Origin is 0.5 units below the box bottom so the ray never intersects
            // the player's own shape.
            float halfHeight = ColliderSize.Y / 2f;
            var origin = new Vector3(
                _transform.Position.X,
                _transform.Position.Y - halfHeight - 0.5f,
                _transform.Position.Z);

            // Ray(position, normalised direction) — direction is straight down.
            var ray    = new JoltPhysicsSharp.Ray(origin, -Vector3.UnitY);
            var filter = new ExcludeBodyFilter(_rb!.BodyID);

            bool hit = physics.PhysicsSystem.NarrowPhaseQuery.CastRay(
                ray, out RayCastResult result,
                null, null, filter);

            return hit && result.Fraction <= GroundCheckDistance;
        }

        // ---- Source movement helpers ----------------------------------------

        /// <summary>
        /// Applies velocity-proportional friction to the horizontal velocity.
        /// Mirrors Source's PM_Friction exactly.
        /// </summary>
        private void ApplyFriction(float dt)
        {
            float speed = MathF.Sqrt(_vx * _vx + _vz * _vz);
            if (speed < 0.01f) { _vx = _vz = 0f; return; }

            float control  = speed < StopSpeed ? StopSpeed : speed;
            float drop     = control * Friction * dt;
            float newSpeed = MathF.Max(speed - drop, 0f) / speed;

            _vx *= newSpeed;
            _vz *= newSpeed;
        }

        /// <summary>
        /// Accelerates toward <paramref name="wishDir"/> up to <paramref name="wishSpeed"/>.
        /// Mirrors Source's PM_Accelerate — only adds velocity in the wish direction,
        /// preserving sideways momentum for air-strafing.
        /// </summary>
        private void Accelerate(Vector3 wishDir, float wishSpeed, float accel, float dt)
        {
            float currentSpeed = _vx * wishDir.X + _vz * wishDir.Z;
            float addSpeed     = wishSpeed - currentSpeed;
            if (addSpeed <= 0f) return;

            float accelSpeed = MathF.Min(accel * wishSpeed * dt, addSpeed);
            _vx += accelSpeed * wishDir.X;
            _vz += accelSpeed * wishDir.Z;
        }

        // ---- Helpers --------------------------------------------------------

        /// <summary>
        /// Excludes a single body from raycast results so the player's own
        /// collider is never reported as a ground hit.
        /// </summary>
        private sealed class ExcludeBodyFilter : BodyFilter
        {
            private readonly BodyID _exclude;
            public ExcludeBodyFilter(BodyID exclude) => _exclude = exclude;
            protected override bool ShouldCollide(BodyID bodyID)       => bodyID   != _exclude;
            protected override bool ShouldCollideLocked(Body body)     => body.ID  != _exclude;
        }
    }
}
