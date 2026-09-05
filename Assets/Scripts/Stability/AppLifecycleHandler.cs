using UnityEngine;
using Genevore.Core;
using Genevore.Systems;
using Genevore.Player;
using Genevore.Combat;

namespace Genevore.Stability
{
    public class AppLifecycleHandler : MonoBehaviour
    {
        [System.Serializable]
        public struct PlayerSnapshot
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float CurrentHP;
            public float MaxHP;
            public float Biomass;
            public int GeneCount;
            public int[] GeneIds;
            public bool Valid;
        }

        [SerializeField] private Transform playerTransform;
        [SerializeField] private GenomeManager genome;
        [SerializeField] private DamageableEntity damageable;
        [SerializeField] private BiomassMetabolism metabolism;
        [SerializeField] private DevourController devour;
        [SerializeField] private MobilePlayerController playerController;
        [SerializeField] private AudioSource[] criticalAudioSources;
        [SerializeField] private bool muteAllAudioOnPause = true;

        private PlayerSnapshot _buffer;
        private bool _paused;
        private float[] _audioVolumes;

        public bool IsPaused => _paused;
        public PlayerSnapshot LastSnapshot => _buffer;

        private void Awake() { AutoWire(); CacheAudioVolumes(); }

        private void AutoWire()
        {
            if (playerTransform == null)
            {
                var pc = FindObjectOfType<MobilePlayerController>();
                if (pc != null) playerTransform = pc.transform;
            }
            if (genome == null) genome = FindObjectOfType<GenomeManager>();
            if (damageable == null && playerTransform != null) damageable = playerTransform.GetComponent<DamageableEntity>();
            if (metabolism == null && playerTransform != null) metabolism = playerTransform.GetComponent<BiomassMetabolism>();
            if (devour == null && playerTransform != null) devour = playerTransform.GetComponent<DevourController>();
            if (playerController == null && playerTransform != null) playerController = playerTransform.GetComponent<MobilePlayerController>();
        }

        private void CacheAudioVolumes()
        {
            if (criticalAudioSources == null) return;
            _audioVolumes = new float[criticalAudioSources.Length];
            for (int i = 0; i < criticalAudioSources.Length; i++)
                _audioVolumes[i] = criticalAudioSources[i] != null ? criticalAudioSources[i].volume : 0f;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) HandlePause(); else HandleResume();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && !_paused) HandlePause();
            else if (hasFocus && _paused) HandleResume();
        }

        private void HandlePause()
        {
            if (_paused) return;
            _paused = true;
            DumpState();
            HaltGameplay();
            MuteAudio();
            Debug.Log("[AppLifecycle] PAUSE — state dumped, audio muted, devour halted.");
        }

        private void HandleResume()
        {
            if (!_paused) return;
            _paused = false;
            RestoreState();
            ResumeGameplay();
            RestoreAudio();
            Debug.Log("[AppLifecycle] RESUME — state restored.");
        }

        public void DumpState()
        {
            _buffer = new PlayerSnapshot
            {
                Valid = true,
                Position = playerTransform != null ? playerTransform.position : Vector3.zero,
                Rotation = playerTransform != null ? playerTransform.rotation : Quaternion.identity,
                CurrentHP = damageable != null ? damageable.CurrentHP : 0f,
                MaxHP = damageable != null ? damageable.MaxHP : 0f,
                Biomass = metabolism != null ? metabolism.CurrentBiomass : 1f,
                GeneCount = genome != null ? genome.GeneCount : 0,
                GeneIds = new int[GenomeManager.MaxGeneSlots]
            };
            if (genome != null)
                for (int i = 0; i < GenomeManager.MaxGeneSlots; i++)
                {
                    var g = genome.GetGeneAt(i);
                    _buffer.GeneIds[i] = g != null ? g.GeneId : 0;
                }
        }

        private void HaltGameplay()
        {
            if (devour != null) devour.ResetState();
            if (playerController != null) playerController.SetJoystickInput(Vector2.zero);
            Time.timeScale = 0f;
        }

        private void ResumeGameplay() { Time.timeScale = 1f; }

        private void MuteAudio()
        {
            if (muteAllAudioOnPause) AudioListener.pause = true;
            if (criticalAudioSources == null) return;
            for (int i = 0; i < criticalAudioSources.Length; i++)
                if (criticalAudioSources[i] != null) criticalAudioSources[i].volume = 0f;
        }

        private void RestoreAudio()
        {
            AudioListener.pause = false;
            if (criticalAudioSources == null || _audioVolumes == null) return;
            for (int i = 0; i < criticalAudioSources.Length; i++)
                if (criticalAudioSources[i] != null && i < _audioVolumes.Length)
                    criticalAudioSources[i].volume = _audioVolumes[i];
        }

        public void RestoreState()
        {
            if (!_buffer.Valid) return;
            if (playerTransform != null)
            {
                var cc = playerTransform.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                playerTransform.SetPositionAndRotation(_buffer.Position, _buffer.Rotation);
                if (cc != null) cc.enabled = true;
            }
            if (damageable != null && _buffer.MaxHP > 0f)
            {
                damageable.ResetFullHealth();
                float missing = damageable.MaxHP - _buffer.CurrentHP;
                if (missing > 0f) damageable.TakeDamage(missing, -1);
            }
        }
    }
}
