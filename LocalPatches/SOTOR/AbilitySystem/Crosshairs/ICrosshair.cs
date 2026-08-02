namespace SOTOR.AbilitySystem.Crosshairs
{

    public interface ICrosshair
    {
        bool IsVisible { get; }

        void Show();

        void Hide();

        void Tick();
    }
}
