using System.Numerics;
using Raylib_cs;

namespace PhrawgEngine
{
    public class Game
    {
        public Workspace workspace = new Workspace();
        public Camera3D camera;

        public void setup()
        {
            Raylib.InitWindow(800, 450, "Raylib 3D C#");

            camera = new Camera3D 
            {
                Position = new Vector3(20.0f, 20.0f, 20.0f),  // Camera location
                Target = new Vector3(0.0f, 0.0f, 0.0f),      // Camera looking at point
                Up = new Vector3(0.0f, 1.0f, 0.0f),          // Camera up vector (rotation)
                FovY = 45.0f,                                // Field of View
                Projection = CameraProjection.Perspective
            };

            // Lock mouse to screen if you are adding first-person controls later
            // Raylib.DisableCursor();

            Raylib.SetTargetFPS(60);     
        
            workspace.AddGameObject<TestObject>();
        }



        public void run()
        {
            // 3. Game Loop
            while (!Raylib.WindowShouldClose())
            {
                // Update camera (e.g., Raylib.UpdateCamera(ref camera, CameraMode.Orbital);)

                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.DarkGray);

                Raylib.BeginMode3D(camera);

                // Do 3d stuff
                workspace.Draw3D();

                Raylib.EndMode3D();

                // Do 2d stuff
                workspace.Draw2D();
                Raylib.DrawText("Welcome to Raylib 3D in C#!", 10, 10, 20, Color.DarkGray);

                Raylib.EndDrawing();
            }

            Raylib.CloseWindow();           
        }

    }
}