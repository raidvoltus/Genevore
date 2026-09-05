using Unity.Mathematics;

namespace Genevore.AI
{
    /// <summary>
    /// Lightweight value-type representation of an enemy that exists outside the
    /// physical render / NavMesh radius. Stored in a NativeArray or managed array
    /// and updated by the background simulator. Zero GameObject overhead.
    /// </summary>
    public struct AbstractEntityData
    {
        public int Id;
        public float3 Position;
        public float HP;
        public float MaxHP;
        public float Attack;
        public byte State;          // 0=Idle/Wander, 1=Combat, 2=Dead
        public int TargetId;        // -1 = none
        public int PrefabHash;      // for re-materialisation into ModuleObjectPool
        public bool IsActive;       // false = free slot

        public const byte StateWander = 0;
        public const byte StateCombat = 1;
        public const byte StateDead = 2;
    }
}
