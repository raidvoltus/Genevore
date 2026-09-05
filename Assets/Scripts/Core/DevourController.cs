using UnityEngine;
using Genevore.Data;

namespace Genevore.Core
{
    /// <summary>
    /// Finite State Machine for the core "Devour" mechanic.
    /// States: Idle → TargetLocked → Executing → Cooldown.
    /// Detection uses Physics.OverlapSphereNonAlloc exclusively (static buffer).
    /// LINQ and allocating OverlapSphere are forbidden.
    /// </summary>
    public class DevourController : MonoBehaviour
    {
        public enum DevourState
        {
            Idle,
            TargetLocked,
            Executing,
            Cooldown
        }

        [Header("Detection")]
        [SerializeField] private float detectRadius = 5f;
        [SerializeField] private LayerMask enemyLayer;
        [SerializeField] private int maxCandidates = 16;

        [Header("Timing")]
        [SerializeField] private float executeDuration = 0.6f;
        [SerializeField] private float cooldownDuration = 1.2f;

        [Header("References")]
        [SerializeField] private GenomeManager genomeManager;
        [SerializeField] private CreatureAssembly creatureAssembly;

        // Static non-alloc buffer — shared, never resized at runtime
        private static readonly Collider[] _overlapBuffer = new Collider[32];

        private DevourState _state = DevourState.Idle;
        private Transform _currentTarget;
        private float _stateTimer;
        private int _devourCount;

        public DevourState CurrentState => _state;
        public int DevourCount => _devourCount;

        private void Awake()
        {
            if (genomeManager == null) genomeManager = GetComponent<GenomeManager>();
            if (creatureAssembly == null) creatureAssembly = GetComponent<CreatureAssembly>();
        }

        private void Update()
        {
            switch (_state)
            {
                case DevourState.Idle:
                    TickIdle();
                    break;
                case DevourState.TargetLocked:
                    TickTargetLocked();
                    break;
                case DevourState.Executing:
                    TickExecuting();
                    break;
                case DevourState.Cooldown:
                    TickCooldown();
                    break;
            }
        }

        private void TickIdle()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, detectRadius, _overlapBuffer, enemyLayer);
            if (count <= 0) return;

            // Pick nearest valid target without allocation
            float bestDistSq = float.MaxValue;
            Transform best = null;

            for (int i = 0; i < count; i++)
            {
                var col = _overlapBuffer[i];
                if (col == null || col.transform == transform) continue;

                float distSq = (col.transform.position - transform.position).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = col.transform;
                }
            }

            if (best != null)
            {
                _currentTarget = best;
                _state = DevourState.TargetLocked;
                _stateTimer = 0f;
            }
        }

        private void TickTargetLocked()
        {
            if (_currentTarget == null)
            {
                _state = DevourState.Idle;
                return;
            }

            // Simple face target (no allocation)
            Vector3 dir = _currentTarget.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 10f);
            }

            // Transition to execute after brief lock (or immediate for benchmark)
            _stateTimer += Time.deltaTime;
            if (_stateTimer >= 0.15f)
            {
                _state = DevourState.Executing;
                _stateTimer = 0f;
            }
        }

        private void TickExecuting()
        {
            _stateTimer += Time.deltaTime;
            if (_stateTimer >= executeDuration)
            {
                PerformDevour();
                _state = DevourState.Cooldown;
                _stateTimer = 0f;
                _currentTarget = null;
            }
        }

        private void TickCooldown()
        {
            _stateTimer += Time.deltaTime;
            if (_stateTimer >= cooldownDuration)
            {
                _state = DevourState.Idle;
                _stateTimer = 0f;
            }
        }

        private void PerformDevour()
        {
            _devourCount++;

            // Trigger mutation / evolution
            if (genomeManager != null)
            {
                bool mutated = genomeManager.ApplyRandomMutation();
                if (mutated && creatureAssembly != null)
                {
                    // Attach corresponding module if gene carries a module hash
                    int lastSlot = genomeManager.GeneCount - 1;
                    var gene = genomeManager.GetGeneAt(lastSlot);
                    if (gene != null && gene.ModulePrefabHash != 0)
                    {
                        creatureAssembly.AttachModule(lastSlot, gene.ModulePrefabHash);
                    }
                }
            }

            // Optional: disable / pool the target enemy here in full implementation
            if (_currentTarget != null)
            {
                // For prototype: simply deactivate
                _currentTarget.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Force a devour cycle for benchmarking (bypasses detection).
        /// </summary>
        public void ForceDevourCycle()
        {
            if (_state != DevourState.Idle && _state != DevourState.Cooldown) return;

            _state = DevourState.Executing;
            _stateTimer = 0f;
            // Target may be null; PerformDevour still runs mutation
        }

        public void ResetState()
        {
            _state = DevourState.Idle;
            _stateTimer = 0f;
            _currentTarget = null;
            _devourCount = 0;
        }
    }
}
