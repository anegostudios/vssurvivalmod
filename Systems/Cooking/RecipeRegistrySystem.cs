using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public static class ApiAdditions
    {
        public static List<CookingRecipe> GetCookingRecipes(this ICoreAPI api)
        {
            return api.ModLoader.GetModSystem<RecipeRegistrySystem>().CookingRecipes;
        }

        public static List<BarrelRecipe> GetBarrelRecipes(this ICoreAPI api)
        {
            return api.ModLoader.GetModSystem<RecipeRegistrySystem>().BarrelRecipes;
        }

        public static List<AlloyRecipe> GetMetalAlloys(this ICoreAPI api)
        {
            return api.ModLoader.GetModSystem<RecipeRegistrySystem>().MetalAlloys;
        }

        public static List<SmithingRecipe> GetSmithingRecipes(this ICoreAPI api)
        {
            return api.ModLoader.GetModSystem<RecipeRegistrySystem>().SmithingRecipes;
        }

        public static List<KnappingRecipe> GetKnappingRecipes(this ICoreAPI api)
        {
            return api.ModLoader.GetModSystem<RecipeRegistrySystem>().KnappingRecipes;
        }

        public static List<ClayFormingRecipe> GetClayformingRecipes(this ICoreAPI api)
        {
            return api.ModLoader.GetModSystem<RecipeRegistrySystem>().ClayFormingRecipes;
        }

        public static void RegisterCookingRecipe(this ICoreServerAPI api, CookingRecipe r)
        {
            api.ModLoader.GetModSystem<RecipeRegistrySystem>().RegisterCookingRecipe(r);
        }
        public static void RegisterSmithingRecipe(this ICoreServerAPI api, SmithingRecipe r)
        {
            api.ModLoader.GetModSystem<RecipeRegistrySystem>().RegisterSmithingRecipe(r);
        }
        public static void RegisterClayFormingRecipe(this ICoreServerAPI api, ClayFormingRecipe r)
        {
            api.ModLoader.GetModSystem<RecipeRegistrySystem>().RegisterClayFormingRecipe(r);
        }
        public static void RegisterKnappingRecipe(this ICoreServerAPI api, KnappingRecipe r)
        {
            api.ModLoader.GetModSystem<RecipeRegistrySystem>().RegisterKnappingRecipe(r);
        }
        public static void RegisterBarrelRecipe(this ICoreServerAPI api, BarrelRecipe r)
        {
            api.ModLoader.GetModSystem<RecipeRegistrySystem>().RegisterBarrelRecipe(r);
        }
        public static void RegisterMetalAlloy(this ICoreServerAPI api, AlloyRecipe r)
        {
            api.ModLoader.GetModSystem<RecipeRegistrySystem>().RegisterMetalAlloy(r);
        }
    }

    public class RecipeRegistrySystem : ModSystem
    {
        /// <summary>
        /// List of all loaded cooking recipes
        /// </summary>
        public List<CookingRecipe> CookingRecipes = new List<CookingRecipe>();
        /// <summary>
        /// List of all loaded barrel recipes
        /// </summary>
        public List<BarrelRecipe> BarrelRecipes = new List<BarrelRecipe>();
        /// <summary>
        /// List of all loaded metal alloys
        /// </summary>
        public List<AlloyRecipe> MetalAlloys = new List<AlloyRecipe>();
        /// <summary>
        /// List of all loaded smithing recipes
        /// </summary>
        public List<SmithingRecipe> SmithingRecipes = new List<SmithingRecipe>();
        /// <summary>
        /// List of all loaded knapping recipes
        /// </summary>
        public List<KnappingRecipe> KnappingRecipes = new List<KnappingRecipe>();
        /// <summary>
        /// List of all loaded clay forming recipes
        /// </summary>
        public List<ClayFormingRecipe> ClayFormingRecipes = new List<ClayFormingRecipe>();


        public override double ExecuteOrder()
        {
            return 0.6;
        }

        public override void Start(ICoreAPI api)
        {
            CookingRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<CookingRecipe>>("cookingrecipes").Recipes;
            MetalAlloys = api.RegisterRecipeRegistry<RecipeRegistryGeneric<AlloyRecipe>>("alloyrecipes").Recipes;

            SmithingRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<SmithingRecipe>>("smithingrecipes").Recipes;
            KnappingRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<KnappingRecipe>>("knappingrecipes").Recipes;
            ClayFormingRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<ClayFormingRecipe>>("clayformingrecipes").Recipes;
            BarrelRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<BarrelRecipe>>("barrelrecipes").Recipes;
        }


        public override void AssetsLoaded(ICoreAPI api)
        {
            if (!(api is ICoreServerAPI sapi)) return;
            Dictionary<AssetLocation, CookingRecipe> recipes = sapi.Assets.GetMany<CookingRecipe>(sapi.Server.Logger, "recipes/cooking");

            foreach (var val in recipes)
            {
                if (!val.Value.Enabled) continue;

                val.Value.Resolve(sapi.World, "cooking recipe " + val.Key);
                RegisterCookingRecipe(val.Value);
            }

            sapi.World.Logger.Event("{0} cooking recipes loaded", recipes.Count);
            sapi.World.Logger.StoryEvent(Lang.Get("Taste and smell..."));
        }



        /// <summary>
        /// Registers a new cooking recipe. These are sent to the client during connect, so only need to register them on the server side.
        /// </summary>
        /// <param name="recipe"></param>
        public void RegisterCookingRecipe(CookingRecipe recipe)
        {
            CookingRecipes.Add(recipe);
        }

        /// <summary>
        /// Registers a new barrel mixing recipe. These are sent to the client during connect, so only need to register them on the server side.
        /// </summary>
        /// <param name="recipe"></param>
        public void RegisterBarrelRecipe(BarrelRecipe recipe)
        {
            if (recipe.Code == null)
            {
                throw new ArgumentException("Barrel recipes must have a non-null code! (choose freely)");
            }

            foreach (var ingred in recipe.Ingredients)
            {
                if (ingred.ConsumeQuantity != null && ingred.ConsumeQuantity > ingred.Quantity)
                {
                    throw new ArgumentException("Barrel recipe with code {0} has an ingredient with ConsumeQuantity > Quantity. Not a valid recipe!");
                }
            }

            BarrelRecipes.Add(recipe);
        }

        /// <summary>
        /// Registers a new metal alloy. These are sent to the client during connect, so only need to register them on the server side.
        /// </summary>
        /// <param name="alloy"></param>
        public void RegisterMetalAlloy(AlloyRecipe alloy)
        {
            MetalAlloys.Add(alloy);
        }

        /// <summary>
        /// Registers a new clay forming recipe. These are sent to the client during connect, so only need to register them on the server side.
        /// </summary>
        /// <param name="recipe"></param>
        public void RegisterClayFormingRecipe(ClayFormingRecipe recipe)
        {
            recipe.RecipeId = ClayFormingRecipes.Count + 1;

            ClayFormingRecipes.Add(recipe);
        }


        /// <summary>
        /// Registers a new metal smithing recipe. These are sent to the client during connect, so only need to register them on the server side.
        /// </summary>
        /// <param name="recipe"></param>
        public void RegisterSmithingRecipe(SmithingRecipe recipe)
        {
            recipe.RecipeId = SmithingRecipes.Count + 1;

            SmithingRecipes.Add(recipe);
        }

        /// <summary>
        /// Registers a new flint knapping recipe. These are sent to the client during connect, so only need to register them on the server side.
        /// </summary>
        /// <param name="recipe"></param>
        public void RegisterKnappingRecipe(KnappingRecipe recipe)
        {
            recipe.RecipeId = KnappingRecipes.Count + 1;

            KnappingRecipes.Add(recipe);
        }





    }
}
