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

        public void Update(float dt, PhysicsServer? physics = null)
        {
            foreach (GameObject obj in objects)
                obj.Update(dt, physics);
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