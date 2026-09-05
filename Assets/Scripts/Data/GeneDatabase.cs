using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Genevore.Data;

namespace Genevore.Data
{
    /// <summary>
    /// Large-scale gene registry. Loads only lightweight metadata at boot.
    /// Visual BodyModuleSO / prefabs are loaded via Addressables only when a gene is equipped.
    /// Target: 50+ gene entries, metadata RAM footprint &lt; 2 MB.
    /// </summary>
    public class GeneDatabase : MonoBehaviour
    {
        public static GeneDatabase Instance { get; private set; }

        [System.Serializable]
        public struct GeneMeta
        {
            public int GeneId;
            public string GeneName;
            public string ElementType;
            public float DropWeight;
            public int ModulePrefabHash;
            public string BodyModuleAddress;
            public StatBlock StatModifiers;
        }

        [Header("Bootstrap")]
        [SerializeField] private string catalogAddress = "GeneCatalog";
        [SerializeField] private bool loadOnAwake = true;

        private readonly Dictionary<int, GeneMeta> _byId = new Dictionary<int, GeneMeta>(64);
        private GeneMeta[] _allMeta = System.Array.Empty<GeneMeta>();
        private bool _ready;

        private readonly Dictionary<int, AsyncOperationHandle<BodyModuleSO>> _moduleHandles =
            new Dictionary<int, AsyncOperationHandle<BodyModuleSO>>(16);

        public bool IsReady => _ready;
        public int GeneCount => _allMeta.Length;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (loadOnAwake)
            {
                LoadCatalogAsync();
            }
        }

        private void OnDestroy()
        {
            foreach (var kvp in _moduleHandles)
            {
                if (kvp.Value.IsValid())
                    Addressables.Release(kvp.Value);
            }
            _moduleHandles.Clear();

            if (Instance == this) Instance = null;
        }

        public void LoadCatalogAsync()
        {
            if (string.IsNullOrEmpty(catalogAddress))
            {
                Debug.LogWarning("[GeneDatabase] No catalogAddress set — using empty DB.");
                _ready = true;
                return;
            }

            var handle = Addressables.LoadAssetAsync<GeneCatalogSO>(catalogAddress);
            handle.Completed += op =>
            {
                if (op.Status != AsyncOperationStatus.Succeeded || op.Result == null)
                {
                    Debug.LogError($"[GeneDatabase] Failed to load catalog '{catalogAddress}'.");
                    _ready = true;
                    return;
                }

                IngestCatalog(op.Result);
                _ready = true;
                Debug.Log($"[GeneDatabase] Loaded {_allMeta.Length} gene metadata entries.");
            };
        }

        public void IngestCatalog(GeneCatalogSO catalog)
        {
            if (catalog == null || catalog.Entries == null)
            {
                _allMeta = System.Array.Empty<GeneMeta>();
                _byId.Clear();
                return;
            }

            _byId.Clear();
            _allMeta = new GeneMeta[catalog.Entries.Length];
            for (int i = 0; i < catalog.Entries.Length; i++)
            {
                var e = catalog.Entries[i];
                var meta = new GeneMeta
                {
                    GeneId = e.GeneId,
                    GeneName = e.GeneName,
                    ElementType = e.ElementType,
                    DropWeight = e.DropWeight,
                    ModulePrefabHash = e.ModulePrefabHash,
                    BodyModuleAddress = e.BodyModuleAddress,
                    StatModifiers = e.StatModifiers
                };
                _allMeta[i] = meta;
                _byId[meta.GeneId] = meta;
            }
        }

        public bool TryGetMeta(int geneId, out GeneMeta meta)
        {
            return _byId.TryGetValue(geneId, out meta);
        }

        public GeneMeta[] GetAllMeta() => _allMeta;

        public int RollDropGeneId()
        {
            if (_allMeta.Length == 0) return -1;

            float total = 0f;
            for (int i = 0; i < _allMeta.Length; i++)
                total += Mathf.Max(0f, _allMeta[i].DropWeight);

            if (total <= 0f) return _allMeta[0].GeneId;

            float r = Random.value * total;
            float acc = 0f;
            for (int i = 0; i < _allMeta.Length; i++)
            {
                acc += Mathf.Max(0f, _allMeta[i].DropWeight);
                if (r <= acc) return _allMeta[i].GeneId;
            }
            return _allMeta[_allMeta.Length - 1].GeneId;
        }

        public void LoadBodyModuleAsync(int geneId, System.Action<BodyModuleSO> onLoaded)
        {
            if (!_byId.TryGetValue(geneId, out var meta))
            {
                onLoaded?.Invoke(null);
                return;
            }

            if (string.IsNullOrEmpty(meta.BodyModuleAddress))
            {
                onLoaded?.Invoke(null);
                return;
            }

            if (_moduleHandles.TryGetValue(geneId, out var existing) && existing.IsValid() && existing.IsDone)
            {
                onLoaded?.Invoke(existing.Result);
                return;
            }

            var handle = Addressables.LoadAssetAsync<BodyModuleSO>(meta.BodyModuleAddress);
            _moduleHandles[geneId] = handle;
            handle.Completed += op =>
            {
                if (op.Status == AsyncOperationStatus.Succeeded)
                    onLoaded?.Invoke(op.Result);
                else
                    onLoaded?.Invoke(null);
            };
        }

        public int EstimateMetadataBytes()
        {
            int perEntry = 20 + 20 + (16 + 8 + 32) * 2;
            return _allMeta.Length * perEntry;
        }
    }

    [CreateAssetMenu(fileName = "GeneCatalog", menuName = "Genevore/Gene Catalog", order = 10)]
    public class GeneCatalogSO : ScriptableObject
    {
        public GeneDatabase.GeneMeta[] Entries;
    }
}
