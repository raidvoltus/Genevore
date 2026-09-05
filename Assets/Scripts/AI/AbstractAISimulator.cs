using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using Genevore.Core;
using Genevore.Combat;
using Genevore.Enemy;

namespace Genevore.AI
{
    public class AbstractAISimulator : MonoBehaviour
    {
        [SerializeField] private float materialiseRadius = 50f;
        [SerializeField] private float dematerialiseRadius = 60f;
        [SerializeField] private int maxAbstractEntities = 256;
        [SerializeField] private float simulationInterval = 2f;
        [SerializeField] private bool useJobSystem = true;
        [SerializeField] private float abstractCombatRange = 4f;
        [SerializeField] private float abstractWanderSpeed = 1.5f;
        [SerializeField] private Transform player;
        [SerializeField] private int defaultEnemyPrefabHash;

        private AbstractEntityData[] _abstract;
        private NativeArray<AbstractEntityData> _nativeAbstract;
        private bool _nativeAllocated;
        private readonly Dictionary<int, GameObject> _physical = new Dictionary<int, GameObject>(64);
        private readonly Dictionary<int, int> _physicalToAbstractIndex = new Dictionary<int, int>(64);
        private float _simTimer;
        private JobHandle _pendingJob;
        private bool _jobScheduled;
        private int _nextId = 1000;

        private void Awake()
        {
            _abstract = new AbstractEntityData[maxAbstractEntities];
            for (int i = 0; i < maxAbstractEntities; i++) { _abstract[i].IsActive = false; _abstract[i].TargetId = -1; }
            if (useJobSystem)
            {
                _nativeAbstract = new NativeArray<AbstractEntityData>(maxAbstractEntities, Allocator.Persistent);
                _nativeAllocated = true;
            }
        }

        private void OnDestroy()
        {
            if (_jobScheduled) { _pendingJob.Complete(); _jobScheduled = false; }
            if (_nativeAllocated && _nativeAbstract.IsCreated) { _nativeAbstract.Dispose(); _nativeAllocated = false; }
        }

        public void SetMaterialiseRadius(float radius)
        {
            materialiseRadius = Mathf.Max(5f, radius);
            dematerialiseRadius = materialiseRadius + 10f;
        }

        private void Update()
        {
            if (player == null) return;
            if (_jobScheduled && _pendingJob.IsCompleted)
            {
                _pendingJob.Complete(); _jobScheduled = false;
                for (int i = 0; i < maxAbstractEntities; i++) _abstract[i] = _nativeAbstract[i];
            }
            UpdateMaterialisation();
            _simTimer += Time.deltaTime;
            if (_simTimer >= simulationInterval && !_jobScheduled) { _simTimer = 0f; RunAbstractSimulation(); }
        }

        private void UpdateMaterialisation()
        {
            float matSq = materialiseRadius * materialiseRadius;
            float dematSq = dematerialiseRadius * dematerialiseRadius;
            float3 playerPos = player.position;
            var toDemat = new List<int>(8);
            foreach (var kvp in _physical)
            {
                if (kvp.Value == null) { toDemat.Add(kvp.Key); continue; }
                if (math.distancesq((float3)kvp.Value.transform.position, playerPos) > dematSq) toDemat.Add(kvp.Key);
            }
            for (int i = 0; i < toDemat.Count; i++) Dematerialise(toDemat[i]);
            for (int i = 0; i < maxAbstractEntities; i++)
            {
                if (!_abstract[i].IsActive || _abstract[i].State == AbstractEntityData.StateDead) continue;
                if (_physical.ContainsKey(_abstract[i].Id)) continue;
                if (math.distancesq(_abstract[i].Position, playerPos) <= matSq) Materialise(i);
            }
        }

        private void Materialise(int abstractIndex)
        {
            ref var data = ref _abstract[abstractIndex];
            int hash = data.PrefabHash != 0 ? data.PrefabHash : defaultEnemyPrefabHash;
            if (ModuleObjectPool.Instance == null) return;
            var go = ModuleObjectPool.Instance.Acquire(hash);
            if (go == null) return;
            go.transform.position = data.Position;
            go.transform.rotation = Quaternion.identity;
            var dmg = go.GetComponent<DamageableEntity>();
            if (dmg != null)
            {
                dmg.ResetFullHealth();
                float missing = dmg.MaxHP - data.HP;
                if (missing > 0f) dmg.TakeDamage(missing, -1);
            }
            var ai = go.GetComponent<EnemyAISandbox>();
            if (ai != null) ai.PoolPrefabHash = hash;
            _physical[data.Id] = go;
            _physicalToAbstractIndex[data.Id] = abstractIndex;
        }

