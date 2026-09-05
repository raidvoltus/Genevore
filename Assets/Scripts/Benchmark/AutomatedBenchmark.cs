using UnityEngine;
using Genevore.Core;

namespace Genevore.Benchmark
{
    /// <summary>
    /// Isolated stress-test harness for Stage 1 exit criteria.
    /// Spawns 15 enemy entities and triggers 100 sequential Devour/Mutation cycles.
    /// Profile on physical mid-range Android device (Snapdragon 720G / 4GB baseline).
    ///
    /// Exit Criteria (must be met):
    /// - GC Alloc during mutation/devour: 0 Bytes per frame
    /// - CPU Main Thread: ≤ 33.3 ms
    /// - Frame Rate: ≥ 30 FPS stable with 15 entities
    /// - Draw Calls / Batches: ≤ 80 in isolated scene
    /// </summary>
    public class AutomatedBenchmark : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DevourController playerDevour;
        [SerializeField] private GenomeManager playerGenome;
        [SerializeField] private CreatureAssembly playerAssembly;
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private Transform enemySpawnRoot;

        [Header("Test Parameters")]
        [SerializeField] private int enemyCount = 15;
        [SerializeField] private int mutationCycles = 100;
        [SerializeField] private float spawnRadius = 8f;
        [SerializeField] private float cycleInterval = 0.05f; // tight for stress

        private GameObject[] _enemies;
        private int _completedCycles;
        private bool _running;
        private float _timer;
        private float _startTime;

        private void Start()
        {
            if (playerDevour == null) playerDevour = FindObjectOfType<DevourController>();
            if (playerGenome == null) playerGenome = FindObjectOfType<GenomeManager>();
            if (playerAssembly == null) playerAssembly = FindObjectOfType<CreatureAssembly>();

            SpawnEnemies();
            _running = true;
            _startTime = Time.realtimeSinceStartup;
            Debug.Log($"[AutomatedBenchmark] Started. Target: {mutationCycles} cycles with {enemyCount} entities.");
        }

        private void SpawnEnemies()
        {
            _enemies = new GameObject[enemyCount];
            if (enemyPrefab == null)
            {
                Debug.LogWarning("[AutomatedBenchmark] No enemyPrefab assigned. Creating dummy placeholders.");
            }

            for (int i = 0; i < enemyCount; i++)
            {
                Vector3 pos = transform.position + Random.insideUnitSphere * spawnRadius;
                pos.y = 0f;

                GameObject enemy;
                if (enemyPrefab != null)
                {
                    enemy = Instantiate(enemyPrefab, pos, Quaternion.identity, enemySpawnRoot);
                }
                else
                {
                    enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    enemy.transform.position = pos;
                    enemy.transform.SetParent(enemySpawnRoot);
                    enemy.layer = LayerMask.NameToLayer("Default"); // adjust as needed
                    var col = enemy.GetComponent<Collider>();
                    if (col != null) col.isTrigger = false;
                }

                enemy.name = $"BenchmarkEnemy_{i}";
                _enemies[i] = enemy;
            }
        }

        private void Update()
        {
            if (!_running) return;

            _timer += Time.deltaTime;
            if (_timer < cycleInterval) return;
            _timer = 0f;

            if (_completedCycles >= mutationCycles)
            {
                Finish();
                return;
            }

            // Force a devour/mutation cycle (zero-alloc path)
            if (playerDevour != null)
            {
                playerDevour.ForceDevourCycle();
            }
            else if (playerGenome != null)
            {
                playerGenome.ApplyRandomMutation();
            }

            _completedCycles++;
        }

        private void Finish()
        {
            _running = false;
            float elapsed = Time.realtimeSinceStartup - _startTime;
            Debug.Log($"[AutomatedBenchmark] COMPLETED {_completedCycles} cycles in {elapsed:F2}s.");
            Debug.Log($"[AutomatedBenchmark] Final gene count: {(playerGenome != null ? playerGenome.GeneCount : -1)}");
            Debug.Log("[AutomatedBenchmark] Profile now in Unity Profiler (connected to device). Verify:");
            Debug.Log("  - GC Alloc == 0 B during cycles");
            Debug.Log("  - Main Thread ≤ 33.3 ms");
            Debug.Log("  - FPS ≥ 30");
            Debug.Log("  - Batches ≤ 80");
        }

        public void ResetBenchmark()
        {
            _completedCycles = 0;
            _timer = 0f;
            _running = true;
            _startTime = Time.realtimeSinceStartup;

            if (playerGenome != null) playerGenome.ClearAllGenes();
            if (playerAssembly != null) playerAssembly.DetachAll();
            if (playerDevour != null) playerDevour.ResetState();

            // Reactivate enemies
            if (_enemies != null)
            {
                for (int i = 0; i < _enemies.Length; i++)
                {
                    if (_enemies[i] != null) _enemies[i].SetActive(true);
                }
            }
        }
    }
}
