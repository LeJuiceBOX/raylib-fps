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
            Raylib.InitWindow(800, 450, "Raylib 3D C#");

            camera = new Camera3D
            {
                Position   = new Vector3(20.0f, 20.0f, 20.0f),
                Target     = new Vector3(0.0f, 0.0f, 0.0f),
                Up         = new Vector3(0.0f, 1.0f, 0.0f),
                FovY       = 45.0f,
                Projection = CameraProjection.Perspective
            };

            Raylib.SetTargetFPS(60);

            workspace.AddGameObject<TestObject>();
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
                Raylib.DrawText("Welcome to Raylib 3D in C#!", 10, 10, 20, Color.White);

                Raylib.EndDrawing();
            }

            physicsServer.Dispose();
            Raylib.CloseWindow();
        }
    }
}