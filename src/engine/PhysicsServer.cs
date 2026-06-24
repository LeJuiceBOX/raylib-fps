using JoltPhysicsSharp;
using System.Numerics;

namespace PhrawgEngine
{
    public class PhysicsServer : IDisposable
    {
        public const byte LayerStatic = 0;
        public const byte LayerMoving = 1;

        private const byte BP_LAYER_NON_MOVING = 0;
        private const byte BP_LAYER_MOVING     = 1;

        public PhysicsSystem PhysicsSystem { get; private set; }
        public BodyInterface BodyInterface  => PhysicsSystem.BodyInterface;

        public TempAllocator TempAllocator => _tempAllocator;

        private readonly BroadPhaseLayerInterfaceTable      _bpLayerInterface;
        private readonly ObjectLayerPairFilterTable         _objLayerFilter;
        private readonly ObjectVsBroadPhaseLayerFilterTable _objVsBpFilter;
        private readonly JobSystemThreadPool                _jobSystem;
        private readonly TempAllocatorImpl                  _tempAllocator;

        public PhysicsServer(Vector3? gravity = null)
        {
            Foundation.Init();

            _bpLayerInterface = new BroadPhaseLayerInterfaceTable(2, 2);
            _bpLayerInterface.MapObjectToBroadPhaseLayer(LayerStatic, BP_LAYER_NON_MOVING);
            _bpLayerInterface.MapObjectToBroadPhaseLayer(LayerMoving, BP_LAYER_MOVING);

            // Must be created before ObjectVsBroadPhaseLayerFilterTable
            _objLayerFilter = new ObjectLayerPairFilterTable(2);
            _objLayerFilter.EnableCollision(LayerStatic, LayerMoving);
            _objLayerFilter.EnableCollision(LayerMoving, LayerMoving);

            // Now we can pass _objLayerFilter as the required ObjectLayerPairFilter arg
            _objVsBpFilter = new ObjectVsBroadPhaseLayerFilterTable(
                _bpLayerInterface,  // BroadPhaseLayerInterface
                2,                  // numBroadPhaseLayers
                _objLayerFilter,    // ObjectLayerPairFilter
                2                   // numObjectLayers
            );

            _jobSystem     = new JobSystemThreadPool();
            _tempAllocator = new TempAllocatorImpl(10 * 1024 * 1024);

            var settings = new PhysicsSystemSettings
            {
                BroadPhaseLayerInterface        = _bpLayerInterface,
                ObjectVsBroadPhaseLayerFilter   = _objVsBpFilter,
                ObjectLayerPairFilter           = _objLayerFilter,
                MaxBodies                       = 1024,
                MaxBodyPairs                    = 1024,
                MaxContactConstraints           = 1024,
                NumBodyMutexes                  = 0,
            };

            PhysicsSystem = new PhysicsSystem(settings);
            PhysicsSystem.Gravity = gravity ?? new Vector3(0, -9.81f, 0);
        }

        public void Step(float dt, int collisionSteps = 1)
        {
            PhysicsSystem.Update(dt, collisionSteps, _jobSystem);
        }

        public void Dispose()
        {
            PhysicsSystem.Dispose();
            _jobSystem.Dispose();
            _tempAllocator.Dispose();
            Foundation.Shutdown();
        }
    }
}