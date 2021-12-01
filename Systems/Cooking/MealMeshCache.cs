using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;


namespace Vintagestory.GameContent
{
    public class MealMeshCache : ModSystem, ITexPositionSource
    {
        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        ICoreClientAPI capi;
        Block mealtextureSourceBlock;

        AssetLocation[] pieShapeLocByFillLevel = new AssetLocation[]
        {
            new AssetLocation("block/food/pie/full-fill0"),
            new AssetLocation("block/food/pie/full-fill1"),
            new AssetLocation("block/food/pie/full-fill2"),
            new AssetLocation("block/food/pie/full-fill3"),
            new AssetLocation("block/food/pie/full-fill4"),
        };

        AssetLocation[] pieShapeBySize = new AssetLocation[]
        {
            new AssetLocation("block/food/pie/quarter"),
            new AssetLocation("block/food/pie/half"),
            new AssetLocation("block/food/pie/threefourths"),
            new AssetLocation("block/food/pie/full"),
        };

        #region Pie Stuff
        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
        protected Shape nowTesselatingShape;

        BlockPie nowTesselatingBlock;
        ItemStack[] contentStacks;
        AssetLocation crustTextureLoc;
        AssetLocation fillingTextureLoc;
        AssetLocation topCrustTextureLoc;

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                AssetLocation texturePath = crustTextureLoc;
                if (textureCode == "filling") texturePath = fillingTextureLoc;
                if (textureCode == "topcrust")
                {
                    texturePath = topCrustTextureLoc;
                }

                if (texturePath == null)
                {
                    capi.World.Logger.Warning("Missing texture path for pie mesh texture code {0}, seems like a missing texture definition or invalid pie block.", textureCode);
                    return capi.BlockTextureAtlas.UnknownTexturePosition;
                }

                TextureAtlasPosition texpos = capi.BlockTextureAtlas[texturePath];

                if (texpos == null)
                {
                    IAsset texAsset = capi.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                    if (texAsset != null)
                    {
                        BitmapRef bmp = texAsset.ToBitmap(capi);
                        capi.BlockTextureAtlas.InsertTextureCached(texturePath, bmp, out _, out texpos);
                    }
                    else
                    {
                        capi.World.Logger.Warning("Pie mesh texture {1} not found.", nowTesselatingBlock.Code, texturePath);
                        texpos = capi.BlockTextureAtlas.UnknownTexturePosition;
                    }
                }


