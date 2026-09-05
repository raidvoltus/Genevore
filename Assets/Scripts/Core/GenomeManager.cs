using UnityEngine;
using Genevore.Data;

namespace Genevore.Core
{
    /// <summary>
    /// Manages genetic mutations using ScriptableObject data.
    /// Max 6 gene slots. Static array only (no List&lt;T&gt;).
    /// All stat calculations use value-type Struct (StatBlock) — zero heap allocation.
    /// </summary>
    public class GenomeManager : MonoBehaviour
    {
        public const int MaxGeneSlots = 6;

        [SerializeField] private GeneDataSO[] availableGenes; // catalog
        [SerializeField] private BodyModuleSO[] availableModules;

        // Static capacity array — never resize
        private readonly GeneDataSO[] _activeGenes = new GeneDataSO[MaxGeneSlots];
        private int _geneCount;

        private StatBlock _cachedTotalStats;
        private bool _statsDirty = true;

        public StatBlock TotalStats
        {
            get
            {
                if (_statsDirty)
                {
                    RecalculateStats();
                }
                return _cachedTotalStats;
            }
        }

        public int GeneCount => _geneCount;

        /// <summary>
        /// Attempt to add a gene by id. Returns false if slots full or gene not found.
        /// </summary>
        public bool TryAddGene(int geneId)
        {
            if (_geneCount >= MaxGeneSlots) return false;

            GeneDataSO gene = null;
            for (int i = 0; i < availableGenes.Length; i++)
            {
                if (availableGenes[i] != null && availableGenes[i].GeneId == geneId)
                {
                    gene = availableGenes[i];
                    break;
                }
            }

            if (gene == null) return false;

            _activeGenes[_geneCount] = gene;
            _geneCount++;
            _statsDirty = true;
            return true;
        }

        /// <summary>
        /// Remove gene at slot. Shifts remaining genes down (no allocation).
        /// </summary>
        public bool RemoveGeneAt(int slot)
        {
            if (slot < 0 || slot >= _geneCount) return false;

            for (int i = slot; i < _geneCount - 1; i++)
            {
                _activeGenes[i] = _activeGenes[i + 1];
            }
            _activeGenes[_geneCount - 1] = null;
            _geneCount--;
            _statsDirty = true;
            return true;
        }

        public void ClearAllGenes()
        {
            for (int i = 0; i < MaxGeneSlots; i++)
            {
                _activeGenes[i] = null;
            }
            _geneCount = 0;
            _statsDirty = true;
        }

        private void RecalculateStats()
        {
            _cachedTotalStats.Reset();
            for (int i = 0; i < _geneCount; i++)
            {
                if (_activeGenes[i] != null)
                {
                    _cachedTotalStats.Add(_activeGenes[i].StatModifiers);
                }
            }
            _statsDirty = false;
        }

        public GeneDataSO GetGeneAt(int slot)
        {
            if (slot < 0 || slot >= _geneCount) return null;
            return _activeGenes[slot];
        }

        public int GetModuleHashForGene(int geneId)
        {
            for (int i = 0; i < availableGenes.Length; i++)
            {
                if (availableGenes[i] != null && availableGenes[i].GeneId == geneId)
                {
                    return availableGenes[i].ModulePrefabHash;
                }
            }
            return 0;
        }

        /// <summary>
        /// Apply a random mutation from available catalog (for benchmark / devour).
        /// </summary>
        public bool ApplyRandomMutation()
        {
            if (availableGenes == null || availableGenes.Length == 0) return false;
            if (_geneCount >= MaxGeneSlots) return false;

            int idx = Random.Range(0, availableGenes.Length);
            var gene = availableGenes[idx];
            if (gene == null) return false;

            return TryAddGene(gene.GeneId);
        }
    }
}
