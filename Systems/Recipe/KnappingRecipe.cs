using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    public class KnappingRecipe : LayeredVoxelRecipe<KnappingRecipe>, IByteSerializable
    {
        public override int QuantityLayers => 1;
        public override string RecipeCategoryCode => "knapping";


        /// <summary>
        /// Creates a deep copy
        /// </summary>
        /// <returns></returns>
        public override KnappingRecipe Clone()
        {
            KnappingRecipe recipe = new KnappingRecipe();

            recipe.Pattern = (string[][])Pattern.Clone();
            recipe.Ingredient = Ingredient.Clone();
            recipe.Output = Output.Clone();
            recipe.Name = Name;
            recipe.RecipeId = RecipeId;

            return recipe;
        }

    }
}
