using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;


namespace Vintagestory.GameContent
{
    public class MealMeshCache : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        ICoreClientAPI capi;
        Block textureSourceBlock;

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            capi = api as ICoreClientAPI;

            api.Event.LeaveWorld += Event_LeaveWorld;
            api.Event.BlockTexturesLoaded += Event_BlockTexturesLoaded;
        }

        private void Event_BlockTexturesLoaded()
        {
            textureSourceBlock = capi.World.GetBlock(new AssetLocation("claypot-cooked"));
        }

        public MeshRef GetOrCreateMealMeshRef(Block blockForHashing, CompositeShape containerShape, CookingRecipe forRecipe, ItemStack[] contentStacks, Vec3f foodTranslate = null)
        {
            Dictionary<int, MeshRef> meshrefs = null;

            object obj;
            if (capi.ObjectCache.TryGetValue("cookedMeshRefs", out obj))
            {
                meshrefs = obj as Dictionary<int, MeshRef>;
            }
            else
            {
                capi.ObjectCache["cookedMeshRefs"] = meshrefs = new Dictionary<int, MeshRef>();
            }

            if (contentStacks == null) return null;

            int mealhashcode = GetMealHashCode(containerShape, capi.World, blockForHashing, contentStacks);

            MeshRef mealMeshRef = null;

            if (!meshrefs.TryGetValue(mealhashcode, out mealMeshRef))
            {
                meshrefs[mealhashcode] = mealMeshRef = capi.Render.UploadMesh(CreateMealMesh(containerShape, forRecipe, contentStacks, foodTranslate));
            }

            return mealMeshRef;
        }

        public MeshData CreateMealMesh(CompositeShape cShape, CookingRecipe forRecipe, ItemStack[] contentStacks, Vec3f foodTranslate = null)
        {
            MealTextureSource source = new MealTextureSource(capi, textureSourceBlock);
            Shape shape = capi.Assets.TryGet("shapes/" + cShape.Base.Path + ".json").ToObject<Shape>();

            MeshData containerMesh;
            capi.Tesselator.TesselateShape("meal", shape, out containerMesh, source, new Vec3f(cShape.rotateX, cShape.rotateY, cShape .rotateZ));

            if (forRecipe != null)
            {
                MeshData foodMesh = GenFoodMixMesh(contentStacks, forRecipe, foodTranslate);
                containerMesh.AddMeshData(foodMesh);
            }

            if (contentStacks != null && contentStacks.Length > 0)
            {
                bool rotten = ContentsRotten(contentStacks);
                if (rotten)
                {
                    Shape contentShape = capi.Assets.TryGet("shapes/block/meal/rot.json").ToObject<Shape>();

                    MeshData contentMesh;
                    capi.Tesselator.TesselateShape("rotcontents", contentShape, out contentMesh, source);
                    containerMesh.AddMeshData(contentMesh);
                }
                else
                {


                    JsonObject obj = contentStacks[0]?.ItemAttributes?["inContainerTexture"];
                    if (obj != null && obj.Exists)
                    {
                        source.ForStack = contentStacks[0];

                        Shape contentShape = capi.Assets.TryGet("shapes/block/meal/pickled.json").ToObject<Shape>();
                        MeshData contentMesh;
                        capi.Tesselator.TesselateShape("picklednmealcontents", contentShape, out contentMesh, source);

                        containerMesh.AddMeshData(contentMesh);
                    }
                }
            }
            
            return containerMesh;
        }


        public static bool ContentsRotten(ItemStack[] contentStacks)
        {
            for (int i = 0; i < contentStacks.Length; i++)
            {
                if (contentStacks[i].Collectible.Code.Path == "rot") return true;
            }

            return false;
        }


        public MeshData GenFoodMixMesh(ItemStack[] contentStacks, CookingRecipe recipe, Vec3f foodTranslate)
        {
            MeshData mergedmesh = null;
            MealTextureSource texSource = new MealTextureSource(capi, textureSourceBlock);
            string shapePath = "shapes/" + recipe.Shape.Base.Path + ".json";
            bool rotten = ContentsRotten(contentStacks);
            if (rotten)
            {
                shapePath = "shapes/block/meal/rot.json";
            }
            

            Shape shape = capi.Assets.TryGet(shapePath).ToObject<Shape>();
            Dictionary<CookingRecipeIngredient, int> usedIngredQuantities = new Dictionary<CookingRecipeIngredient, int>();

            if (rotten)
            {
                capi.Tesselator.TesselateShape(
                    "mealpart", shape, out mergedmesh, texSource,
                    new Vec3f(recipe.Shape.rotateX, recipe.Shape.rotateY, recipe.Shape.rotateZ), 0, 0, null, null
                );
            }
            else
            {

                for (int i = 0; i < contentStacks.Length; i++)
                {
                    texSource.ForStack = contentStacks[i];
                    CookingRecipeIngredient ingred = recipe.GetIngrendientFor(
                        contentStacks[i],
                        usedIngredQuantities.Where(val => val.Key.MaxQuantity <= val.Value).Select(val => val.Key).ToArray()
                    );

                    if (ingred == null)
                    {
                        ingred = recipe.GetIngrendientFor(contentStacks[i]);
                    }
                    else
                    {
                        int cnt = 0;
                        usedIngredQuantities.TryGetValue(ingred, out cnt);
                        cnt++;
                        usedIngredQuantities[ingred] = cnt;
                    }

                    if (ingred == null) continue;


                    MeshData meshpart;
                    string[] selectiveElements = null;

                    CookingRecipeStack recipestack = ingred.GetMatchingStack(contentStacks[i]);

                    if (recipestack.ShapeElement != null) selectiveElements = new string[] { recipestack.ShapeElement };
                    texSource.customTextureMapping = recipestack.TextureMapping;

                    capi.Tesselator.TesselateShape(
                        "mealpart", shape, out meshpart, texSource,
                        new Vec3f(recipe.Shape.rotateX, recipe.Shape.rotateY, recipe.Shape.rotateZ), 0, 0, null, selectiveElements
                    );

                    if (mergedmesh == null) mergedmesh = meshpart;
                    else mergedmesh.AddMeshData(meshpart);
                }

            }


            if (foodTranslate != null) mergedmesh.Translate(foodTranslate);

            return mergedmesh;
        }

        



        private void Event_LeaveWorld()
        {
            if (capi == null) return;

            object obj;
            if (capi.ObjectCache.TryGetValue("mealMeshRefs", out obj))
            {
                Dictionary<int, MeshRef> meshrefs = obj as Dictionary<int, MeshRef>;

                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove("mealMeshRefs");
            }
        }

        private int GetMealHashCode(CompositeShape containerShape, IClientWorldAccessor world, Block block, ItemStack[] contentStacks)
        {
            string s = containerShape.Base.ToShortString() + block.Code.ToShortString();
            for (int i = 0; i < contentStacks.Length; i++)
            {
                if (contentStacks[i].Collectible.Code.Path == "rot")
                {
                    return "rotten".GetHashCode();
                }

                s += contentStacks[i].Collectible.Code.ToShortString();
            }

            return s.GetHashCode();
        }


    }
}