                return texpos;
            }
        }

        #endregion

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            capi = api as ICoreClientAPI;

            api.Event.LeaveWorld += Event_LeaveWorld;
            api.Event.BlockTexturesLoaded += Event_BlockTexturesLoaded;
        }

        private void Event_BlockTexturesLoaded()
        {
            mealtextureSourceBlock = capi.World.GetBlock(new AssetLocation("claypot-cooked"));
        }


        public MeshRef GetOrCreatePieMeshRef(ItemStack pieStack)
        {
            Dictionary<int, MeshRef> meshrefs;

            object obj;
            if (capi.ObjectCache.TryGetValue("pieMeshRefs", out obj))
            {
                meshrefs = obj as Dictionary<int, MeshRef>;
            }
            else
            {
                capi.ObjectCache["pieMeshRefs"] = meshrefs = new Dictionary<int, MeshRef>();
            }

            if (pieStack == null) return null;


            ItemStack[] contentStacks = (pieStack.Block as BlockPie).GetContents(capi.World, pieStack);

            string extrakey = "ct" + pieStack.Attributes.GetInt("topCrustType") + "-bl" + pieStack.Attributes.GetInt("bakeLevel", 0) + "-ps" + pieStack.Attributes.GetInt("pieSize");

            int mealhashcode = GetMealHashCode(pieStack.Block, contentStacks, null, extrakey);

            MeshRef mealMeshRef;

            if (!meshrefs.TryGetValue(mealhashcode, out mealMeshRef))
            {
                MeshData mesh = GetPieMesh(pieStack);
                if (mesh == null) return null;

                meshrefs[mealhashcode] = mealMeshRef = capi.Render.UploadMesh(mesh);
            }

            return mealMeshRef;
        }


        public MeshData GetPieMesh(ItemStack pieStack, ModelTransform transform = null)
        {
            // Slot 0: Base dough
            // Slot 1: Filling
            // Slot 2: Crust dough

            nowTesselatingBlock = pieStack.Block as BlockPie;
            if (nowTesselatingBlock == null) return null;  //This will occur if the pieStack changed to rot

            contentStacks = nowTesselatingBlock.GetContents(capi.World, pieStack);

            int pieSize = pieStack.Attributes.GetInt("pieSize");


            // At this spot we have to determine the textures for "dough" and "filling"
            // Texture determination rules:
            // 1. dough is simple: first itemstack must be dough, take from attributes
            // 2. pie allows 4 items as fillings, but with specific mixing rules
            //    - berries/fruit can be mixed
            //    - vegetables can be mixed
            //    - meat can be mixed
            // no other mixing allowed

            // Thus we deduce: It's enough to test if
            // a) all 4 fillings are equal: Then use texture from inPieProperties from first one
            // b) Otherwise use hardcoded
            //    for item.NutritionProps.FoodCategory == Vegetable   => block/food/pie/fill-mixedvegetable.png
            //    for item.NutritionProps.FoodCategory == Protein   => block/food/pie/fill-mixedmeat.png
            //    for item.NutritionProps.FoodCategory == Fruit   => block/food/pie/fill-mixedfruit.png

            var stackprops = contentStacks.Select(stack => stack?.ItemAttributes?["inPieProperties"]?.AsObject<InPieProperties>(null, stack.Collectible.Code.Domain)).ToArray();

            int bakeLevel = pieStack.Attributes.GetInt("bakeLevel", 0);

            if (stackprops.Length == 0) return null;


            ItemStack cstack = contentStacks[1];
            bool equal = true;
            for (int i = 2; equal && i < contentStacks.Length - 1; i++)
            {
                if (contentStacks[i] == null || cstack == null) continue;

                equal &= cstack.Equals(capi.World, contentStacks[i], GlobalConstants.IgnoredStackAttributes);
                cstack = contentStacks[i];
            }


            if (ContentsRotten(contentStacks))
            {
                crustTextureLoc = new AssetLocation("block/rot/rot");
                fillingTextureLoc = new AssetLocation("block/rot/rot");
                topCrustTextureLoc = new AssetLocation("block/rot/rot");
            }
            else
            {
                if (stackprops[0] != null)
                {
                    crustTextureLoc = stackprops[0].Texture.Clone();
                    crustTextureLoc.Path = crustTextureLoc.Path.Replace("{bakelevel}", "" + (bakeLevel + 1));
                    fillingTextureLoc = new AssetLocation("block/transparent");
                }

                topCrustTextureLoc = new AssetLocation("block/transparent");
                if (stackprops[5] != null)
                {
                    topCrustTextureLoc = stackprops[5].Texture.Clone();
                    topCrustTextureLoc.Path = topCrustTextureLoc.Path.Replace("{bakelevel}", "" + (bakeLevel + 1));
                }

                if (contentStacks[1] != null)
                {
                    EnumFoodCategory fillingFoodCat =
                        contentStacks[1].Collectible.NutritionProps?.FoodCategory
                        ?? contentStacks[1].ItemAttributes?["nutritionPropsWhenInMeal"]?.AsObject<FoodNutritionProperties>()?.FoodCategory
                        ?? EnumFoodCategory.Vegetable
                    ;

                    fillingTextureLoc = equal ? stackprops[1]?.Texture : pieMixedFillingTextures[(int)fillingFoodCat];
                }
            }


            int fillLevel = (contentStacks[1] != null ? 1 : 0) + (contentStacks[2] != null ? 1 : 0) + (contentStacks[3] != null ? 1 : 0) + (contentStacks[4] != null ? 1 : 0);
            bool isComplete = fillLevel == 4;

            AssetLocation shapeloc = isComplete ? pieShapeBySize[pieSize - 1] : pieShapeLocByFillLevel[fillLevel];

            shapeloc.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
            Shape shape = capi.Assets.TryGet(shapeloc).ToObject<Shape>();
            MeshData mesh;

            int topCrustType = pieStack.Attributes.GetInt("topCrustType");
            string[] topCrusts = new string[] { "origin/base/top crust full/*", "origin/base/top crust square/*", "origin/base/top crust diagonal/*" };
            string[] selectiveElements = new string[] { "origin/base/crust regular/*", "origin/base/filling/*", "origin/base/base-quarter/*", "origin/base/fillingquarter/*", topCrusts[topCrustType] };

            capi.Tesselator.TesselateShape("pie", shape, out mesh, this, null, 0, 0, 0, null, selectiveElements);
            if (transform != null) mesh.ModelTransform(transform);

            return mesh;
        }


        public AssetLocation[] pieMixedFillingTextures = new AssetLocation[] {
            new AssetLocation("block/food/pie/fill-mixedfruit"),
            new AssetLocation("block/food/pie/fill-mixedvegetable"), 
            new AssetLocation("block/food/pie/fill-mixedmeat"),
            new AssetLocation("grain-unused-placeholder"),
            new AssetLocation("block/food/pie/fill-mixedcheese") 
        };

        public MeshRef GetOrCreateMealInContainerMeshRef(Block containerBlock, CookingRecipe forRecipe, ItemStack[] contentStacks, Vec3f foodTranslate = null)
        {
            Dictionary<int, MeshRef> meshrefs;

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

            int mealhashcode = GetMealHashCode(containerBlock, contentStacks, foodTranslate);

            MeshRef mealMeshRef;

            if (!meshrefs.TryGetValue(mealhashcode, out mealMeshRef))
            {
                MeshData mesh = GenMealInContainerMesh(containerBlock, forRecipe, contentStacks, foodTranslate);
                
                meshrefs[mealhashcode] = mealMeshRef = capi.Render.UploadMesh(mesh);
            }

            return mealMeshRef;
        }

        public MeshData GenMealInContainerMesh(Block containerBlock, CookingRecipe forRecipe, ItemStack[] contentStacks, Vec3f foodTranslate = null)
        {
            CompositeShape cShape = containerBlock.Shape;
            Shape shape = capi.Assets.TryGet("shapes/" + cShape.Base.Path + ".json").ToObject<Shape>();
            MeshData wholeMesh;
            capi.Tesselator.TesselateShape("meal", shape, out wholeMesh, capi.Tesselator.GetTexSource(containerBlock), new Vec3f(cShape.rotateX, cShape.rotateY, cShape.rotateZ));

            MeshData mealMesh = GenMealMesh(forRecipe, contentStacks, foodTranslate);
            if (mealMesh != null)
            {
                wholeMesh.AddMeshData(mealMesh);
            }

            return wholeMesh;
        }

        public MeshData GenMealMesh(CookingRecipe forRecipe, ItemStack[] contentStacks, Vec3f foodTranslate = null)
        {
            MealTextureSource source = new MealTextureSource(capi, mealtextureSourceBlock);
            
            if (forRecipe != null)
            {
                MeshData foodMesh = GenFoodMixMesh(contentStacks, forRecipe, foodTranslate);
                if (foodMesh != null)
                {
                    return foodMesh;
                }
            }

            if (contentStacks != null && contentStacks.Length > 0)
            {
                bool rotten = ContentsRotten(contentStacks);
                if (rotten)
                {
                    Shape contentShape = capi.Assets.TryGet("shapes/block/food/meal/rot.json").ToObject<Shape>();

                    MeshData contentMesh;
                    capi.Tesselator.TesselateShape("rotcontents", contentShape, out contentMesh, source);

                    if (foodTranslate != null)
                    {
                        contentMesh.Translate(foodTranslate);
                    }

                    return contentMesh;
                }
                else
                {


                    JsonObject obj = contentStacks[0]?.ItemAttributes?["inContainerTexture"];
                    if (obj != null && obj.Exists)
                    {
                        source.ForStack = contentStacks[0];

                        CompositeShape cshape = contentStacks[0]?.ItemAttributes?["inBowlShape"].AsObject<CompositeShape>(new CompositeShape() { Base = new AssetLocation("shapes/block/food/meal/pickled.json") });

                        Shape contentShape = capi.Assets.TryGet(cshape.Base.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/")).ToObject<Shape>();
                        MeshData contentMesh;
                        capi.Tesselator.TesselateShape("picklednmealcontents", contentShape, out contentMesh, source);

                        return contentMesh;
                    }
                }
            }
            
            return null;
        }


        public static bool ContentsRotten(ItemStack[] contentStacks)
        {
            for (int i = 0; i < contentStacks.Length; i++)
            {
                if (contentStacks[i]?.Collectible.Code.Path == "rot") return true;
            }
            return false;
        }
        public static bool ContentsRotten(InventoryBase inv)
        {
            foreach (var slot in inv)
            {
                if (slot.Itemstack?.Collectible.Code.Path == "rot") return true;
            }
            return false;
        }


        public MeshData GenFoodMixMesh(ItemStack[] contentStacks, CookingRecipe recipe, Vec3f foodTranslate)
        {
            MeshData mergedmesh = null;
            MealTextureSource texSource = new MealTextureSource(capi, mealtextureSourceBlock);
            string shapePath = "shapes/" + recipe.Shape.Base.Path + ".json";
            bool rotten = ContentsRotten(contentStacks);
            if (rotten)
            {
                shapePath = "shapes/block/food/meal/rot.json";
            }
            

            Shape shape = capi.Assets.TryGet(shapePath).ToObject<Shape>();
            Dictionary<CookingRecipeIngredient, int> usedIngredQuantities = new Dictionary<CookingRecipeIngredient, int>();

            if (rotten)
            {
                capi.Tesselator.TesselateShape(
                    "mealpart", shape, out mergedmesh, texSource,
                    new Vec3f(recipe.Shape.rotateX, recipe.Shape.rotateY, recipe.Shape.rotateZ)
                );
            }
            else
            {
                HashSet<string> drawnMeshes = new HashSet<string>();

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

                    if (drawnMeshes.Contains(recipestack.ShapeElement + recipestack.TextureMapping)) continue;
                    drawnMeshes.Add(recipestack.ShapeElement + recipestack.TextureMapping);

                    capi.Tesselator.TesselateShape(
                        "mealpart", shape, out meshpart, texSource,
                        new Vec3f(recipe.Shape.rotateX, recipe.Shape.rotateY, recipe.Shape.rotateZ), 0, 0, 0, null, selectiveElements
                    );

                    if (mergedmesh == null) mergedmesh = meshpart;
                    else mergedmesh.AddMeshData(meshpart);
                }

            }


            if (foodTranslate != null && mergedmesh != null) mergedmesh.Translate(foodTranslate);

            return mergedmesh;
        }

        



        private void Event_LeaveWorld()
        {
            if (capi == null) return;

            object obj;
            if (capi.ObjectCache.TryGetValue("cookedMeshRefs", out obj))
            {
                Dictionary<int, MeshRef> meshrefs = obj as Dictionary<int, MeshRef>;

                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove("cookedMeshRefs");
            }
        }

        public int GetMealHashCode(ItemStack stack, Vec3f translate = null, string extraKey = "")
        {
            ItemStack[] contentStacks = (stack.Block as BlockContainer).GetContents(capi.World, stack);

            if (stack.Block is BlockPie)
            {
                extraKey += "ct" + stack.Attributes.GetInt("topCrustType") + "-bl" + stack.Attributes.GetInt("bakeLevel", 0) + "-ps" + stack.Attributes.GetInt("pieSize");
            }

            return GetMealHashCode(stack.Block, contentStacks, translate, extraKey);
        }

        protected int GetMealHashCode(Block block, ItemStack[] contentStacks, Vec3f translate = null, string extraKey = null)
        {
            string shapestring = block.Shape.ToString() + block.Code.ToShortString();
            if (translate != null) shapestring += translate.X + "/" + translate.Y + "/" + translate.Z;

            string contentstring = "";
            for (int i = 0; i < contentStacks.Length; i++)
            {
                if (contentStacks[i] == null) continue;

                if (contentStacks[i].Collectible.Code.Path == "rot")
                {
                    return (shapestring + "rotten").GetHashCode();
                }

                contentstring += contentStacks[i].Collectible.Code.ToShortString();
            }

            return (shapestring + contentstring + extraKey).GetHashCode();
        }


    }
}
