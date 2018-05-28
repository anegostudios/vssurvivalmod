using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class RecipeLoader : ModSystem
    {
        ICoreServerAPI api;

        public override bool AllowRuntimeReload()
        {
            return false;
        }

        public override double ExecuteOrder()
        {
            return 1;
        }

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }


        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;

            api.Event.SaveGameLoaded(OnSaveGameLoaded);
        }

        private void OnSaveGameLoaded()
        {
            LoadAlloyRecipes();
            LoadGridRecipes();
            LoadRecipes<SmithingRecipe>("smithing recipe", "recipes/smithing", (r) => api.RegisterSmithingRecipe(r));
            LoadRecipes<ClayFormingRecipe>("clay forming recipe", "recipes/clayforming", (r) => api.RegisterClayFormingRecipe(r));
            LoadRecipes<KnappingRecipe>("knapping recipe", "recipes/knapping", (r) => api.RegisterKnappingRecipe(r));
        }


        private void LoadAlloyRecipes()
        {
            Dictionary<AssetLocation, AlloyRecipe> alloys = api.Assets.GetMany<AlloyRecipe>(api.Server.Logger, "recipes/alloy");

            foreach (var val in alloys)
            {
                if (!val.Value.Enabled) continue;

                val.Value.Resolve(api.World, "alloy recipe " + val.Key);
                api.RegisterMetalAlloy(val.Value);
            }

            api.World.Logger.Event("{0} metal alloys loaded", alloys.Count);
        }

        


        private void LoadRecipes<T>(string name, string path, System.Action<T> RegisterMethod) where T : RecipeBase<T>
        {
            Dictionary<AssetLocation, T> recipeAssets = api.Assets.GetMany<T>(api.Server.Logger, path);
            int quantityRegistered = 0;
            int quantityIgnored = 0;

            foreach (var val in recipeAssets)
            {
                T recipe = val.Value;
                if (!recipe.Enabled) continue;

                if (recipe.Name == null) recipe.Name = val.Key;

                Dictionary<string, string[]> nameToCodeMapping = recipe.GetNameToCodeMapping(api.World);

                if (nameToCodeMapping.Count > 0)
                {
                    List<T> subRecipes = new List<T>();
                    bool first = true;
                    foreach (var val2 in nameToCodeMapping)
                    {
                        for (int i = 0; i < val2.Value.Length; i++)
                        {
                            T rec;

                            if (first) subRecipes.Add(rec = recipe.Clone());
                            else rec = subRecipes[i];

                            if (rec.Ingredient.Name == val2.Key)
                            {
                                rec.Ingredient.Code = rec.Ingredient.Code.CopyWithPath(rec.Ingredient.Code.Path.Replace("*", val2.Value[i]));
                            }

                            rec.Output.FillPlaceHolder(val2.Key, val2.Value[i]);
                            //rec.Output.Code = rec.Output.Code.CopyWithPath(rec.Output.Code.Path.Replace("{" + val2.Key + "}", val2.Value[i]));
                        }

                        first = false;
                    }

                    foreach (T subRecipe in subRecipes)
                    {
                        if (!subRecipe.Resolve(api.World, name + " " + val.Key))
                        {
                            quantityIgnored++;
                            continue;
                        }
                        RegisterMethod(subRecipe);
                        quantityRegistered++;
                    }

                }
                else
                {
                    if (!recipe.Resolve(api.World, name + " " + val.Key))
                    {
                        quantityIgnored++;
                        continue;
                    }
                    RegisterMethod(recipe);
                    quantityRegistered++;
                }
            }

            
            
            api.World.Logger.Event("{0} {1}s loaded{2}", quantityRegistered, name, quantityIgnored > 0 ? string.Format(" ({0} could not be resolvled)", quantityIgnored) : "");
        }

        



        private void LoadGridRecipes()
        {
            Dictionary<AssetLocation, GridRecipe> recipes = api.Assets.GetMany<GridRecipe>(api.Server.Logger, "recipes/grid");

            foreach (var val in recipes)
            {
                GridRecipe recipe = val.Value;
                if (!recipe.Enabled) continue;
                if (recipe.Name == null) recipe.Name = val.Key;
                
                Dictionary<string, string[]> nameToCodeMapping = recipe.GetNameToCodeMapping(api.World);

                if (nameToCodeMapping.Count > 0)
                {
                    List<GridRecipe> subRecipes = new List<GridRecipe>();
                    bool first = true;
                    foreach (var val2 in nameToCodeMapping)
                    {
                        for (int i = 0; i < val2.Value.Length; i++)
                        {
                            GridRecipe rec;

                            if (first) subRecipes.Add(rec = recipe.Clone());
                            else rec = subRecipes[i];

                            foreach (CraftingRecipeIngredient ingred in rec.Ingredients.Values)
                            {
                                if (ingred.Name == val2.Key)
                                {
                                    ingred.Code.Path = ingred.Code.Path.Replace("*", val2.Value[i]);
                                }
                            }

                            rec.Output.FillPlaceHolder(val2.Key, val2.Value[i]);
                        }

                        first = false;
                    }

                    foreach (GridRecipe subRecipe in subRecipes)
                    {
                        if (!subRecipe.ResolveIngredients(api.World)) continue;
                        api.RegisterCraftingRecipe(subRecipe);
                    }

                }
                else
                {
                    if (!recipe.ResolveIngredients(api.World)) continue;
                    api.RegisterCraftingRecipe(recipe);
                }
            }

            api.World.Logger.Event("{0} crafting recipes loaded", recipes.Count);
        }
    }
}
