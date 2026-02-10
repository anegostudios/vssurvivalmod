using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public abstract class BlockLiquidContainerBase : BlockContainer, ILiquidSource, ILiquidSink
    {
        protected float capacityLitresFromAttributes = 10;
        protected float drinkPortionSizeFromAttributes = 1;
        public virtual float CapacityLitres => capacityLitresFromAttributes;
        public virtual float DrinkPortionSize => drinkPortionSizeFromAttributes;
        public virtual int ContainerSlotId => 0;

        public virtual float TransferSizeLitres => 1;

        public virtual bool CanDrinkFrom => Attributes["canDrinkFrom"].AsBool() == true;
        public virtual bool IsTopOpened => Attributes["isTopOpened"].AsBool() == true;
        public virtual bool AllowHeldLiquidTransfer => Attributes["allowHeldLiquidTransfer"].AsBool() == true;

        Dictionary<string, ItemStack[]> recipeLiquidContents = new Dictionary<string, ItemStack[]>();

        public override void OnHandbookRecipeRender(ICoreClientAPI capi, GridRecipe gridRecipe, ItemSlot dummyslot, double x, double y, double z, double size)
        {
            // 1.16.0: Fugly (but backwards compatible) hack: We temporarily store the ingredient index in an unused field of ItemSlot so that OnHandbookRecipeRender() has access to that number. Proper solution would be to alter the method signature to pass on this value.
            int rindex = dummyslot.BackgroundIcon.ToInt();
            var ingredient = gridRecipe.ResolvedIngredients[rindex];

            JsonObject rprops = ingredient.RecipeAttributes;
            if (rprops?.Exists != true || rprops?["requiresContent"].Exists != true) rprops = gridRecipe.Attributes?["liquidContainerProps"];

            if (rprops?.Exists != true)
            {
                base.OnHandbookRecipeRender(capi, gridRecipe, dummyslot, x, y, z, size);
                return;
            }

            string contentCode = rprops?["requiresContent"]?["code"]?.AsString() ?? gridRecipe.Attributes["liquidContainerProps"]["requiresContent"]["code"].AsString();
            string contentType = rprops?["requiresContent"]?["type"]?.AsString() ?? gridRecipe.Attributes["liquidContainerProps"]["requiresContent"]["type"].AsString();
            float litres = rprops?["requiresLitres"]?.AsFloat() ?? gridRecipe.Attributes["liquidContainerProps"]["requiresLitres"].AsFloat();

            string key = contentType + "-" + contentCode;
            if (!recipeLiquidContents.TryGetValue(key, out ItemStack[] stacks))
            {
                if (contentCode.Contains('*'))
                {
                    EnumItemClass contentClass = contentType == "block" ? EnumItemClass.Block : EnumItemClass.Item;
                    List<ItemStack> lstacks = new List<ItemStack>();
                    var loc = AssetLocation.Create(contentCode, Code.Domain);
                    foreach (var obj in api.World.Collectibles)
                    {
                        if (obj.ItemClass == contentClass && WildcardUtil.Match(loc, obj.Code))
                        {
                            var stack = new ItemStack(obj);
                            var props = GetContainableProps(stack);
                            if (props == null) continue;
                            stack.StackSize = (int)(props.ItemsPerLitre * litres);
                            lstacks.Add(stack);
                        }
                    }
                    stacks = lstacks.ToArray();
                }
                else
                {
                    recipeLiquidContents[key] = stacks = new ItemStack[1];

                    if (contentType == "item") stacks[0] = new ItemStack(capi.World.GetItem(new AssetLocation(contentCode)));
                    else stacks[0] = new ItemStack(capi.World.GetBlock(new AssetLocation(contentCode)));

                    var props = GetContainableProps(stacks[0]);
                    stacks[0].StackSize = (int)((props?.ItemsPerLitre ?? 1) * litres);
                }
            }

            ItemStack filledContainerStack = dummyslot.Itemstack.Clone();

            int index = (int)(capi.ElapsedMilliseconds / 1000) % stacks.Length;

            SetContent(filledContainerStack, stacks[index]);

            dummyslot.Itemstack = filledContainerStack;

            capi.Render.RenderItemstackToGui(
                dummyslot,
                x,
                y,
                z, (float)size * 0.58f, ColorUtil.WhiteArgb,
                true, false, true
            );
        }


        public virtual int GetContainerSlotId(BlockPos pos)
        {
            return ContainerSlotId;
        }
        public virtual int GetContainerSlotId(ItemStack containerStack)
        {
            return ContainerSlotId;
        }

        #region Interaction help
        public WorldInteraction[] interactions { get; protected set; }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (Attributes?["capacityLitres"].Exists == true)
            {
                capacityLitresFromAttributes = Attributes["capacityLitres"].AsInt(10);
            }
            else
            {
                var props = Attributes?["liquidContainerProps"]?.AsObject<LiquidTopOpenContainerProps>(null, Code.Domain);
                if (props != null)
                {
                    capacityLitresFromAttributes = props.CapacityLitres;
                }
            }

            if (Attributes?["drinkPortionSize"].Exists == true)
            {
                drinkPortionSizeFromAttributes = Attributes["drinkPortionSize"].AsInt(1);
            }
            else
            {
                var props = Attributes?["liquidContainerProps"]?.AsObject<LiquidTopOpenContainerProps>(null, Code.Domain);
                if (props != null)
                {
                    drinkPortionSizeFromAttributes = props.DrinkPortionSize;
                }
            }

            if (drinkPortionSizeFromAttributes > capacityLitresFromAttributes)
            {
                api.Logger.Warning($"Drink portion size {drinkPortionSizeFromAttributes} is greater than capacity {capacityLitresFromAttributes} for {Code}, setting drink portion size to capacity.");
                drinkPortionSizeFromAttributes = capacityLitresFromAttributes;
            }

            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "liquidContainerBase", () =>
            {
                List<ItemStack> liquidContainerStacks = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj is BlockLiquidContainerBase blc && blc.IsTopOpened && blc.AllowHeldLiquidTransfer)
                        liquidContainerStacks.Add(new ItemStack(obj));
                }

                var lcstacks = liquidContainerStacks.ToArray();


                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-bucket-rightclick",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = lcstacks
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-bucket-rightclick-sneak",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "shift",
                        Itemstacks = lcstacks
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-bucket-rightclick-sprint",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "ctrl",
                        Itemstacks = lcstacks
                    }
                };
            });
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer).Append(interactions);
        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-fill",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => {
                        return GetCurrentLitres(inSlot.Itemstack) < CapacityLitres;
                    }
                },
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-empty",
                    HotKeyCode = "ctrl",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => {
                        return GetCurrentLitres(inSlot.Itemstack) > 0;
                    }
                },
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-place",
                    HotKeyCode = "shift",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => {
                        return true;
                    }
                }
            };
        }

        #endregion



        #region Take/Remove Contents

        public bool SetCurrentLitres(ItemStack containerStack, float litres)
        {
            WaterTightContainableProps props = GetContentProps(containerStack);
            if (props == null) return false;

            ItemStack contentStack = GetContent(containerStack);
            contentStack.StackSize = (int)(litres * props.ItemsPerLitre);

            SetContent(containerStack, contentStack);
            return true;
        }

        public float GetCurrentLitres(ItemStack containerStack)
        {
            WaterTightContainableProps props = GetContentProps(containerStack);
            if (props == null) return 0;

            return GetContent(containerStack).StackSize / props.ItemsPerLitre;
        }


        public float GetCurrentLitres(BlockPos pos)
        {
            WaterTightContainableProps props = GetContentProps(pos);
            if (props == null) return 0;

            return GetContent(pos).StackSize / props.ItemsPerLitre;
        }


        public bool IsFull(ItemStack containerStack)
        {
            return GetCurrentLitres(containerStack) >= CapacityLitres;
        }

        public bool IsFull(BlockPos pos)
        {
            return GetCurrentLitres(pos) >= CapacityLitres;
        }

