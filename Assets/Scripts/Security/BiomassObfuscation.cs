using UnityEngine;

namespace Genevore.Security
{
    public struct ObfuscatedFloat
    {
        private uint _masked;
        private uint _key;

        public static ObfuscatedFloat From(float value)
        {
            var o = new ObfuscatedFloat();
            o.Set(value);
            return o;
        }

        public void Set(float value)
        {
            if (_key == 0)
                _key = (uint)(UnityEngine.Random.Range(1, int.MaxValue) ^ (int)(Time.realtimeSinceStartup * 1000f));
            uint bits = FloatToUInt(value);
            _masked = bits ^ _key ^ 0xA5A5C3C3u;
        }

        public float Get()
        {
            if (_key == 0) return 0f;
            uint bits = _masked ^ _key ^ 0xA5A5C3C3u;
            return UIntToFloat(bits);
        }

        public void Add(float delta) => Set(Get() + delta);

        private static uint FloatToUInt(float f)
        {
            byte[] b = System.BitConverter.GetBytes(f);
            return System.BitConverter.ToUInt32(b, 0);
        }

        private static float UIntToFloat(uint u)
        {
            byte[] b = System.BitConverter.GetBytes(u);
            return System.BitConverter.ToSingle(b, 0);
        }
    }

    public class SecureBiomassVault : MonoBehaviour
    {
        private ObfuscatedFloat _biomass;
        private ObfuscatedFloat _hp;
        public void WriteBiomass(float v) => _biomass.Set(v);
        public float ReadBiomass() => _biomass.Get();
        public void WriteHP(float v) => _hp.Set(v);
        public float ReadHP() => _hp.Get();
    }
}
