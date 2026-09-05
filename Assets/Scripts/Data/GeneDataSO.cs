using UnityEngine;

namespace Genevore.Data
{
    [CreateAssetMenu(fileName = "GeneData", menuName = "Genevore/Gene Data", order = 0)]
    public class GeneDataSO : ScriptableObject
    {
        public int GeneId;
        public string GeneName;
        public StatBlock StatModifiers;
        public int ModulePrefabHash; // int hash of the BodyModuleSO or prefab key
    }
}
