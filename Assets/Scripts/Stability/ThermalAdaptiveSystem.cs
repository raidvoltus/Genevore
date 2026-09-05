using UnityEngine;
using Genevore.AI;

namespace Genevore.Stability
{
    /// <summary>
    /// Stage 5: monitors rolling frame-time average and degrades quality when
    /// thermal throttling is inferred (sustained FPS &lt; 26). Cap target at 30 FPS.
    /// </summary>
    public class ThermalAdaptiveSystem : MonoBehaviour
    {
        public enum QualityTier { High = 0, Medium = 1, Low = 2, Critical = 3 }

        [SerializeField] private int targetFrameRate = 30;
        [SerializeField] private float sampleWindowSeconds = 10f;
        [SerializeField] private float degradeFpsThreshold = 26f;
        [SerializeField] private float recoverFpsThreshold = 29f;
        [SerializeField] private int consecutiveWindowsToDegrade = 1;
        [SerializeField] private int consecutiveWindowsToRecover = 3;
        [SerializeField] private AbstractAISimulator abstractAI;
        [SerializeField] private float aiRadiusHigh = 50f;
        [SerializeField] private float aiRadiusMedium = 35f;
        [SerializeField] private float aiRadiusLow = 22f;
        [SerializeField] private float aiRadiusCritical = 12f;
        [SerializeField] private float shadowDistanceHigh = 40f;
        [SerializeField] private float shadowDistanceMedium = 25f;
        [SerializeField] private float shadowDistanceLow = 12f;
        [SerializeField] private float shadowDistanceCritical = 0f;

        private float _windowAccum;
        private int _windowFrames;
        private float _lastWindowAvgFps = 30f;
        private int _badWindows;
        private int _goodWindows;
        private QualityTier _tier = QualityTier.High;

        public float LastWindowAvgFps => _lastWindowAvgFps;
        public QualityTier CurrentTier => _tier;
        public float SessionMinFps { get; private set; } = 999f;
        public float SessionMaxFps { get; private set; }
        public int DegradeEvents { get; private set; }

        private void Awake()
        {
            ApplyFrameCap();
            if (abstractAI == null) abstractAI = FindObjectOfType<AbstractAISimulator>();
        }

        private void OnEnable() { ApplyFrameCap(); ApplyTier(_tier, true); }

        private void ApplyFrameCap()
        {
            Application.targetFrameRate = targetFrameRate;
            QualitySettings.vSyncCount = 0;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;
            float fps = 1f / dt;
            if (fps < SessionMinFps) SessionMinFps = fps;
            if (fps > SessionMaxFps) SessionMaxFps = fps;
            _windowAccum += dt;
            _windowFrames++;
            if (_windowAccum >= sampleWindowSeconds)
            {
                float avgFps = _windowFrames / _windowAccum;
                _lastWindowAvgFps = avgFps;
                EvaluateWindow(avgFps);
                _windowAccum = 0f;
                _windowFrames = 0;
            }
        }

        private void EvaluateWindow(float avgFps)
        {
            if (avgFps < degradeFpsThreshold)
            {
                _badWindows++; _goodWindows = 0;
                if (_badWindows >= consecutiveWindowsToDegrade) { _badWindows = 0; Downgrade(); }
            }
            else if (avgFps >= recoverFpsThreshold)
            {
                _goodWindows++; _badWindows = 0;
                if (_goodWindows >= consecutiveWindowsToRecover) { _goodWindows = 0; Upgrade(); }
            }
            else { _badWindows = 0; _goodWindows = 0; }
        }

        private void Downgrade()
        {
            if (_tier >= QualityTier.Critical) return;
            _tier = (QualityTier)((int)_tier + 1);
            DegradeEvents++;
            ApplyTier(_tier, true);
            Debug.LogWarning($"[ThermalAdaptive] DEGRADE → {_tier} (avg FPS {_lastWindowAvgFps:F1})");
        }

        private void Upgrade()
        {
            if (_tier <= QualityTier.High) return;
            _tier = (QualityTier)((int)_tier - 1);
            ApplyTier(_tier, true);
            Debug.Log($"[ThermalAdaptive] RECOVER → {_tier} (avg FPS {_lastWindowAvgFps:F1})");
        }

        private void ApplyTier(QualityTier tier, bool force)
        {
            ApplyFrameCap();
            float shadowDist, aiRadius;
            int qualityLevel;
            switch (tier)
            {
                case QualityTier.High:
                    shadowDist = shadowDistanceHigh; aiRadius = aiRadiusHigh;
                    qualityLevel = Mathf.Min(QualitySettings.names.Length - 1, 2); break;
                case QualityTier.Medium:
                    shadowDist = shadowDistanceMedium; aiRadius = aiRadiusMedium;
                    qualityLevel = Mathf.Min(QualitySettings.names.Length - 1, 1); break;
                case QualityTier.Low:
                    shadowDist = shadowDistanceLow; aiRadius = aiRadiusLow; qualityLevel = 0; break;
                default:
                    shadowDist = shadowDistanceCritical; aiRadius = aiRadiusCritical; qualityLevel = 0; break;
            }
            if (QualitySettings.names != null && QualitySettings.names.Length > 0)
                QualitySettings.SetQualityLevel(Mathf.Clamp(qualityLevel, 0, QualitySettings.names.Length - 1), force);
            QualitySettings.shadowDistance = shadowDist;
            if (tier >= QualityTier.Low) { QualitySettings.particleRaycastBudget = 64; QualitySettings.softParticles = false; }
            else QualitySettings.particleRaycastBudget = 256;
            if (abstractAI != null) abstractAI.SetMaterialiseRadius(aiRadius);
        }

        public void ForceTier(QualityTier tier) { _tier = tier; ApplyTier(tier, true); }

        public void ResetTelemetry()
        {
            SessionMinFps = 999f; SessionMaxFps = 0f; DegradeEvents = 0;
            _badWindows = 0; _goodWindows = 0; _windowAccum = 0f; _windowFrames = 0;
        }
    }
}
