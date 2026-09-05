using System;

namespace Genevore.Data
{
    /// <summary>
    /// Value-type stat container. Zero heap allocation when used as local or field.
    /// </summary>
    [Serializable]
    public struct StatBlock
    {
        public float HP;
        public float Attack;
        public float Defense;
        public float Speed;
        public float Mass;

        public static StatBlock Zero => new StatBlock { HP = 0f, Attack = 0f, Defense = 0f, Speed = 0f, Mass = 0f };

        public static StatBlock operator +(StatBlock a, StatBlock b)
        {
            return new StatBlock
            {
                HP = a.HP + b.HP,
                Attack = a.Attack + b.Attack,
                Defense = a.Defense + b.Defense,
                Speed = a.Speed + b.Speed,
                Mass = a.Mass + b.Mass
            };
        }

        public void Add(in StatBlock other)
        {
            HP += other.HP;
            Attack += other.Attack;
            Defense += other.Defense;
            Speed += other.Speed;
            Mass += other.Mass;
        }

        public void Reset()
        {
            HP = 0f;
            Attack = 0f;
            Defense = 0f;
            Speed = 0f;
            Mass = 0f;
        }
    }
}
