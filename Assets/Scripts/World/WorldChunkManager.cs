using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Genevore.World
{
    /// <summary>
    /// Distance-based world chunk streaming via Addressables (async only).
    /// Hard limit: at most 2 concurrent LoadAssetAsync / LoadSceneAsync operations.
    /// No blocking (synchronous) loads — prevents main-thread stutter on mid-range Android.
    /// </summary>
    public class WorldChunkManager : MonoBehaviour
    {
        public const int MaxConcurrentLoads = 2;

        [System.Serializable]
        public struct ChunkDefinition
        {
            public string AddressableKey;   // Addressables address
            public Vector2Int Coord;       // Grid coordinate
            public Vector3 WorldOrigin;    // World-space origin of this chunk
            public float Size;             // Edge length (meters)
        }

        [Header("Player")]
        [SerializeField] private Transform player;

        [Header("Streaming")]
        [SerializeField] private float loadRadius = 80f;
        [SerializeField] private float unloadRadius = 110f;
        [SerializeField] private float evaluationInterval = 0.5f;
        [SerializeField] private ChunkDefinition[] chunkCatalog;

        [Header("Debug")]
        [SerializeField] private bool logTransitions;

        // Runtime state
        private readonly Dictionary<Vector2Int, LoadedChunk> _loaded = new Dictionary<Vector2Int, LoadedChunk>(32);
        private readonly Queue<Vector2Int> _loadQueue = new Queue<Vector2Int>(16);
        private readonly HashSet<Vector2Int> _queued = new HashSet<Vector2Int>();
        private int _activeLoads;
        private float _evalTimer;

        private struct LoadedChunk
        {
            public AsyncOperationHandle Handle;
            public GameObject Instance;       // if prefab-based
            public bool IsScene;              // if scene-based
            public Vector2Int Coord;
        }

        private void Update()
        {
            if (player == null) return;

            _evalTimer += Time.deltaTime;
            if (_evalTimer >= evaluationInterval)
            {
                _evalTimer = 0f;
                EvaluateStreaming();
            }

            ProcessLoadQueue();
        }

        private void EvaluateStreaming()
        {
            Vector3 playerPos = player.position;
            float loadSq = loadRadius * loadRadius;
            float unloadSq = unloadRadius * unloadRadius;

            // 1. Queue chunks that should be loaded
            if (chunkCatalog != null)
            {
                for (int i = 0; i < chunkCatalog.Length; i++)
                {
                    ref readonly var def = ref chunkCatalog[i];
                    Vector3 center = def.WorldOrigin + new Vector3(def.Size * 0.5f, 0f, def.Size * 0.5f);
                    float distSq = (center - playerPos).sqrMagnitude;

                    if (distSq <= loadSq)
                    {
                        if (!_loaded.ContainsKey(def.Coord) && !_queued.Contains(def.Coord))
                        {
                            _loadQueue.Enqueue(def.Coord);
                            _queued.Add(def.Coord);
                            if (logTransitions)
                                Debug.Log($"[WorldChunkManager] Queued load {def.Coord} key={def.AddressableKey}");
                        }
                    }
                }
            }

            // 2. Unload chunks outside unload radius
            var toUnload = ListPool<Vector2Int>.Get();
            foreach (var kvp in _loaded)
            {
                var def = FindDefinition(kvp.Key);
                if (def.AddressableKey == null) continue;

                Vector3 center = def.WorldOrigin + new Vector3(def.Size * 0.5f, 0f, def.Size * 0.5f);
                float distSq = (center - playerPos).sqrMagnitude;
                if (distSq > unloadSq)
                {
                    toUnload.Add(kvp.Key);
                }
            }

            for (int i = 0; i < toUnload.Count; i++)
            {
                UnloadChunk(toUnload[i]);
            }
            ListPool<Vector2Int>.Release(toUnload);
        }

        private void ProcessLoadQueue()
        {
            while (_activeLoads < MaxConcurrentLoads && _loadQueue.Count > 0)
            {
                Vector2Int coord = _loadQueue.Dequeue();
                _queued.Remove(coord);

                if (_loaded.ContainsKey(coord)) continue;

                var def = FindDefinition(coord);
                if (string.IsNullOrEmpty(def.AddressableKey)) continue;

                StartAsyncLoad(def);
            }
        }

        private void StartAsyncLoad(ChunkDefinition def)
        {
            _activeLoads++;

            bool isScene = def.AddressableKey.EndsWith("_scene");

            if (isScene)
            {
                var handle = Addressables.LoadSceneAsync(def.AddressableKey, LoadSceneMode.Additive, true);
                handle.Completed += op => OnSceneLoaded(op, def);
            }
            else
            {
                var handle = Addressables.InstantiateAsync(def.AddressableKey, def.WorldOrigin, Quaternion.identity);
                handle.Completed += op => OnPrefabLoaded(op, def);
            }
        }

        private void OnSceneLoaded(AsyncOperationHandle<SceneInstance> op, ChunkDefinition def)
        {
            _activeLoads = Mathf.Max(0, _activeLoads - 1);

            if (op.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[WorldChunkManager] Failed to load scene chunk {def.Coord}: {op.OperationException}");
                return;
            }

            _loaded[def.Coord] = new LoadedChunk
            {
                Handle = op,
                Instance = null,
                IsScene = true,
                Coord = def.Coord
            };

            if (logTransitions)
                Debug.Log($"[WorldChunkManager] Scene chunk loaded {def.Coord}");
        }

        private void OnPrefabLoaded(AsyncOperationHandle<GameObject> op, ChunkDefinition def)
        {
            _activeLoads = Mathf.Max(0, _activeLoads - 1);

            if (op.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[WorldChunkManager] Failed to load prefab chunk {def.Coord}: {op.OperationException}");
                return;
            }

            _loaded[def.Coord] = new LoadedChunk
            {
                Handle = op,
                Instance = op.Result,
                IsScene = false,
                Coord = def.Coord
            };

            if (logTransitions)
                Debug.Log($"[WorldChunkManager] Prefab chunk loaded {def.Coord}");
        }

        private void UnloadChunk(Vector2Int coord)
        {
            if (!_loaded.TryGetValue(coord, out var chunk)) return;

            if (chunk.IsScene)
            {
                Addressables.UnloadSceneAsync(chunk.Handle, true);
            }
            else
            {
                if (chunk.Instance != null)
                {
                    Addressables.ReleaseInstance(chunk.Instance);
                }
                else
                {
                    Addressables.Release(chunk.Handle);
                }
            }

            _loaded.Remove(coord);

            if (logTransitions)
                Debug.Log($"[WorldChunkManager] Unloaded chunk {coord}");
        }

        private ChunkDefinition FindDefinition(Vector2Int coord)
        {
            if (chunkCatalog == null) return default;
            for (int i = 0; i < chunkCatalog.Length; i++)
            {
                if (chunkCatalog[i].Coord == coord)
                    return chunkCatalog[i];
            }
            return default;
        }

        public int LoadedChunkCount => _loaded.Count;
        public int PendingLoads => _loadQueue.Count;
        public int ActiveLoads => _activeLoads;

        private void OnDestroy()
        {
            var coords = ListPool<Vector2Int>.Get();
            foreach (var k in _loaded.Keys) coords.Add(k);
            for (int i = 0; i < coords.Count; i++)
                UnloadChunk(coords[i]);
            ListPool<Vector2Int>.Release(coords);
        }
    }

    /// <summary>
    /// Tiny non-allocating list pool for temporary Vector2Int collections.
    /// Avoids GC in EvaluateStreaming unload path.
    /// </summary>
    internal static class ListPool<T>
    {
        private static readonly Stack<List<T>> _pool = new Stack<List<T>>(4);

        public static List<T> Get()
        {
            return _pool.Count > 0 ? _pool.Pop() : new List<T>(16);
        }

        public static void Release(List<T> list)
        {
            list.Clear();
            _pool.Push(list);
        }
    }
}
