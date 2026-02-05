using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Vintagestory.ServerMods;

public sealed class RecipeLoader : ModSystem
{
    public override double ExecuteOrder() => 1;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void AssetsLoaded(ICoreAPI api)
    {
        if (api is not ICoreServerAPI serverApi)
        {
            return;
        }

        bool classExclusiveRecipes = serverApi.World.Config.GetBool("classExclusiveRecipes", true);

        LoadAlloyRecipes(serverApi);

        LoadRecipes<GridRecipe>(serverApi, "grid", "recipes/grid", classExclusiveRecipes, (r) => serverApi.RegisterCraftingRecipe(r as GridRecipe));
        api.Logger.StoryEvent(Lang.Get("Grand inventions..."));

        LoadRecipes<SmithingRecipe>(serverApi, "smithing", "recipes/smithing", classExclusiveRecipes, (r) => serverApi.RegisterSmithingRecipe(r as SmithingRecipe));
        serverApi.World.Logger.StoryEvent(Lang.Get("Burning sparks..."));

        LoadRecipes<ClayFormingRecipe>(serverApi, "clay forming", "recipes/clayforming", classExclusiveRecipes, (r) => serverApi.RegisterClayFormingRecipe(r as ClayFormingRecipe));
        serverApi.World.Logger.StoryEvent(Lang.Get("Molded forms..."));

        LoadRecipes<KnappingRecipe>(serverApi, "knapping", "recipes/knapping", classExclusiveRecipes, (r) => serverApi.RegisterKnappingRecipe(r as KnappingRecipe));
        serverApi.World.Logger.StoryEvent(Lang.Get("Simple tools..."));

        LoadRecipes<BarrelRecipe>(serverApi, "barrel recipe", "recipes/barrel", classExclusiveRecipes, (r) => serverApi.RegisterBarrelRecipe(r as BarrelRecipe));
    }



    private static void LoadAlloyRecipes(ICoreServerAPI api)
    {
        Dictionary<AssetLocation, AlloyRecipe> alloys = api.Assets.GetMany<AlloyRecipe>(api.Server.Logger, "recipes/alloy");

        foreach ((AssetLocation assetPath, AlloyRecipe recipe) in alloys)
        {
            if (!recipe.Enabled) continue;

            recipe.Resolve(api.World, "alloy recipe " + assetPath);
            api.RegisterMetalAlloy(recipe);
        }

        api.World.Logger.Event("{0} metal alloys loaded", alloys.Count);
        api.World.Logger.StoryEvent(Lang.Get("Glimmers in the soil..."));
    }

    private static void LoadRecipes<TRecipe>(ICoreServerAPI api, string name, string path, bool classExclusiveRecipes, Action<IRecipeBase> registerDelegate) where TRecipe : IRecipeBase
    {
        Dictionary<AssetLocation, JToken> files = api.Assets.GetMany<JToken>(api.Logger, path);
        int recipeQuantity = 0;

        int recipesLoaded = 0;
        int failedResolveCount = 0;

        foreach ((AssetLocation location, JToken content) in files)
        {
            if (content is JObject recipeObject)
            {
                TRecipe? parsedContent = recipeObject.ToObject<TRecipe>(location.Domain);
                if (parsedContent == null)
                {
                    api.Logger.Error($"Failed to parse {name} recipe: {location}");
                    continue;
                }

                LoadRecipe(api, location, parsedContent, classExclusiveRecipes, registerDelegate, loaded: ref recipesLoaded, failedResolveCount: ref failedResolveCount);
                recipeQuantity++;
            }
            else if (content is JArray arrayOfRecipes)
            {
                foreach (JToken token in arrayOfRecipes)
                {
                    TRecipe? parsedContent = token.ToObject<TRecipe>(location.Domain);
                    if (parsedContent == null)
                    {
                        api.Logger.Error($"Failed to parse {name} recipe: {location}");
                        continue;
                    }

                    LoadRecipe(api, location, parsedContent, classExclusiveRecipes, registerDelegate, loaded: ref recipesLoaded, failedResolveCount: ref failedResolveCount);
                    recipeQuantity++;
                }
            }
        }

        if (failedResolveCount > 0)
        {
            api.Logger.Event($"{recipeQuantity} {name} recipes loaded from {files.Count} files, failed to resolve {failedResolveCount} recipes");
        } else
        {
            api.Logger.Event($"{recipeQuantity} {name} recipes loaded from {files.Count} files");
        }
            

        RecipeBase.CollectiblePreSearchResultsCache.Clear();
    }

    private static void LoadRecipe(ICoreServerAPI api, AssetLocation assetLocation, IRecipeBase recipe, bool classExclusiveRecipes, Action<IRecipeBase> registerDelegate, ref int loaded, ref int failedResolveCount)
    {
        if (!recipe.Enabled) return;

        if (!classExclusiveRecipes)
        {
            recipe.RequiresTrait = null;
        }

        if (recipe.Name == null)
        {
            recipe.Name = assetLocation;
        }

        recipe.OnParsed(api.World);

        IEnumerable<IRecipeBase> recipes = recipe.GenerateRecipesForAllIngredientCombinations(api.World);

        foreach (IRecipeBase subRecipe in recipes)
        {
            if (subRecipe.Resolve(api.World, "RecipeLoader"))
            {
                registerDelegate.Invoke(subRecipe);
                loaded++;
            }
            else
            {
                failedResolveCount++;
            }
        }
    }
}

