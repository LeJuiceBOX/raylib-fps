using JoltPhysicsSharp;

public class Physics : IDisposable
{
    private PhysicsSystem _physicsSystem;
    private JobSystemThreadPool _jobSystem;
    private TempAllocatorImpl _tempAllocator;

    // Collision layers — keep it simple to start
    private const int OBJ_LAYER_NON_MOVING = 0;
    private const int OBJ_LAYER_MOVING     = 1;
    private const int NUM_OBJ_LAYERS       = 2;

    public BodyInterface Bodies => _physicsSystem.BodyInterface;

    public Physics()
    {
        Foundation.Init(); // global Jolt init — call only once

        _tempAllocator = new TempAllocatorImpl(10 * 1024 * 1024); // 10MB scratch
        _jobSystem     = new JobSystemThreadPool(maxJobs: 2048, maxBarriers: 8);

        var objectLayerPairFilter = new ObjectLayerPairFilterTable(NUM_OBJ_LAYERS);
        objectLayerPairFilter.EnableCollision(OBJ_LAYER_NON_MOVING, OBJ_LAYER_MOVING);
        objectLayerPairFilter.EnableCollision(OBJ_LAYER_MOVING, OBJ_LAYER_MOVING);

        var broadPhaseLayer = new BroadPhaseLayerInterfaceTable(NUM_OBJ_LAYERS, 2);
        broadPhaseLayer.MapObjectToBroadPhaseLayer(OBJ_LAYER_NON_MOVING, BroadPhaseLayer.NonMoving);
        broadPhaseLayer.MapObjectToBroadPhaseLayer(OBJ_LAYER_MOVING,     BroadPhaseLayer.Moving);

        var objectVsBroadPhase = new ObjectVsBroadPhaseLayerFilterTable(
            broadPhaseLayer, 2, objectLayerPairFilter, NUM_OBJ_LAYERS);

        _physicsSystem = new PhysicsSystem();
        _physicsSystem.Init(
            maxBodies:        1024,
            numBodyMutexes:   0,    // 0 = auto
            maxBodyPairs:     1024,
            maxContactConstraints: 1024,
            broadPhaseLayer,
            objectVsBroadPhase,
            objectLayerPairFilter
        );
    }

    public void Step(float deltaTime)
    {
        _physicsSystem.Step(deltaTime, collisionSteps: 1, _tempAllocator, _jobSystem);
    }

    public void Dispose()
    {
        _physicsSystem.Dispose();
        _jobSystem.Dispose();
        _tempAllocator.Dispose();
        Foundation.Shutdown();
    }
}