using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using Genevore.Stability;
using Genevore.Core;
using Genevore.Systems;
using Genevore.Combat;
using Genevore.Player;

namespace Genevore.Cloud
{
    public class CloudSaveSync : MonoBehaviour
    {
        public const byte FormatVersion = 1;
        public const int MaxGeneSlots = 6;

        public struct CloudPayload
        {
            public byte Version;
            public long TimestampUtcTicks;
            public int WorldSeed;
            public float PosX, PosY, PosZ;
            public float Biomass, CurrentHP, MaxHP;
            public byte GeneCount;
            public int GeneId0, GeneId1, GeneId2, GeneId3, GeneId4, GeneId5;
        }

        [SerializeField] private AppLifecycleHandler lifecycle;
        [SerializeField] private GenomeManager genome;
        [SerializeField] private BiomassMetabolism metabolism;
        [SerializeField] private DamageableEntity damageable;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private int worldSeed;
        [SerializeField] private string localFileName = "genevore_cloud.bin";

        private byte[] _lastUploadBytes;
        private CloudPayload _lastPayload;
        private bool _loginSuccess;
        private float _loginStartTime = -1f;

        public bool IsLoggedIn => _loginSuccess;
        public float LastLoginDurationSeconds { get; private set; } = -1f;
        public CloudPayload LastPayload => _lastPayload;
        public byte[] LastUploadBytes => _lastUploadBytes;

        public event Action OnLoginSuccess;
        public event Action<string> OnLoginFailed;
        public event Action OnCloudSaveCompleted;
        public event Action OnCloudLoadCompleted;

        private void Awake() => AutoWire();

        private void AutoWire()
        {
            if (lifecycle == null) lifecycle = FindObjectOfType<AppLifecycleHandler>();
            if (genome == null) genome = FindObjectOfType<GenomeManager>();
            if (metabolism == null) metabolism = FindObjectOfType<BiomassMetabolism>();
            if (playerTransform == null)
            {
                var pc = FindObjectOfType<MobilePlayerController>();
                if (pc != null) playerTransform = pc.transform;
            }
            if (damageable == null && playerTransform != null)
                damageable = playerTransform.GetComponent<DamageableEntity>();
        }

        public void BeginLoginTimer() { _loginStartTime = Time.realtimeSinceStartup; _loginSuccess = false; }

        public void NotifyLoginSuccess()
        {
            if (_loginStartTime > 0f)
                LastLoginDurationSeconds = Time.realtimeSinceStartup - _loginStartTime;
            _loginSuccess = true;
            OnLoginSuccess?.Invoke();
            Debug.Log($"[CloudSave] OnLoginSuccess in {LastLoginDurationSeconds:F2}s");
        }

        public void NotifyLoginFailed(string reason)
        {
            _loginSuccess = false;
            OnLoginFailed?.Invoke(reason);
        }

        public CloudPayload BuildPayloadFromLiveState()
        {
            var p = new CloudPayload
            {
                Version = FormatVersion,
                TimestampUtcTicks = DateTime.UtcNow.Ticks,
                WorldSeed = worldSeed,
                Biomass = metabolism != null ? metabolism.CurrentBiomass : 1f,
                CurrentHP = damageable != null ? damageable.CurrentHP : 0f,
                MaxHP = damageable != null ? damageable.MaxHP : 0f
            };
            if (playerTransform != null)
            {
                var pos = playerTransform.position;
                p.PosX = pos.x; p.PosY = pos.y; p.PosZ = pos.z;
            }
            if (genome != null)
            {
                int count = Mathf.Min(genome.GeneCount, MaxGeneSlots);
                p.GeneCount = (byte)count;
                for (int i = 0; i < MaxGeneSlots; i++)
                {
                    int id = 0;
                    if (i < count) { var g = genome.GetGeneAt(i); if (g != null) id = g.GeneId; }
                    SetGeneId(ref p, i, id);
                }
            }
            if (lifecycle != null && lifecycle.LastSnapshot.Valid && lifecycle.LastSnapshot.GeneIds != null)
            {
                var s = lifecycle.LastSnapshot;
                p.PosX = s.Position.x; p.PosY = s.Position.y; p.PosZ = s.Position.z;
                p.CurrentHP = s.CurrentHP; p.MaxHP = s.MaxHP; p.Biomass = s.Biomass;
                p.GeneCount = (byte)Mathf.Min(s.GeneCount, MaxGeneSlots);
                for (int i = 0; i < MaxGeneSlots; i++)
                    SetGeneId(ref p, i, i < s.GeneIds.Length ? s.GeneIds[i] : 0);
            }
            return p;
        }

