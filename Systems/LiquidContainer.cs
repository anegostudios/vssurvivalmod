using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockTopTextureSource : ITexPositionSource
    {
        ICoreClientAPI capi;
        Block block;

        public BlockTopTextureSource(ICoreClientAPI capi, Block block)
        {
            this.capi = capi;
            this.block = block;
        }

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                TextureAtlasPosition pos = capi.BlockTextureAtlas.GetPosition(block, "up");
                return pos;
            }
        }


    }

    public class ContainerTextureSource : ITexPositionSource
    {
        public ItemStack forContents;
        private ICoreClientAPI capi;

        TextureAtlasPosition contentTextPos;
        CompositeTexture contentTexture;

        public ContainerTextureSource(ICoreClientAPI capi, ItemStack forContents, CompositeTexture contentTexture)
        {
            this.capi = capi;
            this.forContents = forContents;
            this.contentTexture = contentTexture;
        }

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (contentTextPos == null)
                {
                    int textureSubId;

                    textureSubId = ObjectCacheUtil.GetOrCreate<int>(capi, "contenttexture-" + contentTexture.ToString(), () =>
                    {
                        TextureAtlasPosition texPos;
                        int id = 0;

                        BitmapRef bmp = capi.Assets.TryGet(contentTexture.Base.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"))?.ToBitmap(capi);
                        if (bmp != null)
                        {
                            capi.BlockTextureAtlas.InsertTexture(bmp, out id, out texPos);
                            bmp.Dispose();
                        }

                        return id;
                    });

                    contentTextPos = capi.BlockTextureAtlas.Positions[textureSubId];
                }

                return contentTextPos;
            }
        }

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
    }


    public class WaterTightContainableProps
    {
        public bool Containable;
        public float ItemsPerLitre;
        public AssetLocation FillSpillSound = new AssetLocation("sounds/block/water");
        public CompositeTexture Texture;
        public string ClimateColorMap = null;
        public bool AllowSpill = true;
        public WhenSpilledProps WhenSpilled;
        public WhenFilledProps WhenFilled;

        public enum EnumSpilledAction { PlaceBlock, DropContents };

        public class WhenFilledProps
        {
            public JsonItemStack Stack;
        }

        public class WhenSpilledProps
        {
            public Dictionary<int, JsonItemStack> StackByFillLevel;
            public EnumSpilledAction Action;
            public JsonItemStack Stack;
        }
    }

    public interface ILiquidInterfaceBasic
    {

    }

    public abstract class BlockLiquidContainerBase : BlockContainer, ILiquidSource, ILiquidSink
    {
        protected float capacityLitresFromAttributes = 10;
        public virtual float CapacityLitres => capacityLitresFromAttributes;
        public virtual int ContainerSlotId => 0;

        


        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, ItemSlot inSlot, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping)
        {
            base.OnLoadCollectibleMappings(worldForResolve, inSlot, oldBlockIdMapping, oldItemIdMapping);
        }


        public virtual int GetContainerSlotId(IWorldAccessor world, BlockPos pos)
        {
            return ContainerSlotId;
        }
        public virtual int GetContainerSlotId(IWorldAccessor world, ItemStack containerStack)
        {
            return ContainerSlotId;
        }

        #region Interaction help
        protected WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            if (Attributes?["capacityLitres"].Exists == true)
            {
                capacityLitresFromAttributes = Attributes["capacityLitres"].AsInt(10);
            }

            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "liquidContainerBase", () =>
            {
                List<ItemStack> liquidContainerStacks = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if ((obj is BlockBowl && obj.LastCodePart() != "raw") || obj is ILiquidSource || obj is ILiquidSink || obj is BlockWateringCan)
                    {
                        List<ItemStack> stacks = obj.GetHandBookStacks(capi);
                        if (stacks != null) liquidContainerStacks.AddRange(stacks);
                    }
                }

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-bucket-rightclick",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = liquidContainerStacks.ToArray()
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
                        return GetCurrentLitres(api.World, inSlot.Itemstack) < CapacityLitres;
                    }
                },
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-empty",
                    HotKeyCode = "sprint",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => {
                        return GetCurrentLitres(api.World, inSlot.Itemstack) > 0;
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

        public float GetCurrentLitres(IWorldAccessor world, ItemStack containerStack)
        {
            WaterTightContainableProps props = GetContentProps(world, containerStack);
            if (props == null) return 0;

            return GetContent(world, containerStack).StackSize / props.ItemsPerLitre;
        }


        public float GetCurrentLitres(IWorldAccessor world, BlockPos pos)
        {
            WaterTightContainableProps props = GetContentProps(world, pos);
            if (props == null) return 0;

            return GetContent(world, pos).StackSize / props.ItemsPerLitre;
        }


        public WaterTightContainableProps GetContentProps(IWorldAccessor world, ItemStack containerStack)
        {
            ItemStack stack = GetContent(world, containerStack);
            return GetInContainerProps(stack);
        }

        public static WaterTightContainableProps GetInContainerProps(ItemStack stack)
        {
            try
            {
                JsonObject obj = stack?.ItemAttributes?["waterTightContainerProps"];
                if (obj != null && obj.Exists) return obj.AsObject<WaterTightContainableProps>();
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
        public WaterTightContainableProps GetContentProps(IWorldAccessor world, BlockPos pos)
        {
            BlockEntityContainer becontainer = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityContainer;
            if (becontainer == null) return null;

            int slotid = GetContainerSlotId(world, pos);
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
                SetContents(containerStack, new ItemStack[] { });
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
        public void SetContent(IWorldAccessor world, BlockPos pos, ItemStack content)
        {
            BlockEntityContainer beContainer = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityContainer;
            if (beContainer == null) return;

            new DummySlot(content).TryPutInto(world, beContainer.Inventory[GetContainerSlotId(world, pos)], content.StackSize);
            
            beContainer.Inventory[GetContainerSlotId(world, pos)].MarkDirty();
            beContainer.MarkDirty(true);
        }


        /// <summary>
        /// Retrieve the contents of the container stack
        /// </summary>
        /// <param name="world"></param>
        /// <param name="containerStack"></param>
        /// <returns></returns>
        public ItemStack GetContent(IWorldAccessor world, ItemStack containerStack)
        {
            ItemStack[] stacks = GetContents(world, containerStack);
            return (stacks != null && stacks.Length > 0) ? stacks[GetContainerSlotId(world, containerStack)] : null;
        }

        /// <summary>
        /// Retrieve the contents of a placed container
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        public ItemStack GetContent(IWorldAccessor world, BlockPos pos)
        {
            BlockEntityContainer becontainer = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityContainer;
            if (becontainer == null) return null;
            return becontainer.Inventory[GetContainerSlotId(world, pos)].Itemstack;
        }

        /// <summary>
        /// Tries to take out as much items/liquid as possible and returns it
        /// </summary>
        /// <param name="world"></param>
        /// <param name="containerStack"></param>
        /// <param name="quantityItems"></param>
        /// <returns></returns>
        public ItemStack TryTakeContent(IWorldAccessor world, ItemStack containerStack, int quantityItems)
        {
            ItemStack stack = GetContent(world, containerStack);
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
        public ItemStack TryTakeContent(IWorldAccessor world, BlockPos pos, int quantityItem)
        {
            BlockEntityContainer becontainer = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityContainer;
            if (becontainer == null) return null;

            ItemStack stack = becontainer.Inventory[GetContainerSlotId(world, pos)].Itemstack;
            if (stack == null) return null;

            ItemStack takenStack = stack.Clone();
            takenStack.StackSize = quantityItem;

            stack.StackSize -= quantityItem;
            if (stack.StackSize <= 0) becontainer.Inventory[GetContainerSlotId(world, pos)].Itemstack = null;
            else becontainer.Inventory[GetContainerSlotId(world, pos)].Itemstack = stack;

            becontainer.Inventory[GetContainerSlotId(world, pos)].MarkDirty();
            becontainer.MarkDirty(true);

            return takenStack;
        }

        #endregion


        #region PutContents

        /// <summary>
        /// Tries to place in items/liquid and returns actually inserted quantity
        /// </summary>
        /// <param name="world"></param>
        /// <param name="containerStack"></param>
        /// <param name="desiredItems"></param>
        /// <returns></returns>
        public virtual int TryPutContent(IWorldAccessor world, ItemStack containerStack, ItemStack contentStack, int desiredItems)
        {
            if (contentStack == null) return 0;

            ItemStack stack = GetContent(world, containerStack);

            int availItems = contentStack.StackSize;

            ILiquidSink sink = containerStack.Collectible as ILiquidSink;

            if (stack == null)
            {
                WaterTightContainableProps props = GetInContainerProps(contentStack);
                if (props == null || !props.Containable) return 0;


                int placeableItems = (int)(sink.CapacityLitres * props.ItemsPerLitre);

                ItemStack placedstack = contentStack.Clone();
                placedstack.StackSize = GameMath.Min(availItems, desiredItems, placeableItems);
                SetContent(containerStack, placedstack);

                return Math.Min(desiredItems, placeableItems);
            }
            else
            {
                if (!stack.Equals(world, contentStack, GlobalConstants.IgnoredStackAttributes)) return 0;

                WaterTightContainableProps props = GetContentProps(world, containerStack);

                float maxItems = sink.CapacityLitres * props.ItemsPerLitre;
                int placeableItems = (int)(maxItems - (float)stack.StackSize / props.ItemsPerLitre);

                stack.StackSize += Math.Min(placeableItems, desiredItems);

                return Math.Min(placeableItems, desiredItems);
            }
        }

        /// <summary>
        /// Tries to put as much items/liquid as possible into a placed container and returns it how much items it actually moved
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <param name="quantity"></param>
        public virtual int TryPutContent(IWorldAccessor world, BlockPos pos, ItemStack contentStack, int quantity)
        {
            if (contentStack == null) return 0;

            float availItems = contentStack.StackSize;

            ItemStack stack = GetContent(world, pos);
            if (stack == null)
            {
                WaterTightContainableProps props = GetInContainerProps(contentStack);
                if (props == null || !props.Containable) return 0;


                float maxItems = CapacityLitres * props.ItemsPerLitre;
                

                int placeableItems = (int)GameMath.Min(quantity, maxItems, availItems);

                ItemStack placedstack = contentStack.Clone();
                placedstack.StackSize = Math.Min(quantity, placeableItems);
                SetContent(world, pos, placedstack);

                return Math.Min(quantity, placeableItems);
            }
            else
            {
                if (!stack.Equals(world, contentStack, GlobalConstants.IgnoredStackAttributes)) return 0;

                WaterTightContainableProps props = GetContentProps(world, pos);

                float maxItems = CapacityLitres * props.ItemsPerLitre;
                int placeableItems = (int)Math.Min(availItems, maxItems - (float)stack.StackSize);

                stack.StackSize += Math.Min(placeableItems, quantity);
                world.BlockAccessor.GetBlockEntity(pos).MarkDirty(true);
                (world.BlockAccessor.GetBlockEntity(pos) as BlockEntityContainer).Inventory[GetContainerSlotId(world, pos)].MarkDirty();

                return Math.Min(placeableItems, quantity);
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

            if (obj is ILiquidSource && !singleTake)
            {
                int moved = TryPutContent(world, blockSel.Position, (obj as ILiquidSource).GetContent(world, hotbarSlot.Itemstack), singlePut ? 1 : 9999);
                
                if (moved > 0)
                {
                    (obj as ILiquidSource).TryTakeContent(world, hotbarSlot.Itemstack, moved);
                    (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                    
                    return true;
                }
            }

            if (obj is ILiquidSink && !singlePut)
            {
                ItemStack owncontentStack = GetContent(world, blockSel.Position);
                int moved = 0;

                if (hotbarSlot.Itemstack.StackSize == 1)
                {
                    moved = (obj as ILiquidSink).TryPutContent(world, hotbarSlot.Itemstack, owncontentStack, singleTake ? 1 : 9999);
                } else
                {
                    ItemStack containerStack = hotbarSlot.Itemstack.Clone();
                    containerStack.StackSize = 1;
                    moved = (obj as ILiquidSink).TryPutContent(world, containerStack, owncontentStack, singleTake ? 1 : 9999);

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
                    TryTakeContent(world, blockSel.Position, moved);
                    (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                    return true;
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        #endregion


        #region Held Interact

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null || byEntity.Controls.Sneak) return;
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;

            ItemStack contentStack = GetContent(byEntity.World, itemslot.Itemstack);
            bool isEmpty = contentStack == null || contentStack.StackSize == 0;

            Block targetedBlock = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);

            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byEntity.World.BlockAccessor.MarkBlockDirty(blockSel.Position.AddCopy(blockSel.Face));
                byPlayer?.InventoryManager.ActiveHotbarSlot?.MarkDirty();
                return;
            }

            if (!TryFillFromBlock(itemslot, byEntity, blockSel.Position))
            {
                BlockBucket targetBucket = targetedBlock as BlockBucket;
                if (targetBucket != null)
                {
                    WaterTightContainableProps props = GetContentProps(byEntity.World, itemslot.Itemstack);

                    if (targetBucket.TryPutContent(byEntity.World, blockSel.Position, contentStack, 1) > 0)
                    {
                        TryTakeContent(byEntity.World, itemslot.Itemstack, 1);
                        byEntity.World.PlaySoundAt(props.FillSpillSound, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                    }

                }
                else
                {
                    if (byPlayer.WorldData.EntityControls.Sprint)
                    {
                        SpillContents(itemslot, byEntity, blockSel);
                    }
                }
            }

            // Prevent placing on normal use
            handHandling = EnumHandHandling.PreventDefaultAction;
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

            ItemStack contentStack = GetContent(byEntity.World, itemslot.Itemstack);

            if (contentStack != null && contentStack.Equals(props.WhenFilled.Stack.ResolvedItemstack))
            {
                SetContent(itemslot.Itemstack, contentStack);
                itemslot.MarkDirty();
                return true;
            }

            // Is full
            if (contentStack != null && contentStack.StackSize == (int)(props.ItemsPerLitre * CapacityLitres)) return false;

            contentStack = props.WhenFilled.Stack.ResolvedItemstack.Clone();
            contentStack.StackSize = (int)(props.ItemsPerLitre * CapacityLitres);

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
            byEntity.World.PlaySoundAt(props.FillSpillSound, pos.X, pos.Y, pos.Z, byPlayer);

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
            ItemStack contentStack = GetContent(world, byEntityItem.Itemstack);
            bool canFill = contentStack == null || (contentStack.Equals(world, whenFilledStack, GlobalConstants.IgnoredStackAttributes) && GetCurrentLitres(world, byEntityItem.Itemstack) < CapacityLitres);
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

            world.PlaySoundAt(props.FillSpillSound, pos.X, pos.Y, pos.Z, null);
        }


        private bool SpillContents(ItemSlot containerSlot, EntityAgent byEntity, BlockSelection blockSel)
        {
            BlockPos pos = blockSel.Position;
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            IBlockAccessor blockAcc = byEntity.World.BlockAccessor;
            BlockPos secondPos = blockSel.Position.AddCopy(blockSel.Face);


            WaterTightContainableProps props = GetContentProps(byEntity.World, containerSlot.Itemstack);

            if (props == null || !props.AllowSpill || props.WhenSpilled == null) return false;

            if (!byEntity.World.Claims.TryAccess(byPlayer, secondPos, EnumBlockAccessFlags.BuildOrBreak))
            {
                return false;
            }

            if (props.WhenSpilled.Action == WaterTightContainableProps.EnumSpilledAction.PlaceBlock)
            {
                Block waterBlock = byEntity.World.GetBlock(props.WhenSpilled.Stack.Code);

                if (props.WhenSpilled.StackByFillLevel != null)
                {
                    float currentlitres = GetCurrentLitres(byEntity.World, containerSlot.Itemstack);
                    JsonItemStack fillLevelStack = null;
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
                        blockAcc.MarkBlockDirty(secondPos);
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            if (props.WhenSpilled.Action == WaterTightContainableProps.EnumSpilledAction.DropContents)
            {
                props.WhenSpilled.Stack.Resolve(byEntity.World, "liquidcontainerbasespill");

                ItemStack stack = props.WhenSpilled.Stack.ResolvedItemstack.Clone();
                stack.StackSize = (int)(props.ItemsPerLitre * GetContent(byEntity.World, containerSlot.Itemstack).StackSize);

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

            byEntity.World.PlaySoundAt(props.FillSpillSound, pos.X, pos.Y, pos.Z, byPlayer);
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
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            float litres = GetCurrentLitres(world, inSlot.Itemstack);
            ItemStack contentStack = GetContent(world, inSlot.Itemstack);

            if (litres <= 0) dsc.Append(Lang.Get("Empty"));

            else
            {
                string incontainerrname = Lang.Get("incontainer-" + contentStack.Class.ToString().ToLowerInvariant() + "-" + contentStack.Collectible.Code.Path);
                dsc.Append(Lang.Get("Contents: {0} litres of {1}", litres, incontainerrname));
            }
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            float litres = GetCurrentLitres(world, pos);

            BlockEntityContainer becontainer = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityContainer;
            if (becontainer == null) return "";

            ItemSlot slot = becontainer.Inventory[GetContainerSlotId(world, pos)];
            ItemStack contentStack = slot.Itemstack;


            if (litres <= 0) return Lang.Get("Empty");

            string incontainername = Lang.Get("incontainer-" + contentStack.Class.ToString().ToLowerInvariant() + "-" + contentStack.Collectible.Code.Path);
            return Lang.Get("Contents: {0} litres of {1}", litres, incontainername);
        }


        public override void TryMergeStacks(ItemStackMergeOperation op)
        {
            op.MovableQuantity = GetMergableQuantity(op.SinkSlot.Itemstack, op.SourceSlot.Itemstack);
            if (op.MovableQuantity == 0) return;
            if (!op.SinkSlot.CanTakeFrom(op.SourceSlot)) return;

            ItemStack sinkContent = GetContent(op.World, op.SinkSlot.Itemstack);
            ItemStack sourceContent = GetContent(op.World, op.SourceSlot.Itemstack);

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
                int moved = TryPutContent(op.World, op.SinkSlot.Itemstack, sinkContent, sourceContent.StackSize);
                TryTakeContent(op.World, op.SourceSlot.Itemstack, moved);
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

            ItemStack contentStack = GetContent(api.World, inputStack);

            api.World.Logger.VerboseDebug("LiquidContainer.MatchesForCrafting: contentStack null? " + (contentStack==null));

            if (contentStack == null) return false;

            int q = gridRecipe.Attributes["liquidContainerProps"]["requiresQuantity"].AsInt();

            bool a = contentStack.Class.ToString().ToLowerInvariant() == contentType.ToLowerInvariant();
            bool b = WildcardUtil.Match(contentStack.Collectible.Code, new AssetLocation(contentCode));
            bool c = contentStack.StackSize >= q;

            api.World.Logger.VerboseDebug("LiquidContainer.MatchesForCrafting: {0} && {1} && {2}", a, b, c);

            return a && b && c;
        }

        public override void OnConsumedByCrafting(ItemSlot[] allInputSlots, ItemSlot stackInSlot, GridRecipe gridRecipe, CraftingRecipeIngredient fromIngredient, IPlayer byPlayer, int quantity)
        {
            if (gridRecipe.Attributes?["liquidContainerProps"].Exists != true)
            {
                base.OnConsumedByCrafting(allInputSlots, stackInSlot, gridRecipe, fromIngredient, byPlayer, quantity);
                return;
            }

            int q = gridRecipe.Attributes["liquidContainerProps"]["requiresQuantity"].AsInt();
            TryTakeContent(byPlayer.Entity.World, stackInSlot.Itemstack, q);
        }
    }


    public interface ILiquidInterface
    {
        /// <summary>
        /// Liquid Capacity in litres
        /// </summary>
        float CapacityLitres { get; }


        /// <summary>
        /// Current amount of liquid in this container in the inventory. From 0...Capacity
        /// </summary>
        /// <param name="world"></param>
        /// <param name="containerStack"></param>
        /// <returns></returns>
        float GetCurrentLitres(IWorldAccessor world, ItemStack containerStack);

        /// <summary>
        /// Current amount of liquid in this placed container. From 0...Capacity
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        float GetCurrentLitres(IWorldAccessor world, BlockPos pos);


        /// <summary>
        /// Retrives the containable properties of the currently contained itemstack
        /// </summary>
        /// <param name="world"></param>
        /// <param name="containerStack"></param>
        /// <returns></returns>
        WaterTightContainableProps GetContentProps(IWorldAccessor world, ItemStack containerStack);

        /// <summary>
        /// Retrives the containable properties of the container block at given position
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        WaterTightContainableProps GetContentProps(IWorldAccessor world, BlockPos pos);

        /// <summary>
        /// Returns the containing itemstack of the liquid source stack
        /// </summary>
        /// <param name="world"></param>
        /// <param name="containerStack"></param>
        /// <returns></returns>
        ItemStack GetContent(IWorldAccessor world, ItemStack containerStack);

        /// <summary>
        /// Returns the containing itemstack of a placed liquid source block
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        ItemStack GetContent(IWorldAccessor world, BlockPos pos);
    }


    public interface ILiquidSource : ILiquidInterface
    {
        /// <summary>
        /// Tries to take out as much items/liquid as possible and returns it
        /// </summary>
        /// <param name="world"></param>
        /// <param name="containerStack"></param>
        /// <param name="quantity"></param>
        /// <returns></returns>
        ItemStack TryTakeContent(IWorldAccessor world, ItemStack containerStack, int quantity);


        /// <summary>
        /// Tries to take out as much items/liquid as possible from a placed container and returns it
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <param name="quantity"></param>
        ItemStack TryTakeContent(IWorldAccessor world, BlockPos pos, int quantity);
    }


    public interface ILiquidSink : ILiquidInterface
    {
        /// <summary>
        /// Sets the liquid source contents to given stack
        /// </summary>
        /// <param name="containerStack"></param>
        /// <param name="content"></param>
        void SetContent(ItemStack containerStack, ItemStack content);

        /// <summary>
        /// Sets the containers contents to placed liquid source block
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <param name="content"></param>
        void SetContent(IWorldAccessor world, BlockPos pos, ItemStack content);


        /// <summary>
        /// Tries to place in items/liquid and returns actually inserted quantity
        /// </summary>
        /// <param name="world"></param>
        /// <param name="containerStack"></param>
        /// <param name="contentStack"></param>
        /// <param name="quantity"></param>
        /// <returns></returns>
        int TryPutContent(IWorldAccessor world, ItemStack containerStack, ItemStack contentStack, int quantity);

        /// <summary>
        /// Tries to put as much items/liquid as possible into a placed container and returns it how much items it actually moved
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <param name="quantity"></param>
        int TryPutContent(IWorldAccessor world, BlockPos pos, ItemStack contentStack, int quantity);
        
    }
}
