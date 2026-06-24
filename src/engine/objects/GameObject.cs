
using PhrawgEngine;
using Raylib3D;

namespace PhrawgEngine
{
    public abstract class GameObject
    {
        public List<Component> components = [];


        public abstract void Load();
        //public abstract void Ready();
        public abstract void Update(float dt);
        public abstract void Draw2D();
        public abstract void Draw3D();


        public T? GetComponent<T>() where T : Component
        {
            return components.OfType<T>().FirstOrDefault();
        }
        public T AddComponent<T>() where T : Component, new()
        {
            T inst = new T();
            components.Add(inst);
            return inst;
        }


    }
}