        private void Dematerialise(int entityId)
        {
            if (!_physical.TryGetValue(entityId, out var go)) return;
            if (_physicalToAbstractIndex.TryGetValue(entityId, out int absIdx) && absIdx >= 0)
            {
                ref var data = ref _abstract[absIdx];
                data.Position = go != null ? (float3)go.transform.position : data.Position;
                var dmg = go != null ? go.GetComponent<DamageableEntity>() : null;
                if (dmg != null) { data.HP = dmg.CurrentHP; data.MaxHP = dmg.MaxHP; if (!dmg.IsAlive) data.State = AbstractEntityData.StateDead; }
            }
            if (go != null)
            {
                var ai = go.GetComponent<EnemyAISandbox>();
                int hash = ai != null ? ai.PoolPrefabHash : defaultEnemyPrefabHash;
                if (ModuleObjectPool.Instance != null && hash != 0) ModuleObjectPool.Instance.Release(hash, go);
                else go.SetActive(false);
            }
            _physical.Remove(entityId);
            _physicalToAbstractIndex.Remove(entityId);
        }

        private void RunAbstractSimulation()
        {
            if (useJobSystem && _nativeAllocated)
            {
                for (int i = 0; i < maxAbstractEntities; i++) _nativeAbstract[i] = _abstract[i];
                var job = new AbstractSimJob {
                    Entities = _nativeAbstract, DeltaTime = simulationInterval,
                    CombatRange = abstractCombatRange, WanderSpeed = abstractWanderSpeed,
                    Seed = (uint)(Time.frameCount * 7919)
                };
                _pendingJob = job.Schedule();
                _jobScheduled = true;
            }
            else SimulateOnMainThread(simulationInterval);
        }

        private void SimulateOnMainThread(float dt)
        {
            for (int i = 0; i < maxAbstractEntities; i++)
            {
                if (!_abstract[i].IsActive || _abstract[i].State == AbstractEntityData.StateDead) continue;
                if (_physical.ContainsKey(_abstract[i].Id)) continue;
                ref var e = ref _abstract[i];
                float angle = (e.Id * 0.17f + Time.time * 0.3f) % (math.PI * 2f);
                e.Position += new float3(math.cos(angle), 0f, math.sin(angle)) * abstractWanderSpeed * dt;
                float bestDist = abstractCombatRange; int bestIdx = -1;
                for (int j = 0; j < maxAbstractEntities; j++)
                {
                    if (i == j || !_abstract[j].IsActive || _abstract[j].State == AbstractEntityData.StateDead) continue;
                    float d = math.distance(e.Position, _abstract[j].Position);
                    if (d < bestDist) { bestDist = d; bestIdx = j; }
                }
                if (bestIdx >= 0)
                {
                    e.State = AbstractEntityData.StateCombat; e.TargetId = _abstract[bestIdx].Id;
                    _abstract[bestIdx].HP -= e.Attack * 0.1f * dt;
                    if (_abstract[bestIdx].HP <= 0f) { _abstract[bestIdx].HP = 0f; _abstract[bestIdx].State = AbstractEntityData.StateDead; }
                }
                else { e.State = AbstractEntityData.StateWander; e.TargetId = -1; }
            }
        }

        public int RegisterAbstractEntity(float3 position, float hp, float attack, int prefabHash)
        {
            for (int i = 0; i < maxAbstractEntities; i++)
            {
                if (_abstract[i].IsActive) continue;
                int id = _nextId++;
                _abstract[i] = new AbstractEntityData {
                    Id = id, Position = position, HP = hp, MaxHP = hp, Attack = attack,
                    State = AbstractEntityData.StateWander, TargetId = -1, PrefabHash = prefabHash, IsActive = true
                };
                return id;
            }
            return -1;
        }

        public int ActiveAbstractCount { get { int c = 0; for (int i = 0; i < maxAbstractEntities; i++) if (_abstract[i].IsActive) c++; return c; } }
        public int PhysicalCount => _physical.Count;
    }

    [BurstCompile]
    public struct AbstractSimJob : IJob
    {
        public NativeArray<AbstractEntityData> Entities;
        public float DeltaTime, CombatRange, WanderSpeed;
        public uint Seed;

        public void Execute()
        {
            var rng = new Unity.Mathematics.Random(Seed == 0 ? 1u : Seed);
            for (int i = 0; i < Entities.Length; i++)
            {
                var e = Entities[i];
                if (!e.IsActive || e.State == AbstractEntityData.StateDead) continue;
                float angle = rng.NextFloat(0f, math.PI * 2f);
                e.Position += new float3(math.cos(angle), 0f, math.sin(angle)) * WanderSpeed * DeltaTime;
                float bestDist = CombatRange; int bestIdx = -1;
                for (int j = 0; j < Entities.Length; j++)
                {
                    if (i == j) continue;
                    var o = Entities[j];
                    if (!o.IsActive || o.State == AbstractEntityData.StateDead) continue;
                    float d = math.distance(e.Position, o.Position);
                    if (d < bestDist) { bestDist = d; bestIdx = j; }
                }
                if (bestIdx >= 0)
                {
                    e.State = AbstractEntityData.StateCombat; e.TargetId = Entities[bestIdx].Id;
                    var target = Entities[bestIdx];
                    target.HP -= e.Attack * 0.1f * DeltaTime;
                    if (target.HP <= 0f) { target.HP = 0f; target.State = AbstractEntityData.StateDead; }
                    Entities[bestIdx] = target;
                }
                else { e.State = AbstractEntityData.StateWander; e.TargetId = -1; }
                Entities[i] = e;
            }
        }
    }
}
