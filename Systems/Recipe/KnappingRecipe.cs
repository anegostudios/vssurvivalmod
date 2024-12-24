using Vintagestory.API;
using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Defines a stone/flint knapping recipe. Uses all properties from <see cref="LayeredVoxelRecipe{T}"/>, however always uses a single layer.
    /// </summary>
    /// <example><code language="json">
    ///{
    ///  "ingredient": {
    ///    "type": "item",
    ///    "code": "flint"
    ///  },
    ///  "pattern": [
    ///    [
    ///      "___###___",
    ///      "__#####__",
    ///      "__#####__",
    ///      "__#####__",
    ///      "__#####__",
    ///      "__#####__"
    ///    ]
    ///  ],
    ///  "name": "Hoe",
    ///  "output": {
    ///    "type": "item",
    ///    "code": "hoehead-flint"
    ///  }
    ///}
    /// </code></example>
    [DocumentAsJson]
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
