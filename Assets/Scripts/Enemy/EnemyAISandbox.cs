using UnityEngine;
using UnityEngine.AI;
using Genevore.Core;
using Genevore.Combat;

namespace Genevore.Enemy
{
    /// <summary>
    /// Lightweight FSM for sandbox enemies: Wander → Flee (HP &lt; 20%) → Dead.
    /// Uses NavMeshAgent with mobile-friendly settings.
    /// Spawned from ModuleObjectPool; released back to pool after Devour completes.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(DamageableEntity))]
    public class EnemyAISandbox : MonoBehaviour, IPoolable
    {
        public enum AIState
        {
            Wander,
            Flee,
            Dead
        }

        [Header("Wander")]
        [SerializeField] private float wanderRadius = 12f;
        [SerializeField] private float wanderInterval = 3f;

        [Header("Flee")]
        [SerializeField] private float fleeHPThreshold = 0.2f;
        [SerializeField] private float fleeDistance = 8f;
        [SerializeField] private float fleeSpeedMultiplier = 1.4f;

        [Header("Pool")]
        [SerializeField] private int poolPrefabHash; // set by spawner

        private NavMeshAgent _agent;
        private DamageableEntity _damageable;
        private AIState _state = AIState.Wander;
        private float _wanderTimer;
        private Transform _player;
        private float _baseSpeed;
        private bool _released;

        public AIState CurrentState => _state;
        public int PoolPrefabHash
        {
            get => poolPrefabHash;
            set => poolPrefabHash = value;
        }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _damageable = GetComponent<DamageableEntity>();

            // Mobile-friendly defaults
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            _agent.avoidancePriority = 50;
            _agent.acceleration = 12f;
            _agent.angularSpeed = 240f;
            _baseSpeed = _agent.speed;

            if (_damageable != null)
            {
                _damageable.OnDeath += HandleDeath;
                _damageable.OnHealthChanged += HandleHealthChanged;
            }
        }

        private void OnDestroy()
        {
            if (_damageable != null)
            {
                _damageable.OnDeath -= HandleDeath;
                _damageable.OnHealthChanged -= HandleHealthChanged;
            }
        }

        public void OnSpawn()
        {
            _released = false;
            _state = AIState.Wander;
            _wanderTimer = 0f;
            if (_agent != null)
            {
                _agent.enabled = true;
                _agent.isStopped = false;
                _agent.speed = _baseSpeed;
                _agent.ResetPath();
            }
            if (_damageable != null)
            {
                _damageable.ResetFullHealth();
            }
            // Cache player once
            if (_player == null)
            {
                var pc = FindObjectOfType<Genevore.Player.MobilePlayerController>();
                if (pc != null) _player = pc.transform;
            }
        }

        public void OnDespawn()
        {
            if (_agent != null)
            {
                _agent.ResetPath();
                _agent.enabled = false;
            }
            _state = AIState.Dead;
            _player = null;
        }

        private void Update()
        {
            if (_released || _state == AIState.Dead) return;
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;

            switch (_state)
            {
                case AIState.Wander:
                    TickWander();
                    break;
                case AIState.Flee:
                    TickFlee();
                    break;
            }
        }

        private void TickWander()
        {
            _wanderTimer -= Time.deltaTime;
            if (_wanderTimer <= 0f || !_agent.hasPath || _agent.remainingDistance < 0.5f)
            {
                SetRandomWanderDestination();
                _wanderTimer = wanderInterval + Random.Range(-0.5f, 0.5f);
            }
        }

        private void TickFlee()
        {
            if (_player == null)
            {
                _state = AIState.Wander;
                _agent.speed = _baseSpeed;
                return;
            }

            Vector3 away = (transform.position - _player.position).normalized;
            Vector3 target = transform.position + away * fleeDistance;

            if (NavMesh.SamplePosition(target, out NavMeshHit hit, fleeDistance, NavMesh.AllAreas))
            {
                _agent.SetDestination(hit.position);
            }
        }

        private void SetRandomWanderDestination()
        {
            Vector3 randomDir = Random.insideUnitSphere * wanderRadius;
            randomDir.y = 0f;
            Vector3 candidate = transform.position + randomDir;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
            {
                _agent.SetDestination(hit.position);
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (_state == AIState.Dead) return;

            float ratio = max > 0f ? current / max : 0f;
            if (ratio < fleeHPThreshold && _state != AIState.Flee)
            {
                _state = AIState.Flee;
                _agent.speed = _baseSpeed * fleeSpeedMultiplier;
            }
            else if (ratio >= fleeHPThreshold && _state == AIState.Flee)
            {
                _state = AIState.Wander;
                _agent.speed = _baseSpeed;
            }
        }

        private void HandleDeath()
        {
            _state = AIState.Dead;
            if (_agent != null)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }
            // Do NOT release yet — wait for DevourController to finish the eat animation.
            // External system (or DevourController) will call ReleaseToPool after devour.
        }

        /// <summary>
        /// Called by Devour system after the eat sequence completes.
        /// </summary>
        public void ReleaseToPool()
        {
            if (_released) return;
            _released = true;

            if (ModuleObjectPool.Instance != null && poolPrefabHash != 0)
            {
                ModuleObjectPool.Instance.Release(poolPrefabHash, gameObject);
            }
            else
            {
                // Fallback for sandbox without pool config
                gameObject.SetActive(false);
            }
        }
    }
}
