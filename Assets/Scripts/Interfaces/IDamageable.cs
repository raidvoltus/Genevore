using Genevore.Data;

namespace Genevore.Combat
{
    /// <summary>
    /// Zero-allocation damage contract. Implementors must not allocate on TakeDamage.
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        float CurrentHP { get; }
        float MaxHP { get; }

        /// <summary>
        /// Apply damage. amount is already resolved against attacker stats.
        /// Returns true if the target died as a result of this hit.
        /// </summary>
        bool TakeDamage(float amount, int attackerId);

        void Heal(float amount);
    }
}
