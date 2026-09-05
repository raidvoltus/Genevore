using UnityEngine;
using Genevore.Core;
using Genevore.Combat;
using Genevore.Player;

namespace Genevore.Systems
{
    /// <summary>
    /// Metabolic Decay balancer for "limitless evolution".
    /// Biomass grows with equipped modules / scale; energy (HP) drains over time
    /// with a slow exponential curve. Forces continuous hunting without
    /// instantly killing the player at high biomass.
    /// Also scales move speed inversely with biomass.
    /// </summary>
    public class BiomassMetabolism : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GenomeManager genome;
        [SerializeField] private CreatureAssembly assembly;
        [SerializeField] private DamageableEntity damageable;
        [SerializeField] private MobilePlayerController playerController;
        [SerializeField] private ProceduralScaleAdapter scaleAdapter;

        [Header("Biomass Model")]
        [SerializeField] private float baseBiomass = 1f;
        [SerializeField] private float biomassPerModule = 0.85f;
        [SerializeField] private float biomassPerScaleUnit = 0.6f;

        [Header("Decay Curve")]
        [SerializeField] private float baseDrainPerSecond = 0.35f;
        [SerializeField] private float decayExponent = 1.15f;
        [SerializeField] private float maxDrainMultiplier = 12f;
        [SerializeField] private float minBiomassClamp = 0.5f;

        [Header("Movement Penalty")]
        [SerializeField] private float baseMoveSpeed = 5f;
        [SerializeField] private float minMoveSpeed = 1.8f;
        [SerializeField] private float speedPenaltyK = 0.22f;

        [Header("Survival Target")]
        [SerializeField] private float apexIdleSurvivalSeconds = 180f;

        private float _currentBiomass = 1f;
        private float _drainPerSecond;
        private float _cachedMoveSpeed;

        public float CurrentBiomass => _currentBiomass;
        public float DrainPerSecond => _drainPerSecond;
        public float CurrentMoveSpeed => _cachedMoveSpeed;

        public float EstimatedIdleSurvivalSeconds
        {
            get
            {
                if (damageable == null || _drainPerSecond <= 0.0001f) return float.MaxValue;
                return damageable.CurrentHP / _drainPerSecond;
            }
        }

        private void Awake()
        {
            if (genome == null) genome = GetComponent<GenomeManager>();
            if (assembly == null) assembly = GetComponent<CreatureAssembly>();
            if (damageable == null) damageable = GetComponent<DamageableEntity>();
            if (playerController == null) playerController = GetComponent<MobilePlayerController>();
            if (scaleAdapter == null) scaleAdapter = GetComponent<ProceduralScaleAdapter>();

            if (genome != null)
            {
                genome.OnStatsRecalculated += RecalculateBiomass;
                genome.OnGeneEquipped += (_, __) => RecalculateBiomass();
                genome.OnGeneRemoved += _ => RecalculateBiomass();
            }
        }

        private void OnDestroy()
        {
            if (genome != null)
            {
                genome.OnStatsRecalculated -= RecalculateBiomass;
            }
        }

        private void Start()
        {
            RecalculateBiomass();
        }

        private void Update()
        {
            if (damageable == null || !damageable.IsAlive) return;

            if (_drainPerSecond > 0f)
            {
                damageable.TakeDamage(_drainPerSecond * Time.deltaTime, -1);
            }
        }

        public void RecalculateBiomass()
        {
            int modules = genome != null ? genome.GeneCount : 0;
            float scaleFactor = 1f;
            if (scaleAdapter != null)
            {
                scaleFactor = scaleAdapter.CurrentUniformScale;
            }
            else
            {
                scaleFactor = transform.localScale.x;
            }

            _currentBiomass = baseBiomass
                              + modules * biomassPerModule
                              + Mathf.Max(0f, scaleFactor - 1f) * biomassPerScaleUnit;

            _currentBiomass = Mathf.Max(minBiomassClamp, _currentBiomass);

            _drainPerSecond = EvaluateDrain(_currentBiomass);
            _cachedMoveSpeed = EvaluateMoveSpeed(_currentBiomass);

            ApplyMoveSpeedToController(_cachedMoveSpeed);
        }

        /// <summary>
        /// Non-linear drain curve.
        ///
        /// Formula:
        ///   b = max(minBiomassClamp, biomass)
        ///   excess = max(0, b - 1)
        ///   arg = min(decayExponent * excess * 0.15, 8)
        ///   mult = min(exp(arg), maxDrainMultiplier)
        ///   drain = max(0, baseDrainPerSecond * mult)
        ///
        /// Safety: biomass clamped &gt; 0; exp arg capped; drain &ge; 0; no div-by-zero.
        /// </summary>
        public float EvaluateDrain(float biomass)
        {
            float b = Mathf.Max(minBiomassClamp, biomass);
            float excess = Mathf.Max(0f, b - 1f);

            float exponentArg = decayExponent * excess * 0.15f;
            exponentArg = Mathf.Min(exponentArg, 8f);

            float mult = Mathf.Exp(exponentArg);
            mult = Mathf.Min(mult, maxDrainMultiplier);

            float drain = baseDrainPerSecond * mult;
            return Mathf.Max(0f, drain);
        }

        /// <summary>
        /// speed = max(minMoveSpeed, baseMoveSpeed / (1 + k * excess))
        /// Denominator always &ge; 1.
        /// </summary>
        public float EvaluateMoveSpeed(float biomass)
        {
            float b = Mathf.Max(minBiomassClamp, biomass);
            float excess = Mathf.Max(0f, b - 1f);
            float denom = 1f + speedPenaltyK * excess;
            float speed = baseMoveSpeed / denom;
            return Mathf.Max(minMoveSpeed, speed);
        }

        private void ApplyMoveSpeedToController(float speed)
        {
            if (playerController is IMetabolismSpeedReceiver receiver)
            {
                receiver.SetMetabolismMoveSpeed(speed);
            }
        }

        public bool ValidateApexSurvival(float apexBiomass, float fullHP)
        {
            float drain = EvaluateDrain(apexBiomass);
            if (drain <= 0f) return false;
            float seconds = fullHP / drain;
            return seconds <= apexIdleSurvivalSeconds + 0.5f;
        }
    }

    public interface IMetabolismSpeedReceiver
    {
        void SetMetabolismMoveSpeed(float speed);
    }
}
