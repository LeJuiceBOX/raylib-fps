using JoltPhysicsSharp;
using Raylib_cs;
using System.Numerics;

namespace PhrawgEngine
{
    /// <summary>
    /// First-person player controller built on Jolt's <see cref="CharacterVirtual"/>.
    /// CharacterVirtual is a virtual (non-body) character that does its own shape-cast
    /// movement each frame, giving instant wall snap (no Baumgarte depenetration) and
    /// built-in stair-stepping via <c>ExtendedUpdate</c>.
    ///
    /// Horizontal movement mirrors Source 2 ground physics: velocity-based acceleration
    /// with per-frame friction and reduced air control.  Gravity and jumping are managed
    /// manually so they run in Hammer Units/s rather than relying on Jolt's gravity
    /// (which would be in m/s²).
    /// </summary>
    public class Player : GameObject
    {
        // ---- Spawn ----------------------------------------------------------------

        /// <summary>
        /// World position the player spawns at.  Safe to set after
        /// <see cref="Workspace.AddGameObject{T}"/> — CharacterVirtual is created on
        /// the first physics tick, so setting this before then is always safe.
        /// </summary>
        public Vector3 SpawnPosition
        {
            get => _spawnPosition;
            set
            {
                _spawnPosition = value;
                if (_transform != null) _transform.Position = value;
            }
        }
        private Vector3 _spawnPosition = new Vector3(0, 128f, 0);

        // ---- Movement constants -------------------------------------------------

        /// <summary>Maximum horizontal walk speed (Source: sv_maxspeed 320 HU/s).</summary>
        public float MaxSpeed = 320f;

        /// <summary>Maximum horizontal sprint speed while Shift is held.</summary>
        public float SprintSpeed = 520f;

        /// <summary>Ground acceleration (Source: sv_accelerate 10).</summary>
        public float Acceleration = 10f;

        /// <summary>Air-strafing acceleration (Source CS2: sv_airaccelerate 10).</summary>
        public float AirAcceleration = 1f;

        /// <summary>Friction applied when grounded (Source: sv_friction 4, raised for snappier feel).</summary>
        public float Friction = 6f;

        /// <summary>Stop-speed floor used during friction deceleration (Source: sv_stopspeed 100).</summary>
        public float StopSpeed = 75f;

        /// <summary>Upward velocity applied on jump (Source: ~302 HU/s).</summary>
        public float JumpSpeed = 301.993f;

        /// <summary>
        /// Manual gravity in HU/s².  Applied to the vertical velocity each air frame.
        /// CharacterVirtual's internal gravity is set to zero so they don't stack.
        /// </summary>
        public float Gravity = 800f;

        // ---- Stair / floor sticking --------------------------------------------

        /// <summary>
        /// Maximum height in HU that the character can step up onto.
        /// Standard Source stair step is 18 HU.
        /// </summary>
        public float StairStepHeight = 18f;

        /// <summary>
        /// Distance below the current position the character will snap down to stay
        /// in contact with the floor when walking over a small step down or slope edge.
        /// Typically half of <see cref="StairStepHeight"/>.
        /// </summary>
        public float FloorSnapDistance = 9f;

        // ---- Camera ------------------------------------------------------------

        /// <summary>Eye height above the body origin in HU.</summary>
        public float EyeHeight = 64f;

        /// <summary>Mouse look sensitivity in radians per pixel.</summary>
        public float MouseSensitivity = 0.003f;

        // ---- Capsule collider --------------------------------------------------

        /// <summary>
        /// Half-height of the cylindrical section of the capsule in HU.
        /// Combined with <see cref="CapsuleRadius"/>, total height = 2*(halfHeight+radius).
        /// Defaults give a 72 HU tall, 32 HU wide hull matching the Source player.
        /// </summary>
        public float CapsuleHalfHeight = 20f;

        /// <summary>Radius of the capsule end-spheres in HU (half of player width).</summary>
        public float CapsuleRadius = 16f;

        // ---- Private state -----------------------------------------------------

        private CharacterVirtual? _character;
        private Transform?        _transform;
        private bool              _charInitialized = false;

        private float _yaw;
        private float _pitch;

        private float _vx, _vz;   // Horizontal velocity managed by Source movement math.
        private float _vy;         // Vertical velocity managed manually (gravity + jump).
        private bool  _isGrounded;

        // -----------------------------------------------------------------------

        public override void Load()
        {
            _transform          = AddComponent<Transform>();
            _transform.Position = _spawnPosition;

            Raylib.DisableCursor();

            // Derive initial look direction from wherever the camera already points.
            Vector3 dir = Vector3.Normalize(Game.camera.Target - Game.camera.Position);
            _yaw   = MathF.Atan2(dir.Z, dir.X);
            _pitch = MathF.Asin(Math.Clamp(dir.Y, -1f, 1f));
        }

