using Vintagestory.API;
using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Defines a smithing recipe, to be created on an anvil. Uses a total of 6 layers, and gets its properties from <see cref="LayeredVoxelRecipe{T}"/>.
    /// </summary>
    /// <example><code language="json">
    ///{
    ///	"ingredient": {
    ///		"type": "item",
    ///		"code": "ingot-*",
    ///		"name": "metal",
    ///		"allowedVariants": [ "tinbronze", "bismuthbronze", "blackbronze", "silver", "gold", "iron", "meteoriciron", "steel" ]
    ///	},
    ///	"name": "boss",
    ///	"pattern": [
    ///		[
    ///			"____",
    ///			"_##_",
    ///			"_##_",
    ///			"____"
    ///		],
    ///		[
    ///			"####",
    ///			"#__#",
    ///			"#__#",
    ///			"####"
    ///		]
    ///	],
    ///	"output": {
    ///		"type": "item",
    ///		"code": "boss-{metal}"
    ///	}
    ///}
    /// </code></example>
    [DocumentAsJson]
    public class SmithingRecipe : LayeredVoxelRecipe<SmithingRecipe>, IByteSerializable
    {
        public override int QuantityLayers => 6;
        public override string RecipeCategoryCode => "smithing";
        protected override bool RotateRecipe => true;

        /// <summary>
        /// Creates a deep copy
        /// </summary>
        /// <returns></returns>
        public override SmithingRecipe Clone()
        {
            SmithingRecipe recipe = new SmithingRecipe();

            recipe.Pattern = (string[][])Pattern.Clone();
            recipe.Ingredient = Ingredient.Clone();
            recipe.Output = Output.Clone();
            recipe.Name = Name;
            recipe.RecipeId = RecipeId;

            return recipe;
        }
    }
}
