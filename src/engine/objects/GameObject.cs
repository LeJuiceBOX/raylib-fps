namespace PhrawgEngine
{
    public abstract class GameObject
    {
        public List<Component> components = [];

        public virtual void Load() { }
        public virtual void Draw2D() { }
        public virtual void Draw3D() { }

        public virtual void Update(float dt, PhysicsServer? physics = null)
        {
            foreach (var c in components)
            {
                if (c is Rigidbody rb && physics != null)
                    rb.Init_IfNeeded(physics);
                c.Update(dt);
            }
        }

        public T? GetComponent<T>() where T : Component
            => components.OfType<T>().FirstOrDefault();

        public T AddComponent<T>() where T : Component, new()
        {
            var c = new T { Owner = this };
            components.Add(c);
            return c;
        }
    }
}