#nullable enable
        public WaterTightContainableProps? GetContentProps(ItemStack containerStack)
        {
            ItemStack? stack = GetContent(containerStack);
            return GetContainableProps(stack);
        }


        public static int GetTransferStackSize(ILiquidInterface containerBlock, ItemStack contentStack, IPlayer? player = null)
        {
            return GetTransferStackSize(containerBlock, contentStack, player?.Entity?.Controls.ShiftKey == true);
        }

        public static int GetTransferStackSize(ILiquidInterface containerBlock, ItemStack contentStack, bool maxCapacity)
        {
            if (contentStack == null) return 0;

            float litres = containerBlock.TransferSizeLitres;
            var liqProps = GetContainableProps(contentStack);
            float itemsPerLitre = liqProps?.ItemsPerLitre ?? 1;
            int stacksize = (int)(itemsPerLitre * litres);

            if (maxCapacity)
            {
                stacksize = (int)(containerBlock.CapacityLitres * itemsPerLitre);
            }

            return stacksize;
        }




        public static WaterTightContainableProps? GetContainableProps(ItemStack? stack)
        {
            try
            {
                JsonObject? obj = stack?.ItemAttributes?["waterTightContainerProps"];
                if (obj != null && obj.Exists) return obj.AsObject<WaterTightContainableProps>(null, stack!.Collectible.Code.Domain);
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Retrives the containable properties of the currently contained itemstack of a placed water container
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        public WaterTightContainableProps? GetContentProps(BlockPos pos)
        {
            BlockEntityContainer? becontainer = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityContainer;
            if (becontainer == null) return null;

            int slotid = GetContainerSlotId(pos);
            if (slotid >= becontainer.Inventory.Count) return null;

            ItemStack? stack = becontainer.Inventory[slotid]?.Itemstack;
            if (stack == null) return null;

            return GetContainableProps(stack);
        }

        /// <summary>
        /// Sets the containers contents to given stack
        /// </summary>
        /// <param name="containerStack"></param>
        /// <param name="content"></param>
        public void SetContent(ItemStack containerStack, ItemStack content)
        {
            if (content == null)
            {
                SetContents(containerStack, null);
                return;
            }
            SetContents(containerStack, new ItemStack[] { content });
        }


        /// <summary>
        /// Sets the contents to placed container block
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <param name="content"></param>
        public void SetContent(BlockPos pos, ItemStack content)
        {
            BlockEntityContainer? beContainer = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityContainer;
            if (beContainer == null) return;

            new DummySlot(content).TryPutInto(api.World, beContainer.Inventory[GetContainerSlotId(pos)], content.StackSize);

            beContainer.Inventory[GetContainerSlotId(pos)].MarkDirty();
            beContainer.MarkDirty(true);
        }


        /// <summary>
        /// Retrieve the contents of the container stack
        /// </summary>
        /// <param name="world"></param>
        /// <param name="containerStack"></param>
        /// <returns></returns>
        public ItemStack? GetContent(ItemStack containerStack)
        {
            ItemStack[] stacks = GetContents(api.World, containerStack);
            int id = GetContainerSlotId(containerStack);
            return (stacks != null && stacks.Length > 0) ? stacks[Math.Min(stacks.Length - 1, id)] : null;
        }

        /// <summary>
        /// Retrieve the contents of a placed container
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        public ItemStack? GetContent(BlockPos pos)
        {
            BlockEntityContainer? becontainer = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityContainer;
            if (becontainer == null) return null;
            return becontainer.Inventory[GetContainerSlotId(pos)].Itemstack;
        }
#nullable disable

        public override ItemStack CreateItemStackFromJson(ITreeAttribute stackAttr, IWorldAccessor world, string domain)
        {
            bool makefull = stackAttr.HasAttribute("makefull");
            stackAttr.RemoveAttribute("makefull");

            var stack = base.CreateItemStackFromJson(stackAttr, world, domain);

            if (makefull)
            {
                var props = GetContainableProps(stack);
                stack.StackSize = (int)(CapacityLitres * (props?.ItemsPerLitre ?? 1));
            }

            return stack;
        }

        /// <summary>
        /// Tries to take out as much items/liquid as possible and returns it. Note, returns the amount taken out of ONE container, if containerStack has StackSize > 1 then you may want to multiply the result by the StackSize
        /// </summary>
        /// <param name="world"></param>
        /// <param name="containerStack"></param>
        /// <param name="quantityItems"></param>
        /// <returns></returns>
        public ItemStack TryTakeContent(ItemStack containerStack, int quantityItems)
        {
            ItemStack stack = GetContent(containerStack);
            if (stack == null) return null;

            ItemStack takenStack = stack.Clone();
            stack.StackSize -= quantityItems;
            if (stack.StackSize <= 0) SetContent(containerStack, null);
            else
            {
                SetContent(containerStack, stack);
                takenStack.StackSize = quantityItems;
            }

            return takenStack;
        }


        /// <summary>
        /// Tries to take out as much items/liquid as possible from a placed bucket and returns it
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <param name="quantityItem"></param>
        public ItemStack TryTakeContent(BlockPos pos, int quantityItem)
        {
            BlockEntityContainer becontainer = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityContainer;
            if (becontainer == null) return null;

            var slot = becontainer.Inventory[GetContainerSlotId(pos)];
            ItemStack stack = slot.Itemstack;
            if (stack == null) return null;

            ItemStack takenStack = stack.Clone();
            stack.StackSize -= quantityItem;
            if (stack.StackSize <= 0) slot.Itemstack = null;
            else takenStack.StackSize = quantityItem;

            slot.MarkDirty();
            becontainer.MarkDirty(true);

            return takenStack;
        }

        public ItemStack TryTakeLiquid(ItemStack containerStack, float desiredLitres)
        {
            var props = GetContainableProps(GetContent(containerStack));
            if (props == null) return null;

            return TryTakeContent(containerStack, (int)(desiredLitres * props.ItemsPerLitre));
        }

        #endregion


        #region PutContents

        /// <summary>
        /// Tries to place in items/liquid and returns actually inserted quantity
        /// </summary>
        /// <param name="containerStack"></param>
        /// <param name="liquidStack"></param>
        /// <param name="desiredLitres"></param>
        /// <returns>Amount of moved items (stacksize, not litres)</returns>
        public virtual int TryPutLiquid(ItemStack containerStack, ItemStack liquidStack, float desiredLitres)
        {
            if (liquidStack == null) return 0;

            var props = GetContainableProps(liquidStack);
            if (props == null) return 0;

            float epsilon = 0.00001f;
            int desiredItems = (int)(props.ItemsPerLitre * desiredLitres + epsilon);
            int availItems = liquidStack.StackSize;

            ItemStack stack = GetContent(containerStack);
            ILiquidSink sink = containerStack.Collectible as ILiquidSink;

            if (stack == null)
            {
                if (!props.Containable) return 0;

                int placeableItems = (int)(sink.CapacityLitres * props.ItemsPerLitre + epsilon);

                ItemStack placedstack = liquidStack.Clone();
                placedstack.StackSize = GameMath.Min(availItems, desiredItems, placeableItems);
                SetContent(containerStack, placedstack);

                return Math.Min(desiredItems, placeableItems);
            }
            else
            {
                if (!stack.Equals(api.World, liquidStack, GlobalConstants.IgnoredStackAttributes)) return 0;

                float maxItems = sink.CapacityLitres * props.ItemsPerLitre;
                int placeableItems = (int)(maxItems - (float)stack.StackSize);

                int moved = GameMath.Min(availItems, placeableItems, desiredItems);
                stack.StackSize += moved;
                return moved;
            }
        }

        /// <summary>
        /// Tries to put as much items/liquid as possible into a placed container and returns it how much items it actually moved
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="liquidStack"></param>
        /// <param name="desiredLitres"></param>
        /// <returns>Amount of items moved (stacksize, not litres)</returns>
        public virtual int TryPutLiquid(BlockPos pos, ItemStack liquidStack, float desiredLitres)
        {
            if (liquidStack == null) return 0;

            WaterTightContainableProps props = GetContainableProps(liquidStack);

            float itemsPerLitre = props?.ItemsPerLitre ?? 1;
            int desiredItems = (int)(itemsPerLitre * desiredLitres);
            float availItems = liquidStack.StackSize;
            float maxItems = CapacityLitres * itemsPerLitre;

            ItemStack stack = GetContent(pos);
            if (stack == null)
            {
                if (props == null || !props.Containable) return 0;

                int placeableItems = (int)GameMath.Min(desiredItems, maxItems, availItems);
                int movedItems = Math.Min(desiredItems, placeableItems);

                ItemStack placedstack = liquidStack.Clone();
                placedstack.StackSize = movedItems;
                SetContent(pos, placedstack);

                return movedItems;
            }
            else
            {
                if (!stack.Equals(api.World, liquidStack, GlobalConstants.IgnoredStackAttributes)) return 0;

                int placeableItems = (int)Math.Min(availItems, maxItems - (float)stack.StackSize);
                int movedItems = Math.Min(placeableItems, desiredItems);

                stack.StackSize += movedItems;
                api.World.BlockAccessor.GetBlockEntity(pos).MarkDirty(true);
                (api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityContainer).Inventory[GetContainerSlotId(pos)].MarkDirty();

                return movedItems;
            }
        }

        #endregion


        #region Block interact
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (!hotbarSlot.Empty && hotbarSlot.Itemstack.Collectible.Attributes?.IsTrue("handleLiquidContainerInteract") == true)
            {
                EnumHandHandling handling = EnumHandHandling.NotHandled;
                hotbarSlot.Itemstack.Collectible.OnHeldInteractStart(hotbarSlot, byPlayer.Entity, blockSel, null, true, ref handling);
                if (handling == EnumHandHandling.PreventDefault || handling == EnumHandHandling.PreventDefaultAction) return true;
            }

            if (hotbarSlot.Empty || !(hotbarSlot.Itemstack.Collectible is ILiquidInterface)) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            CollectibleObject obj = hotbarSlot.Itemstack.Collectible;

            bool singleTake = byPlayer.WorldData.EntityControls.ShiftKey;
            bool singlePut = byPlayer.WorldData.EntityControls.CtrlKey;

            if (obj is ILiquidSource objLso && !singleTake)
            {
                if (!objLso.AllowHeldLiquidTransfer) return false;

                var contentStackToMove = objLso.GetContent(hotbarSlot.Itemstack);

                float litres = singlePut ? objLso.TransferSizeLitres : objLso.CapacityLitres;
                int moved = TryPutLiquid(blockSel.Position, contentStackToMove, litres);

                if (moved > 0)
                {
                    SplitStackAndPerformAction(byPlayer.Entity, hotbarSlot, (stack) =>
                    {
                        objLso.TryTakeContent(stack, moved);
                        return moved;
                    });
                    DoLiquidMovedEffects(byPlayer, contentStackToMove, moved, EnumLiquidDirection.Pour);

                    return true;
                }
            }


            if (obj is ILiquidSink objLsi && !singlePut)
            {
                if (!objLsi.AllowHeldLiquidTransfer) return false;

                ItemStack owncontentStack = GetContent(blockSel.Position);

                if (owncontentStack == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

                var liquidStackForParticles = owncontentStack.Clone();

                float litres = singleTake ? objLsi.TransferSizeLitres : objLsi.CapacityLitres;

                int moved = SplitStackAndPerformAction(byPlayer.Entity, hotbarSlot, (stack) => objLsi.TryPutLiquid(stack, owncontentStack, litres));
                if (moved > 0)
                {
                    TryTakeContent(blockSel.Position, moved);
                    DoLiquidMovedEffects(byPlayer, liquidStackForParticles, moved, EnumLiquidDirection.Fill);
                    return true;
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }


        public enum EnumLiquidDirection { Fill, Pour }
        public void DoLiquidMovedEffects(IPlayer player, ItemStack contentStack, int moved, EnumLiquidDirection dir)
        {
            if (player == null) return;

            WaterTightContainableProps props = GetContainableProps(contentStack);
            float litresMoved = moved / (props?.ItemsPerLitre ?? 1);

            (player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
            api.World.PlaySoundAt(dir == EnumLiquidDirection.Fill ? props?.FillSound ?? "sounds/effect/water-fill.ogg" : props?.PourSound ?? "sounds/effect/water-pour.ogg", player.Entity, player, true, 16, GameMath.Clamp(litresMoved / 5f, 0.35f, 1f));
            api.World.SpawnCubeParticles(player.Entity.Pos.AheadCopy(0.25).XYZ.Add(0, player.Entity.SelectionBox.Y2 / 2, 0), contentStack, 0.75f, (int)litresMoved * 2, 0.45f);
        }

        #endregion


        #region Held Interact

        protected override void tryEatBegin(ItemSlot slot, EntityAgent byEntity, ref EnumHandHandling handling, string eatSound = "eat", int eatSoundRepeats = 1)
        {
            if (IsEmpty(slot.Itemstack))
            {
                base.tryEatBegin(slot, byEntity, ref handling);
                return;
            }
            base.tryEatBegin(slot, byEntity, ref handling, "drink", 4);
        }

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel != null && api.World.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityGroundStorage begs)
            {
                ItemSlot gslot = begs.GetSlotAt(blockSel);
                if (gslot == null || !gslot.Empty && gslot.Itemstack.Collectible is ILiquidInterface)
                {
                    return;
                }
            }

            if (blockSel == null || byEntity.Controls.ShiftKey)
            {
                // We need to make sure we aren't doing anything
                // with liquid handling before calling up to
                // the base method because it will call out to
                // tryEatBegin and hijack our control flow.
                bool lookingAtLiquidContainer = blockSel != null && api.World.BlockAccessor.GetBlock(blockSel.Position) is BlockLiquidContainerBase;
                bool shouldDrink = !lookingAtLiquidContainer && CanDrinkFrom && GetNutritionProperties(byEntity.World, itemslot.Itemstack, byEntity) != null;

                if (handHandling != EnumHandHandling.PreventDefaultAction && shouldDrink && GetNutritionPropertiesPerLitre(byEntity.World, itemslot.Itemstack, byEntity) != null)
                {
                    tryEatBegin(itemslot, byEntity, ref handHandling, "drink", 4);
                    return;
                }

                if (!byEntity.Controls.ShiftKey || (byEntity.Controls.ShiftKey && !lookingAtLiquidContainer))
                {
                    base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                }

                return;
            }

            if (AllowHeldLiquidTransfer)
            {
                IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;

                ItemStack contentStack = GetContent(itemslot.Itemstack);
                WaterTightContainableProps props = contentStack == null ? null : GetContentProps(contentStack);

                Block targetedBlock = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);

                if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
                {
                    byEntity.World.BlockAccessor.MarkBlockDirty(blockSel.Position.AddCopy(blockSel.Face));
                    byPlayer?.InventoryManager.ActiveHotbarSlot?.MarkDirty();
                    return;
                }

                if (!TryFillFromBlock(itemslot, byEntity, blockSel.Position))
                {
                    BlockLiquidContainerTopOpened targetCntBlock = targetedBlock as BlockLiquidContainerTopOpened;
                    if (targetCntBlock != null)
                    {
                        if (targetCntBlock.TryPutLiquid(blockSel.Position, contentStack, targetCntBlock.CapacityLitres) > 0)
                        {
                            TryTakeContent(itemslot.Itemstack, 1);
                            byEntity.World.PlaySoundAt(props?.FillSpillSound ?? "sounds/block/water", blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                        }

                    }
                    else
                    {
                        if (byEntity.Controls.CtrlKey)
                        {
                            SpillContents(itemslot, byEntity, blockSel);
                        }
                    }
                }
            }

            if (CanDrinkFrom && GetNutritionPropertiesPerLitre(byEntity.World, itemslot.Itemstack, byEntity) != null)
            {
                tryEatBegin(itemslot, byEntity, ref handHandling, "drink", 4);
                return;
            }

            if (IsEmpty(itemslot.Itemstack) && GetNutritionProperties(byEntity.World, itemslot.Itemstack, byEntity) != null)
            {
                tryEatBegin(itemslot, byEntity, ref handHandling);
                return;
            }

            if (AllowHeldLiquidTransfer || CanDrinkFrom)
            {
                // Prevent placing on normal use
                handHandling = EnumHandHandling.PreventDefaultAction;
            }
        }

        protected override bool tryEatStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, ItemStack spawnParticleStack = null)
        {
            return base.tryEatStep(secondsUsed, slot, byEntity, IsEmpty(slot.Itemstack) ? slot.Itemstack : GetContent(slot.Itemstack));
        }

        protected override void tryEatStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity)
        {
            if (IsEmpty(slot.Itemstack)) base.tryEatStop(secondsUsed, slot, byEntity);
            FoodNutritionProperties nutriProps = GetNutritionPropertiesPerLitre(byEntity.World, slot.Itemstack, byEntity);

            if (byEntity.World is IServerWorldAccessor && nutriProps != null && secondsUsed >= 0.95f)
            {
                var containableProps = GetContentProps(slot.Itemstack);
                float litresToDrink = Math.Max(1.0f / (containableProps?.ItemsPerLitre ?? 1), DrinkPortionSize);

                var liquidStack = GetContent(slot.Itemstack);
                var dummyslot = GetContentInDummySlot(slot, liquidStack);

                TransitionState state = UpdateAndGetTransitionState(api.World, dummyslot, EnumTransitionType.Perish);
                float spoilState = state?.TransitionLevel ?? 0;

                float satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, liquidStack, byEntity);
                float healthLossMul = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, liquidStack, byEntity);

                int itemPortionsDrank = SplitStackAndPerformAction(byEntity, slot, (stack) => TryTakeLiquid(stack, litresToDrink)?.StackSize ?? 0);
                if (itemPortionsDrank == 0) return;
                float mul = itemPortionsDrank / containableProps.ItemsPerLitre;

                byEntity.ReceiveSaturation(nutriProps.Satiety * satLossMul * mul, nutriProps.FoodCategory);

                float healthChange = nutriProps.Health * healthLossMul * mul;

                float intox = byEntity.WatchedAttributes.GetFloat("intoxication");
                byEntity.WatchedAttributes.SetFloat("intoxication", Math.Min(1.1f, intox + (nutriProps.Intoxication * mul)));

                float psyche = byEntity.WatchedAttributes.GetFloat("psychedelic");
                byEntity.WatchedAttributes.SetFloat("psychedelic", Math.Min(2.0f, psyche + (nutriProps.Psychedelic * mul)));

                if (healthChange != 0)
                {
                    byEntity.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Internal, Type = healthChange > 0 ? EnumDamageType.Heal : EnumDamageType.Poison }, Math.Abs(healthChange));
                }

                slot.MarkDirty();
                byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID)?.InventoryManager.BroadcastHotbarSlot();
            }
        }


        public override FoodNutritionProperties GetNutritionProperties(IWorldAccessor world, ItemStack itemstack, Entity forEntity)
        {
            if (GetNutritionPropertiesPerLitre(world, itemstack, forEntity)?.Clone() is FoodNutritionProperties nutriProps)
            {
                float litres = GetCurrentLitres(itemstack);
                nutriProps.Health *= litres;
                nutriProps.Satiety *= litres;
                nutriProps.Intoxication *= litres;
                nutriProps.Psychedelic *= litres;
                nutriProps.EatenStack = new JsonItemStack();
                nutriProps.EatenStack.ResolvedItemstack = itemstack.Clone();
                nutriProps.EatenStack.ResolvedItemstack.StackSize = 1;
                (nutriProps.EatenStack.ResolvedItemstack.Collectible as BlockLiquidContainerBase).SetContent(nutriProps.EatenStack.ResolvedItemstack, null);

                return nutriProps;
            }

            return IsEmpty(itemstack) ? base.GetNutritionProperties(world, itemstack, forEntity) : null;
        }

        public FoodNutritionProperties GetNutritionPropertiesPerLitre(IWorldAccessor world, ItemStack itemstack, Entity forEntity)
        {
            if (GetContent(itemstack) is ItemStack contentStack &&
                GetContainableProps(contentStack) is WaterTightContainableProps props)
            {
                if (props.NutritionPropsPerLitre is FoodNutritionProperties nutriPropsPerLitre) return nutriPropsPerLitre;

                if (contentStack.Collectible.GetNutritionProperties(world, contentStack, forEntity)?.Clone() is FoodNutritionProperties nutriProps)
                {
                    // If we have no NutritionPropsPerLitre but we do have nutrition props we'll pretend they're per item and so adjust accordingly
                    float itemsPerLitre = props.ItemsPerLitre;
                    nutriProps.Health *= itemsPerLitre;
                    nutriProps.Satiety *= itemsPerLitre;
                    nutriProps.Intoxication *= itemsPerLitre;
                    nutriProps.Psychedelic *= itemsPerLitre;

                    return nutriProps;
                }
            }

            return null;
        }


        public bool TryFillFromBlock(ItemSlot itemslot, EntityAgent byEntity, BlockPos pos)
        {
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            IBlockAccessor blockAcc = byEntity.World.BlockAccessor;

            Block block = blockAcc.GetBlock(pos, BlockLayersAccess.FluidOrSolid);
            if (block.Attributes?["waterTightContainerProps"].Exists == false) return false;

            WaterTightContainableProps props = block.Attributes?["waterTightContainerProps"]?.AsObject<WaterTightContainableProps>();
            if (props?.WhenFilled == null || !props.Containable) return false;

            props.WhenFilled.Stack.Resolve(byEntity.World, "liquidcontainerbase");

            if (GetCurrentLitres(itemslot.Itemstack) >= CapacityLitres) return false;


            var contentStack = props.WhenFilled.Stack.ResolvedItemstack;
            if (contentStack == null) return false;
            contentStack = contentStack.Clone();
            contentStack.StackSize = 999999;

            int moved = SplitStackAndPerformAction(byEntity, itemslot, (stack) => TryPutLiquid(stack, contentStack, CapacityLitres));

            if (moved > 0)
            {
                DoLiquidMovedEffects(byPlayer, contentStack, moved, EnumLiquidDirection.Fill);
            }

            return true;
        }


        public virtual void TryFillFromBlock(EntityItem byEntityItem, BlockPos pos)
        {
            IWorldAccessor world = byEntityItem.World;

            Block block = world.BlockAccessor.GetBlock(pos);

            if (block.Attributes?["waterTightContainerProps"].Exists == false) return;
            WaterTightContainableProps props = block.Attributes?["waterTightContainerProps"].AsObject<WaterTightContainableProps>();
            if (props?.WhenFilled == null || !props.Containable) return;

            if (props.WhenFilled.Stack.ResolvedItemstack == null) props.WhenFilled.Stack.Resolve(world, "liquidcontainerbase");

            ItemStack whenFilledStack = props.WhenFilled.Stack.ResolvedItemstack;

            ItemStack contentStack = GetContent(byEntityItem.Itemstack);
            bool canFill = contentStack == null || (contentStack.Equals(world, whenFilledStack, GlobalConstants.IgnoredStackAttributes) && GetCurrentLitres(byEntityItem.Itemstack) < CapacityLitres);
            if (!canFill) return;

            whenFilledStack.StackSize = 999999;
            int moved = SplitStackAndPerformAction(byEntityItem, byEntityItem.Slot, (stack) => TryPutLiquid(stack, whenFilledStack, CapacityLitres));
            if (moved > 0)
            {
                world.PlaySoundAt(props.FillSound, pos, -0.4, null);
            }
        }


        private bool SpillContents(ItemSlot containerSlot, EntityAgent byEntity, BlockSelection blockSel)
        {
            BlockPos pos = blockSel.Position;
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            IBlockAccessor blockAcc = byEntity.World.BlockAccessor;
            BlockPos secondPos = blockSel.Position.AddCopy(blockSel.Face);
            var contentStack = GetContent(containerSlot.Itemstack);

            WaterTightContainableProps props = GetContentProps(containerSlot.Itemstack);

            if (props == null || !props.AllowSpill || props.WhenSpilled == null) return false;

            if (!byEntity.World.Claims.TryAccess(byPlayer, secondPos, EnumBlockAccessFlags.BuildOrBreak))
            {
                return false;
            }

            var action = props.WhenSpilled.Action;
            float currentlitres = GetCurrentLitres(containerSlot.Itemstack);

            if (currentlitres > 0 && currentlitres < 10)
            {
                action = WaterTightContainableProps.EnumSpilledAction.DropContents;
            }

            if (action == WaterTightContainableProps.EnumSpilledAction.PlaceBlock)
            {
                Block waterBlock = byEntity.World.GetBlock(props.WhenSpilled.Stack.Code);

                if (props.WhenSpilled.StackByFillLevel != null)
                {
                    props.WhenSpilled.StackByFillLevel.TryGetValue((int)currentlitres, out JsonItemStack fillLevelStack);
                    if (fillLevelStack != null) waterBlock = byEntity.World.GetBlock(fillLevelStack.Code);
                }

                Block currentblock = blockAcc.GetBlock(pos);
                if (!currentblock.DisplacesLiquids(blockAcc, pos))
                {
                    blockAcc.SetBlock(waterBlock.BlockId, pos, BlockLayersAccess.Fluid);
                    blockAcc.TriggerNeighbourBlockUpdate(pos);
                    waterBlock.OnNeighbourBlockChange(byEntity.World, pos, secondPos);
                    blockAcc.MarkBlockDirty(pos);   // Maybe unnecessary to call this server side as this code will be called client-side anyhow
                }
                else
                {
                    if (!blockAcc.GetBlock(secondPos).DisplacesLiquids(blockAcc, pos))
                    {
                        blockAcc.SetBlock(waterBlock.BlockId, secondPos, BlockLayersAccess.Fluid);
                        blockAcc.TriggerNeighbourBlockUpdate(secondPos);
                        waterBlock.OnNeighbourBlockChange(byEntity.World, secondPos, pos);
                        blockAcc.MarkBlockDirty(secondPos);   // Maybe unnecessary to call this server side as this code will be called client-side anyhow
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            if (action == WaterTightContainableProps.EnumSpilledAction.DropContents)
            {
                props.WhenSpilled.Stack.Resolve(byEntity.World, "liquidcontainerbasespill");

                ItemStack stack = props.WhenSpilled.Stack.ResolvedItemstack.Clone();
                stack.StackSize = contentStack.StackSize;

                byEntity.World.SpawnItemEntity(stack, blockSel.Position.ToVec3d().Add(blockSel.HitPosition));
            }


            int moved = SplitStackAndPerformAction(byEntity, containerSlot, (stack) => { SetContent(stack, null); return contentStack.StackSize; });

            DoLiquidMovedEffects(byPlayer, contentStack, moved, EnumLiquidDirection.Pour);
            return true;
        }





        #endregion



        public int SplitStackAndPerformAction(Entity byEntity, ItemSlot slot, System.Func<ItemStack, int> action)
        {
            if (slot.Itemstack == null) return 0;
            if (slot.Itemstack.StackSize == 1)
            {
                int moved = action(slot.Itemstack);

                if (moved > 0)
                {
                    int maxstacksize = slot.Itemstack.Collectible.MaxStackSize;

                    (byEntity as EntityPlayer)?.WalkInventory((pslot) =>
                    {
                        if (pslot.Empty || pslot is ItemSlotCreative || pslot.StackSize == pslot.Itemstack.Collectible.MaxStackSize) return true;
                        int mergableq = slot.Itemstack.Collectible.GetMergableQuantity(slot.Itemstack, pslot.Itemstack, EnumMergePriority.DirectMerge);
                        if (mergableq == 0) return true;

                        var selfLiqBlock = slot.Itemstack.Collectible as BlockLiquidContainerBase;
                        var invLiqBlock = pslot.Itemstack.Collectible as BlockLiquidContainerBase;

                        if ((selfLiqBlock?.GetContent(slot.Itemstack)?.StackSize ?? 0) != (invLiqBlock?.GetContent(pslot.Itemstack)?.StackSize ?? 0)) return true;

                        slot.Itemstack.StackSize += mergableq;
                        pslot.TakeOut(mergableq);

                        slot.MarkDirty();
                        pslot.MarkDirty();
                        return true;
                    });
                }

                return moved;
            }
            else
            {
                ItemStack containerStack = slot.Itemstack.Clone();
                containerStack.StackSize = 1;

                int moved = action(containerStack);

                if (moved > 0)
                {
                    slot.TakeOut(1);
                    if ((byEntity as EntityPlayer)?.Player.InventoryManager.TryGiveItemstack(containerStack, true) != true)
                    {
                        api.World.SpawnItemEntity(containerStack, byEntity.Pos.XYZ);
                    }

                    slot.MarkDirty();
                }

                return moved;
            }
        }

        public override void OnGroundIdle(EntityItem entityItem)
        {
            base.OnGroundIdle(entityItem);

            IWorldAccessor world = entityItem.World;
            if (world.Side != EnumAppSide.Server) return;

            if (entityItem.Swimming && world.Rand.NextDouble() < 0.03)
            {
                TryFillFromBlock(entityItem, entityItem.Pos.AsBlockPos);
            }

            if (entityItem.Swimming && world.Rand.NextDouble() < 0.01)
            {
                ItemStack[] stacks = GetContents(world, entityItem.Itemstack);
                if (MealMeshCache.ContentsRotten(stacks))
                {
                    for (int i = 0; i < stacks.Length; i++)
                    {
                        if (stacks[i] != null && stacks[i].StackSize > 0 && stacks[i].Collectible.Code.Path == "rot")
                        {
                            world.SpawnItemEntity(stacks[i], entityItem.Pos.XYZ);
                        }
                    }

                    SetContent(entityItem.Itemstack, null);
                }
            }
        }

        public override void AddExtraHeldItemInfoPostMaterial(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world)
        {
            GetContentInfo(inSlot, dsc, world);
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            float litres = GetCurrentLitres(pos);

            StringBuilder sb = new StringBuilder();
            BlockEntityContainer becontainer = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityContainer;
            if (becontainer != null)
            {
                if (litres <= 0)
                {
                    sb.AppendLine(Lang.Get("Empty"));
                }
                else
                {
                    ItemSlot slot = becontainer.Inventory[GetContainerSlotId(pos)];
                    ItemStack contentStack = slot.Itemstack;

                    string incontainername = Lang.Get(contentStack.Collectible.Code.Domain + ":incontainer-" + contentStack.Class.ToString().ToLowerInvariant() + "-" + contentStack.Collectible.Code.Path);
                    sb.AppendLine(Lang.Get("Contents:"));
                    sb.AppendLine(" " + Lang.Get("{0} litres of {1}", litres, incontainername));
                    string perishableInfo = PerishableInfoCompact(api, slot, 0, false);
                    if (perishableInfo.Length > 2) sb.AppendLine(perishableInfo.Substring(2));
                }
            }

            StringBuilder sb2 = new StringBuilder();
            foreach (BlockBehavior bh in BlockBehaviors)
            {
                sb2.Append(bh.GetPlacedBlockInfo(world, pos, forPlayer));
            }
            if (sb2.Length > 0)
            {
                sb.AppendLine();             // Insert a blank line if there is more to add (e.g. reinforceable)
                sb.Append(sb2.ToString());
            }

            return sb.ToString();
        }


        public virtual void GetContentInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world)
        {
            float litres = GetCurrentLitres(inSlot.Itemstack);
            ItemStack contentStack = GetContent(inSlot.Itemstack);

            if (litres <= 0) dsc.AppendLine(Lang.Get("Empty"));

            else
            {
                string incontainerrname = Lang.Get(contentStack.Collectible.Code.Domain + ":incontainer-" + contentStack.Class.ToString().ToLowerInvariant() + "-" + contentStack.Collectible.Code.Path);
                dsc.AppendLine(Lang.Get("{0} litres of {1}", litres, incontainerrname));

                var dummyslot = GetContentInDummySlot(inSlot, contentStack);
                TransitionState[] states = contentStack.Collectible.UpdateAndGetTransitionStates(api.World, dummyslot);
                if (states != null && !dummyslot.Empty)
                {
                    bool nowSpoiling = false;
                    foreach (var state in states)
                    {
                        nowSpoiling |= AppendPerishableInfoText(dummyslot, dsc, world, state, nowSpoiling) > 0;
                    }
                }

            }
        }

        public override void TryMergeStacks(ItemStackMergeOperation op)
        {
            op.MovableQuantity = GetMergableQuantity(op.SinkSlot.Itemstack, op.SourceSlot.Itemstack, op.CurrentPriority);
            if (op.MovableQuantity == 0) return;
            if (!op.SinkSlot.CanTakeFrom(op.SourceSlot, op.CurrentPriority)) return;

            ItemStack sinkContent = GetContent(op.SinkSlot.Itemstack);
            ItemStack sourceContent = GetContent(op.SourceSlot.Itemstack);

            if (sinkContent == null && sourceContent == null)
            {
                base.TryMergeStacks(op);
                return;
            }

            if (sinkContent == null || sourceContent == null) { op.MovableQuantity = 0; return; }

            if (!sinkContent.Equals(op.World, sourceContent, GlobalConstants.IgnoredStackAttributes)) { op.MovableQuantity = 0; return; }

            float sourceLitres = GetCurrentLitres(op.SourceSlot.Itemstack) * op.SourceSlot.StackSize;
            float sinkLitres = GetCurrentLitres(op.SinkSlot.Itemstack) * op.SinkSlot.StackSize;

            float sourceCapLitres = op.SourceSlot.StackSize * (op.SourceSlot.Itemstack.Collectible as BlockLiquidContainerBase)?.CapacityLitres ?? 0;
            float sinkCapLitres = op.SinkSlot.StackSize * (op.SinkSlot.Itemstack.Collectible as BlockLiquidContainerBase)?.CapacityLitres ?? 0;

            // Containers are empty, can do a classic merge
            if (sourceCapLitres == 0 || sinkCapLitres == 0)
            {
                base.TryMergeStacks(op);
                return;
            }

            // Containers are equally full, can do a classic merge
            if (GetCurrentLitres(op.SourceSlot.Itemstack) == GetCurrentLitres(op.SinkSlot.Itemstack))
            {
                if (op.MovableQuantity > 0)
                {
                    base.TryMergeStacks(op);
                    return;
                }

                op.MovedQuantity = 0;
                return;
            }

            if (op.CurrentPriority == EnumMergePriority.DirectMerge)
            {
                float movableLitres = Math.Min(sinkCapLitres - sinkLitres, sourceLitres);
                int moved = TryPutLiquid(op.SinkSlot.Itemstack, sourceContent, movableLitres / op.SinkSlot.StackSize);
                DoLiquidMovedEffects(op.ActingPlayer, sinkContent, moved, EnumLiquidDirection.Pour);

                moved *= op.SinkSlot.StackSize;   // We multiply by stacksize because the TryPutLiquid() method returned the amount moved into a *single* SinkSlot itemstack
                TryTakeContent(op.SourceSlot.Itemstack, (int)(0.51f + (float)moved / op.SourceSlot.StackSize));  // We add the 0.51f for a bit of rounding up, otherwising rounding errors can slowly duplicate liquids by 1 portion (0.01 litres), better to lose liquid sometimes by rounding up the amount taken out, call it spillage
                op.SourceSlot.MarkDirty();
                op.SinkSlot.MarkDirty();
            }

            op.MovableQuantity = 0;
            return;
        }

        public override bool MatchesForCrafting(ItemStack inputStack, GridRecipe gridRecipe, CraftingRecipeIngredient ingredient)
        {
            JsonObject rprops = ingredient.RecipeAttributes;
            if (rprops?.Exists != true || rprops?["requiresContent"].Exists != true) rprops = gridRecipe.Attributes?["liquidContainerProps"];

            if (rprops?.Exists != true)
            {
                return base.MatchesForCrafting(inputStack, gridRecipe, ingredient);
            }

            string contentCode = rprops["requiresContent"]["code"].AsString();
            string contentType = rprops["requiresContent"]["type"].AsString();

            ItemStack contentStack = GetContent(inputStack);

            if (contentStack == null) return false;

            float litres = rprops["requiresLitres"].AsFloat();
            var props = GetContainableProps(contentStack);
            int q = (int)((props?.ItemsPerLitre ?? 1) * litres) / inputStack.StackSize;

            bool a = contentStack.Class.ToString().ToLowerInvariant() == contentType.ToLowerInvariant();
            bool b = WildcardUtil.Match(new AssetLocation(contentCode), contentStack.Collectible.Code);
            bool c = contentStack.StackSize >= q;

            return a && b && c;
        }

        public override void OnConsumedByCrafting(ItemSlot[] allInputSlots, ItemSlot stackInSlot, GridRecipe gridRecipe, CraftingRecipeIngredient fromIngredient, IPlayer byPlayer, int quantity)
        {
            JsonObject rprops = fromIngredient.RecipeAttributes;
            if (rprops?.Exists != true || rprops?["requiresContent"].Exists != true) rprops = gridRecipe.Attributes?["liquidContainerProps"];

            if (rprops?.Exists != true)
            {
                base.OnConsumedByCrafting(allInputSlots, stackInSlot, gridRecipe, fromIngredient, byPlayer, quantity);
                return;
            }

            ItemStack contentStack = GetContent(stackInSlot.Itemstack);
            float litres = rprops["requiresLitres"].AsFloat();
            var props = GetContainableProps(contentStack);
            int q = (int)((props?.ItemsPerLitre ?? 1) * litres / stackInSlot.StackSize);

            if (rprops.IsTrue("consumeContainer"))
            {
                stackInSlot.Itemstack.StackSize -= quantity;

                if (stackInSlot.Itemstack.StackSize <= 0)
                {
                    stackInSlot.Itemstack = null;
                    stackInSlot.MarkDirty();
                }
            }
            else
            {
                TryTakeContent(stackInSlot.Itemstack, q);
            }
        }

        public static string PerishableInfoCompact(ICoreAPI Api, ItemSlot contentSlot, float ripenRate, bool withStackName = true)
        {
            StringBuilder dsc = new StringBuilder();

            if (withStackName)
            {
                dsc.Append(contentSlot.Itemstack.GetName());
            }

            TransitionState[] transitionStates = contentSlot.Itemstack?.Collectible.UpdateAndGetTransitionStates(Api.World, contentSlot);

            if (transitionStates != null)
            {
                for (int i = 0; i < transitionStates.Length; i++)
                {
                    string comma = ", ";

                    TransitionState state = transitionStates[i];

                    TransitionableProperties prop = state.Props;
                    float perishRate = contentSlot.Itemstack.Collectible.GetTransitionRateMul(Api.World, contentSlot, prop.Type);

                    if (perishRate <= 0) continue;

                    float transitionLevel = state.TransitionLevel;
                    float freshHoursLeft = state.FreshHoursLeft / perishRate;

                    switch (prop.Type)
                    {
                        case EnumTransitionType.Perish:

                            dsc.Append(comma);
                            if (transitionLevel > 0)
                            {
                                dsc.Append(Lang.Get("{0}% spoiled", (int)Math.Round(transitionLevel * 100)));
                            }
                            else
                            {
                                double hoursPerday = Api.World.Calendar.HoursPerDay;

                                if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                                {
                                    dsc.Append(Lang.Get("fresh for {0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                                }
                                else if (freshHoursLeft > hoursPerday)
                                {
                                    dsc.Append(Lang.Get("fresh for {0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                                }
                                else
                                {
                                    dsc.Append(Lang.Get("fresh for {0} hours", Math.Round(freshHoursLeft, 1)));
                                }
                            }
                            break;

                        case EnumTransitionType.Ripen:

                            dsc.Append(comma);
                            if (transitionLevel > 0)
                            {
                                dsc.Append(Lang.Get("{1:0.#} days left to ripen ({0}%)", (int)Math.Round(transitionLevel * 100), (state.TransitionHours - state.TransitionedHours) / Api.World.Calendar.HoursPerDay / ripenRate));
                            }
                            else
                            {
                                double hoursPerday = Api.World.Calendar.HoursPerDay;

                                if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                                {
                                    dsc.Append(Lang.Get("will ripen in {0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                                }
                                else if (freshHoursLeft > hoursPerday)
                                {
                                    dsc.Append(Lang.Get("will ripen in {0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                                }
                                else
                                {
                                    dsc.Append(Lang.Get("will ripen in {0} hours", Math.Round(freshHoursLeft, 1)));
                                }
                            }
                            break;
                    }
                }

            }

            return dsc.ToString();
        }
    }


}
