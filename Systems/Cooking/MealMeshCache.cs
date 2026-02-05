using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;


namespace Vintagestory.GameContent
{
    public class MealMeshCache : ModSystem, ITexPositionSource
    {
        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        ICoreClientAPI? capi;
        Block? mealtextureSourceBlock;

        #region Pie Stuff
        AssetLocation[] pieShapeLocByFillLevel =
        [
            new ("block/food/pie/full-fill0"),
            new ("block/food/pie/full-fill1"),
            new ("block/food/pie/full-fill2"),
            new ("block/food/pie/full-fill3"),
            new ("block/food/pie/full-fill4"),
        ];

        AssetLocation[] pieShapeBySize =
        [
            new ("block/food/pie/quarter"),
            new ("block/food/pie/half"),
            new ("block/food/pie/threefourths"),
            new ("block/food/pie/full"),
        ];

        public Size2i AtlasSize => capi!.BlockTextureAtlas.Size;
        protected Shape? nowTesselatingShape;

        BlockPie? nowTesselatingBlock;
        ItemStack[]? contentStacks;
        AssetLocation? crustTextureLoc;
        AssetLocation? fillingTextureLoc;
        AssetLocation? topCrustTextureLoc;

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                AssetLocation texturePath = crustTextureLoc!;
                if (textureCode == "filling") texturePath = fillingTextureLoc!;
                if (textureCode == "topcrust")
                {
                    texturePath = topCrustTextureLoc!;
                }

                if (texturePath == null)
                {
                    capi!.World.Logger.Warning("Missing texture path for pie mesh texture code {0}, seems like a missing texture definition or invalid pie block.", textureCode);
                    return capi.BlockTextureAtlas.UnknownTexturePosition;
                }

                TextureAtlasPosition texpos = capi!.BlockTextureAtlas[texturePath];

