using System.Collections.Generic;
using UnityEngine;
using Genevore.Data;

namespace Genevore.Core
{
    /// <summary>
    /// Assembles modular body parts onto a single Master Skeleton at runtime.
    /// Bone re-binding only; never duplicates or creates new bone hierarchy.
    /// Proactive Dictionary mapping built on Awake.
    /// </summary>
    public class CreatureAssembly : MonoBehaviour
    {
        [SerializeField] private Transform masterSkeletonRoot;
        [SerializeField] private Transform moduleContainer;

        // Proactive name -> Transform map (built once)
        private readonly Dictionary<string, Transform> _boneMap = new Dictionary<string, Transform>(64);

        // Currently attached modules: slot -> (hash, instance)
        private readonly GameObject[] _attachedModules = new GameObject[6];
        private readonly int[] _attachedHashes = new int[6];

        private void Awake()
        {
            if (masterSkeletonRoot == null)
            {
                masterSkeletonRoot = transform;
            }

            if (moduleContainer == null)
            {
                var go = new GameObject("ModuleContainer");
                go.transform.SetParent(transform);
                moduleContainer = go.transform;
            }

            BuildBoneMap(masterSkeletonRoot);
        }

        private void BuildBoneMap(Transform root)
        {
            _boneMap.Clear();
            var stack = new Stack<Transform>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                // Use name as key (one-time cost at Awake)
                if (!_boneMap.ContainsKey(current.name))
                {
                    _boneMap[current.name] = current;
                }

                for (int i = 0; i < current.childCount; i++)
                {
                    stack.Push(current.GetChild(i));
                }
            }
        }

        /// <summary>
        /// Attach a module to the given slot. Rebinds SkinnedMeshRenderer.bones to Master Skeleton.
        /// </summary>
        public bool AttachModule(int slotIndex, int prefabHash)
        {
            if (slotIndex < 0 || slotIndex >= 6)
            {
                Debug.LogError($"[CreatureAssembly] Invalid slot index: {slotIndex}");
                return false;
            }

            // Release existing if any
            if (_attachedModules[slotIndex] != null)
            {
                DetachModule(slotIndex);
            }

            var instance = ModuleObjectPool.Instance.Acquire(prefabHash);
            if (instance == null) return false;

            instance.transform.SetParent(moduleContainer, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            var bodyPart = instance.GetComponent<ModularBodyPart>();
            if (bodyPart == null)
            {
                ModuleObjectPool.Instance.Release(prefabHash, instance);
                return false;
            }

            var smr = bodyPart.GetRenderer();
            if (smr == null)
            {
                ModuleObjectPool.Instance.Release(prefabHash, instance);
                return false;
            }

            // Rebind bones to master skeleton. Do NOT add or duplicate bones.
            RebindBones(smr, bodyPart.ModuleData != null ? bodyPart.ModuleData.RequiredBoneNames : null);

            _attachedModules[slotIndex] = instance;
            _attachedHashes[slotIndex] = prefabHash;
            return true;
        }

        private void RebindBones(SkinnedMeshRenderer smr, string[] requiredBoneNames)
        {
            // Build new bones array from master map
            Transform[] newBones;

            if (requiredBoneNames != null && requiredBoneNames.Length > 0)
            {
                newBones = new Transform[requiredBoneNames.Length];
                for (int i = 0; i < requiredBoneNames.Length; i++)
                {
                    if (_boneMap.TryGetValue(requiredBoneNames[i], out var bone))
                    {
                        newBones[i] = bone;
                    }
                    else
                    {
                        Debug.LogWarning($"[CreatureAssembly] Bone '{requiredBoneNames[i]}' not found in master skeleton.");
                        newBones[i] = masterSkeletonRoot; // safe fallback
                    }
                }
            }
            else
            {
                // Fallback: try to match by current bone names
                var oldBones = smr.bones;
                newBones = new Transform[oldBones.Length];
                for (int i = 0; i < oldBones.Length; i++)
                {
                    if (oldBones[i] != null && _boneMap.TryGetValue(oldBones[i].name, out var bone))
                    {
                        newBones[i] = bone;
                    }
                    else
                    {
                        newBones[i] = masterSkeletonRoot;
                    }
                }
            }

            smr.bones = newBones;

            // Set root bone if possible
            if (_boneMap.TryGetValue("Root", out var rootBone) || _boneMap.TryGetValue("Hips", out rootBone))
            {
                smr.rootBone = rootBone;
            }
            else
            {
                smr.rootBone = masterSkeletonRoot;
            }
        }

        public void DetachModule(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 6) return;
            if (_attachedModules[slotIndex] == null) return;

            var instance = _attachedModules[slotIndex];
            var hash = _attachedHashes[slotIndex];

            // Explicitly clear bones before release to avoid dangling references (highest risk)
            var bodyPart = instance.GetComponent<ModularBodyPart>();
            if (bodyPart != null)
            {
                var smr = bodyPart.GetRenderer();
                if (smr != null)
                {
                    smr.bones = System.Array.Empty<Transform>();
                    smr.rootBone = null;
                }
            }

            ModuleObjectPool.Instance.Release(hash, instance);

            _attachedModules[slotIndex] = null;
            _attachedHashes[slotIndex] = 0;
        }

        public void DetachAll()
        {
            for (int i = 0; i < 6; i++)
            {
                DetachModule(i);
            }
        }

        public GameObject GetAttachedModule(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 6) return null;
            return _attachedModules[slotIndex];
        }
    }
}
