using UnityEngine;

namespace Genevore.Data
{
    [CreateAssetMenu(fileName = "BodyModule", menuName = "Genevore/Body Module", order = 1)]
    public class BodyModuleSO : ScriptableObject
    {
        public int ModuleHash;          // Unique int key for pooling (no string keys at runtime)
        public GameObject Prefab;       // Prefab containing SkinnedMeshRenderer
        public string[] RequiredBoneNames;
        public StatBlock BaseStats;
        public int SlotIndex;           // 0-5 preferred slot
    }
}
