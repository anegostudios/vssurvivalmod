using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ItemWorkItem : Item
    {
        public override void GetHeldItemInfo(ItemStack stack, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(stack, dsc, world, withDebugInfo);

            int selectedRecipeNumber = stack.Attributes.GetInt("selectedRecipeNumber");

            if (selectedRecipeNumber < 0 || selectedRecipeNumber >= world.SmithingRecipes.Length)
            {
                dsc.AppendLine("Unknown work item");
                return;
            }

            SmithingRecipe smithRecipe = world.SmithingRecipes[selectedRecipeNumber];
            dsc.AppendLine("Unfinished " + smithRecipe.Name);
        }
    }
}
