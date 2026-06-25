using JoltPhysicsSharp;
using Raylib_cs;
using System.Numerics;

namespace PhrawgEngine
{
    /// <summary>
    /// GMod/Source-faithful character movement implemented as a <see cref="Component"/>.
    ///
    /// Mirrors the logic in HL2's <c>gamemovement.cpp</c> as closely as possible:
    ///   • Velocity-projection acceleration (<c>Accelerate</c>).
    ///   • Per-frame friction with StopSpeed control band.
    ///   • Air movement hard-capped by <c>AirSpeedCap</c> — the canonical Source bhop gate.
    ///   • Jump requires key release between presses (AutoBhop=false) or allows hold (true).
    ///   • Crouch: logical state only (no capsule swap yet — see TODO).
    ///     Eye height lerps smoothly; speed drops to CrouchSpeed.
    ///   • Crouch-jump: holding crouch while jumping is fully supported.
    ///
    /// The owning <see cref="Player"/> drives <see cref="CharacterVirtual"/> and calls
    /// <see cref="ProcessMovement"/> once per physics tick after reading back post-collision
    /// velocity.
    /// </summary>
    public class SourceMovement : Component
    {
        // ---- Public state (read by Player / FirstPersonCamera) ------------------

        /// <summary>Current horizontal velocity X component (HU/s).</summary>
        public float VX;

        /// <summary>Current vertical velocity (HU/s). Positive = up.</summary>
        public float VY;

        /// <summary>Current horizontal velocity Z component (HU/s).</summary>
        public float VZ;

        /// <summary>True when the character is resting on a walkable surface.</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>
        /// Current eye height above Transform.Position, updated each frame.
        /// <see cref="FirstPersonCamera"/> should read this rather than a fixed offset.
        /// </summary>
        public float CurrentEyeHeight { get; private set; }

        /// <summary>True while the crouch key is held and the transition is active or complete.</summary>
        public bool IsCrouching { get; private set; }

        // ---- Private state ------------------------------------------------------

        private bool  _jumpQueued;          // Jump pressed this frame
        private bool  _jumpReleased = true; // Jump key was released at least once since last jump
        private float _crouchFraction;      // 0 = standing, 1 = fully crouched (drives eye lerp)

        // ---- Main entry point ---------------------------------------------------

        /// <summary>
        /// Call once per tick from <see cref="Player.Update"/> after syncing post-collision
        /// velocity into <see cref="VX"/>, <see cref="VY"/>, <see cref="VZ"/>.
        ///
        /// <paramref name="groundState"/> is the current <see cref="CharacterVirtual.GroundState"/>.
        /// <paramref name="cfg"/> is the shared <see cref="PlayerMovementConfig"/>.
        /// <paramref name="camera"/> provides the horizontal wish direction vectors.
        /// </summary>
        public void ProcessMovement(
            float              dt,
            GroundState        groundState,
            PlayerMovementConfig cfg,
            FirstPersonCamera  camera)
        {
            // ----------------------------------------------------------------
            // 1. Ground state
            // ----------------------------------------------------------------
            IsGrounded = groundState == GroundState.OnGround
                      || groundState == GroundState.OnSteepGround;

            if (IsGrounded && VY < 0f) VY = 0f;

            // ----------------------------------------------------------------
            // 2. Crouch
            // ----------------------------------------------------------------
            ProcessCrouch(dt, cfg);

            // ----------------------------------------------------------------
            // 3. Wish direction
            // ----------------------------------------------------------------
            Vector3 fwd   = camera.HorizontalForward;
            Vector3 right = camera.HorizontalRight;

            Vector3 wish = Vector3.Zero;
            if (Raylib.IsKeyDown(KeyboardKey.W)) wish += fwd;
            if (Raylib.IsKeyDown(KeyboardKey.S)) wish -= fwd;
            if (Raylib.IsKeyDown(KeyboardKey.A)) wish += right;
            if (Raylib.IsKeyDown(KeyboardKey.D)) wish -= right;

            float wishLen = wish.Length();
            Vector3 wishDir = wishLen > 1e-6f ? wish / wishLen : Vector3.Zero;

            // Wish speed: crouch overrides sprint/walk.
            float wishSpeed = IsCrouching
                ? cfg.CrouchSpeed
                : Raylib.IsKeyDown(KeyboardKey.LeftShift)
                    ? cfg.SprintSpeed
                    : cfg.MaxSpeed;

            // ----------------------------------------------------------------
            // 4. Jump input — track key-release for non-autobhop.
            // ----------------------------------------------------------------
            bool jumpHeld = Raylib.IsKeyDown(KeyboardKey.Space);

            if (!jumpHeld)
                _jumpReleased = true;

            _jumpQueued = jumpHeld && (cfg.AutoBhop || _jumpReleased);

            // ----------------------------------------------------------------
            // 5. Ground movement
            // ----------------------------------------------------------------
            if (IsGrounded)
            {
                if (_jumpQueued)
                {
                    // GMod: jump immediately applies vertical velocity.
                    // Friction is intentionally skipped on the jump frame so that
                    // landing with horizontal speed and immediately jumping (bhop)
                    // preserves that speed — this is the Source bhop mechanic.
                    VY            = cfg.JumpSpeed;
                    IsGrounded    = false;
                    _jumpReleased = false;

                    // Crouch-jump: holding crouch while jumping is allowed and
                    // uses crouch speed cap, consistent with GMod behaviour.
                    GroundAccelerate(wishDir, wishSpeed, cfg.Acceleration, dt);
                }
                else
                {
                    ApplyFriction(cfg, dt);
                    GroundAccelerate(wishDir, wishSpeed, cfg.Acceleration, dt);
                }
            }
            // ----------------------------------------------------------------
            // 6. Air movement
            // ----------------------------------------------------------------
            else
            {
                AirAccelerate(wishDir, wishSpeed, cfg, dt);
                VY -= cfg.Gravity * dt;
            }

            // ----------------------------------------------------------------
            // 7. Eye height lerp
            // ----------------------------------------------------------------
            float targetEye = IsCrouching ? cfg.CrouchEyeHeight : cfg.StandEyeHeight;
            // Lerp driven by _crouchFraction which ramps in ProcessCrouch.
            CurrentEyeHeight = cfg.StandEyeHeight
                + (cfg.CrouchEyeHeight - cfg.StandEyeHeight) * _crouchFraction;
        }

