namespace SOTOR.AbilitySystem.Rivals
{

    public struct SotorGenotype
    {
        public bool A;
        public bool B;

        public SotorGenotype(bool a, bool b)
        {
            A = a;
            B = b;
        }

        public bool IsCaster => A && B;

        public bool IsCarrier => A != B;

        public override string ToString() => (A ? "M" : "m") + (B ? "M" : "m");
    }
}
