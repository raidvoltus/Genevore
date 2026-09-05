using UnityEngine;
using Unity.Mathematics;
using Genevore.AI;

namespace Genevore.AI
{
    /// <summary>
    /// One-shot or interval seeder that fills the AbstractAISimulator with
    /// background entities so the open world feels populated without physical cost.
    /// </summary>
    public class AbstractPopulationSeeder : MonoBehaviour
    {
        [SerializeField] private AbstractAISimulator simulator;
        [SerializeField] private int count = 80;
        [SerializeField] private float areaRadius = 200f;
        [SerializeField] private float minHP = 30f;
        [SerializeField] private float maxHP = 80f;
        [SerializeField] private float minAttack = 5f;
        [SerializeField] private float maxAttack = 15f;
        [SerializeField] private int prefabHash;
        [SerializeField] private bool seedOnStart = true;

        private void Start()
        {
            if (seedOnStart) Seed();
        }

        [ContextMenu("Seed Population")]
        public void Seed()
        {
            if (simulator == null) simulator = FindObjectOfType<AbstractAISimulator>();
            if (simulator == null) return;

            Vector3 origin = transform.position;
            for (int i = 0; i < count; i++)
            {
                Vector2 circle = Random.insideUnitCircle * areaRadius;
                float3 pos = new float3(origin.x + circle.x, 0f, origin.z + circle.y);
                float hp = Random.Range(minHP, maxHP);
                float atk = Random.Range(minAttack, maxAttack);
                simulator.RegisterAbstractEntity(pos, hp, atk, prefabHash);
            }

            Debug.Log($"[AbstractPopulationSeeder] Registered {count} abstract entities.");
        }
    }
}
