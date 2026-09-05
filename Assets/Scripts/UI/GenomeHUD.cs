using UnityEngine;
using UnityEngine.UI;
using Genevore.Core;
using Genevore.Data;

namespace Genevore.UI
{
    /// <summary>
    /// Event-driven Genome slot visualiser. Max 6 slots.
    /// Updates ONLY when GenomeManager raises OnGeneEquipped / OnGeneRemoved / OnStatsRecalculated.
    /// Never rebuilds Canvas every frame.
    /// </summary>
    public class GenomeHUD : MonoBehaviour
    {
        [System.Serializable]
        public struct SlotUI
        {
            public Image icon;
            public Image background;
            public Text label; // optional, can be null
        }

        [SerializeField] private GenomeManager genomeManager;
        [SerializeField] private SlotUI[] slots = new SlotUI[6];
        [SerializeField] private Sprite emptySlotSprite;
        [SerializeField] private Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);
        [SerializeField] private Color filledColor = Color.white;

        // Optional simple text for total stats (updated only on event)
        [SerializeField] private Text hpText;
        [SerializeField] private Text atkText;

        private void OnEnable()
        {
            if (genomeManager == null)
            {
                genomeManager = FindObjectOfType<GenomeManager>();
            }

            if (genomeManager != null)
            {
                genomeManager.OnGeneEquipped += HandleGeneEquipped;
                genomeManager.OnGeneRemoved += HandleGeneRemoved;
                genomeManager.OnStatsRecalculated += HandleStatsRecalculated;
            }

            RefreshAllSlots();
        }

        private void OnDisable()
        {
            if (genomeManager != null)
            {
                genomeManager.OnGeneEquipped -= HandleGeneEquipped;
                genomeManager.OnGeneRemoved -= HandleGeneRemoved;
                genomeManager.OnStatsRecalculated -= HandleStatsRecalculated;
            }
        }

        private void HandleGeneEquipped(int slot, GeneDataSO gene)
        {
            if (slot < 0 || slot >= slots.Length) return;
            ApplyGeneToSlot(slot, gene);
        }

        private void HandleGeneRemoved(int slot)
        {
            // After a remove, later slots shift; safest is full refresh of remaining.
            RefreshAllSlots();
        }

        private void HandleStatsRecalculated()
        {
            if (genomeManager == null) return;
            var stats = genomeManager.TotalStats;
            if (hpText != null) hpText.text = $"HP {stats.HP:F0}";
            if (atkText != null) atkText.text = $"ATK {stats.Attack:F0}";
        }

        private void RefreshAllSlots()
        {
            if (genomeManager == null) return;

            int count = genomeManager.GeneCount;
            for (int i = 0; i < slots.Length; i++)
            {
                if (i < count)
                {
                    ApplyGeneToSlot(i, genomeManager.GetGeneAt(i));
                }
                else
                {
                    ClearSlot(i);
                }
            }

            HandleStatsRecalculated();
        }

        private void ApplyGeneToSlot(int index, GeneDataSO gene)
        {
            if (index < 0 || index >= slots.Length) return;
            ref SlotUI s = ref slots[index];

            if (s.icon != null)
            {
                // GeneDataSO currently has no Sprite field; leave icon null or extend SO later.
                // For Stage 2 we colour the background and write the name.
                s.icon.enabled = false;
            }

            if (s.background != null)
            {
                s.background.color = filledColor;
            }

            if (s.label != null && gene != null)
            {
                s.label.text = string.IsNullOrEmpty(gene.GeneName) ? gene.GeneId.ToString() : gene.GeneName;
            }
        }

        private void ClearSlot(int index)
        {
            if (index < 0 || index >= slots.Length) return;
            ref SlotUI s = ref slots[index];

            if (s.icon != null)
            {
                s.icon.sprite = emptySlotSprite;
                s.icon.enabled = emptySlotSprite != null;
            }

            if (s.background != null)
            {
                s.background.color = emptyColor;
            }

            if (s.label != null)
            {
                s.label.text = string.Empty;
            }
        }
    }
}
