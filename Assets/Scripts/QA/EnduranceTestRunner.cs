using UnityEngine;
using Genevore.Core;
using Genevore.Player;
using Genevore.Stability;
using Genevore.Systems;

namespace Genevore.QA
{
    public class EnduranceTestRunner : MonoBehaviour
    {
        public enum Phase { Idle, Running, Finished }

        [SerializeField] private float totalDurationSeconds = 45f * 60f;
        [SerializeField] private float telemetryMinuteA = 5f;
        [SerializeField] private float telemetryMinuteB = 35f;
        [SerializeField] private float moveChangeInterval = 4f;
        [SerializeField] private float devourInterval = 2.5f;
        [SerializeField] private float geneSwapInterval = 8f;
        [SerializeField] private float moveInputMagnitude = 0.85f;
        [SerializeField] private MobilePlayerController player;
        [SerializeField] private DevourController devour;
        [SerializeField] private GenomeManager genome;
        [SerializeField] private ThermalAdaptiveSystem thermal;
        [SerializeField] private AppLifecycleHandler lifecycle;
        [SerializeField] private BiomassMetabolism metabolism;
        [SerializeField] private bool runOnStart = false;

        private Phase _phase = Phase.Idle;
        private float _elapsed, _moveTimer, _devourTimer, _geneTimer;
        private Vector2 _currentInput;
        private float _fpsAtMin5 = -1f, _fpsAtMin35 = -1f;
        private float _minFpsAtMin5 = -1f, _minFpsAtMin35 = -1f;
        private long _memAtMin5, _memAtMin35;
        private bool _captured5, _captured35;
        private ThermalAdaptiveSystem.QualityTier _tierAt5, _tierAt35;
        private int _degradeAt5, _degradeAt35;

        public Phase CurrentPhase => _phase;
        public float ElapsedSeconds => _elapsed;
        public float FpsAtMinute5 => _fpsAtMin5;
        public float FpsAtMinute35 => _fpsAtMin35;
        public float SessionMinFps => thermal != null ? thermal.SessionMinFps : -1f;

        private void Start()
        {
            AutoWire();
            if (runOnStart) Begin();
        }

        private void AutoWire()
        {
            if (player == null) player = FindObjectOfType<MobilePlayerController>();
            if (devour == null) devour = FindObjectOfType<DevourController>();
            if (genome == null) genome = FindObjectOfType<GenomeManager>();
            if (thermal == null) thermal = FindObjectOfType<ThermalAdaptiveSystem>();
            if (lifecycle == null) lifecycle = FindObjectOfType<AppLifecycleHandler>();
            if (metabolism == null) metabolism = FindObjectOfType<BiomassMetabolism>();
        }

        [ContextMenu("Begin Endurance Test")]
        public void Begin()
        {
            AutoWire();
            _phase = Phase.Running;
            _elapsed = _moveTimer = _devourTimer = _geneTimer = 0f;
            _captured5 = _captured35 = false;
            _fpsAtMin5 = _fpsAtMin35 = -1f;
            if (thermal != null) thermal.ResetTelemetry();
            Application.targetFrameRate = 30;
            Debug.Log($"[Endurance] START — duration {totalDurationSeconds / 60f:F0} min, target 30 FPS.");
            PickNewMoveDirection();
        }

        private void Update()
        {
            if (_phase != Phase.Running) return;
            if (lifecycle != null && lifecycle.IsPaused) return;
            float dt = Time.unscaledDeltaTime;
            _elapsed += dt;
            DriveMovement(dt);
            DriveDevour(dt);
            DriveGeneSwap(dt);
            CaptureTelemetry();
            if (_elapsed >= totalDurationSeconds) Finish();
        }

        private void DriveMovement(float dt)
        {
            _moveTimer += dt;
            if (_moveTimer >= moveChangeInterval) { _moveTimer = 0f; PickNewMoveDirection(); }
            if (player != null) player.SetJoystickInput(_currentInput);
        }

        private void PickNewMoveDirection()
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            _currentInput = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * moveInputMagnitude;
        }

        private void DriveDevour(float dt)
        {
            _devourTimer += dt;
            if (_devourTimer < devourInterval) return;
            _devourTimer = 0f;
            if (devour != null) devour.ForceDevourCycle();
            else if (genome != null) genome.ApplyRandomMutation();
        }