        // ---- Crouch -------------------------------------------------------------

        private void ProcessCrouch(float dt, PlayerMovementConfig cfg)
        {
            bool crouchHeld = Raylib.IsKeyDown(KeyboardKey.LeftControl)
                           || Raylib.IsKeyDown(KeyboardKey.C);

            // TODO: When capsule shape-swap is available, attempt un-crouch via an
            // upward shape-cast here and block the transition if obstructed.

            float speed = cfg.CrouchTransitionTime > 0f
                ? dt / cfg.CrouchTransitionTime
                : 1f;

            if (crouchHeld)
            {
                _crouchFraction = MathF.Min(_crouchFraction + speed, 1f);
                IsCrouching     = true;
            }
            else
            {
                _crouchFraction = MathF.Max(_crouchFraction - speed, 0f);
                if (_crouchFraction <= 0f) IsCrouching = false;
            }
        }

        // ---- Source friction ----------------------------------------------------

        /// <summary>
        /// Mirrors <c>PM_Friction</c> in gamemovement.cpp exactly.
        ///
        /// The control band (max of speed and StopSpeed) means that at low speeds
        /// the player decelerates as if they were moving at StopSpeed — this prevents
        /// the infinite-time micro-slide that a pure proportional friction would cause.
        /// </summary>
        private void ApplyFriction(PlayerMovementConfig cfg, float dt)
        {
            float speed = MathF.Sqrt(VX * VX + VZ * VZ);
            if (speed < 0.01f) { VX = VZ = 0f; return; }

            // control band: never let friction act on less than StopSpeed equivalent.
            float control  = speed < cfg.StopSpeed ? cfg.StopSpeed : speed;
            float drop     = control * cfg.Friction * dt;
            float newSpeed = MathF.Max(speed - drop, 0f) / speed;

            VX *= newSpeed;
            VZ *= newSpeed;
        }

        // ---- Source ground acceleration -----------------------------------------

        /// <summary>
        /// Mirrors <c>PM_Accelerate</c> in gamemovement.cpp.
        ///
        /// Projects current velocity onto wishDir to find how much speed we already have
        /// in that direction, then adds up to (wishSpeed - currentSpeed) clamped by
        /// accel * wishSpeed * dt.  This naturally caps speed without clamping the vector.
        /// </summary>
        private void GroundAccelerate(Vector3 wishDir, float wishSpeed, float accel, float dt)
        {
            float currentSpeed = VX * wishDir.X + VZ * wishDir.Z;
            float addSpeed     = wishSpeed - currentSpeed;
            if (addSpeed <= 0f) return;

            float accelSpeed = MathF.Min(accel * wishSpeed * dt, addSpeed);
            VX += accelSpeed * wishDir.X;
            VZ += accelSpeed * wishDir.Z;
        }

        // ---- Source air acceleration + AirSpeedCap ------------------------------

        /// <summary>
        /// Mirrors <c>PM_AirAccelerate</c> in gamemovement.cpp.
        ///
        /// The key difference from ground acceleration is <c>AirSpeedCap</c>:
        /// wishSpeed is clamped to <see cref="PlayerMovementConfig.AirSpeedCap"/> before
        /// the add-speed calculation.  This means that no matter how fast the player is
        /// already moving, they can only add up to AirSpeedCap HU/s worth of acceleration
        /// per frame in the wish direction — enabling air strafing (WASD + mouse) to build
        /// speed gradually while preventing a single keypress from spiking velocity.
        ///
        /// GMod canonical AirSpeedCap: 30 HU/s.
        /// </summary>
        private void AirAccelerate(Vector3 wishDir, float wishSpeed, PlayerMovementConfig cfg, float dt)
        {
            // Cap wish speed for air movement — this is the bhop / strafe gate.
            float cappedWishSpeed = MathF.Min(wishSpeed, cfg.AirSpeedCap);

            float currentSpeed = VX * wishDir.X + VZ * wishDir.Z;
            float addSpeed     = cappedWishSpeed - currentSpeed;
            if (addSpeed <= 0f) return;

            float accelSpeed = MathF.Min(cfg.AirAcceleration * wishSpeed * dt, addSpeed);
            VX += accelSpeed * wishDir.X;
            VZ += accelSpeed * wishDir.Z;
        }
    }
}
