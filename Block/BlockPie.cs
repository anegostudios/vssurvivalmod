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
    public enum EnumTopCrustType
    {
        Full, Square, Diagonal
    }

    // Definition: GetContents() must always return a ItemStack[] of array length 6
    // [0] = crust
    // [1-4] = filling
    // [5] = topping (unused atm)
    public class BlockPie : BlockMeal, IBakeableCallback
    {
        public string State => Variant["state"];
        protected override bool PlacedBlockEating => false;

        MealMeshCache? ms;

        WorldInteraction[]? interactions;

        [MemberNotNull(nameof(ms), nameof(interactions))]
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            InteractionHelpYOffset = 0.375f;

            interactions = ObjectCacheUtil.GetOrCreate(api, "pieInteractions-", () =>
            {
                var knifeStacks = BlockUtil.GetKnifeStacks(api);
                List<ItemStack> fillStacks = new List<ItemStack>();
                List<ItemStack> doughStacks = new List<ItemStack>();

                if (fillStacks.Count == 0 && doughStacks.Count == 0)
                {
                    foreach (CollectibleObject obj in api.World.Collectibles)
                    {
                        if (obj is ItemDough)
                        {
                            doughStacks.Add(new ItemStack(obj, 2));
                        }

                        if (obj.Attributes?["inPieProperties"]?.AsObject<InPieProperties?>(null, obj.Code.Domain) != null && !(obj is ItemDough))
                        {
                            fillStacks.Add(new ItemStack(obj, 2));
                        }
                    }
                }

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-pie-cut",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = knifeStacks,
                        GetMatchingStacks = (wi, bs, es) => {
                            if (api.World.BlockAccessor.GetBlockEntity(bs.Position) is BlockEntityPie bec && bec.State is not null or "raw" && bec.SlicesLeft > 1)
                            {
                                return wi.Itemstacks;
                            }
                            return null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-pie-addfilling",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = fillStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            if (api.World.BlockAccessor.GetBlockEntity(bs.Position) is BlockEntityPie bec && bec.State == "raw" && !bec.HasAllFilling)
                            {
                                return wi.Itemstacks;
                            }
                            return null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-pie-addcrust",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = doughStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            if (api.World.BlockAccessor.GetBlockEntity(bs.Position) is BlockEntityPie bec && bec.State == "raw" && bec.HasAllFilling && !bec.HasCrust)
                            {
                                return wi.Itemstacks;
                            }
                            return null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-pie-changecruststyle",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = knifeStacks,
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            if (api.World.BlockAccessor.GetBlockEntity(bs.Position) is BlockEntityPie bec && bec.State == "raw" && bec.HasCrust)
                            {
                                return wi.Itemstacks;
                            }
                            return null;
                        }
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


        public override MeshData? GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos? atBlockPos = null)
        {
            return ms!.GetPieMesh(itemstack);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            if ((world.BlockAccessor.GetBlockEntity(pos) as BlockEntityPie)?.Inventory[0].Itemstack is ItemStack pieStack) return pieStack.Clone();

            return base.OnPickBlock(world, pos);
        }




        public void OnBaked(ItemStack oldStack, ItemStack newStack)
        {
            // Copy over properties and bake the contents
            newStack.Attributes["contents"] = oldStack.Attributes["contents"];
            newStack.Attributes.SetInt("pieSize", oldStack.Attributes.GetAsInt("pieSize"));
            newStack.Attributes.SetInt("topCrustType", oldStack.Attributes.GetAsInt("topCrustType"));
            newStack.Attributes.SetInt("bakeLevel", oldStack.Attributes.GetAsInt("bakeLevel", 0) + 1);

            ItemStack[] stacks = GetContents(api.World, newStack);


            // 1. Cook contents, if there is a cooked version of it
            for (int i = 0; i < stacks.Length; i++)
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
            }


            // Carry over and set perishable properties
            TransitionableProperties[] tprops = newStack.Collectible.GetTransitionableProperties(api.World, newStack, null);

            var perishProps = tprops.FirstOrDefault(p => p.Type == EnumTransitionType.Perish);
            perishProps?.TransitionedStack.Resolve(api.World, "pie perished stack");

            var inv = new DummyInventory(api, 4);
            inv[0].Itemstack = stacks[0];
            inv[1].Itemstack = stacks[1];
            inv[2].Itemstack = stacks[2];
            inv[3].Itemstack = stacks[3];

            if (perishProps != null) CarryOverFreshness(api, inv.Slots, stacks, perishProps);

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

            if (cstack == null) return Lang.Get("pie-empty");

            bool equal = true;
            for (int i = 2; equal && i < cStacks.Length - 1; i++)
            {
                if (cStacks[i] == null) continue;

                equal &= cstack.Equals(api.World, cStacks[i], GlobalConstants.IgnoredStackAttributes);
                cstack = cStacks[i];
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
            else
            {
                return Lang.Get("pie-mixed-" + FillingFoodCategory(cStacks[1]).ToString().ToLowerInvariant() + "-" + state);
            }
        }

        public static EnumFoodCategory FillingFoodCategory(ItemStack? stack)
        {
            return stack?.Collectible.NutritionProps?.FoodCategory
                   ?? stack?.ItemAttributes?["nutritionPropsWhenInMeal"]?.AsObject<FoodNutritionProperties>()?.FoodCategory
                   ?? EnumFoodCategory.Vegetable;
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
            if ((world.BlockAccessor.GetBlockEntity(pos) as BlockEntityPie)?.Inventory is not InventoryBase bepInv || bepInv.Count < 1 || bepInv[0].Itemstack is not ItemStack pieStack) return "";

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

        protected override TransitionState[]? UpdateAndGetTransitionStatesNative(IWorldAccessor world, ItemSlot inslot)
        {
            return base.UpdateAndGetTransitionStatesNative(world, inslot);
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
            if ((world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityPie)?.OnInteract(byPlayer) != true)
            {
                return base.OnBlockInteractStart(world, byPlayer, blockSel);
            }

            return true;
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            // Don't call eating stuff from blockmeal
            //base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
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
            List<ItemStack> doughs = new List<ItemStack>();
            Dictionary<EnumFoodCategory, List<ItemStack>> fillings = new ();
            List<ItemStack> crusts = new List<ItemStack>();
            List<ItemStack> noMixFillings = new List<ItemStack>();

            foreach (var stack in allStacks)
            {
                var pieProps = stack.ItemAttributes?["inPieProperties"]?.AsObject<InPieProperties>();

                if (pieProps?.PartType == EnumPiePartType.Crust) doughs.Add(stack);
                if (pieProps?.PartType == EnumPiePartType.Filling && pieProps.AllowMixing == true)
                {
                    var cat = FillingFoodCategory(stack);
                    if (cat is not EnumFoodCategory.NoNutrition and not EnumFoodCategory.Unknown)
                    {
                        if (fillings.ContainsKey(cat)) fillings[cat].Add(stack);
                        else fillings.Add(cat, [stack]);
                    }
                }
                if (pieProps?.PartType == EnumPiePartType.Crust && pieProps.AllowMixing == true) crusts.Add(stack);

                if (pieProps?.AllowMixing == false) noMixFillings.Add(stack);
            }

            return 
            [
                .. fillings.Select(entry => CreateRecipe(api.World, "mixed-" + entry.Key.ToString().ToLowerInvariant(), doughs, [.. entry.Value], crusts)),
                .. noMixFillings.Select(stack => CreateRecipe(api.World, "single-" + stack.Collectible.Code.Path, doughs, [stack], crusts))
            ];
        }

        private static CookingRecipe CreateRecipe(IWorldAccessor world, string code, List<ItemStack> doughs, List<ItemStack> fillings, List<ItemStack> crusts)
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

            Dictionary<CookingRecipeIngredient, HashSet<ItemStack?>>? validStacksByIngredient = cachedValidStacksByIngredient;

            if (cachedValidStacksByIngredient == null)
            {
                validStacksByIngredient = new();

                foreach (var ingredient in recipe.Ingredients)
                {
                    HashSet<ItemStack?> ingredientStacks = new();
                    List<AssetLocation> ingredientCodes = new();

                    foreach (var vstack in ingredient.ValidStacks.Select(stack => stack.ResolvedItemstack))
                    {
                        ItemStack stack = vstack.Clone();

                        ingredientStacks.Add(stack);
                    }

                    if (ingredient.MinQuantity <= 0) ingredientStacks.Add(null);

                    validStacksByIngredient.Add(ingredient.Clone(), ingredientStacks);
                }

                cachedValidStacksByIngredient = validStacksByIngredient;
            }

            if (validStacksByIngredient == null) return new ItemStack?[6];

            List<ItemStack?> randomMeal = new();

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

                randomMeal = new List<ItemStack?>();

                var ingredient = valIngStacks.Where(entry => entry.Key.Code == "dough").FirstOrDefault().Key;
                var validStacks = valIngStacks.Where(entry => entry.Key.Code == "dough").FirstOrDefault().Value;

                if (ingredient.Code == requestedIngredient?.Code)
                {
                    if (validStacks.First(stack => stack?.Collectible.Code == ingredientStack?.Collectible.Code) is ItemStack stack)
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

                ingredient = valIngStacks.Where(entry => entry.Key.Code == "filling").FirstOrDefault().Key;
                validStacks = valIngStacks.Where(entry => entry.Key.Code == "filling").FirstOrDefault().Value;

                if (ingredient.Code == requestedIngredient?.Code)
                {
                    if (validStacks.First(stack => stack?.Collectible.Code == ingredientStack?.Collectible.Code) is ItemStack stack)
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

                ingredient = valIngStacks.Where(entry => entry.Key.Code == "crust").FirstOrDefault().Key;
                validStacks = valIngStacks.Where(entry => entry.Key.Code == "crust").FirstOrDefault().Value;

                if (requestedIngredient != null)
                {
                    if (ingredient.Code == requestedIngredient?.Code)
                    {
                        if (validStacks.First(stack => stack?.Collectible.Code == ingredientStack?.Collectible.Code) is ItemStack stack)
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