        private static void SetGeneId(ref CloudPayload p, int index, int id)
        {
            switch (index)
            {
                case 0: p.GeneId0 = id; break; case 1: p.GeneId1 = id; break;
                case 2: p.GeneId2 = id; break; case 3: p.GeneId3 = id; break;
                case 4: p.GeneId4 = id; break; case 5: p.GeneId5 = id; break;
            }
        }

        public static byte[] Encode(in CloudPayload payload)
        {
            using (var ms = new MemoryStream(64))
            using (var gzip = new GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal, true))
            using (var bw = new BinaryWriter(gzip))
            {
                bw.Write(payload.Version); bw.Write(payload.TimestampUtcTicks); bw.Write(payload.WorldSeed);
                bw.Write(payload.PosX); bw.Write(payload.PosY); bw.Write(payload.PosZ);
                bw.Write(payload.Biomass); bw.Write(payload.CurrentHP); bw.Write(payload.MaxHP);
                bw.Write(payload.GeneCount);
                bw.Write(payload.GeneId0); bw.Write(payload.GeneId1); bw.Write(payload.GeneId2);
                bw.Write(payload.GeneId3); bw.Write(payload.GeneId4); bw.Write(payload.GeneId5);
                bw.Flush(); gzip.Close();
                return ms.ToArray();
            }
        }

        public static bool TryDecode(byte[] data, out CloudPayload payload)
        {
            payload = default;
            if (data == null || data.Length < 8) return false;
            try
            {
                using (var ms = new MemoryStream(data))
                using (var gzip = new GZipStream(ms, CompressionMode.Decompress))
                using (var br = new BinaryReader(gzip))
                {
                    payload.Version = br.ReadByte();
                    if (payload.Version != FormatVersion) return false;
                    payload.TimestampUtcTicks = br.ReadInt64();
                    payload.WorldSeed = br.ReadInt32();
                    payload.PosX = br.ReadSingle(); payload.PosY = br.ReadSingle(); payload.PosZ = br.ReadSingle();
                    payload.Biomass = br.ReadSingle(); payload.CurrentHP = br.ReadSingle(); payload.MaxHP = br.ReadSingle();
                    payload.GeneCount = br.ReadByte();
                    payload.GeneId0 = br.ReadInt32(); payload.GeneId1 = br.ReadInt32(); payload.GeneId2 = br.ReadInt32();
                    payload.GeneId3 = br.ReadInt32(); payload.GeneId4 = br.ReadInt32(); payload.GeneId5 = br.ReadInt32();
                    return true;
                }
            }
            catch { return false; }
        }

        public static CloudPayload ResolveConflict(in CloudPayload local, in CloudPayload remote)
            => remote.TimestampUtcTicks >= local.TimestampUtcTicks ? remote : local;

        [ContextMenu("Save Local + Prepare Upload")]
        public void SaveNow()
        {
            AutoWire();
            _lastPayload = BuildPayloadFromLiveState();
            _lastUploadBytes = Encode(_lastPayload);
            try { File.WriteAllBytes(Path.Combine(Application.persistentDataPath, localFileName), _lastUploadBytes); }
            catch (Exception e) { Debug.LogError("[CloudSave] " + e.Message); }
            OnCloudSaveCompleted?.Invoke();
            Debug.Log($"[CloudSave] Encoded {_lastUploadBytes.Length} bytes");
        }

        public void OnRemoteDataReceived(byte[] remoteBytes)
        {
            if (!TryDecode(remoteBytes, out var remote)) return;
            var winner = ResolveConflict(BuildPayloadFromLiveState(), remote);
            ApplyPayload(winner);
            _lastPayload = winner;
            _lastUploadBytes = Encode(winner);
            OnCloudLoadCompleted?.Invoke();
        }

        public void ApplyPayload(in CloudPayload p)
        {
            if (playerTransform != null)
            {
                var cc = playerTransform.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                playerTransform.SetPositionAndRotation(new Vector3(p.PosX, p.PosY, p.PosZ), playerTransform.rotation);
                if (cc != null) cc.enabled = true;
            }
            if (damageable != null && p.MaxHP > 0f)
            {
                damageable.ResetFullHealth();
                float missing = damageable.MaxHP - p.CurrentHP;
                if (missing > 0f) damageable.TakeDamage(missing, -1);
            }
        }

        private void OnApplicationPause(bool pause) { if (pause) SaveNow(); }
    }
}
