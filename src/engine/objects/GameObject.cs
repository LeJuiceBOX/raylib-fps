namespace PhrawgEngine
{
    /// <summary>
    /// Base class for all objects that live in a <see cref="Workspace"/>.
    /// A GameObject is a named container of <see cref="Component"/>s; behaviour
    /// is added by attaching components rather than subclassing.
    /// <para>
    /// Override <see cref="Load"/> for one-time setup, <see cref="Draw3D"/> /
    /// <see cref="Draw2D"/> for rendering, and leave per-frame logic to
    /// individual components via <see cref="Component.Update"/>.
    /// </para>
    /// </summary>
    public abstract class GameObject
    {
        /// <summary>All components attached to this object.</summary>
        public List<Component> components = [];

        /// <summary>Called once after the object is added to the workspace.</summary>
        public virtual void Load() { }

        /// <summary>Called each frame inside a Raylib BeginMode3D/EndMode3D block.</summary>
        public virtual void Draw2D() { }

        /// <summary>Called each frame inside a Raylib BeginDrawing/EndDrawing block, outside 3D mode.</summary>
        public virtual void Draw3D() { }

        /// <summary>
        /// Updates all components. Physics components (<see cref="Rigidbody"/>,
        /// <see cref="BrushCollider"/>) are lazy-initialised on the first tick
        /// via <see cref="Game.physicsServer"/>.
        /// </summary>
        public virtual void Update(float dt)
        {
            foreach (var c in components)
            {
                if (c is Rigidbody rb)     rb.Init_IfNeeded(Game.physicsServer);
                if (c is BrushCollider bc) bc.Init_IfNeeded(Game.physicsServer);
                c.Update(dt);
            }
        }

        /// <summary>Called when the object is removed from the workspace. Unloads all components.</summary>
        public virtual void Unload()
        {
            foreach (var c in components)
                c.Unload();
        }

        /// <summary>
        /// Returns the first attached component of type <typeparamref name="T"/>,
        /// or <c>null</c> if none exists.
        /// </summary>
        public T? GetComponent<T>() where T : Component
            => components.OfType<T>().FirstOrDefault();

        /// <summary>
        /// Creates a new instance of <typeparamref name="T"/>, wires its
        /// <see cref="Component.Owner"/> back to this object, appends it to
        /// the component list, and returns it.
        /// </summary>
        public T AddComponent<T>() where T : Component, new()
        {
            var c = new T { Owner = this };
            components.Add(c);
            return c;
        }
    }
}
