using JoltPhysicsSharp;
using System.Numerics;

namespace PhrawgEngine
{
    /// <summary>
    /// Integrates a game object with the Jolt Physics simulation.
    /// Reads back position and rotation into the sibling <see cref="Transform"/>
    /// every frame after the physics step.
    /// Set config fields before the first <see cref="Update"/> call; Jolt body
    /// creation is deferred until the first physics tick via <see cref="Init_IfNeeded"/>.
    /// </summary>
    public class Rigidbody : Component
    {
        /// <summary>The Jolt body handle for this object.</summary>
        public BodyID BodyID { get; private set; }

        // ---- Config — set before the first physics tick ----

        /// <summary>Linear velocity applied when the body is first created.</summary>
        public Vector3 StartVelocity = Vector3.Zero;

        /// <summary>Jolt motion type: Dynamic, Static, or Kinematic.</summary>
        public MotionType MotionType = MotionType.Dynamic;

        /// <summary>Collision layer. Use <see cref="PhysicsServer.LayerMoving"/> or <see cref="PhysicsServer.LayerStatic"/>.</summary>
        public byte Layer = PhysicsServer.LayerMoving;

        /// <summary>Bounciness coefficient (0 = no bounce, 1 = perfectly elastic).</summary>
        public float Restitution = 0.3f;

        /// <summary>
        /// Scales the global gravity vector applied to this body.
        /// Set to 0 to disable Jolt gravity entirely (e.g. for a player controller
        /// that manages its own gravity).
        /// </summary>
        public float GravityFactor = 1f;

        private BodyInterface _bodyInterface;
        private bool _initialized = false;
        private Transform? _transform;

        /// <summary>
        /// Creates the Jolt body using the current config fields and the sibling
        /// <see cref="Shape"/> and <see cref="Transform"/> components.
        /// </summary>
        public void Init(PhysicsServer physics)
        {
            _bodyInterface = physics.BodyInterface;
            _transform = Owner?.GetComponent<Transform>() ?? new Transform();

            var shapeSettings = Owner?.GetComponent<Shape>()?.GetShapeSettings()
                                ?? new BoxShapeSettings(new Vector3(0.5f, 0.5f, 0.5f));

            var settings = new BodyCreationSettings(
                shapeSettings,
                _transform.Position,
                _transform.Rotation,
                MotionType,
                Layer
            );
            settings.Restitution    = Restitution;
            settings.GravityFactor  = GravityFactor;

            BodyID = _bodyInterface.CreateAndAddBody(settings, Activation.Activate);

            if (StartVelocity != Vector3.Zero)
                _bodyInterface.SetLinearVelocity(BodyID, StartVelocity);

            _initialized = true;
        }

        /// <summary>Syncs position and rotation from Jolt into the sibling <see cref="Transform"/>.</summary>
        public override void Update(float dt)
        {
            if (!_initialized || _transform is null) return;

            _transform.Position = _bodyInterface.GetPosition(BodyID);
            _transform.Rotation = _bodyInterface.GetRotation(BodyID);
        }

        /// <summary>Called by <see cref="GameObject.Update"/> — initialises on the first physics tick.</summary>
        internal void Init_IfNeeded(PhysicsServer physics)
        {
            if (!_initialized) Init(physics);
        }

        /// <summary>Returns the current linear velocity of the body from Jolt.</summary>
        public Vector3 GetVelocity()
            => _initialized ? _bodyInterface.GetLinearVelocity(BodyID) : Vector3.Zero;

        /// <summary>Applies an instantaneous impulse to the body.</summary>
        public void AddImpulse(Vector3 impulse)
        {
            if (_initialized) _bodyInterface.AddImpulse(BodyID, impulse);
        }

        /// <summary>Directly sets the linear velocity of the body.</summary>
        public void SetVelocity(Vector3 velocity)
        {
            if (_initialized) _bodyInterface.SetLinearVelocity(BodyID, velocity);
        }

        /// <summary>Directly sets the angular velocity of the body. Use Vector3.Zero to lock rotation.</summary>
        public void SetAngularVelocity(Vector3 velocity)
        {
            if (_initialized) _bodyInterface.SetAngularVelocity(BodyID, velocity);
        }

        /// <inheritdoc/>
        public override void Unload() => Destroy();

        /// <summary>Removes and destroys the body from the physics world.</summary>
        public void Destroy()
        {
            if (!_initialized) return;
            _bodyInterface.RemoveBody(BodyID);
            _bodyInterface.DestroyBody(BodyID);
            _initialized = false;
        }
    }
}
