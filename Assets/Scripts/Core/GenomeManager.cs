using UnityEngine;
using Genevore.Data;

namespace Genevore.Core
{
    /// <summary>
    /// Manages genetic mutations using ScriptableObject data.
    /// Max 6 gene slots. Static array only (no List&lt;T&gt;).
    /// All stat calculations use value-type Struct (StatBlock) — zero heap allocation.
    /// Stage 2: Event-driven notifications for HUD and combat systems.
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

        /// <summary>
        /// Raised after any gene change that causes stats to be recalculated.
        /// GenomeHUD and DamageableEntity subscribe. Zero string parameters.
        /// </summary>
        public event System.Action OnStatsRecalculated;

        /// <summary>
        /// Raised when a gene is successfully equipped. Args: slotIndex, gene.
        /// </summary>
        public event System.Action<int, GeneDataSO> OnGeneEquipped;

        /// <summary>
        /// Raised when a gene is removed. Args: slotIndex.
        /// </summary>
        public event System.Action<int> OnGeneRemoved;

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
        /// Preferred Stage-2 API. Adds gene and raises events.
        /// </summary>
        public bool EquipGene(int geneId)
        {
            bool success = TryAddGeneInternal(geneId);
            if (success)
            {
                int slot = _geneCount - 1;
                var gene = _activeGenes[slot];
                OnGeneEquipped?.Invoke(slot, gene);
                NotifyStatsChanged();
            }
            return success;
        }

        /// <summary>
        /// Preferred Stage-2 API. Removes gene and raises events.
        /// </summary>
        public bool UnequipGeneAt(int slot)
        {
            bool success = RemoveGeneAtInternal(slot);
            if (success)
            {
                OnGeneRemoved?.Invoke(slot);
                NotifyStatsChanged();
            }
            return success;
        }

        /// <summary>
        /// Legacy / internal path used by Stage-1 AutomatedBenchmark.
        /// Still functional; events are raised when called via EquipGene.
        /// </summary>
        public bool TryAddGene(int geneId) => EquipGene(geneId);

        public bool RemoveGeneAt(int slot) => UnequipGeneAt(slot);

        private bool TryAddGeneInternal(int geneId)
        {
            if (_geneCount >= MaxGeneSlots) return false;
            if (availableGenes == null) return false;

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

        private bool RemoveGeneAtInternal(int slot)
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
            NotifyStatsChanged();
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

        private void NotifyStatsChanged()
        {
            // Ensure cache is warm before listeners read TotalStats
            var _ = TotalStats;
            OnStatsRecalculated?.Invoke();
        }

        public GeneDataSO GetGeneAt(int slot)
        {
            if (slot < 0 || slot >= _geneCount) return null;
            return _activeGenes[slot];
        }

        public int GetModuleHashForGene(int geneId)
        {
            if (availableGenes == null) return 0;
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

            return EquipGene(gene.GeneId);
        }
    }
}
