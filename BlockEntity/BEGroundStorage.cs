using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{

    public class BlockEntityGroundStorage : BlockEntityDisplay, IBlockEntityContainer, ITexPositionSource
    {
        public object inventoryLock = new object(); // Because OnTesselation runs in another thread

        protected InventoryGeneric inventory;

        public GroundStorageProperties StorageProps { get; protected set; }
        public bool forceStorageProps = false;
        protected EnumGroundStorageLayout? overrideLayout;

        public int TransferQuantity => StorageProps?.TransferQuantity ?? 1;
        public int BulkTransferQuantity => StorageProps.Layout == EnumGroundStorageLayout.Stacking ? StorageProps.BulkTransferQuantity : 1;

        protected virtual int invSlotCount => 4;
        protected Cuboidf[] colBoxes;
        protected Cuboidf[] selBoxes;

        ItemSlot isUsingSlot;

        public override int DisplayedItems {
            get
            {
                if (StorageProps == null) return 0;
                switch (StorageProps.Layout)
                {
                    case EnumGroundStorageLayout.SingleCenter: return 1;
                    case EnumGroundStorageLayout.Halves: return 2;
                    case EnumGroundStorageLayout.WallHalves: return 2;
                    case EnumGroundStorageLayout.Quadrants: return 4;
                    case EnumGroundStorageLayout.Stacking: return 1;
                }

                return 0;
            }
        }

        public int TotalStackSize
        {
            get
            {
                int sum = 0;
                foreach (var slot in inventory) sum += slot.StackSize;
                return sum;
            }
        }

        public int Capacity
        {
            get { 
                switch (StorageProps.Layout)
                {
                    case EnumGroundStorageLayout.SingleCenter: return 1;
                    case EnumGroundStorageLayout.Halves: return 2;
                    case EnumGroundStorageLayout.WallHalves: return 2;
                    case EnumGroundStorageLayout.Quadrants: return 4;
                    case EnumGroundStorageLayout.Stacking: return StorageProps.StackingCapacity;
                    default: return 1;
                }
            }
        }
        
        public override InventoryBase Inventory
        {
            get { return inventory; }
        }

        public override string InventoryClassName
        {
            get { return "groundstorage"; }
        }

        public override string AttributeTransformCode => "groundStorageTransform";

        public float MeshAngle { get; set; }
        public BlockFacing AttachFace { get; set; }

        public override TextureAtlasPosition this[string textureCode]
        {
            get
            {
                // Prio 1: Get from list of explicility defined textures
                if (StorageProps.Layout == EnumGroundStorageLayout.Stacking && StorageProps.StackingTextures != null)
                {
                    if (StorageProps.StackingTextures.TryGetValue(textureCode, out var texturePath))
                    {
                        return getOrCreateTexPos(texturePath);
                    }
                }

                // Prio 2: Try other texture sources
                return base[textureCode];
            }
        }

        public bool CanAttachBlockAt(BlockFacing blockFace, Cuboidi attachmentArea)
        {
            return blockFace == BlockFacing.UP && StorageProps.Layout == EnumGroundStorageLayout.Stacking && inventory[0].StackSize == Capacity && StorageProps.UpSolid;
        }

        public BlockEntityGroundStorage() : base()
        {
            inventory = new InventoryGeneric(invSlotCount, null, null, (int slotId, InventoryGeneric inv) => new ItemSlot(inv));
            foreach (var slot in inventory)
            {
                slot.StorageType |= EnumItemStorageFlags.Backpack;
            }

            inventory.OnGetAutoPushIntoSlot = GetAutoPushIntoSlot;
            inventory.OnGetAutoPullFromSlot = GetAutoPullFromSlot;

            colBoxes = new Cuboidf[] { new Cuboidf(0, 0, 0, 1, 0.25f, 1) };
            selBoxes = new Cuboidf[] { new Cuboidf(0, 0, 0, 1, 0.25f, 1) };
        }

        private ItemSlot GetAutoPullFromSlot(BlockFacing atBlockFace)
        {
            return null;
        }

        private ItemSlot GetAutoPushIntoSlot(BlockFacing atBlockFace, ItemSlot fromSlot)
        {
            return null;
        }

        public void ForceStorageProps(GroundStorageProperties storageProps)
        {
            StorageProps = storageProps;
            forceStorageProps = true;
        }


        public override void Initialize(ICoreAPI api)
        {
            capi = api as ICoreClientAPI;
            base.Initialize(api);

            DetermineStorageProperties(null);

            if (capi != null)
            {
                updateMeshes();
                //initMealRandomizer();
            }
        }

        // For trailer making
        /*void initMealRandomizer()
        {
            RegisterGameTickListener(Every50ms, 150);

            IWorldAccessor w = Api.World;
            rndMeals = new RndMeal[]
            {
                    new RndMeal()
                    {
                        recipeCode = "jam",
                        stacks = new ItemStack[][] {
                            gs("honeyportion"),
                            gs("honeyportion"),
                            anyFruit(),
                            anyFruitOrNothing(),
                        }
                    },
                    new RndMeal()
                    {
                        recipeCode = "porridge",
                        stacks = new ItemStack[][] {
                            gs("grain-spelt"),
                            gs("grain-spelt"),
                            anyFruitOrNothing(),
                            anyFruitOrNothing(),
                            anyVegetableOrNothing(),
                            anyVegetableOrNothing(),
                            honeyOrNothing()
                        }
                    },
                    new RndMeal()
                    {
                        recipeCode = "porridge",
                        stacks = new ItemStack[][] {
                            gs("grain-flax"),
                            gs("grain-flax"),
                            anyFruitOrNothing(),
                            anyFruitOrNothing(),
                            anyVegetableOrNothing(),
                            anyVegetableOrNothing(),
                            honeyOrNothing()
                        }
                    },
                    new RndMeal()
                    {
                        recipeCode = "porridge",
                        stacks = new ItemStack[][] {
                            gs("grain-rice"),
                            gs("grain-rice"),
                            anyFruitOrNothing(),
                            anyFruitOrNothing(),
                            anyVegetableOrNothing(),
                            anyVegetableOrNothing(),
                            honeyOrNothing()
                        }
                    },
                    new RndMeal()
                    {
                        recipeCode = "porridge",
                        stacks = new ItemStack[][] {
                            gs("grain-sunflower"),
                            gs("grain-sunflower"),
                            anyFruitOrNothing(),
                            anyFruitOrNothing(),
                            anyVegetableOrNothing(),
                            anyVegetableOrNothing(),
                            honeyOrNothing()
                        }
                    },
                    new RndMeal()
                    {
                        recipeCode = "soup",
                        stacks = new ItemStack[][]
                        {
                            gs("waterportion"),
                            anyVegetable(),
                            anyVegetableOrNothing(),
                            anyVegetableOrNothing(),
                            anyMeatOrEggOrNothing()
                        }
                    },
                    new RndMeal()
                    {
                        recipeCode = "vegetablestew",
                        stacks = new ItemStack[][]
                        {
                            anyVegetable(),
                            anyVegetable(),
                            anyVegetableOrNothing(),
                            anyVegetableOrNothing(),
                            anyMeatOrEggOrNothing()
                        }
                    },
                    new RndMeal()
                    {
                        recipeCode = "meatystew",
                        stacks = new ItemStack[][]
                        {
                            gs("redmeat-raw"),
                            gs("redmeat-raw"),
                            eggOrNothing(),
                            anyMeatOrEggOrNothing(),
                            anyVegetableOrNothing(),
                            anyVegetableOrNothing(),
                            anyFruitOrNothing(),
                            honeyOrNothing()
                        }
                    },
                    new RndMeal()
                    {
                        recipeCode = "meatystew",
                        stacks = new ItemStack[][]
                        {
                            gs("poultry-raw"),
                            gs("poultry-raw"),
                            eggOrNothing(),
                            anyMeatOrEggOrNothing(),
                            anyVegetableOrNothing(),
                            anyVegetableOrNothing(),
                            anyFruitOrNothing(),
                            honeyOrNothing()
                        }
                    },
                    new RndMeal()
                    {
                        recipeCode = "scrambledeggs",
                        stacks = new ItemStack[][]
                        {
                            gs("egg-chicken-raw"),
                            gs("egg-chicken-raw"),
                            anyCheeserNothing(),
                            anyCheeserNothing(),
                            anyVegetableOrNothing(),
                            anyVegetableOrNothing(),
                        }
                    },
            };
        }



        ItemStack[] anyFruitOrNothing()
        {
            return gs(null, "fruit-blueberry", "fruit-cranberry", "fruit-redcurrant", "fruit-whitecurrant", "fruit-blackcurrant", "fruit-saguaro", "fruit-pomegrante", "fruit-lychee", "fruit-breadfuit", "fruit-redapple", "fruit-pinkapple", "fruit-yellowapple", "fruit-cherry", "fruit-olive", "fruit-peach", "fruit-pear", "fruit-orange", "fruit-mango");
        }

        ItemStack[] anyCheeserNothing()
        {
            return gs(null, "cheese-cheddar-1slice", "cheese-blue-1slice");
        }


        ItemStack[] anyFruit()
        {
            return gs("fruit-blueberry", "fruit-cranberry", "fruit-redcurrant", "fruit-whitecurrant", "fruit-blackcurrant", "fruit-saguaro", "fruit-pomegrante", "fruit-lychee", "fruit-breadfuit", "fruit-redapple", "fruit-pinkapple", "fruit-yellowapple", "fruit-cherry", "fruit-olive", "fruit-peach", "fruit-pear", "fruit-orange", "fruit-mango");
        }

        ItemStack[] anyVegetableOrNothing()
        {
            return gs(null, "vegetable-carrot", "vegetable-cabbage", "vegetable-onion", "vegetable-turnip", "vegetable-parsnip", "vegetable-pumpkin", "mushroom-kingbolete-normal", "mushroom-fieldmushroom-normal");
        }

        ItemStack[] anyVegetable()
        {
            return gs("vegetable-carrot", "vegetable-cabbage", "vegetable-onion", "vegetable-turnip", "vegetable-parsnip", "vegetable-pumpkin", "mushroom-kingbolete-normal", "mushroom-fieldmushroom-normal");
        }

        ItemStack[] anyMeatOrEggOrNothing()
        {
            return gs(null, "redmeat-raw", "poultry-raw", "egg-chicken-raw");
        }

        ItemStack[] eggOrNothing()
        {
            return gs(null, "egg-chicken-raw");
        }


        ItemStack[] honeyOrNothing()
        {
            return gs(null, "honeyportion");
        }

        ItemStack[] gs(params string[] codes)
        {
            int index = 0;
            ItemStack[] stacks = new ItemStack[codes.Length];
            for (int i = 0; i < stacks.Length; i++)
            {
                if (codes[i] == null)
                {
                    continue;
                }

                Item item = Api.World.GetItem(new AssetLocation(codes[i]));
                if (item == null)
                {
                    Block block = Api.World.GetBlock(new AssetLocation(codes[i]));
                    if (block == null)
                    {
                        continue;
                    }

                    stacks[index++] = new ItemStack(block);
                }
                else
                {
                    stacks[index++] = new ItemStack(item);
                }
            }

            return stacks;
        }

        class RndMeal
        {
            public string recipeCode;
            public ItemStack[][] stacks;
        }

        RndMeal[] rndMeals;

        private void Every50ms(float t1)
        {
            foreach (var slot in inventory)
            {
                IBlockMealContainer blockMeal = slot.Itemstack?.Collectible as IBlockMealContainer;
                if (blockMeal == null) continue;
                
                RndMeal rndMeal = rndMeals[Api.World.Rand.Next(rndMeals.Length)];
                
                var istacks = new ItemStack[rndMeal.stacks.Length];

                int index = 0;
                for (int i = 0; i < rndMeal.stacks.Length; i++)
                {
                    ItemStack[] stacks = rndMeal.stacks[i];
                    ItemStack stack = stacks[Api.World.Rand.Next(stacks.Length)];

                    if (stack == null) continue;
                    istacks[index++] = stack;

                    if (index == 4) break;
                }

                blockMeal.SetContents(rndMeal.recipeCode, slot.Itemstack, istacks, 1);
            }

            updateMeshes();
            MarkDirty(true);
        }*/




        public Cuboidf[] GetSelectionBoxes()
        {
            return selBoxes;
        }

        public Cuboidf[] GetCollisionBoxes()
        {
            return colBoxes;
        }

        public virtual bool OnPlayerInteractStart(IPlayer player, BlockSelection bs)
        {
            ItemSlot hotbarSlot = player.InventoryManager.ActiveHotbarSlot;

            if (!hotbarSlot.Empty && !hotbarSlot.Itemstack.Collectible.HasBehavior<CollectibleBehaviorGroundStorable>()) return false;

            if (!BlockBehaviorReinforcable.AllowRightClickPickup(Api.World, Pos, player)) return false;

            DetermineStorageProperties(hotbarSlot);

            bool ok = false;

            if (StorageProps != null)
            {
                if (!hotbarSlot.Empty && StorageProps.SprintKey && !player.Entity.Controls.CtrlKey) return false;

                var hitPos = rotatedOffset(bs.HitPosition.ToVec3f(), MeshAngle);

                if (StorageProps.Layout == EnumGroundStorageLayout.Quadrants && inventory.Empty)
                {
                    double dx = Math.Abs(hitPos.X - 0.5);
                    double dz = Math.Abs(hitPos.Z - 0.5);
                    if (dx < 2 / 16f && dz < 2 / 16f)
                    {
                        overrideLayout = EnumGroundStorageLayout.SingleCenter;
                        DetermineStorageProperties(hotbarSlot);
                    }
                }

                switch (StorageProps.Layout)
                {
                    case EnumGroundStorageLayout.SingleCenter:
                        ok = putOrGetItemSingle(inventory[0], player, bs);
                        break;


                    case EnumGroundStorageLayout.WallHalves:
                    case EnumGroundStorageLayout.Halves:
                        if (hitPos.X < 0.5)
                        {
                            ok = putOrGetItemSingle(inventory[0], player, bs);
                        }
                        else
                        {
                            ok = putOrGetItemSingle(inventory[1], player, bs);
                        }
                        break;

                    case EnumGroundStorageLayout.Quadrants:
                        int pos = ((hitPos.X > 0.5) ? 2 : 0) + ((hitPos.Z > 0.5) ? 1 : 0);
                        ok = putOrGetItemSingle(inventory[pos], player, bs);
                        break;

                    case EnumGroundStorageLayout.Stacking:
                        ok = putOrGetItemStacking(player, bs);
                        break;
                }
            }

            if (ok)
            {
                MarkDirty(true);
                updateMeshes();
            }

            if (inventory.Empty)
            {
                Api.World.BlockAccessor.SetBlock(0, Pos);
                Api.World.BlockAccessor.TriggerNeighbourBlockUpdate(Pos);
            }

            return ok;
        }



        public bool OnPlayerInteractStep(float secondsUsed, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (isUsingSlot?.Itemstack?.Collectible is IContainedInteractable collIci)
            {
                return collIci.OnContainedInteractStep(secondsUsed, this, isUsingSlot, byPlayer, blockSel);
            }

            isUsingSlot = null;
            return false;
        }


        public void OnPlayerInteractStop(float secondsUsed, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (isUsingSlot?.Itemstack.Collectible is IContainedInteractable collIci)
            {
                collIci.OnContainedInteractStop(secondsUsed, this, isUsingSlot, byPlayer, blockSel);
            }

            isUsingSlot = null;
        }






        public ItemSlot GetSlotAt(BlockSelection bs)
        {
            if (StorageProps == null) return null;

            switch (StorageProps.Layout)
            {
                case EnumGroundStorageLayout.SingleCenter:
                    return inventory[0];

                case EnumGroundStorageLayout.Halves:
                case EnumGroundStorageLayout.WallHalves:
                    if (bs.HitPosition.X < 0.5)
                    {
                        return inventory[0];
                    }
                    else
                    {
                        return inventory[1];
                    }

                case EnumGroundStorageLayout.Quadrants:
                    var hitPos = rotatedOffset(bs.HitPosition.ToVec3f(), MeshAngle);
                    int pos = ((hitPos.X > 0.5) ? 2 : 0) + ((hitPos.Z > 0.5) ? 1 : 0);
                    return inventory[pos];

                case EnumGroundStorageLayout.Stacking:
                    return inventory[0];
            }

            return null;
        }



        public bool OnTryCreateKiln()
        {
            ItemStack stack = inventory.FirstNonEmptySlot.Itemstack;
            if (stack == null) return false;

            if (stack.StackSize > StorageProps.MaxFireable)
            {
                capi?.TriggerIngameError(this, "overfull", Lang.Get("Can only fire up to {0} at once.", StorageProps.MaxFireable));
                return false;
            }
            
            if (stack.Collectible.CombustibleProps == null || stack.Collectible.CombustibleProps.SmeltingType != EnumSmeltType.Fire)
            {
                capi?.TriggerIngameError(this, "notfireable", Lang.Get("This is not a fireable block or item", StorageProps.MaxFireable));
                return false;
            }


            return true;
        }

        public virtual void DetermineStorageProperties(ItemSlot sourceSlot)
        {
            ItemStack sourceStack = inventory.FirstNonEmptySlot?.Itemstack ?? sourceSlot?.Itemstack;

            if (!forceStorageProps)
            {
                if (StorageProps == null)
                {
                    if (sourceStack == null) return;

                    StorageProps = sourceStack.Collectible?.GetBehavior<CollectibleBehaviorGroundStorable>()?.StorageProps;
                }
            }

            if (StorageProps == null) return;  // Seems necessary to avoid crash with certain items placed in game version 1.15-pre.1?

            if (StorageProps.CollisionBox != null)
            {
                colBoxes[0] = selBoxes[0] = StorageProps.CollisionBox.Clone();
            } else
            {
                if (sourceStack?.Block != null)
                {
                    colBoxes[0] = selBoxes[0] = sourceStack.Block.CollisionBoxes[0].Clone();
                }
            }

            if (StorageProps.SelectionBox != null)
            {
                selBoxes[0] = StorageProps.SelectionBox.Clone();
            }

            if (StorageProps.CbScaleYByLayer != 0)
            {
                colBoxes[0] = colBoxes[0].Clone();
                colBoxes[0].Y2 *= ((int)Math.Ceiling(StorageProps.CbScaleYByLayer * inventory[0].StackSize) * 8) / 8;

                selBoxes[0] = colBoxes[0];
            }

            if (overrideLayout != null)
            {
                StorageProps = StorageProps.Clone();
                StorageProps.Layout = (EnumGroundStorageLayout)overrideLayout;
            }
        }



        protected bool putOrGetItemStacking(IPlayer byPlayer, BlockSelection bs)
        {
            if (Api.Side == EnumAppSide.Client)
            {
                (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                return true;
            }

            BlockPos abovePos = Pos.UpCopy();
            BlockEntity be = Api.World.BlockAccessor.GetBlockEntity(abovePos);
            if (be is BlockEntityGroundStorage beg)
            {
                return beg.OnPlayerInteractStart(byPlayer, bs);
            }

            bool sneaking = byPlayer.Entity.Controls.ShiftKey;


            ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (sneaking && hotbarSlot.Empty) return false;

            if (sneaking && TotalStackSize >= Capacity)
            {
                Block pileblock = Api.World.BlockAccessor.GetBlock(Pos);
                Block aboveblock = Api.World.BlockAccessor.GetBlock(abovePos);

                if (aboveblock.IsReplacableBy(pileblock))
                {
                    BlockGroundStorage bgs = pileblock as BlockGroundStorage;
                    var bsc = bs.Clone();
                    bsc.Position.Up();
                    bsc.Face = null;
                    return bgs.CreateStorage(Api.World, bsc, byPlayer);
                }

                return false;
            }


            bool equalStack = inventory[0].Empty || hotbarSlot.Itemstack != null && hotbarSlot.Itemstack.Equals(Api.World, inventory[0].Itemstack, GlobalConstants.IgnoredStackAttributes);

            if (sneaking && !equalStack)
            {
                return false;
            }

            lock (inventoryLock)
            {
                if (sneaking)
                {
                    return TryPutItem(byPlayer);
                }
                else
                {
                    return TryTakeItem(byPlayer);
                }
            }
        }


        public virtual bool TryPutItem(IPlayer player)
        {
            if (TotalStackSize >= Capacity) return false;

            ItemSlot hotbarSlot = player.InventoryManager.ActiveHotbarSlot;

            if (hotbarSlot.Itemstack == null) return false;

            ItemSlot invSlot = inventory[0];

            if (invSlot.Empty)
            {
                if (hotbarSlot.TryPutInto(Api.World, invSlot, TransferQuantity) > 0)
                {
                    Api.World.PlaySoundAt(StorageProps.PlaceRemoveSound.WithPathPrefixOnce("sounds/"), Pos.X, Pos.Y, Pos.Z, null, 0.88f + (float)Api.World.Rand.NextDouble() * 0.24f, 16);
                }
                return true;
            }

            if (invSlot.Itemstack.Equals(Api.World, hotbarSlot.Itemstack, GlobalConstants.IgnoredStackAttributes))
            {
                bool putBulk = player.Entity.Controls.CtrlKey;

                int q = GameMath.Min(hotbarSlot.StackSize, putBulk ? BulkTransferQuantity : TransferQuantity, Capacity - TotalStackSize);

                // add to the pile and average item temperatures
                int oldSize = invSlot.Itemstack.StackSize;
                invSlot.Itemstack.StackSize += q;
                if (oldSize + q > 0)
                {
                    float tempPile = invSlot.Itemstack.Collectible.GetTemperature(Api.World, invSlot.Itemstack);
                    float tempAdded = hotbarSlot.Itemstack.Collectible.GetTemperature(Api.World, hotbarSlot.Itemstack);
                    invSlot.Itemstack.Collectible.SetTemperature(Api.World, invSlot.Itemstack, (tempPile * oldSize + tempAdded * q) / (oldSize + q), false);
                }

                if (player.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    hotbarSlot.TakeOut(q);
                    hotbarSlot.OnItemSlotModified(null);
                }

                Api.World.PlaySoundAt(StorageProps.PlaceRemoveSound.WithPathPrefixOnce("sounds/"), Pos.X, Pos.Y, Pos.Z, null, 0.88f + (float)Api.World.Rand.NextDouble() * 0.24f, 16);

                MarkDirty();

                Cuboidf[] collBoxes = Api.World.BlockAccessor.GetBlock(Pos).GetCollisionBoxes(Api.World.BlockAccessor, Pos);
                if (collBoxes != null && collBoxes.Length > 0 && CollisionTester.AabbIntersect(collBoxes[0], Pos.X, Pos.Y, Pos.Z, player.Entity.SelectionBox, player.Entity.SidedPos.XYZ))
                {
                    player.Entity.SidedPos.Y += collBoxes[0].Y2 - (player.Entity.SidedPos.Y - (int)player.Entity.SidedPos.Y);
                }

                

                return true;
            }

            return false;
        }

        public bool TryTakeItem(IPlayer player)
        {
            bool takeBulk = player.Entity.Controls.CtrlKey;
            int q = GameMath.Min(takeBulk ? BulkTransferQuantity : TransferQuantity, TotalStackSize);

            if (inventory[0]?.Itemstack != null)
            {
                ItemStack stack = inventory[0].TakeOut(q);
                player.InventoryManager.TryGiveItemstack(stack);

                if (stack.StackSize > 0)
                {
                    Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
            }

            if (TotalStackSize == 0)
            {
                Api.World.BlockAccessor.SetBlock(0, Pos);
            }

            Api.World.PlaySoundAt(StorageProps.PlaceRemoveSound, Pos.X, Pos.Y, Pos.Z, player, 0.88f + (float)Api.World.Rand.NextDouble() * 0.24f, 16);

            MarkDirty();

            (player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

            return true;
        }



        bool putOrGetItemSingle(ItemSlot ourSlot, IPlayer player, BlockSelection bs)
        {
            isUsingSlot = null;
            if (!ourSlot.Empty && ourSlot.Itemstack.Collectible is IContainedInteractable collIci)
            {
                if (collIci.OnContainedInteractStart(this, ourSlot, player, bs))
                {
                    BlockGroundStorage.IsUsingContainedBlock = true;
                    isUsingSlot = ourSlot;
                    return true;
                }
            }

            ItemSlot hotbarSlot = player.InventoryManager.ActiveHotbarSlot;
            if (!hotbarSlot.Empty && !inventory.Empty)
            {
                bool layoutEqual = StorageProps.Layout == hotbarSlot.Itemstack.Collectible.GetBehavior<CollectibleBehaviorGroundStorable>()?.StorageProps.Layout;
                if (!layoutEqual) return false;
            }


            lock (inventoryLock)
            {
                if (ourSlot.Empty)
                {
                    if (hotbarSlot.Empty) return false;

                    if (player.WorldData.CurrentGameMode == EnumGameMode.Creative)
                    {
                        ItemStack stack = hotbarSlot.Itemstack.Clone();
                        stack.StackSize = 1;
                        if (new DummySlot(stack).TryPutInto(Api.World, ourSlot, TransferQuantity) > 0) {
                            Api.World.PlaySoundAt(StorageProps.PlaceRemoveSound, Pos.X, Pos.Y, Pos.Z, player, 0.88f + (float)Api.World.Rand.NextDouble() * 0.24f, 16);
                        }
                    } else {
                        if (hotbarSlot.TryPutInto(Api.World, ourSlot, TransferQuantity) > 0)
                        {
                            Api.World.PlaySoundAt(StorageProps.PlaceRemoveSound, Pos.X, Pos.Y, Pos.Z, player, 0.88f + (float)Api.World.Rand.NextDouble() * 0.24f, 16);
                        }
                    }
                }
                else
                {
                    if (!player.InventoryManager.TryGiveItemstack(ourSlot.Itemstack, true))
                    {
                        Api.World.SpawnItemEntity(ourSlot.Itemstack, new Vec3d(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5));
                    }

                    Api.World.PlaySoundAt(StorageProps.PlaceRemoveSound, Pos.X, Pos.Y, Pos.Z, player, 0.88f + (float)Api.World.Rand.NextDouble() * 0.24f, 16);

                    ourSlot.Itemstack = null;
                    ourSlot.MarkDirty();
                }
            }

            return true;
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            forceStorageProps = tree.GetBool("forceStorageProps");
            if (forceStorageProps)
            {
                StorageProps = JsonUtil.FromString<GroundStorageProperties>(tree.GetString("storageProps"));
            }

            overrideLayout = null;
            if (tree.HasAttribute("overrideLayout"))
            {
                overrideLayout = (EnumGroundStorageLayout)tree.GetInt("overrideLayout");
            }

            if (this.Api != null)
            {
                DetermineStorageProperties(null);
            }

            if (worldForResolving.Side == EnumAppSide.Client && Api != null)
            {
                updateMeshes();
            }

            MeshAngle = tree.GetFloat("meshAngle");
            AttachFace = BlockFacing.ALLFACES[tree.GetInt("attachFace")];
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("forceStorageProps", forceStorageProps);
            if (forceStorageProps)
            {
                tree.SetString("storageProps", JsonUtil.ToString(StorageProps));
            }
            if (overrideLayout != null)
            {
                tree.SetInt("overrideLayout", (int)overrideLayout);
            }

            tree.SetFloat("meshAngle", MeshAngle);
            tree.SetInt("attachFace", AttachFace?.Index ?? 0);
        }


        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            // Handled by block.GetDrops()
            /*if (Api.World.Side == EnumAppSide.Server)
            {
                inventory.DropAll(Pos.ToVec3d().Add(0.5, 0.5, 0.5), 4);
            }*/
        }



        public virtual string GetBlockName()
        {
            var props = StorageProps;
            if (props == null || inventory.Empty) return Lang.Get("Empty pile");

            string[] contentSummary = getContentSummary();
            if (contentSummary.Length == 1)
            {
                var firstSlot = inventory.FirstNonEmptySlot;

                ItemStack stack = firstSlot.Itemstack;
                int sumQ = inventory.Sum(s => s.StackSize);

                if (firstSlot.Itemstack.Collectible is IContainedCustomName ccn)
                {
                    string name = ccn.GetContainedName(firstSlot, sumQ);
                    if (name != null) return name;
                }


                if (sumQ == 1) return stack.GetName();
                return contentSummary[0];
            }

            return Lang.Get("Ground Storage");
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (inventory.Empty) return;

            string[] contentSummary = getContentSummary();

            ItemStack stack = inventory.FirstNonEmptySlot.Itemstack;
            // Only add supplemental info for non-BlockEntities (otherwise it will be wrong or will get into a recursive loop, because right now this BEGroundStorage is the BlockEntity)
            if (contentSummary.Length == 1 && !(stack.Collectible is IContainedCustomName) && stack.Class == EnumItemClass.Block && ((Block)stack.Collectible).EntityClass == null)  
            {
                string detailedInfo = stack.Block.GetPlacedBlockInfo(Api.World, Pos, forPlayer);
                if (detailedInfo != null && detailedInfo.Length > 0) dsc.Append(detailedInfo);
            } else
            {
                foreach (var line in contentSummary) dsc.AppendLine(line);
            }
        }

        public virtual string[] getContentSummary()
        {
            OrderedDictionary<string, int> dict = new OrderedDictionary<string, int>();

            foreach (var slot in inventory)
            {
                if (slot.Empty) continue;
                int cnt;

                string stackName = slot.Itemstack.GetName();

                if (slot.Itemstack.Collectible is IContainedCustomName ccn)
                {
                    stackName = ccn.GetContainedInfo(slot);
                }

                if (!dict.TryGetValue(stackName, out cnt)) cnt = 0;

                dict[stackName] = cnt + slot.StackSize;
            }

            return dict.Select(elem => Lang.Get("{0}x {1}", elem.Value, elem.Key)).ToArray();
        }



        public override bool OnTesselation(ITerrainMeshPool meshdata, ITesselatorAPI tesselator)
        {
            lock (inventoryLock)
            {
                return base.OnTesselation(meshdata, tesselator);
            }
        }


        
        Vec3f rotatedOffset(Vec3f offset, float radY)
        {
            Matrixf mat = new Matrixf();
            mat.Translate(0.5f, 0.5f, 0.5f).RotateY(radY).Translate(-0.5f, -0.5f, -0.5f);
            return mat.TransformVector(new Vec4f(offset.X, offset.Y, offset.Z, 1)).XYZ;
        }


        protected override float[][] genTransformationMatrices()
        {
            float[][] tfMatrices = new float[DisplayedItems][];

            Vec3f[] offs = new Vec3f[DisplayedItems];

            lock (inventoryLock)
            {
                switch (StorageProps.Layout)
                {
                    case EnumGroundStorageLayout.SingleCenter:
                        offs[0] = new Vec3f();
                        break;

                    case EnumGroundStorageLayout.Halves:
                    case EnumGroundStorageLayout.WallHalves:
                        // Left
                        offs[0] = new Vec3f(-0.25f, 0, 0);
                        // Right
                        offs[1] = new Vec3f(0.25f, 0, 0);
                        break;

                    case EnumGroundStorageLayout.Quadrants:
                        // Top left
                        offs[0] = new Vec3f(-0.25f, 0, -0.25f);
                        // Top right
                        offs[1] = new Vec3f(-0.25f, 0, 0.25f);
                        // Bot left
                        offs[2] = new Vec3f(0.25f, 0, -0.25f);
                        // Bot right
                        offs[3] = new Vec3f(0.25f, 0, 0.25f);
                        break;

                    case EnumGroundStorageLayout.Stacking:
                        offs[0] = new Vec3f();
                        break;
                }
            }

            for (int i = 0; i < tfMatrices.Length; i++)
            {
                Vec3f off = offs[i];
                off = new Matrixf().RotateY(-MeshAngle).TransformVector(off.ToVec4f(0)).XYZ;

                tfMatrices[i] =
                    new Matrixf()
                    .Translate(off.X, off.Y, off.Z)
                    .Translate(0.5f, 0, 0.5f)
                    .RotateY(MeshAngle)
                    .Translate(-0.5f, 0, -0.5f)
                    .Values
                ;
            }

            return tfMatrices;
        }

        protected override string getMeshCacheKey(ItemStack stack)
        {
            return (StorageProps.ModelItemsToStackSizeRatio > 0 ? stack.StackSize : 1) + "x" + base.getMeshCacheKey(stack);
        }

        protected override MeshData getOrCreateMesh(ItemStack stack, int index)
        {
            if (StorageProps.Layout == EnumGroundStorageLayout.Stacking)
            {
                MeshData mesh = getMesh(stack);
                if (mesh != null) return mesh;

                var loc = StorageProps.StackingModel.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
                nowTesselatingShape = Shape.TryGet(capi, loc);
                nowTesselatingObj = stack.Collectible;

                if (nowTesselatingShape == null)
                {
                    capi.Logger.Error("Stacking model shape for collectible " + stack.Collectible.Code + " not found. Block will be invisible!");
                    return null;
                }

                capi.Tesselator.TesselateShape("storagePile", nowTesselatingShape, out mesh, this, null, 0, 0, 0, (int)Math.Ceiling(StorageProps.ModelItemsToStackSizeRatio * stack.StackSize));

                string key = getMeshCacheKey(stack);
                MeshCache[key] = mesh;

                return mesh;
            }

            return base.getOrCreateMesh(stack, index);
        }


        public bool TryFire()
        {
            foreach (var slot in inventory)
            {
                if (slot.Empty) continue;

            }

            return true;
        }

    }
}
