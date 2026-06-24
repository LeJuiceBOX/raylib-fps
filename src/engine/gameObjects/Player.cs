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

        // ---- Movement config ----------------------------------------------------

        /// <summary>All tunable movement constants. Change before the first tick.</summary>
        public PlayerMovementConfig Movement = PlayerMovementConfig.Default;

        // ---- Private state -----------------------------------------------------

        private CharacterVirtual? _character;
        private Transform?        _transform;
        private FirstPersonCamera? _camera;
        private bool              _charInitialized = false;

        private float _vx, _vz;   // Horizontal velocity managed by Source movement math.
        private float _vy;         // Vertical velocity managed manually (gravity + jump).
        private bool  _isGrounded;

        // -----------------------------------------------------------------------

        public override void Load()
        {
            _transform          = AddComponent<Transform>();
            _transform.Position = _spawnPosition;

            _camera = AddComponent<FirstPersonCamera>();
            _camera.EyeHeight        = 64f;
            _camera.MouseSensitivity = 0.003f;
            _camera.InitFromCamera();

            Raylib.DisableCursor();
        }

        private void InitCharacter(PhysicsServer physics)
        {
            // Build a capsule that matches the original box hull (72 H × 32 W HU).
            // CapsuleShape(halfHeightOfCylinder, radius): total height = 2*(H+R).
            var capsule = new CapsuleShape(Movement.CapsuleHalfHeight, Movement.CapsuleRadius);

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
                SupportingVolume          = new Plane(Vector3.UnitY, -Movement.CapsuleRadius),
            };

            _character = new CharacterVirtual(
                settings,
                _transform!.Position,
                Quaternion.Identity,
                0UL,
                physics.PhysicsSystem);
        }

        public override void Update(float dt)
        {
            // Lazy-init CharacterVirtual on the first physics tick.
            if (!_charInitialized && _transform != null)
            {
                InitCharacter(Game.physicsServer);
                _charInitialized = true;
            }

            if (_character == null || _transform == null || _camera == null) return;

            // ----------------------------------------------------------------
            // 0. Inherit collision-corrected velocity from last frame.
            //    CharacterVirtual clips LinearVelocity against surfaces it hit,
            //    so reading back all three components handles both wall sliding
            //    (X/Z zeroed into wall) and ceiling hits (Y zeroed when blocked).
            // ----------------------------------------------------------------
            Vector3 postCollision = _character.LinearVelocity;
            _vx = postCollision.X;
            _vy = postCollision.Y;
            _vz = postCollision.Z;

            // ----------------------------------------------------------------
            // 1. Mouse look is handled by FirstPersonCamera component.
            // ----------------------------------------------------------------

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
            Vector3 fwd   = _camera.HorizontalForward;
            Vector3 right = _camera.HorizontalRight;

            Vector3 wish = Vector3.Zero;
            if (Raylib.IsKeyDown(KeyboardKey.W)) wish += fwd;
            if (Raylib.IsKeyDown(KeyboardKey.S)) wish -= fwd;
            if (Raylib.IsKeyDown(KeyboardKey.A)) wish += right;
            if (Raylib.IsKeyDown(KeyboardKey.D)) wish -= right;

            float wishLen = wish.Length();
            Vector3 wishDir = wishLen > 0f ? wish / wishLen : Vector3.Zero;
            float maxSpeed  = Raylib.IsKeyDown(KeyboardKey.LeftShift) ? Movement.SprintSpeed : Movement.MaxSpeed;

            // ----------------------------------------------------------------
            // 4. Ground movement
            // ----------------------------------------------------------------
            if (_isGrounded)
            {
                if (Raylib.IsKeyDown(KeyboardKey.Space))
                {
                    // Skip friction on jump frame so bhop preserves horizontal speed.
                    _vy         = Movement.JumpSpeed;
                    _isGrounded = false;
                    Accelerate(wishDir, maxSpeed, Movement.Acceleration, dt);
                }
                else
                {
                    ApplyFriction(dt);
                    Accelerate(wishDir, maxSpeed, Movement.Acceleration, dt);
                }
            }
            // ----------------------------------------------------------------
            // 5. Air movement
            // ----------------------------------------------------------------
            else
            {
                Accelerate(wishDir, maxSpeed, Movement.AirAcceleration, dt);
                _vy -= Movement.Gravity * dt;
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

            var updateSettings = new ExtendedUpdateSettings
            {
                WalkStairsStepUp     = new Vector3(0f,  Movement.StairStepHeight,   0f),
                StickToFloorStepDown = new Vector3(0f, -Movement.FloorSnapDistance, 0f),
            };

            // ObjectLayer tells CharacterVirtual which layer this character occupies
            // so the physics system's pair filter determines what it can collide with.
            _character.ExtendedUpdate(dt, updateSettings, PhysicsServer.LayerMoving, Game.physicsServer.PhysicsSystem);

            // Sync authoritative position back into the Transform every frame.
            _transform.Position = _character.Position;

            // Run component updates (including FirstPersonCamera) after position is finalised.
            base.Update(dt);
        }

        // ---- Source movement helpers ------------------------------------------

        private void ApplyFriction(float dt)
        {
            float speed = MathF.Sqrt(_vx * _vx + _vz * _vz);
            if (speed < 0.01f) { _vx = _vz = 0f; return; }

            float control  = speed < Movement.StopSpeed ? Movement.StopSpeed : speed;
            float drop     = control * Movement.Friction * dt;
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
