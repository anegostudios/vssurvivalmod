using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// For liquid containers that are open on the top and thus need render their contents
    /// </summary>
    public class BlockLiquidContainerTopOpened : BlockLiquidContainerBase, IContainedMeshSource
    {
        LiquidTopOpenContainerProps Props;
        protected virtual string meshRefsCacheKey => Code.ToShortString() + "meshRefs";
        protected virtual AssetLocation emptyShapeLoc => Props.EmptyShapeLoc;
        protected virtual AssetLocation contentShapeLoc => Props.OpaqueContentShapeLoc;
        protected virtual AssetLocation liquidContentShapeLoc => Props.LiquidContentShapeLoc;

        public override float TransferSizeLitres => Props.TransferSizeLitres;

        public override float CapacityLitres => Props.CapacityLitres;

        

        /// <summary>
        /// Max fill height
        /// </summary>
        protected virtual float liquidMaxYTranslate => Props.LiquidMaxYTranslate;
        protected virtual float liquidYTranslatePerLitre => liquidMaxYTranslate / CapacityLitres;


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (Attributes?["liquidContainerProps"].Exists == true)
            {
                Props = Attributes["liquidContainerProps"].AsObject<LiquidTopOpenContainerProps>(null, Code.Domain);
            }
        }

        #region Render
        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            Dictionary<int, MeshRef> meshrefs;

            object obj;
            if (capi.ObjectCache.TryGetValue(meshRefsCacheKey, out obj))
            {
                meshrefs = obj as Dictionary<int, MeshRef>;
            }
            else
            {
                capi.ObjectCache[meshRefsCacheKey] = meshrefs = new Dictionary<int, MeshRef>();
            }

            ItemStack contentStack = GetContent(itemstack);
            if (contentStack == null) return;

            int hashcode = GetStackCacheHashCode(contentStack);

            if (!meshrefs.TryGetValue(hashcode, out MeshRef meshRef))
            {
                MeshData meshdata = GenMesh(capi, contentStack);
                meshrefs[hashcode] = meshRef = capi.Render.UploadMesh(meshdata);
            }

            renderinfo.ModelRef = meshRef;
        }


        protected int GetStackCacheHashCode(ItemStack contentStack)
        {
            string s = contentStack.StackSize + "x" + contentStack.Collectible.Code.ToShortString();
            return s.GetHashCode();
        }



        public override void OnUnloaded(ICoreAPI api)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            if (capi == null) return;

            object obj;
            if (capi.ObjectCache.TryGetValue(meshRefsCacheKey, out obj))
            {
                Dictionary<int, MeshRef> meshrefs = obj as Dictionary<int, MeshRef>;

                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove(meshRefsCacheKey);
            }
        }

        public MeshData GenMesh(ICoreClientAPI capi, ItemStack contentStack, BlockPos forBlockPos = null)
        {
            Shape shape = capi.Assets.TryGet(emptyShapeLoc.WithPathAppendixOnce(".json")).ToObject<Shape>();
            MeshData bucketmesh;
            capi.Tesselator.TesselateShape(this, shape, out bucketmesh, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));

            if (contentStack != null)
            {
                WaterTightContainableProps props = GetInContainerProps(contentStack);

                ContainerTextureSource contentSource = new ContainerTextureSource(capi, contentStack, props.Texture);

                var loc = props.IsOpaque ? contentShapeLoc : liquidContentShapeLoc;
                shape = capi.Assets.TryGet(loc.WithPathAppendixOnce(".json")).ToObject<Shape>();
                MeshData contentMesh;
                capi.Tesselator.TesselateShape(GetType().Name, shape, out contentMesh, contentSource, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));

                contentMesh.Translate(0, GameMath.Min(liquidMaxYTranslate, contentStack.StackSize / props.ItemsPerLitre * liquidYTranslatePerLitre), 0);

                if (props.ClimateColorMap != null)
                {
                    int col = capi.World.ApplyColorMapOnRgba(props.ClimateColorMap, null, ColorUtil.WhiteArgb, 196, 128, false);
                    if (forBlockPos != null)
                    {
                        col = capi.World.ApplyColorMapOnRgba(props.ClimateColorMap, null, ColorUtil.WhiteArgb, forBlockPos.X, forBlockPos.Y, forBlockPos.Z, false);
                    }

                    byte[] rgba = ColorUtil.ToBGRABytes(col);

                    for (int i = 0; i < contentMesh.Rgba.Length; i++)
                    {
                        contentMesh.Rgba[i] = (byte)((contentMesh.Rgba[i] * rgba[i % 4]) / 255);
                    }
                }

                for (int i = 0; i < contentMesh.Flags.Length; i++)
                {
                    contentMesh.Flags[i] = contentMesh.Flags[i] & ~(1 << 12); // Remove water waving flag
                }

                bucketmesh.AddMeshData(contentMesh);

                // Water flags
                if (forBlockPos != null)
                {
                    bucketmesh.CustomInts = new CustomMeshDataPartInt(bucketmesh.FlagsCount);
                    bucketmesh.CustomInts.Count = bucketmesh.FlagsCount;
                    bucketmesh.CustomInts.Values.Fill(0x4000000); // light foam only

                    bucketmesh.CustomFloats = new CustomMeshDataPartFloat(bucketmesh.FlagsCount * 2);
                    bucketmesh.CustomFloats.Count = bucketmesh.FlagsCount * 2;
                }
            }


            return bucketmesh;
        }

        public MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos forBlockPos = null)
        {
            ItemStack contentStack = GetContent(itemstack);
            return GenMesh(api as ICoreClientAPI, contentStack, forBlockPos);
        }

        public string GetMeshCacheKey(ItemStack itemstack)
        {
            var contentStack = GetContent(itemstack);
            string s = itemstack.Collectible.Code.ToShortString() + "-" + contentStack?.StackSize + "x" + contentStack?.Collectible.Code.ToShortString();
            return s;
        }

        #endregion
    }

    public abstract class BlockLiquidContainerBase : BlockContainer, ILiquidSource, ILiquidSink
    {
        protected float capacityLitresFromAttributes = 10;
        public virtual float CapacityLitres => capacityLitresFromAttributes;
        public virtual int ContainerSlotId => 0;

        public virtual float TransferSizeLitres => 1;



        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, ItemSlot inSlot, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping)
        {
            base.OnLoadCollectibleMappings(worldForResolve, inSlot, oldBlockIdMapping, oldItemIdMapping);
        }

        public override void OnHandbookRecipeRender(ICoreClientAPI capi, GridRecipe gridRecipe, ItemSlot dummyslot, double x, double y, double size)
        {
            if (gridRecipe.Attributes?["liquidContainerProps"].Exists != true)
            {
                base.OnHandbookRecipeRender(capi, gridRecipe, dummyslot, x, y, size);
                return;
            }

            string contentCode = gridRecipe.Attributes["liquidContainerProps"]["requiresContent"]["code"].AsString();
            string contentType = gridRecipe.Attributes["liquidContainerProps"]["requiresContent"]["type"].AsString();

            ItemStack filledContainerStack = dummyslot.Itemstack.Clone();
            ItemStack contentStack;
            if (contentType == "item") contentStack = new ItemStack(capi.World.GetItem(new AssetLocation(contentCode)));
            else contentStack = new ItemStack(capi.World.GetBlock(new AssetLocation(contentCode)));

            float litres = gridRecipe.Attributes["liquidContainerProps"]["requiresLitres"].AsFloat();
            var props = GetInContainerProps(contentStack);
            contentStack.StackSize = (int)(props.ItemsPerLitre * litres);

            SetContent(filledContainerStack, contentStack);

            dummyslot.Itemstack = filledContainerStack;

            capi.Render.RenderItemstackToGui(
                dummyslot,
                x,
                y,
                100, (float)size * 0.58f, ColorUtil.WhiteArgb,
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
        protected WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (Attributes?["capacityLitres"].Exists == true)
            {
                capacityLitresFromAttributes = Attributes["capacityLitres"].AsInt(10);
            } else
            {
                var props = Attributes?["liquidContainerProps"]?.AsObject<LiquidTopOpenContainerProps>(null, Code.Domain);
                if (props != null)
                {
                    capacityLitresFromAttributes = props.CapacityLitres;
                }
            }

            


            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "liquidContainerBase", () =>
            {
                List<ItemStack> liquidContainerStacks = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj is ILiquidSource || obj is ILiquidSink || obj is BlockWateringCan)
                    {
                        List<ItemStack> stacks = obj.GetHandBookStacks(capi);
                        if (stacks == null) continue;

                        foreach (var stack in stacks)
                        {
                            stack.StackSize = 1;
                            liquidContainerStacks.Add(stack);
                        }
                    }
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
                        HotKeyCode = "sneak",
                        Itemstacks = lcstacks
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-bucket-rightclick-sprint",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "sprint",
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
                    HotKeyCode = "sprint",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => {
                        return GetCurrentLitres(inSlot.Itemstack) > 0;
                    }
                },
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-place",
                    HotKeyCode = "sneak",
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


        public WaterTightContainableProps GetContentProps(ItemStack containerStack)
        {
            ItemStack stack = GetContent(containerStack);
            return GetInContainerProps(stack);
        }


        public static int GetTransferStackSize(ILiquidInterface containerBlock, ItemStack contentStack, IPlayer player = null)
        {
            return GetTransferStackSize(containerBlock, contentStack, player?.Entity?.Controls.Sneak == true);
        }

        public static int GetTransferStackSize(ILiquidInterface containerBlock, ItemStack contentStack, bool maxCapacity)
        {
            if (contentStack == null) return 0;

            float litres = containerBlock.TransferSizeLitres;
            var liqProps = GetInContainerProps(contentStack);
            int stacksize = (int)(liqProps.ItemsPerLitre * litres);

            if (maxCapacity)
            {
                stacksize = (int)(containerBlock.CapacityLitres * liqProps.ItemsPerLitre);
            }

            return stacksize;
        }




        public static WaterTightContainableProps GetInContainerProps(ItemStack stack)
        {
            try
            {
                JsonObject obj = stack?.ItemAttributes?["waterTightContainerProps"];
                if (obj != null && obj.Exists) return obj.AsObject<WaterTightContainableProps>(null, stack.Collectible.Code.Domain);
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
        public WaterTightContainableProps GetContentProps(BlockPos pos)
        {
            BlockEntityContainer becontainer = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityContainer;
            if (becontainer == null) return null;

            int slotid = GetContainerSlotId(pos);
            if (slotid >=becontainer.Inventory.Count) return null;

            ItemStack stack = becontainer.Inventory[slotid]?.Itemstack;
            if (stack == null) return null;

            return GetInContainerProps(stack);
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
            BlockEntityContainer beContainer = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityContainer;
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
        public ItemStack GetContent(ItemStack containerStack)
        {
            ItemStack[] stacks = GetContents(api.World, containerStack);
            int id = GetContainerSlotId(containerStack);
            return (stacks != null && stacks.Length > 0) ? stacks[id] : null;
        }

        /// <summary>
        /// Retrieve the contents of a placed container
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        public ItemStack GetContent(BlockPos pos)
        {
            BlockEntityContainer becontainer = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityContainer;
            if (becontainer == null) return null;
            return becontainer.Inventory[GetContainerSlotId(pos)].Itemstack;
        }


        public override ItemStack CreateItemStackFromJson(ITreeAttribute stackAttr, IWorldAccessor world, string domain)
        {
            var stack = base.CreateItemStackFromJson(stackAttr, world, domain);

            if (stackAttr.HasAttribute("makefull"))
            {
                var props = GetInContainerProps(stack);
                stack.StackSize = (int)(CapacityLitres * props.ItemsPerLitre);
            }

            return stack;
        }

        /// <summary>
        /// Tries to take out as much items/liquid as possible and returns it
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
            takenStack.StackSize = quantityItems;

            stack.StackSize -= quantityItems;
            if (stack.StackSize <= 0) SetContent(containerStack, null);
            else SetContent(containerStack, stack);

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

            ItemStack stack = becontainer.Inventory[GetContainerSlotId(pos)].Itemstack;
            if (stack == null) return null;

            ItemStack takenStack = stack.Clone();
            takenStack.StackSize = quantityItem;

            stack.StackSize -= quantityItem;
            if (stack.StackSize <= 0) becontainer.Inventory[GetContainerSlotId(pos)].Itemstack = null;
            else becontainer.Inventory[GetContainerSlotId(pos)].Itemstack = stack;

            becontainer.Inventory[GetContainerSlotId(pos)].MarkDirty();
            becontainer.MarkDirty(true);

            return takenStack;
        }

        #endregion


        #region PutContents

        /// <summary>
        /// Tries to place in items/liquid and returns actually inserted quantity
        /// </summary>
        /// <param name="containerStack"></param>
        /// <param name="liquidStack"></param>
        /// <param name="desiredLitres"></param>
        /// <returns></returns>
        public virtual int TryPutLiquid(ItemStack containerStack, ItemStack liquidStack, float desiredLitres)
        {
            if (liquidStack == null) return 0;

            var props = GetInContainerProps(liquidStack);
            int desiredItems = (int)(props.ItemsPerLitre * desiredLitres);
            int availItems = liquidStack.StackSize;

            ItemStack stack = GetContent(containerStack);
            ILiquidSink sink = containerStack.Collectible as ILiquidSink;

            if (stack == null)
            {
                if (props == null || !props.Containable) return 0;

                int placeableItems = (int)(sink.CapacityLitres * props.ItemsPerLitre);

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

                stack.StackSize += Math.Min(placeableItems, desiredItems);

                return Math.Min(placeableItems, desiredItems);
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

            var props = GetInContainerProps(liquidStack);
            int desiredItems = (int)(props.ItemsPerLitre * desiredLitres);
            float availItems = liquidStack.StackSize;
            float maxItems = CapacityLitres * props.ItemsPerLitre;

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

            bool singleTake = byPlayer.WorldData.EntityControls.Sneak;
            bool singlePut = byPlayer.WorldData.EntityControls.Sprint;

            if (obj is ILiquidSource objLso && !singleTake)
            {
                var contentStackToMove = objLso.GetContent(hotbarSlot.Itemstack);

                float litres = singlePut ? objLso.TransferSizeLitres : objLso.CapacityLitres;
                int moved = TryPutLiquid(blockSel.Position, contentStackToMove, litres);
                
                if (moved > 0)
                {
                    objLso.TryTakeContent(hotbarSlot.Itemstack, moved);
                    DoLiquidMovedEffects(byPlayer, contentStackToMove, moved, EnumLiquidDirection.Pour);
                    return true;
                }
            }

            if (obj is ILiquidSink objLsi && !singlePut)
            {
                ItemStack owncontentStack = GetContent(blockSel.Position);

                if (owncontentStack == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

                var liquidStackForParticles = owncontentStack.Clone();

                float litres = singleTake ? objLsi.TransferSizeLitres : objLsi.CapacityLitres;
                int moved;

                if (hotbarSlot.Itemstack.StackSize == 1)
                {
                    moved = objLsi.TryPutLiquid(hotbarSlot.Itemstack, owncontentStack, litres);
                } else
                {
                    ItemStack containerStack = hotbarSlot.Itemstack.Clone();
                    containerStack.StackSize = 1;
                    moved = objLsi.TryPutLiquid(containerStack, owncontentStack, litres);

                    if (moved > 0)
                    {
                        hotbarSlot.TakeOut(1);
                        if (!byPlayer.InventoryManager.TryGiveItemstack(containerStack, true))
                        {
                            api.World.SpawnItemEntity(containerStack, byPlayer.Entity.SidedPos.XYZ);
                        }
                    }
                }

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

            WaterTightContainableProps props = GetInContainerProps(contentStack);
            float litresMoved = (float)moved / props.ItemsPerLitre;

            (player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
            api.World.PlaySoundAt(dir == EnumLiquidDirection.Fill ? new AssetLocation("sounds/effect/water-fill.ogg") : new AssetLocation("sounds/effect/water-pour.ogg"), player.Entity, player, true, 16, GameMath.Min(1, litresMoved / 5f));
            api.World.SpawnCubeParticles(player.Entity.Pos.AheadCopy(0.25).XYZ.Add(0, player.Entity.CollisionBox.Y2 / 2, 0), contentStack, 0.75f, (int)litresMoved * 2, 0.45f);
        }

        #endregion


        #region Held Interact

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null || byEntity.Controls.Sneak)
            {
                base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
                return;
            }

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
                        byEntity.World.PlaySoundAt(props.FillSpillSound, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                    }

                }
                else
                {
                    if (byEntity.Controls.Sprint)
                    {
                        SpillContents(itemslot, byEntity, blockSel);
                    }
                }
            }

            if (GetNutritionProperties(byEntity.World, itemslot.Itemstack, byEntity) != null)
            {
                tryEatBegin(itemslot, byEntity, ref handHandling, "drink", 4);
                return;
            }

            // Prevent placing on normal use
            handHandling = EnumHandHandling.PreventDefaultAction;
        }

        public override FoodNutritionProperties GetNutritionProperties(IWorldAccessor world, ItemStack itemstack, Entity forEntity)
        {
            ItemStack contentStack = GetContent(itemstack);
            WaterTightContainableProps props = contentStack == null ? null : GetInContainerProps(contentStack);

            if (props?.NutritionPropsPerLitre != null)
            {
                var nutriProps = props.NutritionPropsPerLitre.Clone();
                float litre = (float)contentStack.StackSize / props.ItemsPerLitre;
                nutriProps.Health *= litre;
                nutriProps.Satiety *= litre;
                nutriProps.EatenStack = new JsonItemStack();
                nutriProps.EatenStack.ResolvedItemstack = itemstack.Clone();
                (nutriProps.EatenStack.ResolvedItemstack.Collectible as BlockLiquidContainerBase).SetContent(nutriProps.EatenStack.ResolvedItemstack, null);

                return nutriProps;
            }

            return base.GetNutritionProperties(world, itemstack, forEntity);
        }


        public bool TryFillFromBlock(ItemSlot itemslot, EntityAgent byEntity, BlockPos pos)
        {
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            IBlockAccessor blockAcc = byEntity.World.BlockAccessor;

            Block block = blockAcc.GetBlock(pos);
            if (block.Attributes?["waterTightContainerProps"].Exists == false) return false;

            WaterTightContainableProps props = block.Attributes?["waterTightContainerProps"]?.AsObject<WaterTightContainableProps>();
            if (props?.WhenFilled == null || !props.Containable) return false;

            props.WhenFilled.Stack.Resolve(byEntity.World, "liquidcontainerbase");

            ItemStack contentStack = GetContent(itemslot.Itemstack);

            if (contentStack != null && contentStack.Equals(props.WhenFilled.Stack.ResolvedItemstack))
            {
                SetContent(itemslot.Itemstack, contentStack);
                itemslot.MarkDirty();
                return true;
            }

            // Is full
            int maxStackSize = (int)(CapacityLitres / props.ItemsPerLitre);
            if (contentStack != null && contentStack.StackSize == maxStackSize) return false;

            contentStack = props.WhenFilled.Stack.ResolvedItemstack.Clone();

            WaterTightContainableProps cprops = contentStack.ItemAttributes?["waterTightContainerProps"]?.AsObject<WaterTightContainableProps>();

            contentStack.StackSize = (int)(cprops.ItemsPerLitre * CapacityLitres);

            ItemStack fullContainerStack = new ItemStack(this);
            SetContent(fullContainerStack, contentStack);


            if (itemslot.Itemstack.StackSize <= 1)
            {
                itemslot.Itemstack = fullContainerStack;
            }
            else
            {
                itemslot.TakeOut(1);
                if (!byPlayer.InventoryManager.TryGiveItemstack(fullContainerStack, true))
                {
                    byEntity.World.SpawnItemEntity(fullContainerStack, byEntity.SidedPos.XYZ);
                }
            }

            itemslot.MarkDirty();

            DoLiquidMovedEffects(byPlayer, contentStack, contentStack.StackSize, EnumLiquidDirection.Fill);
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

            whenFilledStack.StackSize = (int)(props.ItemsPerLitre * CapacityLitres);

            ItemStack fullContainerStack = new ItemStack(this);
            SetContent(fullContainerStack, whenFilledStack);

            if (byEntityItem.Itemstack.StackSize <= 1)
            {
                byEntityItem.Itemstack = fullContainerStack;
            }
            else
            {
                byEntityItem.Itemstack.StackSize--;
                world.SpawnItemEntity(fullContainerStack, byEntityItem.SidedPos.XYZ);
            }

            //world.PlaySoundAt(props.FillSpillSound, pos.X, pos.Y, pos.Z, null);
            world.PlaySoundAt(new AssetLocation("sounds/effect/water-fill.ogg"), pos.X, pos.Y, pos.Z, null);
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
                    JsonItemStack fillLevelStack;
                    props.WhenSpilled.StackByFillLevel.TryGetValue((int)currentlitres, out fillLevelStack);
                    if (fillLevelStack != null) waterBlock = byEntity.World.GetBlock(fillLevelStack.Code);
                }

                Block currentblock = blockAcc.GetBlock(pos);
                if (currentblock.Replaceable >= 6000)
                {
                    blockAcc.SetBlock(waterBlock.BlockId, pos);
                    blockAcc.TriggerNeighbourBlockUpdate(pos);
                    blockAcc.MarkBlockDirty(pos);
                }
                else
                {
                    if (blockAcc.GetBlock(secondPos).Replaceable >= 6000)
                    {
                        blockAcc.SetBlock(waterBlock.BlockId, secondPos);
                        blockAcc.TriggerNeighbourBlockUpdate(pos);
                        blockAcc.MarkBlockDirty(secondPos);
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


            ItemStack emptyContainerStack = new ItemStack(this);

            if (containerSlot.Itemstack.StackSize <= 1)
            {
                containerSlot.Itemstack = emptyContainerStack;
                containerSlot.MarkDirty();
            }
            else
            {
                containerSlot.TakeOut(1);
                if (!byPlayer.InventoryManager.TryGiveItemstack(emptyContainerStack, true))
                {
                    byEntity.World.SpawnItemEntity(emptyContainerStack, byEntity.SidedPos.XYZ);
                }
            }

            DoLiquidMovedEffects(byPlayer, contentStack, contentStack.StackSize, EnumLiquidDirection.Pour);
            return true;
        }





        #endregion




        public override void OnGroundIdle(EntityItem entityItem)
        {
            base.OnGroundIdle(entityItem);

            IWorldAccessor world = entityItem.World;
            if (world.Side != EnumAppSide.Server) return;

            if (entityItem.Swimming && world.Rand.NextDouble() < 0.03)
            {
                TryFillFromBlock(entityItem, entityItem.SidedPos.AsBlockPos);
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
                            world.SpawnItemEntity(stacks[i], entityItem.ServerPos.XYZ);
                        }
                    }

                    SetContent(entityItem.Itemstack, null);
                }
            }
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            GetContentInfo(inSlot, dsc, world);
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            float litres = GetCurrentLitres(pos);

            BlockEntityContainer becontainer = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityContainer;
            if (becontainer == null) return "";

            ItemSlot slot = becontainer.Inventory[GetContainerSlotId(pos)];
            ItemStack contentStack = slot.Itemstack;


            if (litres <= 0) return Lang.Get("Empty");

            string incontainername = Lang.Get("incontainer-" + contentStack.Class.ToString().ToLowerInvariant() + "-" + contentStack.Collectible.Code.Path);
            string text = Lang.Get("Contents:\n{0} litres of {1}", litres, incontainername);
            if (litres == 1)
            {
                text = Lang.Get("Contents:\n{0} litre of {1}", litres, incontainername);
            }
            

            text += PerishableInfoCompact(api, slot, 0, false);

            return text;
        }


        public virtual void GetContentInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world)
        {
            float litres = GetCurrentLitres(inSlot.Itemstack);
            ItemStack contentStack = GetContent(inSlot.Itemstack);

            if (litres <= 0) dsc.Append(Lang.Get("Empty"));

            else
            {
                string incontainerrname = Lang.Get("incontainer-" + contentStack.Class.ToString().ToLowerInvariant() + "-" + contentStack.Collectible.Code.Path);
                if (litres == 1)
                {
                    dsc.Append(Lang.Get("Contents: {0} litre of {1}", litres, incontainerrname));
                } else
                {
                    dsc.Append(Lang.Get("Contents: {0} litres of {1}", litres, incontainerrname));
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

            WaterTightContainableProps props = GetInContainerProps(sourceContent);
            float maxItems = CapacityLitres * props.ItemsPerLitre;
            int sourceEmptySpace = (int)(maxItems - (float)sourceContent.StackSize / props.ItemsPerLitre);
            int sinkEmptySpace = (int)(maxItems - (float)sinkContent.StackSize / props.ItemsPerLitre);
            if (sourceEmptySpace == 0 && sinkEmptySpace == 0)
            {
                // Full buckets are not stackable
                op.MovableQuantity = 0;
                //base.TryMergeStacks(op);
                return;
            }

            if (op.CurrentPriority == EnumMergePriority.DirectMerge)
            {
                int moved = TryPutLiquid(op.SinkSlot.Itemstack, sinkContent, CapacityLitres);
                TryTakeContent(op.SourceSlot.Itemstack, moved);
                op.SourceSlot.MarkDirty();
                op.SinkSlot.MarkDirty();
            }

            op.MovableQuantity = 0;
            return;
        }

        public override bool MatchesForCrafting(ItemStack inputStack, GridRecipe gridRecipe, CraftingRecipeIngredient ingredient)
        {
            if (gridRecipe.Attributes?["liquidContainerProps"].Exists != true)
            {
                return base.MatchesForCrafting(inputStack, gridRecipe, ingredient);
            }

            string contentCode = gridRecipe.Attributes["liquidContainerProps"]["requiresContent"]["code"].AsString();
            string contentType = gridRecipe.Attributes["liquidContainerProps"]["requiresContent"]["type"].AsString();

            ItemStack contentStack = GetContent(inputStack);

            if (contentStack == null) return false;

            float litres = gridRecipe.Attributes["liquidContainerProps"]["requiresLitres"].AsFloat();
            var props = GetInContainerProps(contentStack);
            int q = (int)(props.ItemsPerLitre * litres);

            bool a = contentStack.Class.ToString().ToLowerInvariant() == contentType.ToLowerInvariant();
            bool b = WildcardUtil.Match(contentStack.Collectible.Code, new AssetLocation(contentCode));
            bool c = contentStack.StackSize >= q;

            return a && b && c;
        }

        public override void OnConsumedByCrafting(ItemSlot[] allInputSlots, ItemSlot stackInSlot, GridRecipe gridRecipe, CraftingRecipeIngredient fromIngredient, IPlayer byPlayer, int quantity)
        {
            if (gridRecipe.Attributes?["liquidContainerProps"].Exists != true)
            {
                base.OnConsumedByCrafting(allInputSlots, stackInSlot, gridRecipe, fromIngredient, byPlayer, quantity);
                return;
            }

            ItemStack contentStack = GetContent(stackInSlot.Itemstack);
            float litres = gridRecipe.Attributes["liquidContainerProps"]["requiresLitres"].AsFloat();
            var props = GetInContainerProps(contentStack);
            int q = (int)(props.ItemsPerLitre * litres);

            TryTakeContent(stackInSlot.Itemstack, q);
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


                            if (transitionLevel > 0)
                            {
                                dsc.Append(comma + Lang.Get("{0}% spoiled", (int)Math.Round(transitionLevel * 100)));
                            }
                            else
                            {
                                double hoursPerday = Api.World.Calendar.HoursPerDay;

                                if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                                {
                                    dsc.Append(comma + Lang.Get("fresh for {0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                                }
                                else if (freshHoursLeft > hoursPerday)
                                {
                                    dsc.Append(comma + Lang.Get("fresh for {0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                                }
                                else
                                {
                                    dsc.Append(comma + Lang.Get("fresh for {0} hours", Math.Round(freshHoursLeft, 1)));
                                }
                            }
                            break;

                        case EnumTransitionType.Ripen:

                            if (transitionLevel > 0)
                            {
                                dsc.Append(comma + Lang.Get("{1:0.#} days left to ripen ({0}%)", (int)Math.Round(transitionLevel * 100), (state.TransitionHours - state.TransitionedHours) / Api.World.Calendar.HoursPerDay / ripenRate));
                            }
                            else
                            {
                                double hoursPerday = Api.World.Calendar.HoursPerDay;

                                if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                                {
                                    dsc.Append(comma + Lang.Get("will ripen in {0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                                }
                                else if (freshHoursLeft > hoursPerday)
                                {
                                    dsc.Append(comma + Lang.Get("will ripen in {0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                                }
                                else
                                {
                                    dsc.Append(comma + Lang.Get("will ripen in {0} hours", Math.Round(freshHoursLeft, 1)));
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
