using System.Collections.Generic;

namespace SOTOR.AbilitySystem.Rivals
{

    public static class SotorBattleAllyTally
    {

        private static readonly List<int> _allies = new List<int>();
        private static readonly List<string> _names = new List<string>();

        public static bool Record(Trad trad, string casterName)
        {
            if (trad == Trad.None) return false;
            int key = (int)trad;
            if (_allies.Contains(key)) return false;
            _allies.Add(key);
            _names.Add(casterName ?? string.Empty);
            return true;
        }

        public static bool HasAny => _allies.Count > 0;

        public struct Ally
        {
            public Trad Tradition;
            public string CasterName;
        }

        public static List<Ally> Take()
        {
            var result = new List<Ally>();
            for (int i = 0; i < _allies.Count; i++)
            {
                result.Add(new Ally
                {
                    Tradition = (Trad)_allies[i],
                    CasterName = i < _names.Count ? _names[i] : string.Empty,
                });
            }
            _allies.Clear();
            _names.Clear();
            return result;
        }

        public static bool PlayerWon { get; private set; }

        public static void NoteResult(bool playerVictory)
        {
            PlayerWon = playerVictory;
        }

        public static void Clear() { _allies.Clear(); _names.Clear(); PlayerWon = false; }
    }
}
