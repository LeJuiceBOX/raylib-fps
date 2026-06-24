using JoltPhysicsSharp;
using System.Numerics;

namespace PhrawgEngine
{
    public class Rigidbody : Component
    {
        public BodyID BodyID { get; private set; }

        // Config — set these before calling Init()
        public Vector3 StartVelocity = Vector3.Zero;
        public ShapeSettings ShapeSettings = new BoxShapeSettings(new Vector3(0.5f, 0.5f, 0.5f));
        public MotionType MotionType = MotionType.Dynamic;
        public byte Layer = PhysicsServer.LayerMoving;
        public float Restitution = 0.3f;

        private BodyInterface _bodyInterface;
        private bool _initialized = false;

        private Transform? _transform;

        public void Init(PhysicsServer physics)
        {
            _bodyInterface = physics.BodyInterface;

            // Resolve a sibling Transform if present; create one if not.
            _transform = Owner?.GetComponent<Transform>()
                         ?? new Transform();

            var settings = new BodyCreationSettings(
                ShapeSettings,
                _transform.Position,
                _transform.Rotation,
                MotionType,
                Layer
            );
            settings.Restitution = Restitution;

            BodyID = _bodyInterface.CreateAndAddBody(settings, Activation.Activate);

            if (StartVelocity != Vector3.Zero)
                _bodyInterface.SetLinearVelocity(BodyID, StartVelocity);

            _initialized = true;
        }

        public override void Update(float dt)
        {
            if (!_initialized || _transform is null) return;

            _transform.Position = _bodyInterface.GetPosition(BodyID);
            _transform.Rotation = _bodyInterface.GetRotation(BodyID);
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