        private void DriveGeneSwap(float dt)
        {
            _geneTimer += dt;
            if (_geneTimer < geneSwapInterval) return;
            _geneTimer = 0f;
            if (genome == null) return;
            if (genome.GeneCount > 0) genome.UnequipGeneAt(Random.Range(0, genome.GeneCount));
            genome.ApplyRandomMutation();
        }

        private void CaptureTelemetry()
        {
            float minutes = _elapsed / 60f;
            if (!_captured5 && minutes >= telemetryMinuteA)
            {
                _captured5 = true;
                _fpsAtMin5 = thermal != null ? thermal.LastWindowAvgFps : 1f / Mathf.Max(0.001f, Time.unscaledDeltaTime);
                _minFpsAtMin5 = thermal != null ? thermal.SessionMinFps : _fpsAtMin5;
                _memAtMin5 = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                if (thermal != null) { _tierAt5 = thermal.CurrentTier; _degradeAt5 = thermal.DegradeEvents; }
                Debug.Log($"[Endurance] t={telemetryMinuteA:F0}min avgFPS={_fpsAtMin5:F1} sessionMin={_minFpsAtMin5:F1} mem={_memAtMin5 / (1024f * 1024f):F1}MB tier={_tierAt5}");
            }
            if (!_captured35 && minutes >= telemetryMinuteB)
            {
                _captured35 = true;
                _fpsAtMin35 = thermal != null ? thermal.LastWindowAvgFps : 1f / Mathf.Max(0.001f, Time.unscaledDeltaTime);
                _minFpsAtMin35 = thermal != null ? thermal.SessionMinFps : _fpsAtMin35;
                _memAtMin35 = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                if (thermal != null) { _tierAt35 = thermal.CurrentTier; _degradeAt35 = thermal.DegradeEvents; }
                Debug.Log($"[Endurance] t={telemetryMinuteB:F0}min avgFPS={_fpsAtMin35:F1} sessionMin={_minFpsAtMin35:F1} mem={_memAtMin35 / (1024f * 1024f):F1}MB tier={_tierAt35}");
                LogThermalComparison();
            }
        }

        private void LogThermalComparison()
        {
            Debug.Log("========== THERMAL PROFILE (empirical) ==========");
            Debug.Log($"  Minute {telemetryMinuteA:F0}: avgFPS={_fpsAtMin5:F1} minFPS={_minFpsAtMin5:F1} tier={_tierAt5} memMB={_memAtMin5 / (1024f * 1024f):F1}");
            Debug.Log($"  Minute {telemetryMinuteB:F0}: avgFPS={_fpsAtMin35:F1} minFPS={_minFpsAtMin35:F1} tier={_tierAt35} memMB={_memAtMin35 / (1024f * 1024f):F1}");
            float fpsDelta = _fpsAtMin35 - _fpsAtMin5;
            long memDelta = _memAtMin35 - _memAtMin5;
            Debug.Log($"  \u0394 FPS (35-5): {fpsDelta:F1}");
            Debug.Log($"  \u0394 RAM (35-5): {memDelta / (1024f * 1024f):F2} MB");
            if (memDelta > 15L * 1024L * 1024L)
                Debug.LogWarning("[Endurance] POSSIBLE SLOW LEAK: RAM grew >15 MB between min 5 and 35. Capture Memory Profiler snapshots.");
            if (_minFpsAtMin35 > 0f && _minFpsAtMin35 < 25f)
                Debug.LogError($"[Endurance] FAIL: session min FPS {_minFpsAtMin35:F1} < 25 at/after minute 35.");
            Debug.Log("=================================================");
        }

        private void Finish()
        {
            _phase = Phase.Finished;
            if (player != null) player.SetJoystickInput(Vector2.zero);
            Debug.Log($"[Endurance] FINISHED after {_elapsed / 60f:F1} minutes. Session min FPS={SessionMinFps:F1} degradeEvents={(thermal != null ? thermal.DegradeEvents : 0)}");
            if (_captured5 && _captured35) LogThermalComparison();
        }

        [ContextMenu("Simulate OS Interrupt (dump+resume)")]
        public void SimulateInterruptCycle()
        {
            if (lifecycle == null) return;
            lifecycle.DumpState();
            Debug.Log("[Endurance] Simulated interrupt dump OK. Valid=" + lifecycle.LastSnapshot.Valid);
        }
    }
}
