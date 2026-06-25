using JoltPhysicsSharp;
using Raylib_cs;
using System.Numerics;

namespace PhrawgEngine
{
    /// <summary>
    /// First-person player controller built on Jolt's <see cref="CharacterVirtual"/>.
    ///
    /// This class is intentionally thin: it owns the <see cref="CharacterVirtual"/> and
    /// <see cref="Transform"/>, syncs post-collision velocity into <see cref="SourceMovement"/>
    /// each tick, feeds the result back, and steps the character.  All movement math lives
    /// in <see cref="SourceMovement"/>.
    ///
    /// Coordinate space: everything is in Hammer Units (HU).  1 HU = 1 engine unit.
    /// No scale conversion is applied — map geometry is imported 1:1.
    ///
    /// ---- Stair stepping --------------------------------------------------------
    /// Jolt's WalkStairs sub-step (inside ExtendedUpdate) works by:
    ///   1. Casting the shape UP by WalkStairsStepUp.
    ///   2. Moving horizontally by the current frame displacement.
    ///   3. Casting the shape DOWN to land on the step surface.
    ///
    /// For this to succeed, LinearVelocity must be set to the *desired* velocity
    /// (post-accel, pre-collision) BEFORE ExtendedUpdate is called, NOT to the
    /// post-collision velocity from the previous frame.  Reading back the clipped
    /// velocity and feeding it straight in is the classic mistake that causes the
    /// character to stop dead at step faces.
    ///
    /// The solution: post-collision velocity is only used to inherit wall-slide and
    /// ceiling cancellation for the *vertical* component (VY).  Horizontal (VX/VZ)
    /// is owned entirely by SourceMovement and never overwritten by clipped results.
    /// WalkStairs then always has a nonzero horizontal velocity to work with.
    /// ---------------------------------------------------------------------------
    /// </summary>
    public class Player : GameObject
    {
        // ---- Spawn ---------------------------------------------------------------

        /// <summary>
        /// World position the player spawns at.  Safe to set before the first tick;
        /// <see cref="CharacterVirtual"/> is created lazily on the first physics update.
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
        private Vector3 _spawnPosition = new Vector3(0f, 128f, 0f);

        // ---- Movement config ----------------------------------------------------

        /// <summary>All tunable movement constants.  Assign before the first tick.</summary>
        public PlayerMovementConfig Movement = PlayerMovementConfig.Default;

        // ---- Private state ------------------------------------------------------

        private CharacterVirtual?  _character;
        private Transform?         _transform;
        private FirstPersonCamera? _camera;
        private SourceMovement?    _movement;
        private bool               _charInitialized;

        // ---- Load ---------------------------------------------------------------

        public override void Load()
        {
            _transform          = AddComponent<Transform>();
            _transform.Position = _spawnPosition;

            _movement = AddComponent<SourceMovement>();

            _camera = AddComponent<FirstPersonCamera>();
            _camera.MouseSensitivity = 0.003f;
            _camera.InitFromCamera();

            Raylib.DisableCursor();
        }

        // ---- CharacterVirtual init (lazy, first tick) ----------------------------

        private void InitCharacter(PhysicsServer physics)
        {
            // Standing capsule.  Total height = 2 * (HalfHeight + Radius).
            // HL2 standing hull: 72 H x 32 W HU → HalfHeight=20, Radius=16 → 2*(20+16)=72 ✓
            //
            // TODO: swap to CrouchCapsule when crouching (requires CharacterVirtual.SetShape
            // or a rebuild — deferred until the wrapper exposes it).
            var capsule = new CapsuleShape(
                Movement.StandCapsuleHalfHeight,
                Movement.StandCapsuleRadius);

            var settings = new CharacterVirtualSettings
            {
                Shape                     = capsule,
                Up                        = Vector3.UnitY,
                MaxSlopeAngle             = MathF.PI / 4f,   // 45° walkable
                CharacterPadding          = 0.02f,
                PenetrationRecoverySpeed  = 1f,

                // Larger predictive contact distance so the step face is detected
                // before the capsule embeds in it.  At 2 HU (the original value)
                // the contact resolves as a wall and WalkStairs never fires.
                // Half the capsule radius (8 HU) gives reliable early detection
                // without causing false contacts on open ground.
                PredictiveContactDistance = Movement.StandCapsuleRadius * 0.5f,

                BackFaceMode              = BackFaceMode.IgnoreBackFaces,
                SupportingVolume          = new Plane(Vector3.UnitY, -Movement.StandCapsuleRadius),
            };

            _character = new CharacterVirtual(
                settings,
                _transform!.Position,
                Quaternion.Identity,
                0UL,
                physics.PhysicsSystem);
        }

