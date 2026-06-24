using JoltPhysicsSharp;
using System.Numerics;

namespace PhrawgEngine
{
    public class RigidbodyComponent : Component
    {
        public BodyID  BodyID   { get; private set; }
        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; } = Quaternion.Identity;


        // Config — set these before calling Init()
        public Vector3 StartVelocity = Vector3.Zero;
        public Vector3 StartPosition = Vector3.Zero;
        public Quaternion StartRotation = Quaternion.Identity;
        public ShapeSettings ShapeSettings = new BoxShapeSettings(new Vector3(0.5f, 0.5f, 0.5f));
        public MotionType MotionType = MotionType.Dynamic;
        public byte       Layer      = PhysicsServer.LayerMoving;
        public float      Restitution = 0.3f;
        
        
        private BodyInterface _bodyInterface;
        private bool _initialized = false;


        /// <summary>Call this after setting config fields.</summary>
        public void Init(PhysicsServer physics)
        {
            _bodyInterface = physics.BodyInterface;

            var settings = new BodyCreationSettings(
                ShapeSettings,
                new Vector3(StartPosition.X, StartPosition.Y, StartPosition.Z),
                StartRotation,
                MotionType,
                Layer
            );
            settings.Restitution = Restitution;

            BodyID = _bodyInterface.CreateAndAddBody(settings, Activation.Activate);
            Position = StartPosition;
            Rotation = StartRotation;

            if (StartVelocity != Vector3.Zero)
                _bodyInterface.SetLinearVelocity(BodyID, StartVelocity);

            _initialized = true;
        }

        public override void Update(float dt)
        {
            if (!_initialized) return;

            Position = _bodyInterface.GetPosition(BodyID);  // returns Vector3 directly
            Rotation = _bodyInterface.GetRotation(BodyID);
        }

        internal void Init_IfNeeded(PhysicsServer physics)
        {
            if (!_initialized) Init(physics);
        }

        public void AddImpulse(Vector3 impulse)
        {
            if (_initialized)
                _bodyInterface.AddImpulse(BodyID, impulse);
        }

        public void SetVelocity(Vector3 velocity)
        {
            if (_initialized)
                _bodyInterface.SetLinearVelocity(BodyID, velocity);
        }

        public void Destroy()
        {
            if (!_initialized) return;
            _bodyInterface.RemoveBody(BodyID);
            _bodyInterface.DestroyBody(BodyID);
            _initialized = false;
        }
    }
}