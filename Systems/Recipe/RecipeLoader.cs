using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

#nullable disable

namespace Vintagestory.ServerMods
{
    public class RecipeLoader : ModSystem
    {
        ICoreServerAPI api;
        static readonly Regex PlaceholderRegex = new Regex(@"\{([^\{\}]+)\}", RegexOptions.Compiled);

        public override double ExecuteOrder()
        {
            return 1;
        }

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }


        bool classExclusiveRecipes = true;
        public override void AssetsLoaded(ICoreAPI api)
        {
            if (!(api is ICoreServerAPI sapi)) return;
            this.api = sapi;

            classExclusiveRecipes = sapi.World.Config.GetBool("classExclusiveRecipes", true);

            LoadAlloyRecipes();
            
            LoadRecipes<SmithingRecipe>("smithing recipe", "recipes/smithing", (r) => sapi.RegisterSmithingRecipe(r));
            sapi.World.Logger.StoryEvent(Lang.Get("Burning sparks..."));
            LoadRecipes<ClayFormingRecipe>("clay forming recipe", "recipes/clayforming", (r) => sapi.RegisterClayFormingRecipe(r));
            sapi.World.Logger.StoryEvent(Lang.Get("Molded forms..."));
            LoadRecipes<KnappingRecipe>("knapping recipe", "recipes/knapping", (r) => sapi.RegisterKnappingRecipe(r));
            sapi.World.Logger.StoryEvent(Lang.Get("Simple tools..."));

            LoadRecipes<BarrelRecipe>("barrel recipe", "recipes/barrel", (r) => sapi.RegisterBarrelRecipe(r));
        }


        public void LoadAlloyRecipes()
        {
            Dictionary<AssetLocation, AlloyRecipe> alloys = api.Assets.GetMany<AlloyRecipe>(api.Server.Logger, "recipes/alloy");

            foreach (var val in alloys)
            {
                if (!val.Value.Enabled) continue;

                val.Value.Resolve(api.World, "alloy recipe " + val.Key);
                api.RegisterMetalAlloy(val.Value);
            }

            api.World.Logger.Event("{0} metal alloys loaded", alloys.Count);
            api.World.Logger.StoryEvent(Lang.Get("Glimmers in the soil..."));
        }



        public void LoadRecipes<T>(string name, string path, Action<T> RegisterMethod) where T : IRecipeBase<T>
        {
            Dictionary<AssetLocation, JToken> files = api.Assets.GetMany<JToken>(api.Server.Logger, path);
            int recipeQuantity = 0;
            int quantityRegistered = 0;
            int quantityIgnored = 0;

            foreach (var val in files)
            {
                if (val.Value is JObject)
                {
                    LoadGenericRecipe(name, val.Key, val.Value.ToObject<T>(val.Key.Domain), RegisterMethod, ref quantityRegistered, ref quantityIgnored);
                    recipeQuantity++;
                }
                if (val.Value is JArray)
                {
                    foreach (var token in (val.Value as JArray))
                    {
                        LoadGenericRecipe(name, val.Key, token.ToObject<T>(val.Key.Domain), RegisterMethod, ref quantityRegistered, ref quantityIgnored);
                        recipeQuantity++;
                    }
                }
            }

            api.World.Logger.Event("{0} {1}s loaded{2}", quantityRegistered, name, quantityIgnored > 0 ? string.Format(" ({0} could not be resolved)", quantityIgnored) : "");
        }