        private void InitCharacter(PhysicsServer physics)
        {
            // Build a capsule that matches the original box hull (72 H × 32 W HU).
            // CapsuleShape(halfHeightOfCylinder, radius): total height = 2*(H+R).
            var capsule = new CapsuleShape(CapsuleHalfHeight, CapsuleRadius);

            var settings = new CharacterVirtualSettings
            {
                Shape                     = capsule,
                Up                        = Vector3.UnitY,
                MaxSlopeAngle             = MathF.PI / 4f,  // 45° max walkable slope
                CharacterPadding          = 0.02f,           // thin skin to avoid tunnelling
                PenetrationRecoverySpeed  = 1f,
                PredictiveContactDistance = 2f,              // HU; look slightly ahead for contacts
                BackFaceMode              = BackFaceMode.IgnoreBackFaces,
                // Supporting volume: plane at the bottom of the lower sphere.
                // Jolt convention: Plane(normal, D) where N·x = D → plane at y = -CapsuleRadius.
                SupportingVolume          = new Plane(Vector3.UnitY, -CapsuleRadius),
            };

            _character = new CharacterVirtual(
                settings,
                _transform!.Position,
                Quaternion.Identity,
                0UL,
                physics.PhysicsSystem);
        }

        public override void Update(float dt, PhysicsServer? physics = null)
        {
            // Lazy-init CharacterVirtual on the first tick that has a physics server.
            if (!_charInitialized && physics != null && _transform != null)
            {
                InitCharacter(physics);
                _charInitialized = true;
            }

            if (_character == null || _transform == null || physics == null) return;

            // ----------------------------------------------------------------
            // 0. Inherit collision-corrected horizontal velocity from last frame.
            //    CharacterVirtual clips LinearVelocity to remove the component
            //    that penetrated a surface, so reading it back here means we
            //    naturally slide along walls without re-accelerating into them.
            // ----------------------------------------------------------------
            Vector3 postCollision = _character.LinearVelocity;
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
            // 2. Ground detection — use CharacterVirtual's ground state.
            //    OnGround      = walkable surface (≤ MaxSlopeAngle).
            //    OnSteepGround = too steep to walk but still in contact.
            //    InAir         = nothing below.
            // ----------------------------------------------------------------
            var groundState = _character.GroundState;
            _isGrounded = groundState == GroundState.OnGround
                       || groundState == GroundState.OnSteepGround;

            if (_isGrounded && _vy < 0f) _vy = 0f;

            // ----------------------------------------------------------------
            // 3. Wish direction (horizontal plane only — pitch doesn't move you).
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
                    // Skip friction on jump frame so bhop preserves horizontal speed.
                    _vy         = JumpSpeed;
                    _isGrounded = false;
                    Accelerate(wishDir, maxSpeed, Acceleration, dt);
                }
                else
                {
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
            // 6. Drive CharacterVirtual and step.
            //
            //    We pass Vector3.Zero for gravity because we already applied it
            //    to _vy above — letting CharacterVirtual apply gravity too would
            //    double it.
            //
            //    ExtendedUpdate (vs plain Update) also runs two sub-steps:
            //      • StickToFloor  — snaps the character down FloorSnapDistance HU
            //        so they stay grounded over small downward ledges.
            //      • WalkStairs    — tries stepping up StairStepHeight HU when the
            //        character is blocked horizontally but clear higher up, giving
            //        smooth stair climbing without needing an explicit stair mesh.
            //
            //    Null filter arguments mean "collide with everything", matching the
            //    behaviour of NarrowPhaseQuery.CastRay with null filters elsewhere.
            // ----------------------------------------------------------------
            _character.LinearVelocity = new Vector3(_vx, _vy, _vz);

            var updateSettings = new ExtendedUpdateSettings();
            updateSettings.WalkStairsStepUp     = new Vector3(0f,  StairStepHeight,   0f);
            updateSettings.StickToFloorStepDown = new Vector3(0f, -FloorSnapDistance, 0f);

            _character.ExtendedUpdate(dt, Vector3.Zero, updateSettings, null, null, null, null, physics.TempAllocator);

            // Sync authoritative position back into the Transform every frame.
            _transform.Position = _character.Position;

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

        // ---- Source movement helpers ------------------------------------------

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

        private void Accelerate(Vector3 wishDir, float wishSpeed, float accel, float dt)
        {
            float currentSpeed = _vx * wishDir.X + _vz * wishDir.Z;
            float addSpeed     = wishSpeed - currentSpeed;
            if (addSpeed <= 0f) return;

            float accelSpeed = MathF.Min(accel * wishSpeed * dt, addSpeed);
            _vx += accelSpeed * wishDir.X;
            _vz += accelSpeed * wishDir.Z;
        }
    }
}