                if (texpos == null)
                {
                    IAsset texAsset = capi.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                    if (texAsset != null)
                    {
                        BitmapRef bmp = texAsset.ToBitmap(capi);
                        capi.BlockTextureAtlas.GetOrInsertTexture(texturePath, out _, out texpos, () => bmp);
                    }
                    else
                    {
                        capi.World.Logger.Warning("Pie mesh texture {1} not found.", nowTesselatingBlock?.Code, texturePath);
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

            capi = api;

            api.Event.LeaveWorld += Event_LeaveWorld;
            api.Event.BlockTexturesLoaded += Event_BlockTexturesLoaded;
        }

        private void Event_BlockTexturesLoaded()
        {
            mealtextureSourceBlock = capi!.World.GetBlock(new AssetLocation("claypot-blue-cooked"));
        }

        public override void Dispose()
        {
            if (capi?.ObjectCache.TryGetValue("pieMeshRefs", out var objPi) == true && objPi is Dictionary<int, MultiTextureMeshRef> meshRefs)
            {
                foreach (var (_, meshRef) in meshRefs)
                {
                    meshRef.Dispose();
                }
                capi.ObjectCache.Remove("pieMeshRefs");
            }
        }

        public MultiTextureMeshRef? GetOrCreatePieMeshRef(ItemStack? pieStack)
        {
            Dictionary<int, MultiTextureMeshRef> meshrefs;

            if (capi!.ObjectCache.TryGetValue("pieMeshRefs", out object? obj))
            {
                meshrefs = obj as Dictionary<int, MultiTextureMeshRef> ?? [];
            }
            else
            {
                capi.ObjectCache["pieMeshRefs"] = meshrefs = [];
            }

            if (pieStack?.Block is not BlockPie pieBlock) return null;


            ItemStack?[] contentStacks = pieBlock.GetContents(capi.World, pieStack);

            string extrakey = "ct" + (BlockPie.GetTopCrustType(pieStack) ?? "full") + "-bl" + pieStack.Attributes.GetAsInt("bakeLevel", 0) + "-ps" + pieStack.Attributes.GetAsInt("pieSize");

            int mealhashcode = GetMealHashCode(pieBlock, contentStacks, null, extrakey);


            if (!meshrefs.TryGetValue(mealhashcode, out MultiTextureMeshRef? mealMeshRef))
            {
                if (GetPieMesh(pieStack) is not MeshData mesh) return null;

                meshrefs[mealhashcode] = mealMeshRef = capi.Render.UploadMultiTextureMesh(mesh);
            }

            return mealMeshRef;
        }


        public MeshData? GetPieMesh(ItemStack? pieStack, ModelTransform? transform = null)
        {
            // Slot 0: Base dough
            // Slot 1: Filling
            // Slot 2: Crust dough

            nowTesselatingBlock = pieStack?.Block as BlockPie;
            if (nowTesselatingBlock == null) return null;  //This will occur if the pieStack changed to rot

            contentStacks = nowTesselatingBlock.GetContents(capi!.World, pieStack);

            int pieSize = pieStack?.Attributes.GetAsInt("pieSize") ?? 0;


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

            var stackprops = contentStacks.Select(stack => stack?.ItemAttributes?["inPieProperties"]?.AsObject<InPieProperties?>(null, stack.Collectible.Code.Domain)).ToArray();

            int bakeLevel = pieStack?.Attributes.GetAsInt("bakeLevel", 0) ?? 0;

            if (stackprops.Length == 0) return null;


            ItemStack cstack = contentStacks[1];
            var foodCats = contentStacks.Select(BlockPie.FillingFoodCategory).ToArray();
            EnumFoodCategory foodCat = foodCats[1];

            bool equal = true;
            bool foodCatEquals = true;
            IEnumerable<string> mixCodes = stackprops[1]?.MixingCodes ?? [];
            for (int i = 2; (equal || foodCatEquals || mixCodes.Any()) && i < contentStacks.Length - 1; i++)
            {
                if (contentStacks[i] == null || cstack == null) continue;

                equal &= cstack.Equals(capi.World, contentStacks[i], GlobalConstants.IgnoredStackAttributes);
                foodCatEquals &= contentStacks[i] == null || foodCats[i] == foodCats[1];
                mixCodes = stackprops[i]?.MixingCodes.Intersect(mixCodes) ?? [];

                cstack = contentStacks[i];
                foodCat = foodCats[i];
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
                    crustTextureLoc = stackprops[0]!.Texture.Clone();
                    crustTextureLoc.Path = crustTextureLoc.Path.Replace("{bakelevel}", "" + (bakeLevel + 1));
                    fillingTextureLoc = new AssetLocation("block/transparent");
                }

                topCrustTextureLoc = new AssetLocation("block/transparent");
                if (stackprops[5] != null)
                {
                    topCrustTextureLoc = stackprops[5]!.Texture.Clone();
                    topCrustTextureLoc.Path = topCrustTextureLoc.Path.Replace("{bakelevel}", "" + (bakeLevel + 1));
                }

                if (contentStacks[1] != null)
                {
                    var fillingCat = foodCats[1];
                    if (fillingCat == EnumFoodCategory.NoNutrition) fillingCat = EnumFoodCategory.Unknown;
                    fillingTextureLoc = getPieFillingTexture(stackprops, mixCodes.ToArray(), equal, foodCatEquals, fillingCat);
                }
            }


            int fillLevel = (contentStacks[1] != null ? 1 : 0) + (contentStacks[2] != null ? 1 : 0) + (contentStacks[3] != null ? 1 : 0) + (contentStacks[4] != null ? 1 : 0);
            bool isComplete = fillLevel == 4;

            AssetLocation shapeloc = isComplete ? pieShapeBySize[pieSize - 1] : pieShapeLocByFillLevel[fillLevel];

            shapeloc.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
            Shape shape = API.Common.Shape.TryGet(capi, shapeloc);

            string topCrustShapeElement = BlockPie.TopCrustTypes.First(type => type.Code.EqualsFast(BlockPie.GetTopCrustType(pieStack) ?? "full")).ShapeElement;
            string[] selectiveElements = ["origin/base/crust regular/*", "origin/base/filling/*", "origin/base/base-quarter/*", "origin/base/fillingquarter/*", topCrustShapeElement];

            capi.Tesselator.TesselateShape("pie", shape, out MeshData mesh, this, null, 0, 0, 0, null, selectiveElements);
            if (transform != null) mesh.ModelTransform(transform);

            return mesh;
        }

        public Dictionary<int, AssetLocation> pieMixingCodeFillingTextures = [];
        private AssetLocation? getPieFillingTexture(InPieProperties?[] pieProps, string[] mixingCodes, bool singleFilling, bool singleFoodCat, EnumFoodCategory fillingCat)
        {
            if (singleFilling) return pieProps[1]?.Texture;

            if (!singleFoodCat && mixingCodes.Length > 0)
            {
                if (pieMixingCodeFillingTextures.TryGetValue(mixingCodes[0].GetHashCode(), out var loc))
                {
                    return loc;
                }
                else pieMixingCodeFillingTextures.Add(mixingCodes[0].GetHashCode(), new("block/food/pie/fill-mixed" + mixingCodes[0]));
            }

            return pieMixedCategoryFillingTextures[(int)fillingCat];
        }


