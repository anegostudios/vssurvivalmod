using System;
using System.Collections.Generic;
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

        MealMeshCache ms;

        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            InteractionHelpYOffset = 0.375f;

            interactions = ObjectCacheUtil.GetOrCreate(api, "pieInteractions-", () =>
            {
                List<ItemStack> knifeStacks = new List<ItemStack>();
                List<ItemStack> fillStacks = new List<ItemStack>();
                List<ItemStack> doughStacks = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj.Tool == EnumTool.Knife || obj.Tool == EnumTool.Sword)
                    {
                        knifeStacks.Add(new ItemStack(obj));
                    }
                    if (obj is ItemDough)
                    {
                        doughStacks.Add(new ItemStack(obj, 2));
                    }

                    var pieProps = obj.Attributes?["inPieProperties"]?.AsObject<InPieProperties>(null, obj.Code.Domain);
                    if (pieProps != null && !(obj is ItemDough))
                    {
                        fillStacks.Add(new ItemStack(obj, 2));
                    }
                }

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-pie-cut",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = knifeStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) => {
                            BlockEntityPie bec = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityPie;
                            if (bec?.Inventory[0]?.Itemstack != null && (bec.Inventory[0].Itemstack.Collectible as BlockPie).State != "raw" && bec.SlicesLeft > 1)
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
                            BlockEntityPie bec = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityPie;
                            if (bec?.Inventory[0]?.Itemstack != null && (bec.Inventory[0].Itemstack.Collectible as BlockPie).State == "raw" && !bec.HasAllFilling)
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
                            BlockEntityPie bec = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityPie;
                            if (bec?.Inventory[0]?.Itemstack != null && (bec.Inventory[0].Itemstack.Collectible as BlockPie).State == "raw" && bec.HasAllFilling && !bec.HasCrust)
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
                        Itemstacks = knifeStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            BlockEntityPie bec = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityPie;
                            if (bec?.Inventory[0]?.Itemstack != null && (bec.Inventory[0].Itemstack.Collectible as BlockPie).State == "raw" && bec.HasCrust)
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

            /*foreach (var val in api.World.Collectibles)
            {
                if (val.Attributes?["inPieProperties"].Exists == true)
                {
                    var pieprops = val.Attributes["inPieProperties"]?.AsObject<InPieProperties>();
                    if (pieprops.PartType == EnumPiePartType.Filling)
                    {
                        Console.WriteLine(string.Format("\"pie-single-{0}\": \"{1} pie\",", val.Code.Path, new ItemStack(val).GetName()));
                    }
                }
            }*/
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


        protected bool canEat(ItemSlot slot) {
            return
                slot.Itemstack.Attributes.GetInt("pieSize") == 1
                && State != "raw"
            ;
        }




        ModelTransform oneSliceTranformGui = new ModelTransform()
        {
            Origin = new Vec3f(0.375f, 0.1f, 0.375f),
            Scale = 2.82f,
            Rotation = new Vec3f(-27, 132, -5)
        }.EnsureDefaultValues();

        ModelTransform oneSliceTranformTp = new ModelTransform()
        {
            Translation = new Vec3f(-0.82f, -0.34f, -0.57f),
            Origin = new Vec3f(0.5f, 0.13f, 0.5f),
            Scale = 0.7f,
            Rotation = new Vec3f(-49, 29, -112)
        }.EnsureDefaultValues();


        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);

            if (itemstack.Attributes.GetInt("pieSize") == 1)
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

            renderinfo.ModelRef = ms.GetOrCreatePieMeshRef(itemstack);
        }


        public override MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos atBlockPos = null)
        {
            return ms.GetPieMesh(itemstack);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            BlockEntityPie bec = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityPie;
            if (bec?.Inventory[0]?.Itemstack != null) return bec.Inventory[0].Itemstack.Clone();

            return base.OnPickBlock(world, pos);
        }

        


        public void OnBaked(ItemStack oldStack, ItemStack newStack)
        {
            // Copy over properties and bake the contents
            newStack.Attributes["contents"] = oldStack.Attributes["contents"];
            newStack.Attributes.SetInt("pieSize", oldStack.Attributes.GetInt("pieSize"));
            newStack.Attributes.SetInt("topCrustType", oldStack.Attributes.GetInt("topCrustType"));
            newStack.Attributes.SetInt("bakeLevel", oldStack.Attributes.GetInt("bakeLevel", 0) + 1);

            ItemStack[] stacks = GetContents(api.World, newStack);


            // 1. Cook contents, if there is a cooked version of it
            for (int i = 0; i < stacks.Length; i++)
            {
                CombustibleProperties props = stacks[i]?.Collectible?.CombustibleProps;
                if (props != null)
                {
                    ItemStack cookedStack = props.SmeltedStack?.ResolvedItemstack.Clone();

                    TransitionState state = UpdateAndGetTransitionState(api.World, new DummySlot(cookedStack), EnumTransitionType.Perish);

                    if (state != null)
                    {
                        TransitionState smeltedState = cookedStack.Collectible.UpdateAndGetTransitionState(api.World, new DummySlot(cookedStack), EnumTransitionType.Perish);

                        float nowTransitionedHours = (state.TransitionedHours / (state.TransitionHours + state.FreshHours)) * 0.8f * (smeltedState.TransitionHours + smeltedState.FreshHours) - 1;

                        cookedStack.Collectible.SetTransitionState(cookedStack, EnumTransitionType.Perish, Math.Max(0, nowTransitionedHours));
                    }
                }
            }


            // Carry over and set perishable properties
            TransitionableProperties[] tprops = newStack.Collectible.GetTransitionableProperties(api.World, newStack, null);
            
            var perishProps = tprops.FirstOrDefault(p => p.Type == EnumTransitionType.Perish);
            perishProps.TransitionedStack.Resolve(api.World, "pie perished stack");

            var inv = new DummyInventory(api, 4);
            inv[0].Itemstack = stacks[0];
            inv[1].Itemstack = stacks[1];
            inv[2].Itemstack = stacks[2];
            inv[3].Itemstack = stacks[3];

            CarryOverFreshness(api, inv.Slots, stacks, perishProps);

            SetContents(newStack, stacks);
        }

        public void TryPlacePie(EntityAgent byEntity, BlockSelection blockSel)
        {
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            var pieprops = hotbarSlot.Itemstack.ItemAttributes["inPieProperties"]?.AsObject<InPieProperties>();
            if (pieprops == null || pieprops.PartType != EnumPiePartType.Crust) return;

            BlockPos abovePos = blockSel.Position.UpCopy();

            Block atBlock = api.World.BlockAccessor.GetBlock(abovePos);
            if (atBlock.Replaceable < 6000) return;

            api.World.BlockAccessor.SetBlock(Id, abovePos);

            BlockEntityPie bepie = api.World.BlockAccessor.GetBlockEntity(abovePos) as BlockEntityPie;
            bepie.OnPlaced(byPlayer);
        }


        
        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            BlockEntityPie bec = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityPie;
            if (bec?.Inventory[0]?.Itemstack != null) return GetHeldItemName(bec.Inventory[0].Itemstack);

            return base.GetPlacedBlockName(world, pos);
        }

        public override string GetHeldItemName(ItemStack itemStack)
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
            } else
            {
                EnumFoodCategory fillingFoodCat =
                    cStacks[1].Collectible.NutritionProps?.FoodCategory
                    ?? cStacks[1].ItemAttributes?["nutritionPropsWhenInMeal"]?.AsObject<FoodNutritionProperties>()?.FoodCategory
                    ?? EnumFoodCategory.Vegetable
                ;

                return Lang.Get("pie-mixed-" + fillingFoodCat.ToString().ToLowerInvariant() + "-" + state);
            }           
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            int pieSie = inSlot.Itemstack.Attributes.GetInt("pieSize");
            ItemStack pieStack = inSlot.Itemstack;
            float servingsLeft = GetQuantityServings(world, inSlot.Itemstack);
            if (!inSlot.Itemstack.Attributes.HasAttribute("quantityServings")) servingsLeft = 1;

            if (pieSie == 1)
            {
                dsc.AppendLine(Lang.Get("pie-slice-single", servingsLeft));
            } else
            {
                dsc.AppendLine(Lang.Get("pie-slices", pieSie));
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
            BlockEntityPie bep = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityPie;
            if (bep?.Inventory == null || bep.Inventory.Count < 1 || bep.Inventory.Empty) return "";

            BlockMeal mealblock = api.World.GetBlock(new AssetLocation("bowl-meal")) as BlockMeal;

            ItemStack pieStack = bep.Inventory[0].Itemstack;
            ItemStack[] stacks = GetContents(api.World, pieStack);
            StringBuilder sb = new StringBuilder();

            TransitionableProperties[] propsm = pieStack.Collectible.GetTransitionableProperties(api.World, pieStack, null);
            if (propsm != null && propsm.Length > 0)
            {
                pieStack.Collectible.AppendPerishableInfoText(bep.Inventory[0], sb, api.World);
            }

            float servingsLeft = GetQuantityServings(world, bep.Inventory[0].Itemstack);
            if (!bep.Inventory[0].Itemstack.Attributes.HasAttribute("quantityServings")) servingsLeft = bep.SlicesLeft / 4f;

            float[] nmul = GetNutritionHealthMul(pos, null, forPlayer.Entity);

            sb.AppendLine(mealblock.GetContentNutritionFacts(api.World, bep.Inventory[0], stacks, null, true, nmul[0] * servingsLeft, nmul[1] * servingsLeft));
            
            return sb.ToString();
        }

        protected override TransitionState[] UpdateAndGetTransitionStatesNative(IWorldAccessor world, ItemSlot inslot)
        {
            return base.UpdateAndGetTransitionStatesNative(world, inslot);
        }

        public override TransitionState UpdateAndGetTransitionState(IWorldAccessor world, ItemSlot inslot, EnumTransitionType type)
        {
            ItemStack[] cstacks = GetContents(world, inslot.Itemstack);
            UnspoilContents(world, cstacks);
            SetContents(inslot.Itemstack, cstacks);

            return base.UpdateAndGetTransitionState(world, inslot, type);
        }

        public override TransitionState[] UpdateAndGetTransitionStates(IWorldAccessor world, ItemSlot inslot)
        {
            ItemStack[] cstacks = GetContents(world, inslot.Itemstack);
            UnspoilContents(world, cstacks);
            SetContents(inslot.Itemstack, cstacks);

            
            return base.UpdateAndGetTransitionStatesNative(world, inslot);
        }


        public override string GetContentNutritionFacts(IWorldAccessor world, ItemSlot inSlotorFirstSlot, ItemStack[] contentStacks, EntityAgent forEntity, bool mulWithStacksize = false, float nutritionMul = 1, float healthMul = 1)
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

                attr.SetDouble("createdTotalHours", world.Calendar.TotalHours);
                attr.SetDouble("lastUpdatedTotalHours", world.Calendar.TotalHours);
                var transitionedHours = (attr["transitionedHours"] as FloatArrayAttribute)?.value;
                for (int j = 0; transitionedHours != null && j < transitionedHours.Length; j++)
                {
                    transitionedHours[j] = 0;
                }
            }
        }


        public override float[] GetNutritionHealthMul(BlockPos pos, ItemSlot slot, EntityAgent forEntity)
        {
            float satLossMul = 1f;

            if (slot == null && pos != null)
            {
                BlockEntityPie bep = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityPie;
                slot = bep.Inventory[0];
            }

            if (slot != null)
            {
                TransitionState state = slot.Itemstack.Collectible.UpdateAndGetTransitionState(api.World, slot, EnumTransitionType.Perish);
                float spoilState = state != null ? state.TransitionLevel : 0;
                satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, slot.Itemstack, forEntity);
            }

            return new float[] { Attributes["nutritionMul"].AsFloat(1) * satLossMul, satLossMul };
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityPie bep = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityPie;

            return bep.OnInteract(byPlayer);
        }

        public override int GetRandomContentColor(ICoreClientAPI capi, ItemStack[] stacks)
        {
            ItemStack[] cstacks = GetContents(capi.World, stacks[0]);
            if (cstacks.Length == 0) return 0;

            ItemStack rndStack = cstacks[capi.World.Rand.Next(stacks.Length)];
            return rndStack.Collectible.GetRandomColor(capi, rndStack);
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            var baseinteractions = base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
            baseinteractions = baseinteractions.RemoveEntry(1);

            var allinteractions = interactions.Append(baseinteractions);
            return allinteractions;
        }
    }
}

