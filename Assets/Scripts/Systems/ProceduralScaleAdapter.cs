using UnityEngine;
using Genevore.Core;

namespace Genevore.Systems
{
    /// <summary>
    /// Adjusts CharacterController (height, radius, center) when biomass / visual scale changes.
    /// Primitive colliders only (Capsule via CharacterController). No MeshCollider.
    /// After scale change, corrects Y against ground to prevent clipping through terrain/NavMesh.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ProceduralScaleAdapter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GenomeManager genome;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private CharacterController characterController;

        [Header("Base Capsule (at scale 1)")]
        [SerializeField] private float baseHeight = 2f;
        [SerializeField] private float baseRadius = 0.4f;
        [SerializeField] private float baseCenterY = 1f;

        [Header("Scale Model")]
        [SerializeField] private float scalePerModule = 0.08f;
        [SerializeField] private float minScale = 0.85f;
        [SerializeField] private float maxScale = 3.5f;
        [SerializeField] private float scaleLerpSpeed = 6f;

        [Header("Ground Correction")]
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float groundProbeUp = 2f;
        [SerializeField] private float groundProbeDown = 10f;
        [SerializeField] private float skinWidthMargin = 0.05f;

        private float _targetScale = 1f;
        private float _currentScale = 1f;

        public float CurrentUniformScale => _currentScale;

        private void Awake()
        {
            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (genome == null) genome = GetComponent<GenomeManager>();
            if (visualRoot == null) visualRoot = transform;

            if (genome != null)
            {
                genome.OnGeneEquipped += OnGeneChanged;
                genome.OnGeneRemoved += OnGeneRemoved;
                genome.OnStatsRecalculated += RecalculateTargetScale;
            }
        }

        private void OnDestroy()
        {
            if (genome != null)
            {
                genome.OnGeneEquipped -= OnGeneChanged;
                genome.OnGeneRemoved -= OnGeneRemoved;
                genome.OnStatsRecalculated -= RecalculateTargetScale;
            }
        }

        private void Start()
        {
            RecalculateTargetScale();
            ApplyScaleImmediate(_targetScale);
        }

        private void Update()
        {
            if (Mathf.Abs(_currentScale - _targetScale) > 0.0005f)
            {
                _currentScale = Mathf.Lerp(_currentScale, _targetScale, Time.deltaTime * scaleLerpSpeed);
                ApplyScale(_currentScale);
                CorrectGroundPenetration();
            }
        }

        private void OnGeneChanged(int slot, Genevore.Data.GeneDataSO gene) => RecalculateTargetScale();
        private void OnGeneRemoved(int slot) => RecalculateTargetScale();

        public void RecalculateTargetScale()
        {
            int count = genome != null ? genome.GeneCount : 0;
            float s = 1f + count * scalePerModule;
            _targetScale = Mathf.Clamp(s, minScale, maxScale);
        }

        public void ApplyScaleImmediate(float scale)
        {
            _currentScale = Mathf.Clamp(scale, minScale, maxScale);
            _targetScale = _currentScale;
            ApplyScale(_currentScale);
            CorrectGroundPenetration();
        }

        private void ApplyScale(float scale)
        {
            if (visualRoot != null)
            {
                visualRoot.localScale = new Vector3(scale, scale, scale);
            }

            if (characterController != null)
            {
                bool wasEnabled = characterController.enabled;
                characterController.enabled = false;

                characterController.height = baseHeight * scale;
                characterController.radius = baseRadius * scale;
                characterController.center = new Vector3(0f, baseCenterY * scale, 0f);

                characterController.enabled = wasEnabled;
            }
        }

        private void CorrectGroundPenetration()
        {
            if (characterController == null) return;

            Vector3 origin = transform.position + Vector3.up * groundProbeUp;
            float probeDistance = groundProbeUp + groundProbeDown;

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, probeDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                float feetY = hit.point.y + skinWidthMargin;
                float halfHeight = characterController.height * 0.5f;
                float bottomOffset = characterController.center.y - halfHeight;
                float desiredPosY = feetY - bottomOffset;

                Vector3 pos = transform.position;
                if (pos.y < desiredPosY)
                {
                    pos.y = desiredPosY;
                    transform.position = pos;
                }
            }

            ResolveLateralPenetration();
        }

        private void ResolveLateralPenetration()
        {
            if (characterController == null) return;

            Collider[] buffer = PenetrationBuffer;
            int count = Physics.OverlapSphereNonAlloc(
                transform.position + characterController.center,
                characterController.radius + 0.1f,
                buffer,
                groundMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                var other = buffer[i];
                if (other == null || other.transform == transform) continue;

                if (Physics.ComputePenetration(
                        characterController, transform.position, transform.rotation,
                        other, other.transform.position, other.transform.rotation,
                        out Vector3 direction, out float distance))
                {
                    direction.y = 0f;
                    if (direction.sqrMagnitude > 0.0001f)
                    {
                        transform.position += direction.normalized * distance;
                    }
                }
            }
        }

        private static readonly Collider[] PenetrationBuffer = new Collider[8];

        /// <summary>
        /// Stress helper: apply scale N times in sequence (exit-criteria validation).
        /// </summary>
        public void DebugForceScaleSteps(int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                float t = (i + 1) / (float)steps;
                float s = Mathf.Lerp(minScale, maxScale, t);
                ApplyScaleImmediate(s);
            }
        }
    }
}
