using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ItemWorkItem : Item
    {
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            int selectedRecipeNumber = inSlot.Itemstack.Attributes.GetInt("selectedRecipeNumber");

            if (selectedRecipeNumber < 0 || selectedRecipeNumber >= world.SmithingRecipes.Count)
            {
                dsc.AppendLine("Unknown work item");
                return;
            }

            SmithingRecipe smithRecipe = world.SmithingRecipes[selectedRecipeNumber];
            dsc.AppendLine(Lang.Get("Unfinished {0}", smithRecipe.Output.ResolvedItemstack.GetName()));
        }
    }
}
