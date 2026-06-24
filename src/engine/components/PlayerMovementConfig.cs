namespace PhrawgEngine
{
    /// <summary>
    /// All tunable constants for the Source-style player movement in <see cref="Player"/>.
    /// Assign to <see cref="Player.Movement"/> before the first tick.
    /// </summary>
    public struct PlayerMovementConfig
    {
        public float MaxSpeed;
        public float SprintSpeed;
        public float Acceleration;
        public float AirAcceleration;
        public float Friction;
        public float StopSpeed;
        public float JumpSpeed;
        public float Gravity;
        public float StairStepHeight;
        public float FloorSnapDistance;
        public float CapsuleHalfHeight;
        public float CapsuleRadius;

        public static PlayerMovementConfig Default => new()
        {
            MaxSpeed          = 320f,
            SprintSpeed       = 520f,
            Acceleration      = 10f,
            AirAcceleration   = 1f,
            Friction          = 6f,
            StopSpeed         = 75f,
            JumpSpeed         = 301.993f,
            Gravity           = 800f,
            StairStepHeight   = 18f,
            FloorSnapDistance = 9f,
            CapsuleHalfHeight = 20f,
            CapsuleRadius     = 16f,
        };
    }
}