        public AssetLocation[] pieMixedCategoryFillingTextures = [
            new ("block/food/pie/fill-mixedfruit"),
            new ("block/food/pie/fill-mixedvegetable"),
            new ("block/food/pie/fill-mixedmeat"),
            new ("block/food/pie/fill-mixedgrain"),
            new ("block/food/pie/fill-mixedcheese"),
            new ("block/food/pie/fill-unknown")
        ];


        public Dictionary<int, MultiTextureMeshRef> GetCookedMeshRefs()
        {
            Dictionary<int, MultiTextureMeshRef> meshrefs;

            if (capi!.ObjectCache.TryGetValue("cookedMeshRefs", out object? obj))
            {
                meshrefs = obj as Dictionary<int, MultiTextureMeshRef> ?? [];
            }
            else
            {
                capi.ObjectCache["cookedMeshRefs"] = meshrefs = [];
            }

            return meshrefs;
        }

        public MultiTextureMeshRef? GetOrCreateMealInContainerMeshRef(IBlockMealContainer be, ItemStack containerStack, Vec3f? foodTranslate)
        {
            return GetOrCreateMealInContainerMeshRef(containerStack.Block, be.GetCookingRecipe(capi!.World, containerStack), be.GetNonEmptyContents(capi.World, containerStack), foodTranslate);
        }

        public MultiTextureMeshRef? GetOrCreateMealInContainerMeshRef(Block containerBlock, CookingRecipe? forRecipe, ItemStack?[]? contentStacks, Vec3f? foodTranslate = null)
        {
            if (contentStacks == null) return null;

            int mealhashcode = GetMealHashCode(containerBlock, contentStacks, foodTranslate);
            Dictionary<int, MultiTextureMeshRef> meshrefs = GetCookedMeshRefs();

            if (!meshrefs.TryGetValue(mealhashcode, out MultiTextureMeshRef? mealMeshRef))
            {
                MeshData mesh = GenMealInContainerMesh(containerBlock, forRecipe, contentStacks, foodTranslate);

                meshrefs[mealhashcode] = mealMeshRef = capi!.Render.UploadMultiTextureMesh(mesh);
            }

            return mealMeshRef;
        }


        public MeshData GenMealInContainerMesh(Block containerBlock, CookingRecipe? forRecipe, ItemStack?[] contentStacks, Vec3f? foodTranslate = null)
        {
            CompositeShape cShape = containerBlock.Shape;
            var loc = cShape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");

            Shape shape = API.Common.Shape.TryGet(capi, loc);
            capi!.Tesselator.TesselateShape("meal", shape, out MeshData wholeMesh, capi.Tesselator.GetTextureSource(containerBlock), new Vec3f(cShape.rotateX, cShape.rotateY, cShape.rotateZ));

            if (GenMealMesh(forRecipe, contentStacks, foodTranslate) is MeshData mealMesh) wholeMesh.AddMeshData(mealMesh);

            return wholeMesh;
        }

