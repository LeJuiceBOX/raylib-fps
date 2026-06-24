

using Raylib3D;


namespace PhrawgEngine
{
    public class Workspace
    {
        private bool isLoaded = false;

        private List<GameObject> objects = [];


        public T AddGameObject<T>() where T : GameObject, new()
        {
            T inst = new T();
            objects.Add(inst);
            inst.Load();
            return inst;
        }

        public void Update(float dt)
        {
            foreach (GameObject obj in objects)
            {
                obj.Update(dt);
            }
        }

        public void Draw3D()
        {
            foreach (GameObject obj in objects)
            {
                obj.Draw3D();
            }            
        }

        public void Draw2D()
        {
            foreach (GameObject obj in objects)
            {
                obj.Draw2D();
            } 
        }
    }
}