        // ---- Update -------------------------------------------------------------

        public override void Update(float dt)
        {
            // Lazy-init on the first physics tick.
            if (!_charInitialized && _transform != null)
            {
                InitCharacter(Game.physicsServer);
                _charInitialized = true;
            }

            if (_character == null || _transform == null || _camera == null || _movement == null)
                return;

            // ----------------------------------------------------------------
            // 1. Inherit ONLY the vertical post-collision component from Jolt.
            //
            //    Horizontal (VX/VZ) is intentionally NOT read back from
            //    LinearVelocity here.  When the capsule hits the vertical face
            //    of a step, Jolt zeros the horizontal component of LinearVelocity
            //    to resolve the contact.  If we fed that back in, WalkStairs
            //    would receive zero horizontal velocity and have no direction to
            //    attempt the step-up — causing the player to stop dead.
            //
            //    Instead, VX/VZ are owned exclusively by SourceMovement and
            //    persist across frames, carrying the last-desired horizontal
            //    velocity into ExtendedUpdate so WalkStairs always has a valid
            //    direction to work with.
            //
            //    VY IS read back so that ceiling hits (Y clipped to zero) and
            //    slope contacts are still correctly reflected.
            // ----------------------------------------------------------------
            _movement.VY = _character.LinearVelocity.Y;

            // ----------------------------------------------------------------
            // 2. Run GMod/Source movement logic (wish dir, friction, accel,
            //    jump, crouch, gravity, eye lerp).
            // ----------------------------------------------------------------
            _movement.ProcessMovement(dt, _character.GroundState, Movement, _camera);

            // ----------------------------------------------------------------
            // 3. Drive CharacterVirtual with the new velocity and step.
            //
            //    LinearVelocity is set to the *desired* velocity (post-accel,
            //    pre-collision).  ExtendedUpdate then:
            //      a) Runs a normal Update (shape-cast move, collision response).
            //      b) WalkStairs: if horizontally blocked, casts UP by
            //         WalkStairsStepUp, moves forward, casts DOWN to land.
            //      c) StickToFloor: casts DOWN by StickToFloorStepDown to
            //         maintain ground contact over small ledge drops.
            //
            //    Gravity is NOT passed (Vector3.Zero) — SourceMovement already
            //    applied it to VY; passing it here would double it.
            // ----------------------------------------------------------------
            _character.LinearVelocity = new Vector3(_movement.VX, _movement.VY, _movement.VZ);

            var updateSettings = new ExtendedUpdateSettings
            {
                WalkStairsStepUp     = new Vector3(0f,  Movement.StairStepHeight,   0f),
                StickToFloorStepDown = new Vector3(0f, -Movement.FloorSnapDistance, 0f),
            };

            _character.ExtendedUpdate(
                dt,
                updateSettings,
                PhysicsServer.LayerMoving,
                Game.physicsServer.PhysicsSystem);

            // ----------------------------------------------------------------
            // 4. Sync authoritative position back into Transform.
            // ----------------------------------------------------------------
            _transform.Position = _character.Position;

            // ----------------------------------------------------------------
            // 5. Run component updates (FirstPersonCamera reads CurrentEyeHeight
            //    from SourceMovement, so order matters: movement before camera).
            // ----------------------------------------------------------------
            base.Update(dt);
        }
    }
}