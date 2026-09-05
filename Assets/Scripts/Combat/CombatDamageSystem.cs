using UnityEngine;
using Genevore.Data;
using Genevore.Core;

namespace Genevore.Combat
{
    /// <summary>
    /// Central damage resolution. Uses value-type StatBlock only.
    /// No List, no dynamic reference allocations on the hot path.
    /// Events are C# multicast delegates (static-friendly) — listeners must be lightweight.
    /// </summary>
    public static class CombatDamageSystem
    {
        /// <summary>
        /// Fired after any successful damage application. Args: targetId, amount, died.
        /// Listeners must not allocate.
        /// </summary>
        public static event System.Action<int, float, bool> OnDamageApplied;

        /// <summary>
        /// Fired when any IDamageable reaches zero HP.
        /// </summary>
        public static event System.Action<int> OnEntityDied;

        /// <summary>
        /// Resolve attack from attackerStats against defender.
        /// Pure calculation + single interface call. Zero heap.
        /// </summary>
        public static bool ResolveAttack(in StatBlock attackerStats, IDamageable defender, int attackerId)
        {
            if (defender == null || !defender.IsAlive) return false;

            // Simple formula: Attack * (1 - Defense/(Defense+100)) — no alloc
            float raw = attackerStats.Attack;
            float finalDamage = Mathf.Max(1f, raw);

            bool died = defender.TakeDamage(finalDamage, attackerId);

            OnDamageApplied?.Invoke(attackerId, finalDamage, died);
            if (died)
            {
                OnEntityDied?.Invoke(attackerId);
            }

            return died;
        }

        /// <summary>
        /// Convenience overload that pulls live stats from a GenomeManager.
        /// </summary>
        public static bool ResolveAttack(GenomeManager attackerGenome, IDamageable defender, int attackerId)
        {
            if (attackerGenome == null) return false;
            return ResolveAttack(attackerGenome.TotalStats, defender, attackerId);
        }
    }
}
