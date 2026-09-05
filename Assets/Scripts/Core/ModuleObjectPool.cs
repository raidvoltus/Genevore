using System.Collections.Generic;
using UnityEngine;
using Genevore.Data;

namespace Genevore.Core
{
    /// <summary>
    /// Zero-allocation object pool for body modules (SkinnedMeshRenderer prefabs).
    /// Keys are int hashes only. No string comparisons at runtime.
    /// Instantiate/Destroy are forbidden during gameplay; pool must be pre-warmed.
    /// </summary>
    public class ModuleObjectPool : MonoBehaviour
    {
        public static ModuleObjectPool Instance { get; private set; }

        [System.Serializable]
        public struct PoolConfig
        {
            public int PrefabHash;
            public GameObject Prefab;
            public int PrewarmCount;
        }

        [SerializeField] private PoolConfig[] configs;
        [SerializeField] private Transform poolRoot;

        // int hash -> queue of inactive instances
        private readonly Dictionary<int, Queue<GameObject>> _pools = new Dictionary<int, Queue<GameObject>>(16);
        private readonly Dictionary<int, GameObject> _prefabLookup = new Dictionary<int, GameObject>(16);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (poolRoot == null)
            {
                var rootGo = new GameObject("[ModulePoolRoot]");
                rootGo.transform.SetParent(transform);
                poolRoot = rootGo.transform;
            }

            PrewarmAll();
        }

        private void PrewarmAll()
        {
            if (configs == null) return;

            for (int i = 0; i < configs.Length; i++)
            {
                var cfg = configs[i];
                if (cfg.Prefab == null || cfg.PrefabHash == 0) continue;

                if (!_pools.ContainsKey(cfg.PrefabHash))
                {
                    _pools[cfg.PrefabHash] = new Queue<GameObject>(cfg.PrewarmCount);
                    _prefabLookup[cfg.PrefabHash] = cfg.Prefab;
                }

                for (int j = 0; j < cfg.PrewarmCount; j++)
                {
                    var instance = CreateInstance(cfg.PrefabHash, cfg.Prefab);
                    _pools[cfg.PrefabHash].Enqueue(instance);
                }
            }
        }

        private GameObject CreateInstance(int hash, GameObject prefab)
        {
            // Only called during prewarm / Awake. Never during gameplay.
            var go = Instantiate(prefab, poolRoot);
            go.SetActive(false);

            var poolable = go.GetComponent<IPoolable>();
            if (poolable == null)
            {
                // Ensure every pooled module implements IPoolable
                Debug.LogError($"[ModuleObjectPool] Prefab {prefab.name} (hash={hash}) is missing IPoolable component.");
            }

            return go;
        }

        /// <summary>
        /// Acquire a module instance by int hash. Returns null if pool is empty (should never happen if prewarmed correctly).
        /// </summary>
        public GameObject Acquire(int prefabHash)
        {
            if (!_pools.TryGetValue(prefabHash, out var queue))
            {
                Debug.LogError($"[ModuleObjectPool] Unknown prefab hash: {prefabHash}");
                return null;
            }

            GameObject instance;
            if (queue.Count > 0)
            {
                instance = queue.Dequeue();
            }
            else
            {
                // Fallback only if misconfigured. Log critical warning.
                // Still zero-allocation path is preferred; this should be prevented by prewarm sizing.
                if (_prefabLookup.TryGetValue(prefabHash, out var prefab))
                {
                    Debug.LogWarning($"[ModuleObjectPool] Pool exhausted for hash {prefabHash}. Creating emergency instance (violates strict zero-alloc if during gameplay).");
                    instance = CreateInstance(prefabHash, prefab);
                }
                else
                {
                    return null;
                }
            }

            instance.SetActive(true);
            var poolable = instance.GetComponent<IPoolable>();
            poolable?.OnSpawn();
            return instance;
        }

        /// <summary>
        /// Release a module back to the pool. Must call IPoolable.OnDespawn to reset state.
        /// </summary>
        public void Release(int prefabHash, GameObject instance)
        {
            if (instance == null) return;

            var poolable = instance.GetComponent<IPoolable>();
            poolable?.OnDespawn();

            instance.SetActive(false);
            instance.transform.SetParent(poolRoot);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            if (!_pools.TryGetValue(prefabHash, out var queue))
            {
                queue = new Queue<GameObject>();
                _pools[prefabHash] = queue;
            }
            queue.Enqueue(instance);
        }

        public int GetPooledCount(int prefabHash)
        {
            return _pools.TryGetValue(prefabHash, out var q) ? q.Count : 0;
        }
    }
}