        void LoadGenericRecipe<T>(string className, AssetLocation path, T recipe, Action<T> RegisterMethod, ref int quantityRegistered, ref int quantityIgnored) where T : IRecipeBase<T>
        {
            if (!recipe.Enabled) return;
            if (recipe.Name == null) recipe.Name = path;

            Dictionary<string, string[]> nameToCodeMapping = recipe.GetNameToCodeMapping(api.World);

            if (nameToCodeMapping.Count > 0)
            {
                List<string> emptyMappings = new List<string>();
                foreach (var val2 in nameToCodeMapping)
                {
                    if (val2.Value == null || val2.Value.Length == 0)
                    {
                        emptyMappings.Add(val2.Key);
                    }
                }

                if (emptyMappings.Count > 0)
                {
                    AddInvalidRecipe(className, path, "wildcard name(s) have no matches: " + string.Join(", ", emptyMappings));
                    quantityIgnored++;
                    return;
                }

                List<T> subRecipes = new List<T>();

                int qCombs = 0;
                bool first = true;
                foreach (var val2 in nameToCodeMapping)
                {
                    if (first) qCombs = val2.Value.Length;
                    else qCombs *= val2.Value.Length;
                    first = false;
                }

                first = true;
                foreach (var val2 in nameToCodeMapping)
                {
                    string variantCode = val2.Key;
                    string[] variants = val2.Value;

                    for (int i = 0; i < qCombs; i++)
                    {
                        T rec;

                        if (first) subRecipes.Add(rec = recipe.Clone());
                        else rec = subRecipes[i];

                        if (rec.Ingredients != null)
                        {
                            foreach (var ingred in rec.Ingredients)
                            {
                                if (ingred.Name == variantCode)
                                {
                                    ingred.Code = ingred.Code.CopyWithPath(ingred.Code.Path.Replace("*", variants[i % variants.Length]));
                                }
                            }
                        }

                        rec.Output.FillPlaceHolder(val2.Key, variants[i % variants.Length]);
                    }

                    first = false;
                }

                if (subRecipes.Count == 0)
                {
                    api.World.Logger.Warning("{1} file {0} make uses of wildcards, but no blocks or item matching those wildcards were found.", path, className);
                    AddInvalidRecipe(className, path, "wildcards did not match any blocks or items");
                    quantityIgnored++;
                    return;
                }

                bool outputChecked = false;
                foreach (T subRecipe in subRecipes)
                {
                    if (!outputChecked)
                    {
                        string[] placeholders = GetPlaceholders(subRecipe.Output?.Code);
                        if (placeholders.Length > 0)
                        {
                            AddInvalidRecipe(className, path, "output contains unresolved placeholders " + string.Join(", ", placeholders));
                            quantityIgnored++;
                            return;
                        }
                        outputChecked = true;
                    }

                    if (!subRecipe.Resolve(api.World, className + " " + path))
                    {
                        AddInvalidRecipe(className, path, "failed to resolve output " + subRecipe.Output?.Code);
                        quantityIgnored++;
                        continue;
                    }
                    RegisterMethod(subRecipe);
                    quantityRegistered++;
                }

            }
            else
            {
                string[] placeholders = GetPlaceholders(recipe.Output?.Code);
                if (placeholders.Length > 0)
                {
                    AddInvalidRecipe(className, path, "output contains unresolved placeholders " + string.Join(", ", placeholders));
                    quantityIgnored++;
                    return;
                }

                if (!recipe.Resolve(api.World, className + " " + path))
                {
                    AddInvalidRecipe(className, path, "failed to resolve output " + recipe.Output?.Code);
                    quantityIgnored++;
                    return;
                }

                RegisterMethod(recipe);
                quantityRegistered++;
            }
        }

        void AddInvalidRecipe(string className, AssetLocation path, string reason)
        {
            RecipeValidationErrors.Add(string.Format("{0} {1}: {2}", className, path.ToShortString(), reason));
        }

        static string[] GetPlaceholders(AssetLocation code)
        {
            if (code?.Path == null) return Array.Empty<string>();

            MatchCollection matches = PlaceholderRegex.Matches(code.Path);
            if (matches.Count == 0) return Array.Empty<string>();

            string[] placeholders = new string[matches.Count];
            for (int i = 0; i < matches.Count; i++)
            {
                placeholders[i] = matches[i].Value;
            }

            return placeholders;
        }

        
    }
}

