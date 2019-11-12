using System.Linq;
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

            int recipeId = inSlot.Itemstack.Attributes.GetInt("selectedRecipeId");
            SmithingRecipe recipe = api.World.SmithingRecipes.FirstOrDefault(r => r.RecipeId == recipeId);

            if (recipe == null)
            {
                dsc.AppendLine("Unknown work item");
                return;
            }

            dsc.AppendLine(Lang.Get("Unfinished {0}", recipe.Output.ResolvedItemstack.GetName()));
        }
    }
}
