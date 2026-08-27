namespace SOTOR.AbilitySystem.Rivals
{

    public static class SotorRumourLogic
    {

        public const int RumourCost = 100;

        public const string DirectionHere = "here";
        public const string DirectionUnknown = "unknown";

        public static string DirectionKeySuffix(float ax, float ay, float bx, float by)
        {
            float dx = bx - ax;
            float dy = by - ay;

            if (dx * dx + dy * dy < 1f) return DirectionHere;

            float adx = dx < 0f ? -dx : dx;
            float ady = dy < 0f ? -dy : dy;

            const float Cardinal = 2.414f;

            if (ady > adx * Cardinal) return dy > 0f ? "north" : "south";
            if (adx > ady * Cardinal) return dx > 0f ? "east" : "west";
            if (dy > 0f) return dx > 0f ? "northeast" : "northwest";
            return dx > 0f ? "southeast" : "southwest";
        }

        public static bool TraditionIsGossipable(Trad t)
        {
            if (t == Trad.None) return false;
            return !SotorTraditions.IsMemberOnly(t);
        }
    }
}
