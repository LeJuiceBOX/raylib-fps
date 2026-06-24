using System.Numerics;
using Raylib_cs;

namespace PhrawgEngine
{
    public class Game
    {
        public static Workspace workspace = new Workspace();
        public static PhysicsServer physicsServer = new PhysicsServer();
        public static Camera3D camera;

        public void Setup()
        {
            Raylib.InitWindow(800, 450, "Raylib 3D | PhrawgEngine");

            camera = new Camera3D
            {
                Position   = new Vector3(20.0f, 20.0f, 20.0f),
                Target     = new Vector3(0.0f, 0.0f, 0.0f),
                Up         = new Vector3(0.0f, 1.0f, 0.0f),
                FovY       = 85f,
                Projection = CameraProjection.Perspective
            };

            //var cam = workspace.AddGameObject<FreeCam>();
            //cam.MoveSpeed = 300f;
            //cam.MouseSensitivity = 0.002f;

            var player = workspace.AddGameObject<Player>();
            player.SpawnPosition = new Vector3(0, 600f, 0); // somewhere above the floor 

            Raylib.SetTargetFPS(60);

            workspace.AddGameObject<TestObject>();
            var map = workspace.AddGameObject<BrushObject>();
            map.LoadMap("assets/maps/zs-bunker.map", "assets/textures");
        }

        public void Run()
        {
            while (!Raylib.WindowShouldClose())
            {
                float dt = Raylib.GetFrameTime();

                physicsServer.Step(dt);
                workspace.Update(dt, physicsServer);

                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.DarkGray);

                Raylib.BeginMode3D(camera);
                workspace.Draw3D();
                Raylib.EndMode3D();

                workspace.Draw2D();
                Raylib.DrawText("Raylib3D | PhrawgEngine", 10, 10, 20, Color.White);

                Raylib.EndDrawing();
            }

            physicsServer.Dispose();
            Raylib.CloseWindow();
        }
    }
}