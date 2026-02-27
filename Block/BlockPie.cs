using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class PieTopCrustType
    {
        public required string Code;
        public required string ShapeElement;
    }

    // Definition: GetContents() must always return a ItemStack[] of array length 6
    // [0] = crust
    // [1-4] = filling
    // [5] = topping (unused atm)
    public class BlockPie : BlockMeal, IBakeableCallback, IShelvable
    {
        public string State => Variant["state"];
        protected override bool PlacedBlockEating => false;

        public EnumShelvableLayout? GetShelvableType(ItemStack stack)
        {
            return stack.Attributes.GetAsInt("pieSize") switch
            {
                1 => EnumShelvableLayout.Quadrants,
                2 => EnumShelvableLayout.Halves,
                _ => EnumShelvableLayout.SingleCenter
            };
        }
        public ModelTransform? GetOnShelfTransform(ItemStack stack)
        {
            return GetShelvableType(stack) switch
            {
                EnumShelvableLayout.Quadrants => stack.Collectible.Attributes?["onShelfQuarterTransform"].AsObject<ModelTransform>(),
                EnumShelvableLayout.Halves => stack.Collectible.Attributes?["onShelfHalfTransform"].AsObject<ModelTransform>(),
                _ => stack.Collectible.Attributes?["onShelfFullTransform"].AsObject<ModelTransform>()
            };
        }

        MealMeshCache? ms;

        WorldInteraction[]? interactions;

        public static PieTopCrustType[] TopCrustTypes = null!;

        [MemberNotNull(nameof(ms), nameof(interactions))]
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            TopCrustTypes ??= api.Assets.Get("config/pietopcrusttypes.json").ToObject<PieTopCrustType[]>();

            InteractionHelpYOffset = 0.375f;

            interactions = ObjectCacheUtil.GetOrCreate(api, "pieInteractions-", () =>
            {
                var knifeStacks = ObjectCacheUtil.GetToolStacks(api, EnumTool.Knife);
                List<ItemStack> fillStacks = [];
                List<ItemStack> doughStacks = [];

                if (fillStacks.Count == 0 && doughStacks.Count == 0)
                {
                    foreach (var obj in api.World.Collectibles)
                    {
                        if (obj is ItemDough)
                        {
                            doughStacks.Add(new ItemStack(obj, 2));
                        }

                        if (obj.Attributes?["inPieProperties"].AsObject<InPieProperties?>(null, obj.Code.Domain)?.PartType == EnumPiePartType.Filling)
                        {
                            fillStacks.Add(new ItemStack(obj, 2));
                        }
                    }
                }

                return new WorldInteraction[]
                {
                    new() {
                        ActionLangCode = "blockhelp-pie-cut",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = knifeStacks,
                        GetMatchingStacks = (wi, bs, _) => GetBlockEntity<BlockEntityPie>(bs.Position) is { State: not null and not "raw", SlicesLeft: > 1 } ? wi.Itemstacks : null
                    },
                    new() {
                        ActionLangCode = "blockhelp-pie-addfilling",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = fillStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, _) => GetBlockEntity<BlockEntityPie>(bs.Position) is { State: "raw", HasAllFilling: false } ? wi.Itemstacks : null
                    },
                    new() {
                        ActionLangCode = "blockhelp-pie-addcrust",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = doughStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, _) => GetBlockEntity<BlockEntityPie>(bs.Position) is { State: "raw", HasAllFilling: true, HasCrust: false } ? wi.Itemstacks : null
                    },
                    new() {
                        ActionLangCode = "blockhelp-pie-changecruststyle",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = knifeStacks,
                        GetMatchingStacks = (wi, bs, _) => GetBlockEntity<BlockEntityPie>(bs.Position) is { State: "raw", HasCrust: true } ? wi.Itemstacks : null
                    }
                };
            });

            ms = api.ModLoader.GetModSystem<MealMeshCache>();

            displayContentsInfo = false;
        }


        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (!canEat(slot)) return;
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (!canEat(slot)) return false;

            return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (!canEat(slot)) return;

            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
        }


        protected bool canEat(ItemSlot slot)
        {
            return slot.Itemstack?.Attributes?.GetAsInt("pieSize") == 1 && State != "raw";
        }




        ModelTransform oneSliceTranformGui = new ModelTransform()
        {
            Origin = new FastVec3f(0.375f, 0.1f, 0.375f),
            Scale = 2.82f,
            Rotation = new FastVec3f(-27, 132, -5)
        }.EnsureDefaultValues();

        ModelTransform oneSliceTranformTp = new ModelTransform()
        {
            Translation = new FastVec3f(-0.82f, -0.34f, -0.57f),
            Origin = new FastVec3f(0.5f, 0.13f, 0.5f),
            Scale = 0.7f,
            Rotation = new FastVec3f(-49, 29, -112)
        }.EnsureDefaultValues();


        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);

            if (itemstack.Attributes.GetAsInt("pieSize") == 1)
            {
                if (target == EnumItemRenderTarget.Gui)
                {
                    renderinfo.Transform = oneSliceTranformGui;
                }
                if (target == EnumItemRenderTarget.HandTp)
                {
                    renderinfo.Transform = oneSliceTranformTp;
                }
            }

            renderinfo.ModelRef = ms!.GetOrCreatePieMeshRef(itemstack);
        }


        public override MeshData? GenMesh(ItemSlot slot, ITextureAtlasAPI targetAtlas, BlockPos? atBlockPos = null)
        {
            return ms!.GetPieMesh(slot.Itemstack);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            if ((world.BlockAccessor.GetBlockEntity(pos) as BlockEntityPie)?.Inventory[0].Itemstack is { } pieStack) return pieStack.Clone();

            return base.OnPickBlock(world, pos);
        }




        public void OnBaked(ItemStack oldStack, ItemStack newStack)
        {
            // Copy over properties and bake the contents
            newStack.Attributes["contents"] = oldStack.Attributes["contents"];
            newStack.Attributes.SetInt("pieSize", oldStack.Attributes.GetAsInt("pieSize"));
            newStack.Attributes.SetString("topCrustType", GetTopCrustType(oldStack));
            newStack.Attributes.SetInt("bakeLevel", oldStack.Attributes.GetAsInt("bakeLevel", 0) + 1);

            ItemStack[] stacks = GetContents(api.World, newStack);

            // 1. Cook contents, if there is a cooked version of it
            /*for (int i = 0; i < stacks.Length; i++)
            {
                if (stacks[i]?.Collectible.CombustibleProps.SmeltedStack?.ResolvedItemstack?.Clone() is ItemStack cookedStack)
                {
                    ItemSlot slot = new DummySlot(cookedStack);

                    if (UpdateAndGetTransitionState(api.World, slot, EnumTransitionType.Perish) is TransitionState state && cookedStack.Collectible.UpdateAndGetTransitionState(api.World, slot, EnumTransitionType.Perish) is TransitionState smeltedState)
                    {
                        float nowTransitionedHours = state.TransitionedHours / (state.TransitionHours + state.FreshHours) * 0.8f * (smeltedState.TransitionHours + smeltedState.FreshHours) - 1;

                        cookedStack.Collectible.SetTransitionState(cookedStack, EnumTransitionType.Perish, Math.Max(0, nowTransitionedHours));
                    }

                    stacks[i] = cookedStack;
                }
            }*/// This breaks pies by causing them to have cooked meat and stuff inside which the game doesn't know how to handle.

            SetContents(newStack, stacks);
        }

        public void TryPlacePie(EntityAgent byEntity, BlockSelection blockSel)
        {
            IPlayer? byPlayer = (byEntity as EntityPlayer)?.Player;
            ItemSlot? hotbarSlot = byPlayer?.InventoryManager.ActiveHotbarSlot;

            var pieprops = hotbarSlot?.Itemstack?.ItemAttributes["inPieProperties"]?.AsObject<InPieProperties>();
            if (pieprops?.PartType != EnumPiePartType.Crust) return;

            BlockPos abovePos = blockSel.Position.UpCopy();

            Block atBlock = api.World.BlockAccessor.GetBlock(abovePos);
            if (atBlock.Replaceable < 6000) return;

            api.World.BlockAccessor.SetBlock(Id, abovePos);

            (api.World.BlockAccessor.GetBlockEntity(abovePos) as BlockEntityPie)?.OnPlaced(byPlayer);
        }



        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            if ((world.BlockAccessor.GetBlockEntity(pos) as BlockEntityPie)?.Inventory[0]?.Itemstack is ItemStack pieStack) return GetHeldItemName(pieStack);

            return base.GetPlacedBlockName(world, pos);
        }

        public override string GetHeldItemName(ItemStack? itemStack)
        {
            ItemStack[] cStacks = GetContents(api.World, itemStack);
            if (cStacks.Length <= 1) return Lang.Get("pie-empty");

            ItemStack cstack = cStacks[1];
            var foodCats = cStacks.Select(FillingFoodCategory).ToArray();
            EnumFoodCategory foodCat = foodCats[1];

            if (cstack == null) return Lang.Get("pie-empty");

            bool equal = true;
            bool foodCatEquals = true;
            IEnumerable<string> mixCodes = cstack.ItemAttributes["inPieProperties"].AsObject<InPieProperties>()?.MixingCodes ?? [];
            for (int i = 2; (equal || foodCatEquals || mixCodes.Any()) && i < cStacks.Length - 1; i++)
            {
                if (cStacks[i] == null) continue;

                equal &= cstack.Equals(api.World, cStacks[i], GlobalConstants.IgnoredStackAttributes);
                foodCatEquals &= cStacks[i] == null || foodCats[i] == foodCat;
                mixCodes = cstack.ItemAttributes["inPieProperties"].AsObject<InPieProperties>()?.MixingCodes.Intersect(mixCodes) ?? [];

                cstack = cStacks[i];
                foodCat = foodCats[i];
            }

            string state = Variant["state"];

            if (MealMeshCache.ContentsRotten(cStacks))
            {
                return Lang.Get("pie-single-rotten");
            }

            if (equal)
            {
                return Lang.Get("pie-single-" + cstack.Collectible.Code.ToShortString() + "-" + state);
            }

            if (!foodCatEquals && mixCodes.Any())
            {
                return Lang.Get("pie-mixed-" + mixCodes.First() + "-" +state);
            }

            return Lang.Get("pie-mixed-" + FillingFoodCategory(cStacks[1]).ToString().ToLowerInvariant() + "-" + state);
        }

        public static EnumFoodCategory FillingFoodCategory(ItemStack? stack)
        {
            if (stack == null) return EnumFoodCategory.Vegetable;
            EnumFoodCategory? category = stack.ItemAttributes["inPieProperties"].AsObject<InPieProperties>()?.FoodCategory;

            if (category == null)
            {
                var liquidProps = BlockLiquidContainerBase.GetContainableProps(stack);
                if (liquidProps != null)
                {
                    category = liquidProps.NutritionPropsPerLitreWhenInMeal?.FoodCategory
                            ?? liquidProps.NutritionPropsPerLitre?.FoodCategory;
                }
            }
            category ??= stack.ItemAttributes?["nutritionPropsWhenInMeal"]?.AsObject<FoodNutritionProperties>()?.FoodCategory
                      ?? stack.Collectible.GetNutritionProperties(null, stack, null)?.FoodCategory;

            return category ?? EnumFoodCategory.Vegetable;
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            if (inSlot.Itemstack is not ItemStack pieStack) return;

            int pieSize = pieStack.Attributes.GetAsInt("pieSize");
            float servingsLeft = GetQuantityServings(world, pieStack);
            if (!pieStack.Attributes.HasAttribute("quantityServings")) servingsLeft = 1;

            if (pieSize == 1)
            {
                dsc.AppendLine(Lang.Get("pie-slice-single", servingsLeft));
            }
            else
            {
                dsc.AppendLine(Lang.Get("pie-slices", pieSize));
            }


            TransitionableProperties[] propsm = pieStack.Collectible.GetTransitionableProperties(api.World, pieStack, null);
            if (propsm != null && propsm.Length > 0)
            {
                pieStack.Collectible.AppendPerishableInfoText(inSlot, dsc, api.World);
            }

            ItemStack[] stacks = GetContents(api.World, pieStack);

            var forEntity = (world as IClientWorldAccessor)?.Player?.Entity;


            float[] nmul = GetNutritionHealthMul(null, inSlot, forEntity);
            dsc.AppendLine(GetContentNutritionFacts(api.World, inSlot, stacks, null, true, servingsLeft * nmul[0], servingsLeft * nmul[1]));
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            if ((world.BlockAccessor.GetBlockEntity(pos) as BlockEntityPie)?.Inventory is not { } bepInv || bepInv.Count < 1 || bepInv[0].Itemstack is not { } pieStack) return "";

            ItemStack[] stacks = GetContents(api.World, pieStack);
            StringBuilder sb = new StringBuilder();

            TransitionableProperties[] propsm = pieStack.Collectible.GetTransitionableProperties(api.World, pieStack, null);
            if (propsm != null && propsm.Length > 0)
            {
                pieStack.Collectible.AppendPerishableInfoText(bepInv[0], sb, api.World);
            }

            float servingsLeft = GetQuantityServings(world, pieStack);
            if (pieStack.Attributes?.HasAttribute("quantityServings") == false) servingsLeft = pieStack.Attributes.GetAsInt("pieSize") / 4f;

            float[] nmul = GetNutritionHealthMul(pos, null, forPlayer.Entity);

            string str = sb.ToString();
            str += GetContentNutritionFacts(api.World, bepInv[0], stacks, null, true, nmul[0] * servingsLeft, nmul[1] * servingsLeft) ?? "";


            return str;
        }

        public override TransitionState? UpdateAndGetTransitionState(IWorldAccessor world, ItemSlot inslot, EnumTransitionType type)
        {
            ItemStack[] cstacks = GetContents(world, inslot.Itemstack);
            UnspoilContents(world, cstacks);
            SetContents(inslot.Itemstack, cstacks);

            return base.UpdateAndGetTransitionState(world, inslot, type);
        }

        public override TransitionState[]? UpdateAndGetTransitionStates(IWorldAccessor world, ItemSlot inslot)
        {
            ItemStack[] cstacks = GetContents(world, inslot.Itemstack);
            UnspoilContents(world, cstacks);
            SetContents(inslot.Itemstack, cstacks);


            return base.UpdateAndGetTransitionStatesNative(world, inslot);
        }


        public override string GetContentNutritionFacts(IWorldAccessor world, ItemSlot inSlotorFirstSlot, ItemStack[] contentStacks, EntityAgent? forEntity, bool mulWithStacksize = false, float nutritionMul = 1, float healthMul = 1)
        {
            UnspoilContents(world, contentStacks);

            return base.GetContentNutritionFacts(world, inSlotorFirstSlot, contentStacks, forEntity, mulWithStacksize, nutritionMul, healthMul);
        }


        protected void UnspoilContents(IWorldAccessor world, ItemStack[] cstacks)
        {
            // Dont spoil the pie contents, the pie itself has a spoilage timer. Semi hacky fix reset their spoil timers each update

            for (int i = 0; i < cstacks.Length; i++)
            {
                ItemStack cstack = cstacks[i];
                if (cstack == null) continue;

                if (!(cstack.Attributes["transitionstate"] is ITreeAttribute))
                {
                    cstack.Attributes["transitionstate"] = new TreeAttribute();
                }
                ITreeAttribute attr = (ITreeAttribute)cstack.Attributes["transitionstate"];

                if (attr.HasAttribute("createdTotalHours"))
                {
                    attr.SetDouble("createdTotalHours", world.Calendar.TotalHours);
                    attr.SetDouble("lastUpdatedTotalHours", world.Calendar.TotalHours);
                    var transitionedHours = (attr["transitionedHours"] as FloatArrayAttribute)?.value;
                    for (int j = 0; transitionedHours != null && j < transitionedHours.Length; j++)
                    {
                        transitionedHours[j] = 0;
                    }
                }
            }
        }


        public override float[] GetNutritionHealthMul(BlockPos? pos, ItemSlot? slot, EntityAgent? forEntity)
        {
            float satLossMul = 1f;

            if (slot == null && pos != null)
            {
                slot = (api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityPie)?.Inventory[0];
            }

            if (slot != null)
            {
                TransitionState? state = slot.Itemstack?.Collectible.UpdateAndGetTransitionState(api.World, slot, EnumTransitionType.Perish);
                float spoilState = state != null ? state.TransitionLevel : 0;
                satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, slot.Itemstack, forEntity);
            }

            return [Attributes["nutritionMul"].AsFloat(1) * satLossMul, satLossMul];
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            return GetBlockEntity<BlockEntityPie>(blockSel.Position)?.OnInteract(byPlayer) == true || base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            // Don't call eating stuff from blockmeal
            //base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
        }

        public static ItemStack? TakeSlice(ref ItemStack? stack)
        {
            if (stack?.Clone() is not { } outStack) return null;

            int size = stack.Attributes.GetAsInt("pieSize");
            float servings = stack.Attributes.GetFloat("quantityServings");

            if (size <= 1 && !outStack.Attributes.HasAttribute("quantityServings"))
            {
                outStack.Attributes.SetFloat("quantityServings", 0.25f);
                stack = null;
            }
            else
            {
                stack.Attributes.SetInt("pieSize", size - 1);
                if (stack.Attributes.HasAttribute("quantityServings"))
                {
                    stack.Attributes.SetFloat("quantityServings", servings - 0.25f);
                }

                outStack.Attributes.SetInt("pieSize", 1);
                outStack.Attributes.SetFloat("quantityServings", 0.25f);
            }

            outStack.Attributes.SetBool("bakeable", false);

            return outStack;
        }

        public override bool MatchesForCrafting(ItemStack inputStack, IRecipeBase gridRecipe, IRecipeIngredient ingredient)
        {
            if (gridRecipe.Name != "pieslice") return base.MatchesForCrafting(inputStack, gridRecipe, ingredient);

            return inputStack?.Collectible is BlockPie && inputStack.Attributes.GetAsInt("pieSize") > 1;
        }

        public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, IRecipeBase byRecipe)
        {
            if (byRecipe.Name != "pieslice")
            {
                base.OnCreatedByCrafting(allInputslots, outputSlot, byRecipe);
                return;
            }

            if (outputSlot.Itemstack == null) return;

            foreach (var slot in allInputslots)
            {
                if (slot.Itemstack?.Collectible is not BlockPie) continue;

                var pieStack = slot.Itemstack.Clone();
                outputSlot.Itemstack = TakeSlice(ref pieStack);
            }
        }

        public override void OnConsumedByCrafting(ItemSlot[] allInputSlots, ItemSlot stackInSlot, IRecipeBase recipe, IRecipeIngredient fromIngredient, IPlayer byPlayer, int quantity)
        {
            if (recipe.Name != "pieslice")
            {
                base.OnConsumedByCrafting(allInputSlots, stackInSlot, recipe, fromIngredient, byPlayer, quantity);
                return;
            }

            var pieStack = stackInSlot.Itemstack.Clone();
            TakeSlice(ref pieStack);
            if (pieStack?.Attributes.GetAsInt("pieSize") == 1)
            {
                pieStack.Attributes.SetFloat("quantityServings", 0.25f);
                pieStack.Attributes.SetBool("bakeable", false);
            }
            stackInSlot.Itemstack = pieStack;
        }

        public override int GetRandomContentColor(ICoreClientAPI capi, ItemStack[] stacks)
        {
            if (stacks.Length == 0) return 0x808080;
            ItemStack[] cstacks = GetContents(capi.World, stacks[0]);
            if (cstacks.Length == 0) return 0x808080;

            ItemStack rndStack = cstacks[capi.World.Rand.Next(stacks.Length)];
            return rndStack.Collectible.GetRandomColor(capi, rndStack);
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            var baseinteractions = base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
            baseinteractions = baseinteractions.RemoveAt(1);

            var allinteractions = interactions.Append(baseinteractions);
            return allinteractions;
        }

        public static List<CookingRecipe> GetHandbookRecipes(ICoreAPI api, ItemStack[] allStacks)
        {
            List<ItemStack> doughs = [];
            Dictionary<EnumFoodCategory, List<ItemStack>> categoryFillings = [];
            Dictionary<string, List<ItemStack>> mixedFillings = [];
            List<ItemStack> crusts = [];
            List<ItemStack> noMixFillings = [];

            foreach (var stack in allStacks)
            {
                var pieProps = stack.ItemAttributes?["inPieProperties"]?.AsObject<InPieProperties>();

                if (pieProps?.PartType == EnumPiePartType.Crust) doughs.Add(stack);
                if (pieProps is { PartType: EnumPiePartType.Filling, AllowMixing: true })
                {
                    foreach (var code in pieProps.MixingCodes)
                    {
                        if (mixedFillings.TryGetValue(code, out var value)) value.Add(stack);
                        else mixedFillings.Add(code, [stack]);
                    }

                    var cat = FillingFoodCategory(stack);
                    if (cat is not EnumFoodCategory.NoNutrition and not EnumFoodCategory.Unknown)
                    {
                        if (categoryFillings.TryGetValue(cat, out var value)) value.Add(stack);
                        else categoryFillings.Add(cat, [stack]);
                    }
                }
                if (pieProps is { PartType: EnumPiePartType.Crust, AllowMixing: true }) crusts.Add(stack);

                if (pieProps is { AllowMixing: false, MixingCodes.Length: 0 }) noMixFillings.Add(stack);
            }

            return
            [
                .. categoryFillings.Select(entry => CreateRecipe(api.World, "mixed-" + entry.Key.ToString().ToLowerInvariant(), doughs, [.. entry.Value], crusts)),
                .. mixedFillings.Select(entry => CreateRecipe(api.World, "mixed-" + entry.Key.ToLowerInvariant(), doughs, [.. entry.Value], crusts)),
                .. noMixFillings.Select(stack => CreateRecipe(api.World, "single-" + stack.Collectible.Code.ToShortString(), doughs, [stack], crusts))
            ];
        }

        private static CookingRecipe CreateRecipe(IWorldAccessor world, string code, List<ItemStack> doughs, List<ItemStack> fillings, List<ItemStack> crusts, bool mixedRecipe = false)
        {
            return new()
            {
                Code = code,
                Ingredients =
                [
                    new ()
                    {
                        Code = "dough",
                        TypeName = "bottomcrust",
                        MinQuantity = 1,
                        MaxQuantity = 1,
                        ValidStacks = [.. doughs.Select<ItemStack, CookingRecipeStack>(dough => new ()
                        {
                            Code = dough.Collectible.Code,
                            Type = dough.Collectible.ItemClass,
                            Quantity = 2,
                            ResolvedItemstack = dough.Clone()
                        })]
                    },
                    new ()
                    {
                        Code = "filling",
                        TypeName = "piefilling",
                        MinQuantity = 4,
                        MaxQuantity = 4,
                        ValidStacks = [.. fillings.Select<ItemStack, CookingRecipeStack>(filling => new ()
                        {
                            Code = filling.Collectible.Code,
                            Type = filling.Collectible.ItemClass,
                            Quantity = 2,
                            ResolvedItemstack = filling.Clone()
                        })]
                    },
                    new ()
                    {
                        Code = "crust",
                        TypeName = "topcrust",
                        MinQuantity = 0,
                        MaxQuantity = 1,
                        ValidStacks = [.. crusts.Select<ItemStack, CookingRecipeStack>(crust => new ()
                        {
                            Code = crust.Collectible.Code,
                            Type = crust.Collectible.ItemClass,
                            Quantity = 2,
                            ResolvedItemstack = crust.Clone()
                        })]
                    }
                ],
                PerishableProps = new()
            };
        }

        public static ItemStack?[] GenerateRandomPie(ICoreAPI api, ref Dictionary<CookingRecipeIngredient, HashSet<ItemStack?>>? cachedValidStacksByIngredient, CookingRecipe recipe, ItemStack? ingredientStack = null)
        {
            if (recipe.Ingredients == null) return new ItemStack?[6];

            var validStacksByIngredient = cachedValidStacksByIngredient;

            if (validStacksByIngredient == null)
            {
                validStacksByIngredient = [];

                foreach (var ingredient in recipe.Ingredients)
                {
                    HashSet<ItemStack?> ingredientStacks = [];

                    ingredient.Resolve(api.World, "handbook meal recipes");
                    foreach (var astack in ingredient.ValidStacks.Select(stack => stack.ResolvedItemstack))
                    {
                        if (ingredient.GetMatchingStack(astack) is not { } vstack) continue;

                        ItemStack stack = astack.Clone();
                        stack.StackSize = vstack.StackSize;

                        if (BlockLiquidContainerBase.GetContainableProps(stack) is { } props)
                        {
                            stack.StackSize *= (int)(props.ItemsPerLitre * ingredient.PortionSizeLitres);
                        }

                        ingredientStacks.Add(stack);
                    }

                    if (ingredient.MinQuantity <= 0) ingredientStacks.Add(null);

                    validStacksByIngredient.Add(ingredient.Clone(), ingredientStacks);
                }

                cachedValidStacksByIngredient = validStacksByIngredient;
            };

            if (validStacksByIngredient == null) return new ItemStack?[6];

            List<ItemStack?> randomMeal = [];

            while (!recipe.Matches(randomMeal.ToArray()))
            {
                var valIngStacks = new Dictionary<CookingRecipeIngredient, List<ItemStack?>>();
                foreach (var entry in validStacksByIngredient) valIngStacks.Add(entry.Key.Clone(), entry.Value.ToList());
                valIngStacks = valIngStacks.OrderBy(x => api.World.Rand.Next()).ToDictionary(item => item.Key, item => item.Value);

                CookingRecipeIngredient? requestedIngredient = null;
                if (ingredientStack != null)
                {
                    var validIngredients = recipe.Ingredients.Where(ingredient => ingredient.Matches(ingredientStack)).ToList();
                    requestedIngredient = validIngredients[api.World.Rand.Next(validIngredients.Count)].Clone();
                }

                randomMeal = [];

                var ingredient = valIngStacks.FirstOrDefault(entry => entry.Key.Code == "dough").Key;
                var validStacks = valIngStacks.FirstOrDefault(entry => entry.Key.Code == "dough").Value;

                if (ingredient.Code == requestedIngredient?.Code)
                {
                    if (validStacks.First(stack => stack?.Collectible.Code == ingredientStack?.Collectible.Code) is { } stack)
                    {
                        randomMeal.Add(stack.Clone());

                        ingredient.MinQuantity--;
                        ingredient.MaxQuantity--;
                    }
                    requestedIngredient = null;
                }

                while (ingredient.MinQuantity > 0)
                {
                    randomMeal.Add(validStacks[api.World.Rand.Next(validStacks.Count)]?.Clone());

                    ingredient.MinQuantity--;
                    ingredient.MaxQuantity--;
                }

                ingredient = valIngStacks.FirstOrDefault(entry => entry.Key.Code == "filling").Key;
                validStacks = valIngStacks.FirstOrDefault(entry => entry.Key.Code == "filling").Value;

                if (ingredient.Code == requestedIngredient?.Code)
                {
                    if (validStacks.First(stack => stack?.Collectible.Code == ingredientStack?.Collectible.Code) is { } stack)
                    {
                        randomMeal.Add(stack.Clone());

                        ingredient.MinQuantity--;
                        ingredient.MaxQuantity--;
                    }
                    requestedIngredient = null;
                }

                while (ingredient.MinQuantity > 0)
                {
                    randomMeal.Add(validStacks[api.World.Rand.Next(validStacks.Count)]?.Clone());

                    ingredient.MinQuantity--;
                    ingredient.MaxQuantity--;
                }

                ingredient = valIngStacks.FirstOrDefault(entry => entry.Key.Code == "crust").Key;
                validStacks = valIngStacks.FirstOrDefault(entry => entry.Key.Code == "crust").Value;

                if (requestedIngredient != null)
                {
                    if (ingredient.Code == requestedIngredient?.Code)
                    {
                        if (validStacks.First(stack => stack?.Collectible.Code == ingredientStack?.Collectible.Code) is { } stack)
                        {
                            randomMeal.Add(stack.Clone());
                            ingredient.MaxQuantity--;

                            requestedIngredient = null;
                        }
                    }
                }
                else if (ingredient.MaxQuantity > 0)
                {
                    var stack = validStacks[api.World.Rand.Next(validStacks.Count)];

                    if (stack != null)
                    {
                        randomMeal.Add(stack.Clone());
                        ingredient.MaxQuantity--;
                    }
                }

                while (randomMeal.Count < 6) randomMeal.Add(null);
            }

            return randomMeal.ToArray();
        }

        [return: NotNullIfNotNull(nameof(pieStack))]
        public static ItemStack? CycleTopCrustType(ItemStack? pieStack)
        {
            if (pieStack == null) return null;

            string topCrustType = GetTopCrustType(pieStack);

            pieStack.Attributes.SetString("topCrustType", TopCrustTypes[(TopCrustTypes.IndexOf(type => type.Code.EqualsFast(topCrustType)) + 1) % TopCrustTypes.Length].Code);
            return pieStack;
        }

        [return: NotNullIfNotNull(nameof(pieStack))]
        public static string? GetTopCrustType(ItemStack? pieStack)
        {
            if (pieStack == null) return null;

            string topCrustType = pieStack.Attributes.GetAsString("topCrustType", "full");
            if (!TopCrustTypes.Any(type => type.Code.EqualsFast(topCrustType)))
            {
                switch (topCrustType.ToInt())
                {
                    default:
                    case 0:
                        topCrustType = "full";
                        break;
                    case 1:
                        topCrustType = "square";
                        break;
                    case 2:
                        topCrustType = "diagonal";
                        break;
                }

                pieStack.Attributes.SetString("topCrustType", topCrustType);
            }

            return topCrustType;
        }

        public override string HandbookPageCodeForStack(IWorldAccessor world, ItemStack stack)
        {
            string? type = null;

            if (GetContents(world, stack) is ItemStack[] contents && contents.Length > 1)
            {
                if (contents[1]?.ItemAttributes?["inPieProperties"]?.AsObject<InPieProperties>()?.AllowMixing == false)
                {
                    type = "single-" + contents[1].Collectible.Code.ToShortString();
                }
                else type = "mixed-" + FillingFoodCategory(contents[1]).ToString().ToLowerInvariant();

                return "handbook-mealrecipe-" + type + "-pie";
            }
            else return "craftinginfo-pie";
        }
    }
}

