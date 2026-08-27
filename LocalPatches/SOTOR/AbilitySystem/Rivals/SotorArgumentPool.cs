using System.Collections.Generic;

namespace SOTOR.AbilitySystem.Rivals
{

    public static class SotorArgumentPool
    {

        public const int OfferedCount = 3;

        public enum ArgSkill
        {
            Charm,
            Leadership,
            Spellcraft,
            Trade,
            Roguery,
            Medicine,
            Tactics,
            Steward,
        }

        public struct Argument
        {
            public string StringId;
            public ArgSkill Skill;

            public Argument(string stringId, ArgSkill skill)
            {
                StringId = stringId;
                Skill = skill;
            }
        }

        public static readonly Argument[] All =
        {
            new Argument("sotor_teach_arg_charm",      ArgSkill.Charm),
            new Argument("sotor_teach_arg_leadership", ArgSkill.Leadership),
            new Argument("sotor_teach_arg_spellcraft", ArgSkill.Spellcraft),
            new Argument("sotor_teach_arg_trade",      ArgSkill.Trade),
            new Argument("sotor_teach_arg_roguery",    ArgSkill.Roguery),
            new Argument("sotor_teach_arg_medicine",   ArgSkill.Medicine),
            new Argument("sotor_teach_arg_tactics",    ArgSkill.Tactics),
            new Argument("sotor_teach_arg_steward",    ArgSkill.Steward),
        };

        public static List<int> Draw(float roll01)
        {
            int n = All.Length;
            int take = OfferedCount < n ? OfferedCount : n;

            uint seed = (uint)(roll01 * 4294967295.0);
            if (seed == 0u) seed = 0x9E3779B9u;

            var bag = new List<int>(n);
            for (int i = 0; i < n; i++) bag.Add(i);

            var picked = new List<int>(take);
            for (int k = 0; k < take; k++)
            {

                seed ^= seed << 13;
                seed ^= seed >> 17;
                seed ^= seed << 5;
                int idx = (int)(seed % (uint)bag.Count);
                picked.Add(bag[idx]);
                bag.RemoveAt(idx);
            }

            picked.Sort();
            return picked;
        }

        public const int MaxSkill = 250;
        public const int BaselineOffset = 2;

        public static int SkillStrengthShift(int skillValue)
        {
            if (skillValue < 0) skillValue = 0;
            if (skillValue > MaxSkill) skillValue = MaxSkill;

            float t = (float)skillValue / MaxSkill;
            return (int)System.Math.Round((t * 2f - 1f) * BaselineOffset);
        }

        public static int CritIndex(float roll01)
        {
            uint seed = (uint)(roll01 * 4294967295.0);
            if (seed == 0u) seed = 0x9E3779B9u;

            seed ^= seed << 7;
            seed ^= seed >> 9;
            return (int)(seed % (uint)OfferedCount);
        }

        public static List<Argument> Offered(float roll01)
        {
            var result = new List<Argument>(OfferedCount);
            foreach (int i in Draw(roll01)) result.Add(All[i]);
            return result;
        }
    }
}
