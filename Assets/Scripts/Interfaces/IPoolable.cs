namespace Genevore.Core
{
    /// <summary>
    /// Interface for pooled objects. Must reset all runtime state on despawn
    /// to prevent visual contamination and memory leaks.
    /// </summary>
    public interface IPoolable
    {
        void OnSpawn();
        void OnDespawn();
    }
}
