using JoltPhysicsSharp;
using Raylib_cs;

namespace PhrawgEngine
{
    public class Workspace
    {
        private List<GameObject> objects = [];

        public T AddGameObject<T>() where T : GameObject, new()
        {
            T inst = new T();
            objects.Add(inst);
            inst.Load();
            return inst;
        }


        public List<GameObject> GetGameObjects()
        {
            return objects;
        }

        public void Update(float dt)
        {
            foreach (GameObject obj in objects)
                obj.Update(dt);
        }

        public void Unload()
        {
            foreach (GameObject obj in objects)
                obj.Unload();
            objects.Clear();
        }

        public void Draw3D()
        {
            foreach (GameObject obj in objects)
                obj.Draw3D();
        }

        public void Draw2D()
        {
            foreach (GameObject obj in objects)
                obj.Draw2D();
        }
    }
}