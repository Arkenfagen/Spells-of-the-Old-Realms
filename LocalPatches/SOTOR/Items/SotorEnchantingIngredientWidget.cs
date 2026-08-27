using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.TwoDimension;

namespace SOTOR.Items
{

    public class SotorEnchantingIngredientWidget : RichTextWidget
    {
        public SotorEnchantingIngredientWidget(UIContext context) : base(context)
        {
        }

        protected override void OnRender(TwoDimensionContext twoDimensionContext, TwoDimensionDrawContext drawContext)
        {
            if (int.TryParse(Text, out var value) && value < 0)
            {
                Brush = Context.GetBrush("SotorEnchantingIngredientRed");
            }
            Brush.FontSize = 30;
            base.OnRender(twoDimensionContext, drawContext);
        }
    }
}
