using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// A definition for a top crust type, including its code and shape element.
    /// </summary>
    /// <example language="json">
    /// {
    ///     "code": "full",
    ///     "shapeElement": "origin/base/top crust full/*"
    /// }
    /// </example>
    [DocumentAsJson]
    public class PieTopCrustType
    {
        public required string Code;
        public required string ShapeElement;
    }

    /// <summary>
    /// GetContents() must always return an ItemStack[] of length 6. Unused or empty slots are represented as null.
    ///
    /// [0]: Base dough (should never be null)
    /// [1-4]: Filling
    /// [5]: Crust dough or topping
    /// </summary>
    public class BlockPie : BlockMeal, IBakeableCallback, IShelvable
    {
        /// <summary>
        /// The pie's cooking stage; "raw", "partbaked", "perfect", or "charred"
        /// </summary>
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
        public DisplayableAttributes? GetDisplayableProps(ItemSlot slot, string displayType)
        {
            string sliceType = "displayable" + (slot.Itemstack?.Attributes.GetAsInt("pieSize") switch
            {
                1 => "Quarter",
                2 => "Half",
                _ => "Whole"
            });

            return Attributes?[sliceType][displayType].AsObject<DisplayableAttributes>(null);
        }

        MealMeshCache? meshCache;

        WorldInteraction[]? interactions;

        public static PieTopCrustType[] TopCrustTypes = null!;
        public static string GetRandomCrustType(IWorldAccessor world) => TopCrustTypes[world.Rand.Next(TopCrustTypes.Length)].Code;

        protected static HashSet<string> reportedMissingPieProps = [];

        /// <summary>
        /// Report that an object in a pie is missing its InPieProperties.
        /// Use this to prevent repeated error spam.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        /// <param name="code">The collectible code.</param>
        /// <returns>true if the element is added to the HashSet object; false if the element is already present.</returns>
        public static bool ReportMissingPieProps(ILogger logger, string code)
        {
            if (!reportedMissingPieProps.Contains(code))
            {
                logger.Error($"{code} is already in a pie, but it no longer has inPieProperties. It's still edible, but some things will break.");
            }

            return reportedMissingPieProps.Add(code);
        }


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            TopCrustTypes ??= api.Assets.Get("config/pietopcrusttypes.json").ToObject<PieTopCrustType[]>();

            InteractionHelpYOffset = 0.375f;

            displayContentsInfo = false;

            PlacedPriorityInteract = true;

            if (api is not ICoreClientAPI capi) return;

            meshCache ??= capi.ModLoader.GetModSystem<MealMeshCache>() ?? throw new Exception("Expected the meal mesh cache mod system to be available");

            interactions = ObjectCacheUtil.GetOrCreate(api, "pieInteractions-", () =>
            {
                var knifeStacks = ObjectCacheUtil.GetToolStacks(api, EnumTool.Knife);
                List<ItemStack> doughStacks = [];
                List<ItemStack> fillStacks = [];
                List<ItemStack> toppingStacks = [];

                if (fillStacks.Count == 0 && doughStacks.Count == 0)
                {
                    foreach (var obj in api.World.Collectibles)
                    {
                        InPieProperties? pieProps = InPieProperties.ReadFrom(obj, out string? piePropsErr);
                        if (pieProps == null)
                        {
                            if (piePropsErr != null) api.World.Logger.Error(piePropsErr);
                            continue;
                        }

                        switch (pieProps.PartType)
                        {
                            case EnumPiePartType.Filling:
                                fillStacks.Add(new ItemStack(obj, pieProps.ItemsPerPortion()));
                                break;
                            case EnumPiePartType.Topping:
                                toppingStacks.Add(new ItemStack(obj, pieProps.ItemsPerPortion()));
                                break;
                            case EnumPiePartType.Crust:
                                doughStacks.Add(new ItemStack(obj, pieProps.ItemsPerPortion()));
                                toppingStacks.Add(new ItemStack(obj, pieProps.ItemsPerPortion()));
                                break;
                        }

                        if (pieProps.PartType is not EnumPiePartType.Filling and not EnumPiePartType.Topping) continue;

                        int nonFoodCatCodes = pieProps.MixingCodes.Where(code => !code.Equals(pieProps.FoodCategory.ToString(), StringComparison.OrdinalIgnoreCase)).ToArray().Length;
                        if (nonFoodCatCodes == 0 && pieProps.FoodCategory == EnumFoodCategory.NoNutrition)
                        {
                            api.World.Logger.Error($"InPieProperties for filling {obj.Code} has no mixing codes and food category NoNutrition. It cannot be added to pies! See the documentation.");
                        }

                        if (!pieProps.AllowMixing && pieProps.PartType == EnumPiePartType.Filling && nonFoodCatCodes > 0)
                        {
                            api.World.Logger.Error($"InPieProperties for filling {obj.Code} has explicit mixingCodes, but allowMixing is disabled. Mixing codes will be ignored. Don't do this intentionally. Allow mixing or remove the mixing codes to suppress this error.");
                        }
                    }
                }

                return new WorldInteraction[]
                {
                    new() {
                        ActionLangCode = "blockhelp-pie-cut",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = knifeStacks,
                        GetMatchingStacks = (wi, bs, _) => {
                            if (capi.World.BlockAccessor.GetBlockEntity(bs.Position) is not BlockEntityPie pie) return null;
                            return pie.State != "raw" && pie.SlicesLeft > 1 ? wi.Itemstacks : null;
                        }
                    },
                    new() {
                        ActionLangCode = "blockhelp-pie-addfilling",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = fillStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, _) => {
                            if (capi.World.BlockAccessor.GetBlockEntity(bs.Position) is not BlockEntityPie pie) return null;
                            return pie.State == "raw" && !pie.HasAllFilling ? wi.Itemstacks.Where(pie.CanAddIngredient).ToArray() : null;
                        }
                    },
                    new() {
                        ActionLangCode = "blockhelp-pie-addcrustortopping",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = toppingStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, _) => {
                            if (capi.World.BlockAccessor.GetBlockEntity(bs.Position) is not BlockEntityPie pie) return null;
                            return pie.State == "raw" && pie.HasAllFilling && pie.ToppingType == null ? wi.Itemstacks.Where(pie.CanAddIngredient).ToArray() : null;
                        }
                    },
                    new() {
                        ActionLangCode = "blockhelp-pie-changecruststyle",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = [..knifeStacks, ..doughStacks],
                        GetMatchingStacks = (wi, bs, _) => {
                            if (capi.World.BlockAccessor.GetBlockEntity(bs.Position) is not BlockEntityPie pie) return null;
                            return pie.State == "raw" && pie.ToppingType == EnumPiePartType.Crust ? wi.Itemstacks : null;
                        }
                    }
                };
            });
        }



        public override bool CanEat(ItemSlot slot, EntityAgent byEntity)
        {
            return slot.Itemstack?.Attributes?.GetAsInt("pieSize") == 1
                   && State != "raw"
                   && base.CanEat(slot, byEntity);
        }

        public override bool OnContainedInteractStart(BlockEntity be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            var tool = byPlayer.InventoryManager.ActiveTool;
            var hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if ((tool == EnumTool.Knife || tool == EnumTool.Sword) && State != "raw")
            {
                var pieStack = slot.Itemstack;
                if (api.Side == EnumAppSide.Client || pieStack == null || pieStack.Attributes.GetAsInt("pieSize") <= 1) return true;

                var sliceStack = TakeSlice(ref pieStack);
                if (sliceStack == null) return true;

                hotbarSlot?.Itemstack?.Collectible.DamageItem(byPlayer.Entity.World, byPlayer.Entity, hotbarSlot);
                if (!byPlayer.InventoryManager.TryGiveItemstack(sliceStack))
                {
                    api.World.SpawnItemEntity(sliceStack, be.Pos);
                }

                api.World.Logger.Audit("{0} Took 1x{1} slice from Pie in {2} at {3}.",
                    byPlayer.PlayerName,
                    sliceStack.Collectible.Code,
                    be.Block.Code,
                    be.Pos
                );

                slot.Itemstack = pieStack;
                slot.MarkDirty();
                (be as BlockEntityDisplay)?.MarkMeshesDirty();
                be.MarkDirty(true);

                return true;
            }

            return base.OnContainedInteractStart(be, slot, byPlayer, blockSel);
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

            renderinfo.ModelRef = meshCache!.GetOrCreatePieMeshRef(itemstack);
        }


        public override MeshData? GenMesh(ItemSlot slot, ITextureAtlasAPI targetAtlas, BlockPos? atBlockPos = null)
        {
            return meshCache!.GetPieMesh(slot.Itemstack);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            var pieStack = (world.BlockAccessor.GetBlockEntity(pos) as BlockEntityPie)?.Inventory[0]?.Itemstack;
            return pieStack != null ? pieStack.Clone() : base.OnPickBlock(world, pos);
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
            InPieProperties? pieProps = InPieProperties.ReadFrom(hotbarSlot?.Itemstack);

            if (pieProps == null || pieProps.PartType != EnumPiePartType.Crust || pieProps.IsLiquid) return;

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
            ItemStack?[] cStacks = GetContents(api.World, itemStack);
            if (cStacks.Length <= 1 || cStacks[1] == null) return Lang.Get("pie-empty");

            // Use null-forgiving in this method in case the pie is malformed,
            // e.g. an ingredient lost its pie props. Report it because its
            // a content error.
            for (int i = 0; i < 6; i++)
            {
                if (cStacks[i] != null && InPieProperties.ReadFrom(cStacks[i]) == null)
                {
                    ReportMissingPieProps(api.Logger, cStacks[i]!.Collectible.Code);
                }
            }

            bool singleIngredient = true;
            string[] mixCodes = [..InPieProperties.ReadFrom(cStacks[1])?.MixingCodes ?? []];
            for (int i = 2; i < cStacks.Length - 1; i++)
            {
                if (cStacks[i] == null) continue;

                singleIngredient &= cStacks[i]?.Equals(api.World, cStacks[1], GlobalConstants.IgnoredStackAttributes) ?? true;
                mixCodes = InPieProperties.GetSharedMixingCodes(cStacks[i], mixCodes);

                if (!singleIngredient && mixCodes.Length == 0) break;
            }

            string state = Variant["state"];

            string mixCode = mixCodes.FirstOrDefault() ?? "missing";

            if (!singleIngredient && mixCode == "missing")
            {
                api.Logger.Error($"Pie does not have any valid mixing codes! They were likely removed from one of the ingredients: [\n    {string.Join("\n    ", cStacks)}\n]");
            }

            string pieName = Lang.Get(singleIngredient
                ? "pie-single-" + cStacks[1]!.Collectible.Code.ToShortString() + "-" + state
                : "pie-mixed-" + mixCode + "-" + state);

            if (cStacks[5] != null && InPieProperties.ReadFrom(cStacks[5])?.PartType != EnumPiePartType.Crust)
            {
                pieName = Lang.Get("meal-topping-ingredient-format", cStacks[5]?.Collectible.GetHeldItemName(cStacks[5]), pieName.ToLowerInvariant());
            }

            return pieName;
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            if (inSlot.Itemstack is not ItemStack pieStack) return;

            int pieSize = pieStack.Attributes.GetAsInt("pieSize");
            float servingsLeft = GetQuantityServings(world, pieStack);
            if (pieStack.Attributes?.HasAttribute("quantityServings") == false)
            {
                servingsLeft = pieStack.Attributes.GetAsInt("pieSize") / 4f;
            }

            dsc.AppendLine(pieSize == 1 ? Lang.Get("pie-slice-single", servingsLeft) : Lang.Get("pie-slices", pieSize));


            TransitionableProperties[] propsm = pieStack.Collectible.GetTransitionableProperties(world, pieStack, null);
            if (propsm?.Length > 0)
            {
                pieStack.Collectible.AppendPerishableInfoText(inSlot, dsc, world);
            }

            ItemStack[] stacks = GetContents(world, pieStack);

            var forEntity = (world as IClientWorldAccessor)?.Player?.Entity;


            float[] nmul = GetNutritionHealthMul(null, inSlot, forEntity);
            dsc.AppendLine(GetContentNutritionFacts(world, inSlot, stacks, null, true, servingsLeft * nmul[0], servingsLeft * nmul[1]));

            if (!withDebugInfo) return;

            dsc.AppendLine("<font color=\"#bbbbbb\">Id:" + Id + "</font>");
            dsc.AppendLine("<font color=\"#bbbbbb\">Code: " + Code + "</font>");
            if (world.Api is not ICoreClientAPI capi) return;

            if (capi.Input.KeyboardKeyStateRaw[(int)GlKeys.ShiftLeft])
            {
                dsc.AppendLine("<font color=\"#bbbbbb\">Attributes: " + inSlot.Itemstack.Attributes.ToJsonToken() + "</font>\n");
            }

            var sb = new StringBuilder(1024);
            sb.AppendJoin(", ", capi.CollectibleTagRegistry.SlowEnumerateTagNames(GetTags(inSlot.Itemstack)));
            if (sb.Length > 0) dsc.AppendLine($"<font color=\"#bbbbbb\">Tags: {sb}</font>");
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            var bepInv = (world.BlockAccessor.GetBlockEntity(pos) as BlockEntityPie)?.Inventory;
            if (bepInv == null || bepInv.Count < 1) return string.Empty;
            ItemSlot? pieSlot = bepInv[0];
            ItemStack? pieStack = pieSlot?.Itemstack;
            if (pieStack == null) return string.Empty;

            ItemStack[] stacks = GetContents(world, pieStack);
            StringBuilder sb = new();

            var propsm = pieStack.Collectible.GetTransitionableProperties(world, pieStack, null);
            if (propsm?.Length > 0) pieStack.Collectible.AppendPerishableInfoText(pieSlot, sb, world);

            float servingsLeft = GetQuantityServings(world, pieStack);
            if (pieStack.Attributes?.HasAttribute("quantityServings") == false)
            {
                servingsLeft = pieStack.Attributes.GetAsInt("pieSize") / 4f;
            }

            float[] nmul = GetNutritionHealthMul(pos, null, forPlayer.Entity);

            string str = sb.ToString();
            str += GetContentNutritionFacts(world, pieSlot!, stacks, null, true, nmul[0] * servingsLeft, nmul[1] * servingsLeft);


            return str;
        }

        // Skip over the meal transitioning code because only the pie itself spoils, not its contents.
        public override TransitionState[]? UpdateAndGetTransitionStates(IWorldAccessor world, ItemSlot inslot)
        {
            var contents = GetContents(world, inslot.Itemstack);
            if (contents != null)
            {
                // Make sure all our ingredients are time frozen
                foreach (var stack in contents) stack?.Attributes.SetBool("timeFrozen", true);
                SetContents(inslot.Itemstack, contents);
            }

            return UpdateAndGetTransitionStatesNative(world, inslot);
        }

        public override float[] GetNutritionHealthMul(BlockPos? pos, ItemSlot? slot, EntityAgent? forEntity)
        {
            float satLossMul = 1f;
            float healthLoss = 1f;

            if (slot == null && pos != null)
            {
                slot = (api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityPie)?.Inventory[0];
            }

            if (slot?.Itemstack?.Collectible is BlockPie)
            {
                float spoilState = UpdateAndGetTransitionState(api.World, slot, EnumTransitionType.Perish)?.TransitionLevel ?? 0;
                satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, slot.Itemstack, forEntity);
                healthLoss = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, slot.Itemstack, forEntity);
            }

            return [Attributes["nutritionMul"].AsFloat(1) * satLossMul, healthLoss];
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

        /// <summary>
        /// Take a slice from the pie in the itemstack and set pieSize and quantityServings.
        ///
        /// Once sliced, a pie is no longer bakeable.
        /// </summary>
        /// <param name="stack">The pie to take a slice from.</param>
        /// <returns>The new slice as an ItemStack.</returns>
        public static ItemStack? TakeSlice(ref ItemStack? stack)
        {
            if (stack == null) return null;
            ItemStack outStack = stack.Clone();

            int size = stack.Attributes.GetAsInt("pieSize");
            float servings = stack.Attributes.GetFloat("quantityServings");

            if (size <= 1 && !outStack.Attributes.HasAttribute("quantityServings"))
            {
                outStack.Attributes.SetFloat("quantityServings", 0.25f);
                stack = null;
            }
            else
            {
                size--;
                stack.Attributes.SetInt("pieSize", size);
                if (stack.Attributes.HasAttribute("quantityServings"))
                {
                    stack.Attributes.SetFloat("quantityServings", servings - 0.25f);
                }
                else stack.Attributes.SetFloat("quantityServings", size * 0.25f);

                outStack.Attributes.SetInt("pieSize", 1);
                outStack.Attributes.SetFloat("quantityServings", 0.25f);
            }

            if (size == 1) stack?.Attributes.SetBool("bakeable", false);
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
                outputSlot.Itemstack = TakeSlice(ref pieStack!);
            }
        }

        public override void OnConsumedByCrafting(ItemSlot[] allInputSlots, ItemSlot stackInSlot, IRecipeBase recipe, IRecipeIngredient fromIngredient, IPlayer byPlayer, int quantity)
        {
            if (recipe.Name != "pieslice")
            {
                base.OnConsumedByCrafting(allInputSlots, stackInSlot, recipe, fromIngredient, byPlayer, quantity);
                return;
            }

            var pieStack = stackInSlot.Itemstack?.Clone();
            TakeSlice(ref pieStack);
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
            List<(InPieProperties pieProps, ItemStack stack)> stacksWithProps = [];
            List<ItemStack> crusts = [];
            List<ItemStack> noMixFillings = [];
            List<ItemStack> alwaysMixToppings = [];
            Dictionary<string, List<ItemStack>> mixedFillings = [];
            Dictionary<string, List<ItemStack>> toppingsByCode = [];

            decimal portionSizeLitres = 0.01m;
            foreach (var stack in allStacks)
            {
                InPieProperties? pieProps = InPieProperties.ReadFrom(stack);
                if (pieProps == null) continue;
                stacksWithProps.Add((pieProps, stack.Clone()));
                while (portionSizeLitres > 1 / (decimal)pieProps.ItemsPerLitre)
                {
                    portionSizeLitres /= 10;
                }
            }

            foreach ((var pieProps, var stack) in stacksWithProps)
            {
                stack.StackSize = pieProps.ItemsPerPortion();
                if (pieProps.IsLiquid)
                {
                    stack.StackSize *= (int)((1 / (decimal)pieProps.ItemsPerLitre) / portionSizeLitres);
                }

                switch (pieProps.PartType)
                {
                    case EnumPiePartType.Crust:
                        crusts.Add(stack);
                        break;
                    case EnumPiePartType.Filling:
                        if (pieProps.AllowMixing)
                        {
                            foreach (string mixCode in pieProps.MixingCodes)
                            {
                                if (!mixedFillings.TryGetValue(mixCode, out var fillings))
                                {
                                    mixedFillings[mixCode] = fillings = [];
                                }
                                fillings.Add(stack);
                            }
                            break;
                        }

                        noMixFillings.Add(stack);
                        break;
                    case EnumPiePartType.Topping:
                        if (!pieProps.AllowMixing)
                        {
                            foreach (string mixCode in pieProps.MixingCodes)
                            {
                                if (!toppingsByCode.TryGetValue(mixCode, out var toppings))
                                {
                                    toppingsByCode[mixCode] = toppings = [];
                                }

                                toppings.Add(stack);
                            }
                            break;
                        }

                        alwaysMixToppings.Add(stack);
                        break;
                }
            }

            return
            [
                .. mixedFillings.Select(e => createRecipe("mixed-" + e.Key, crusts, [.. e.Value], [..alwaysMixToppings, ..toppingsByCode.Where(t => t.Key == e.Key).SelectMany(t => t.Value)], (float)portionSizeLitres)),
                .. noMixFillings.Select(stack => createRecipe("single-" + stack.Collectible.Code.ToShortString(), crusts, [stack], [..alwaysMixToppings, ..toppingsByCode.Where(t => InPieProperties.HasMixingCode(stack, t.Key)).SelectMany(t => t.Value)], (float)portionSizeLitres))
            ];
        }

        static CookingRecipeStack createCookingRecipeStack(ItemStack stack)
        {
            return new()
            {
                Code = stack.Collectible.Code,
                Type = stack.Collectible.ItemClass,
                StackSize = stack.StackSize,
                ResolvedItemStack = stack.Clone()
            };
        }

        private static CookingRecipe createRecipe(string code, List<ItemStack> crusts, List<ItemStack> fillings, List<ItemStack> toppings, float portionSizeLitres)
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
                        ValidStacks = [.. crusts.Select(createCookingRecipeStack)]
                    },
                    new ()
                    {
                        Code = "filling",
                        TypeName = "piefilling",
                        MinQuantity = 4,
                        MaxQuantity = 4,
                        PortionSizeLitres = portionSizeLitres,
                        ValidStacks = [.. fillings.Select(createCookingRecipeStack)]
                    },
                    new ()
                    {
                        Code = "crust",
                        TypeName = "topcrust",
                        MinQuantity = 0,
                        MaxQuantity = 1,
                        ValidStacks = [.. crusts.Select(createCookingRecipeStack)]
                    },
                    new ()
                    {
                        Code = "topping",
                        TypeName = "topping",
                        MinQuantity = 0,
                        MaxQuantity = 1,
                        PortionSizeLitres = portionSizeLitres,
                        ValidStacks = [.. toppings.Select(createCookingRecipeStack)]
                    }
                ],
                PerishableProps = new()
            };
        }

        private static void AddRandomIngredients(IWorldAccessor world, ref List<ItemStack?> pie, CookingRecipe recipe, string ingredientCode, ref Dictionary<CookingRecipeIngredient, List<ItemStack?>> valIngStacks, ref CookingRecipeIngredient? requestedIngredient, ItemStack? requestedIngredientStack)
        {
            if (pie.Count >= 6) return;
            (CookingRecipeIngredient ingredient, List<ItemStack?> validStacks) = valIngStacks.FirstOrDefault(entry => entry.Key.Code == ingredientCode);

            // Try to fulfill the ingredient request
            if (ingredient.Code == requestedIngredient?.Code)
            {
                var stack = validStacks.First(validStack => validStack?.Collectible.Code == requestedIngredientStack?.Collectible.Code);
                if (stack != null)
                {
                    pie.Add(stack.Clone());

                    ingredient.MinQuantity--;
                    ingredient.MaxQuantity--;
                }

                requestedIngredient = null;

                // Without this, we could add a requested dough/topping and then another, which breaks the pie.
                if (ingredient.MaxQuantity <= 0) return;
            }
            else if (ingredientCode == "crust" && requestedIngredient?.Code == "topping")
            {
                return; // We want to skip adding a crust if the requested ingredient is a topping
            }

            // Only fillings need the code below here for filtering, so we skip the list copying for crusts and toppings.
            if (ingredientCode != "filling")
            {
                var randStack = validStacks.ElementAtOrDefault(world.Rand.Next(validStacks.Count));
                if (randStack == null) return;
                pie.Add(randStack.Clone());
                return;
            }

            List<ItemStack?> filteredValidStacks = validStacks;
            string recipeCode = recipe.Code?.Split("-").ElementAtOrDefault(1) ?? "";
            while (ingredient.MinQuantity > 0)
            {
                if (filteredValidStacks.Count > 0)
                {
                    ItemStack? stack = filteredValidStacks[world.Rand.Next(filteredValidStacks.Count)]?.Clone();
                    // Get the list of codes for this ingredient that can be filtered out
                    string[] ingredientCodes = InPieProperties.ReadFrom(stack)!.MixingCodes.Where(code => code != recipeCode)?.ToArray() ?? [];
                    // Remove all the other ingredients that share any codes
                    filteredValidStacks = filteredValidStacks.Where(stack => !InPieProperties.ReadFrom(stack)!.MixingCodes.Intersect(ingredientCodes).Any()).ToList();
                    pie.Add(stack);
                }
                else pie.Add(validStacks[world.Rand.Next(validStacks.Count)]?.Clone());

                ingredient.MinQuantity--;
                ingredient.MaxQuantity--;
            }
        }

        // NOTE: This method can return an array of 6 nulls if there is a coding error, so check it before using SetContents to avoid accidentally removing contents entirely.
        public static ItemStack?[] GenerateRandomPie(ICoreAPI api, ref Dictionary<CookingRecipeIngredient, HashSet<ItemStack?>>? cachedValidStacksByIngredient, CookingRecipe recipe, ItemStack? ingredientStack = null)
        {
            if (recipe.Ingredients == null) return new ItemStack?[6];

            var validStacksByIngredient = cachedValidStacksByIngredient;

            if (validStacksByIngredient == null)
            {
                validStacksByIngredient = [];

                foreach (var ingredient in recipe.Ingredients)
                {
                    ingredient.Resolve(api.World, "handbook meal recipes");
                    HashSet<ItemStack?> ingredientStacks = [..ingredient.ValidStacks.Select(stack => stack.ResolvedItemstack)];

                    if (ingredient.MinQuantity <= 0) ingredientStacks.Add(null);

                    validStacksByIngredient.Add(ingredient.Clone(), ingredientStacks);
                }

                cachedValidStacksByIngredient = validStacksByIngredient;
            }

            var valIngStacks = new Dictionary<CookingRecipeIngredient, List<ItemStack?>>();
            foreach ((var ingred, var stacks) in validStacksByIngredient) valIngStacks.Add(ingred.Clone(), stacks.ToList());
            valIngStacks = valIngStacks.OrderBy(_ => api.World.Rand.Next()).ToDictionary(item => item.Key, item => item.Value);


            CookingRecipeIngredient? requestedIngredient = null;
            if (ingredientStack != null)
            {
                var validIngredients = recipe.Ingredients.Where(ingredient => ingredient.Matches(ingredientStack)).ToList();
                requestedIngredient = validIngredients[api.World.Rand.Next(validIngredients.Count)].Clone();
            }

            List<ItemStack?> randomPie = [];
            AddRandomIngredients(api.World, ref randomPie, recipe, "dough", ref valIngStacks, ref requestedIngredient, ingredientStack);
            AddRandomIngredients(api.World, ref randomPie, recipe, "filling", ref valIngStacks, ref requestedIngredient, ingredientStack);
            AddRandomIngredients(api.World, ref randomPie, recipe, "crust", ref valIngStacks, ref requestedIngredient, ingredientStack);
            AddRandomIngredients(api.World, ref randomPie, recipe, "topping", ref valIngStacks, ref requestedIngredient, ingredientStack);
            if (randomPie.Count == 5) randomPie.Add(null);

            if (randomPie.Count != 6)
            {
                api.Logger.Error($"Random pie [ {string.Join(", ", randomPie)} ] has a length that is not 6. Making it completely empty to prevent worse issues. This is a coding error, so please report it.");
                return [null, null, null, null, null, null];
            }

            if (!recipe.Matches([.. randomPie]))
            {
                api.Logger.Error($"Random pie [ {string.Join(", ", randomPie)} ] is invalid or does not match recipe {recipe.Code}. Making it completely empty to prevent worse issues. This is a coding error, so please report it.");
                return [null, null, null, null, null, null];
            }

            return [.. randomPie];
        }

        /// <summary>
        /// Cycle the crust type to the next index. Does nothing to toppings because they have a single shape.
        /// </summary>
        /// <param name="pieStack"></param>
        /// <returns></returns>
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
            if (TopCrustTypes.Any(type => type.Code.EqualsFast(topCrustType))) return topCrustType;
            topCrustType = topCrustType.ToInt() switch
            {
                1 => "square",
                2 => "diagonal",
                _ => "full"
            };

            pieStack.Attributes.SetString("topCrustType", topCrustType);

            return topCrustType;
        }

        public override string HandbookPageCodeForStack(IWorldAccessor world, ItemStack stack)
        {
            ItemStack?[] cStacks = GetContents(api.World, stack);
            if (cStacks.Length <= 1 || cStacks[1] == null) return "craftinginfo-pie";

            // Use null-forgiving in this method in case the pie is malformed,
            // e.g. an ingredient lost its pie props. Report it because its
            // a content error.
            for (int i = 0; i < 6; i++)
            {
                if (cStacks[i] != null && InPieProperties.ReadFrom(cStacks[i]) == null)
                {
                    ReportMissingPieProps(api.Logger, cStacks[i]!.Collectible.Code);
                }
            }

            bool singleIngredient = true;
            string[] mixCodes = [..InPieProperties.ReadFrom(cStacks[1])?.MixingCodes ?? []];
            for (int i = 2; i < cStacks.Length - 1; i++)
            {
                if (cStacks[i] == null) continue;

                singleIngredient &= cStacks[i]!.Equals(api.World, cStacks[1], GlobalConstants.IgnoredStackAttributes);
                mixCodes = InPieProperties.GetSharedMixingCodes(cStacks[i], mixCodes);

                if (!singleIngredient && mixCodes.Length == 0) break;
            }

            if (InPieProperties.ReadFrom(cStacks[5]) is InPieProperties toppingProps && toppingProps.PartType == EnumPiePartType.Topping)
            {
                singleIngredient = false;
                if (!toppingProps.AllowMixing) mixCodes = InPieProperties.GetSharedMixingCodes(cStacks[5], mixCodes);
            }

            string pieType = singleIngredient ? "single-" + cStacks[1]!.Collectible.Code.ToShortString()
                                              : "mixed-" + mixCodes.FirstOrDefault("unknown").ToLowerInvariant();

            return $"handbook-mealrecipe-{pieType}-pie";
        }
    }
}