        public MeshData? GenMealMesh(CookingRecipe? forRecipe, ItemStack?[] contentStacks, Vec3f? foodTranslate = null)
        {
            MealTextureSource source;
            try
            {
                source = new MealTextureSource(capi!, mealtextureSourceBlock!);
            }
            catch
            {
                capi!.Logger.Error("Unable to create meal texture source for recipe: " + forRecipe?.Code + " for: " + mealtextureSourceBlock?.Code.ToShortString());
                throw;
            }

            if (forRecipe != null && GenFoodMixMesh(contentStacks, forRecipe, foodTranslate) is MeshData foodMesh) return foodMesh;

            if (contentStacks != null && contentStacks.Length > 0)
            {
                bool rotten = ContentsRotten(contentStacks);
                if (rotten)
                {
                    Shape contentShape = API.Common.Shape.TryGet(capi, "shapes/block/food/meal/rot.json");

                    capi!.Tesselator.TesselateShape("rotcontents", contentShape, out MeshData contentMesh, source);

                    if (foodTranslate != null)
                    {
                        contentMesh.Translate(foodTranslate);
                    }

                    return contentMesh;
                }
                else
                {
                    if (contentStacks[0]?.ItemAttributes?["inContainerTexture"] is JsonObject obj)
                    {
                        source.ForStack = contentStacks[0]!;

                        CompositeShape? cshape = contentStacks[0]!.ItemAttributes["inBowlShape"]?.AsObject(new CompositeShape() { Base = new ("shapes/block/food/meal/pickled.json") });

                        Shape contentShape = API.Common.Shape.TryGet(capi, cshape?.Base.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/"));
                        capi!.Tesselator.TesselateShape("picklednmealcontents", contentShape, out MeshData contentMesh, source);

                        return contentMesh;
                    }
                }
            }

            return null;
        }


        public static bool ContentsRotten(ItemStack?[] contentStacks)
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


        public MeshData? GenFoodMixMesh(ItemStack?[] contentStacks, CookingRecipe recipe, Vec3f? foodTranslate)
        {
            MeshData? mergedmesh = null;
            MealTextureSource texSource = new MealTextureSource(capi!, mealtextureSourceBlock!);

            var shapePath = recipe.Shape!.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");

            bool rotten = ContentsRotten(contentStacks);
            if (rotten)
            {
                shapePath = new AssetLocation("shapes/block/food/meal/rot.json");
            }

            Shape shape = Shape.TryGet(capi, shapePath);
            Dictionary<CookingRecipeIngredient, int> usedIngredQuantities = new Dictionary<CookingRecipeIngredient, int>();

            if (rotten)
            {
                capi!.Tesselator.TesselateShape(
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
                    CookingRecipeIngredient? ingred = recipe.GetIngrendientFor(
                        contentStacks[i],
                        usedIngredQuantities.Where(val => val.Key.MaxQuantity <= val.Value).Select(val => val.Key).ToArray()
                    );

                    if (ingred == null)
                    {
                        ingred = recipe.GetIngrendientFor(contentStacks[i]);
                    }
                    else
                    {
                        usedIngredQuantities.TryGetValue(ingred, out int cnt);
                        cnt++;
                        usedIngredQuantities[ingred] = cnt;
                    }

                    if (ingred == null) continue;


                    string[]? selectiveElements = null;

                    if (ingred.GetMatchingStack(contentStacks[i]) is not CookingRecipeStack recipestack) continue;

                    if (recipestack.ShapeElement != null) selectiveElements = [recipestack.ShapeElement];
                    texSource.customTextureMapping = recipestack.TextureMapping;

                    if (drawnMeshes.Contains(recipestack.ShapeElement + recipestack.TextureMapping)) continue;
                    drawnMeshes.Add(recipestack.ShapeElement + recipestack.TextureMapping);

                    capi!.Tesselator.TesselateShape(
                        "mealpart", shape, out MeshData meshpart, texSource,
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

            if (capi.ObjectCache.TryGetValue("cookedMeshRefs", out object? obj) && obj is Dictionary<int, MultiTextureMeshRef> meshrefs)
            {
                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove("cookedMeshRefs");
            }
        }

        public int GetMealHashCode(ItemStack stack, Vec3f? translate = null, string extraKey = "")
        {
            if ((stack.Block as BlockContainer)?.GetContents(capi!.World, stack) is not ItemStack?[] contentStacks) return 0;

            if (stack.Block is BlockPie)
            {
                extraKey += "ct" + (BlockPie.GetTopCrustType(stack) ?? "full") + "-bl" + stack.Attributes.GetAsInt("bakeLevel", 0) + "-ps" + stack.Attributes.GetAsInt("pieSize");
            }

            return GetMealHashCode(stack.Block, contentStacks, translate, extraKey);
        }

        protected int GetMealHashCode(Block block, ItemStack?[] contentStacks, Vec3f? translate = null, string? extraKey = null)
        {
            string shapestring = block.Shape.ToString() + block.Code.ToShortString();
            if (translate != null) shapestring += translate.X + "/" + translate.Y + "/" + translate.Z;

            string contentstring = "";
            for (int i = 0; i < contentStacks.Length; i++)
            {
                if (contentStacks[i] == null) continue;

                if (contentStacks[i]!.Collectible.Code.Path == "rot")
                {
                    return (shapestring + "rotten").GetHashCode();
                }

                contentstring += contentStacks[i]!.Collectible.Code.ToShortString();
            }

            return (shapestring + contentstring + extraKey).GetHashCode();
        }


    }
}
