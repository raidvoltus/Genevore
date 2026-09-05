using UnityEngine;
using Genevore.Data;
using Genevore.Core;

namespace Genevore.Combat
{
    /// <summary>
    /// Concrete IDamageable used by both player and sandbox enemies.
    /// HP is driven by StatBlock (value type). No Lists.
    /// </summary>
    public class DamageableEntity : MonoBehaviour, IDamageable
    {
        [SerializeField] private float baseMaxHP = 100f;
        [SerializeField] private GenomeManager genome; // optional; if present, MaxHP is influenced by genes

        private float _currentHP;
        private float _maxHP;
        private int _entityId;
        private static int _nextId = 1;

        public bool IsAlive => _currentHP > 0f;
        public float CurrentHP => _currentHP;
        public float MaxHP => _maxHP;
        public int EntityId => _entityId;

        /// <summary>
        /// Fired only when this entity's HP changes. UI / AI can subscribe.
        /// </summary>
        public event System.Action<float, float> OnHealthChanged; // current, max
        public event System.Action OnDeath;

        private void Awake()
        {
            _entityId = _nextId++;
            RecalculateMaxHP();
            _currentHP = _maxHP;
        }

        public void BindGenome(GenomeManager g)
        {
            genome = g;
            RecalculateMaxHP();
            _currentHP = Mathf.Min(_currentHP, _maxHP);
            OnHealthChanged?.Invoke(_currentHP, _maxHP);
        }

        public void RecalculateMaxHP()
        {
            float geneBonus = 0f;
            if (genome != null)
            {
                geneBonus = genome.TotalStats.HP;
            }
            _maxHP = Mathf.Max(1f, baseMaxHP + geneBonus);
        }

        public bool TakeDamage(float amount, int attackerId)
        {
            if (!IsAlive) return false;

            _currentHP = Mathf.Max(0f, _currentHP - amount);
            OnHealthChanged?.Invoke(_currentHP, _maxHP);

            if (_currentHP <= 0f)
            {
                OnDeath?.Invoke();
                return true;
            }
            return false;
        }

        public void Heal(float amount)
        {
            if (!IsAlive) return;
            _currentHP = Mathf.Min(_maxHP, _currentHP + amount);
            OnHealthChanged?.Invoke(_currentHP, _maxHP);
        }

        public void ResetFullHealth()
        {
            RecalculateMaxHP();
            _currentHP = _maxHP;
            OnHealthChanged?.Invoke(_currentHP, _maxHP);
        }

        /// <summary>
        /// Force death for pool release path (after Devour).
        /// </summary>
        public void ForceKill()
        {
            _currentHP = 0f;
            OnHealthChanged?.Invoke(0f, _maxHP);
            OnDeath?.Invoke();
        }
    }
}
