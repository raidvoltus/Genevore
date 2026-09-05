using UnityEngine;
using Genevore.Core;

namespace Genevore.Enemy
{
    /// <summary>
    /// Simple continuous spawner for the Vertical Slice sandbox.
    /// Pulls enemies from ModuleObjectPool (Stage 1) so we stay zero-allocation after pre-warm.
    /// </summary>
    public class EnemySpawnerSandbox : MonoBehaviour
    {
        [SerializeField] private int enemyPrefabHash;
        [SerializeField] private int maxAlive = 15;
        [SerializeField] private float spawnInterval = 2.5f;
        [SerializeField] private float spawnRadius = 14f;
        [SerializeField] private Transform player;

        private float _timer;
        private int _aliveCount;

        private void Update()
        {
            if (ModuleObjectPool.Instance == null) return;

            _timer += Time.deltaTime;
            if (_timer < spawnInterval) return;
            _timer = 0f;

            if (_aliveCount >= maxAlive) return;

            Vector3 pos = GetSpawnPosition();
            var go = ModuleObjectPool.Instance.Acquire(enemyPrefabHash);
            if (go == null) return;

            go.transform.position = pos;
            go.transform.rotation = Quaternion.identity;

            var ai = go.GetComponent<EnemyAISandbox>();
            if (ai != null)
            {
                ai.PoolPrefabHash = enemyPrefabHash;
            }

            _aliveCount++;
        }

        private Vector3 GetSpawnPosition()
        {
            Vector3 center = player != null ? player.position : transform.position;
            Vector2 circle = Random.insideUnitCircle.normalized * spawnRadius;
            Vector3 pos = center + new Vector3(circle.x, 0f, circle.y);
            pos.y = 0f;
            return pos;
        }

        /// <summary>
        /// Called by Devour / AI when an enemy is returned to the pool.
        /// </summary>
        public void NotifyEnemyReleased()
        {
            _aliveCount = Mathf.Max(0, _aliveCount - 1);
        }
    }
}
