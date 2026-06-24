namespace PhrawgEngine
{
    public abstract class GameObject
    {
        public List<Component> components = [];

        public virtual void Load() {}
        public virtual void Draw2D() {}
        public virtual void Draw3D() {}

        // Physics is optional — only used if the object has a RigidbodyComponent
        public virtual void Update(float dt, PhysicsServer? physics = null)
        {
            foreach (var c in components)
            {
                if (c is RigidbodyComponent rb && physics != null)
                    rb.Init_IfNeeded(physics); // safe no-op after first call
                c.Update(dt);
            }
        }

        public T? GetComponent<T>() where T : Component
            => components.OfType<T>().FirstOrDefault();

        public T AddComponent<T>() where T : Component, new()
        {
            T inst = new T();
            components.Add(inst);
            return inst;
        }
    }
}