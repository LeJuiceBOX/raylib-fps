using System.Numerics;
using PhrawgEngine;
using Raylib_cs;

namespace Raylib3D
{
    class Program
    {
        static void Main(string[] args)
        {
            Game Game = new Game();

            Game.setup();
            Game.run();
        }
    }
}
