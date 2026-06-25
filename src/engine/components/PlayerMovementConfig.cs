namespace PhrawgEngine
{
    /// <summary>
    /// All tunable constants for the GMod/Source-style player movement.
    /// Values are in Hammer Units (HU) and HU/s unless noted otherwise.
    ///
    /// Reference: HL2 gamemovement.cpp, GMod lua/entities/player_manager.lua defaults.
    /// </summary>
    public struct PlayerMovementConfig
    {
        // ---- Speed ---------------------------------------------------------------

        /// <summary>Normal ground walk speed (HU/s). GMod default: 200.</summary>
        public float MaxSpeed;

        /// <summary>
        /// Shift/sprint ground speed (HU/s). GMod doesn't have a native sprint but
        /// 320 is the HL2 run speed used in many gamemodes.
        /// </summary>
        public float SprintSpeed;

        /// <summary>
        /// Speed while crouching (HU/s). GMod default: 100.
        /// </summary>
        public float CrouchSpeed;

        // ---- Acceleration --------------------------------------------------------

        /// <summary>
        /// Ground acceleration constant. Source formula:
        ///   accelSpeed = Min(accel * wishSpeed * dt, addSpeed)
        /// GMod/HL2 ground: 10.
        /// </summary>
        public float Acceleration;

        /// <summary>
        /// Air acceleration constant. Source caps per-frame air accel to
        /// <see cref="AirSpeedCap"/> so this value only matters for small dt.
        /// GMod/HL2 air: 10 (but hard-capped by AirSpeedCap below).
        /// </summary>
        public float AirAcceleration;

        /// <summary>
        /// Maximum speed added per frame during air movement, regardless of accel.
        /// This is the real bhop/strafe limiter in Source. HL2/GMod: 30 HU/s.
        /// Lower = tighter air strafing cap; 30 is the canonical value.
        /// </summary>
        public float AirSpeedCap;

        // ---- Friction -----------------------------------------------------------

        /// <summary>
        /// Ground friction multiplier. GMod/HL2: 4.
        /// </summary>
        public float Friction;

        /// <summary>
        /// Speed below which full <see cref="Friction"/> is applied even if the
        /// player is moving slower. Prevents micro-sliding. HL2: 100.
        /// </summary>
        public float StopSpeed;

        // ---- Jump / Gravity -----------------------------------------------------

        /// <summary>
        /// Vertical velocity applied on jump (HU/s). HL2: 268.3 (sqrt(2 * 900 * 40)).
        /// We use 301.993 to match the ~45 HU jump height with 800 HU/s² gravity.
        /// </summary>
        public float JumpSpeed;

        /// <summary>Gravity (HU/s²). HL2/GMod: 600. Quake: 800. Tune to feel.</summary>
        public float Gravity;

        /// <summary>
        /// The player must release the jump key before jumping again (true = GMod behaviour).
        /// Prevents holding space for auto-bhop. Set false for server-side autobhop feel.
        /// </summary>
        public bool AutoBhop;

        // ---- Crouch -------------------------------------------------------------

        /// <summary>
        /// Time in seconds to fully transition between standing and crouching.
        /// GMod: ~0.2 s.
        /// </summary>
        public float CrouchTransitionTime;

        /// <summary>
        /// Eye height above the Transform origin while standing (HU). HL2: 64.
        /// </summary>
        public float StandEyeHeight;

        /// <summary>
        /// Eye height above the Transform origin while crouching (HU). HL2: 28.
        /// </summary>
        public float CrouchEyeHeight;

        // ---- Capsule ------------------------------------------------------------

        // Standing capsule: total height = 2*(HalfHeight + Radius).
        // HL2 standing hull 72 H x 32 W → HalfHeight=20, Radius=16 → 2*(20+16)=72 ✓

        /// <summary>Half-height of the cylinder portion of the standing capsule (HU).</summary>
        public float StandCapsuleHalfHeight;

        /// <summary>Radius of the standing capsule (HU).</summary>
        public float StandCapsuleRadius;

        // Crouch capsule: total height = 2*(HalfHeight + Radius).
        // HL2 crouch hull 36 H x 32 W → HalfHeight=2, Radius=16 → 2*(2+16)=36 ✓

        /// <summary>Half-height of the cylinder portion of the crouch capsule (HU).</summary>
        public float CrouchCapsuleHalfHeight;

        /// <summary>Radius of the crouch capsule (HU).</summary>
        public float CrouchCapsuleRadius;

        // ---- Stair / floor snap -------------------------------------------------

        /// <summary>
        /// Maximum step height the character can climb (HU). HL2/GMod: 18.
        /// </summary>
        public float StairStepHeight;

        /// <summary>
        /// How far down CharacterVirtual snaps to maintain ground contact (HU).
        /// Prevents floaty landings on gentle slopes. HL2: ~9.
        /// </summary>
        public float FloorSnapDistance;

        // ---- Defaults -----------------------------------------------------------

        /// <summary>
        /// GMod-faithful defaults in Hammer Units.
        /// Gravity is 600 to match HL2 (not Quake 800).
        /// AirSpeedCap 30 gives the classic Source bhop window.
        /// </summary>
        public static PlayerMovementConfig Default => new()
        {
            // Speed
            MaxSpeed               = 200f,
            SprintSpeed            = 320f,
            CrouchSpeed            = 100f,

            // Acceleration
            Acceleration           = 10f,
            AirAcceleration        = 10f,
            AirSpeedCap            = 30f,

            // Friction
            Friction               = 4f,
            StopSpeed              = 100f,

            // Jump / gravity
            JumpSpeed              = 268.3f,    // HL2: sqrt(2 * 600 * 45) ≈ 268
            Gravity                = 600f,
            AutoBhop               = true,     // Must re-press space each jump (GMod default)

            // Crouch
            CrouchTransitionTime   = 0.2f,
            StandEyeHeight         = 64f,
            CrouchEyeHeight        = 28f,

            // Capsule — standing
            StandCapsuleHalfHeight = 20f,
            StandCapsuleRadius     = 16f,

            // Capsule — crouching
            CrouchCapsuleHalfHeight = 2f,
            CrouchCapsuleRadius     = 16f,

            // Stair / floor
            StairStepHeight        = 64f,
            FloorSnapDistance      = 9f,
        };
    }
}