namespace PhrawgEngine
{
    public class Component
    {
        public GameObject? Owner { get; internal set; }

        public virtual void Load() { }
        public virtual void Update(float dt) { }
        public virtual void Unload() { }
    }
}
