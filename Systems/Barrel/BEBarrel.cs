﻿using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockEntityBarrel : BlockEntityLiquidContainer
    {
        public int CapacityLitres { get; set; } = 50;

        GuiDialogBarrel invDialog;

        // Slot 0: Input/Item slot
        // Slot 1: Liquid slot
        public override string InventoryClassName => "barrel";

        MeshData currentMesh;
        BlockBarrel ownBlock;

        public bool Sealed;
        public double SealedSinceTotalHours;

        public BarrelRecipe CurrentRecipe;
        public int CurrentOutSize;

        public bool CanSeal
        {
            get
            {
                FindMatchingRecipe();
                if (CurrentRecipe != null && CurrentRecipe.SealHours > 0) return true;
                return false;
            }
        }

        public BlockEntityBarrel()
        {
            inventory = new InventoryGeneric(2, null, null, (id, self) =>
            {
                if (id == 0) return new ItemSlotBarrelInput(self);
                else return new ItemSlotLiquidOnly(self, 50);
            });
            inventory.BaseWeight = 1;
            inventory.OnGetSuitability = GetSuitability;


            inventory.SlotModified += Inventory_SlotModified;
            inventory.OnAcquireTransitionSpeed += Inventory_OnAcquireTransitionSpeed1;
        }

        private float Inventory_OnAcquireTransitionSpeed1(EnumTransitionType transType, ItemStack stack, float mul)
        {
            // Don't spoil while sealed
            if (Sealed && CurrentRecipe != null && CurrentRecipe.SealHours > 0) return 0;

            return mul;
        }

        private float GetSuitability(ItemSlot sourceSlot, ItemSlot targetSlot, bool isMerge)
        {
            // prevent for example rot overflowing into the liquid slot, on a shift-click, when slot[0] is already full of 64 x rot.   Rot can be accepted in the liquidOnly slot because it has containableProps (perhaps it shouldn't?)
            if (targetSlot == inventory[1])
            {
                if (inventory[0].StackSize > 0)
                {
                    ItemStack currentStack = inventory[0].Itemstack;
                    ItemStack testStack = sourceSlot.Itemstack;
                    if (currentStack.Collectible.Equals(currentStack, testStack, GlobalConstants.IgnoredStackAttributes)) return -1;
                }
            }

            // normal behavior
            return (isMerge ? (inventory.BaseWeight + 3) : (inventory.BaseWeight + 1)) + (sourceSlot.Inventory is InventoryBasePlayer ? 1 : 0);
        }


        protected override ItemSlot GetAutoPushIntoSlot(BlockFacing atBlockFace, ItemSlot fromSlot)
        {
            if (atBlockFace == BlockFacing.UP) return inventory[0];
            return null;
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            ownBlock = Block as BlockBarrel;

            if (ownBlock?.Attributes?["capacityLitres"].Exists == true)
            {
                CapacityLitres = ownBlock.Attributes["capacityLitres"].AsInt(50);
                (inventory[1] as ItemSlotLiquidOnly).CapacityLitres = CapacityLitres;
            }

            if (api.Side == EnumAppSide.Client && currentMesh == null)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
            if (api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(OnEvery3Second, 3000);
            }

            FindMatchingRecipe();
        }

        bool ignoreChange = false;

        private void Inventory_SlotModified(int slotId)
        {
            if (ignoreChange) return;

            if (slotId == 0 || slotId == 1)
            {
                invDialog?.UpdateContents();
                if (Api?.Side == EnumAppSide.Client)
                {
                    currentMesh = GenMesh();
                }

                MarkDirty(true);
                FindMatchingRecipe();
            }

        }


        private void FindMatchingRecipe()
        {
            ItemSlot[] inputSlots = new ItemSlot[] { inventory[0], inventory[1] };
            CurrentRecipe = null;

            foreach (var recipe in Api.GetBarrelRecipes())
            {
                int outsize;

                if (recipe.Matches(inputSlots, out outsize))
                {
                    ignoreChange = true;

                    if (recipe.SealHours > 0)
                    {
                        CurrentRecipe = recipe;
                        CurrentOutSize = outsize;

                    } else
                    {
                        if (Api?.Side == EnumAppSide.Server)
                        {
                            recipe.TryCraftNow(Api, 0, inputSlots);
                            MarkDirty(true);
                            Api.World.BlockAccessor.MarkBlockEntityDirty(Pos);
                        }
                    }


                    invDialog?.UpdateContents();
                    if (Api?.Side == EnumAppSide.Client)
                    {
                        currentMesh = GenMesh();
                        MarkDirty(true);
                    }

                    ignoreChange = false;
                    return;
                }
            }
        }


        private void OnEvery3Second(float dt)
        {
            if (!inventory[0].Empty && CurrentRecipe == null)
            {
                FindMatchingRecipe();
            }

            if (CurrentRecipe != null)
            {
                if (Sealed && CurrentRecipe.TryCraftNow(Api, Api.World.Calendar.TotalHours - SealedSinceTotalHours, new ItemSlot[] { inventory[0], inventory[1] }) == true)
                {
                    MarkDirty(true);
                    Api.World.BlockAccessor.MarkBlockEntityDirty(Pos);
                    Sealed = false;
                }

            } else
            {
                if (Sealed)
                {
                    Sealed = false;
                    MarkDirty(true);
                }
            }
        }



        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            if (!Sealed)
            {
                base.OnBlockBroken(byPlayer);
            }

            invDialog?.TryClose();
            invDialog = null;
        }


        public void SealBarrel()
        {
            if (Sealed) return;

            Sealed = true;
            SealedSinceTotalHours = Api.World.Calendar.TotalHours;
            MarkDirty(true);
        }




        public void OnPlayerRightClick(IPlayer byPlayer)
        {
            if (Sealed) return;

            FindMatchingRecipe();

            if (Api.Side == EnumAppSide.Client)
            {
                toggleInventoryDialogClient(byPlayer);
            }
        }


        protected void toggleInventoryDialogClient(IPlayer byPlayer)
        {
            if (invDialog == null)
            {
                ICoreClientAPI capi = Api as ICoreClientAPI;
                invDialog = new GuiDialogBarrel(Lang.Get("Barrel"), Inventory, Pos, Api as ICoreClientAPI);
                invDialog.OnClosed += () =>
                {
                    invDialog = null;
                    capi.Network.SendBlockEntityPacket(Pos, (int)EnumBlockEntityPacketId.Close, null);
                    capi.Network.SendPacketClient(Inventory.Close(byPlayer));
                };
                invDialog.OpenSound = AssetLocation.Create("sounds/block/barrelopen", Block.Code.Domain);
                invDialog.CloseSound = AssetLocation.Create("sounds/block/barrelclose", Block.Code.Domain);

                invDialog.TryOpen();
                capi.Network.SendPacketClient(Inventory.Open(byPlayer));
                capi.Network.SendBlockEntityPacket(Pos, (int)EnumBlockEntityPacketId.Open, null);
            }
            else
            {
                invDialog.TryClose();
            }
        }

        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(player, packetid, data);

            if (packetid < 1000)
            {
                Inventory.InvNetworkUtil.HandleClientPacket(player, packetid, data);

                // Tell server to save this chunk to disk again
                Api.World.BlockAccessor.GetChunkAtBlockPos(Pos).MarkModified();

                return;
            }

            if (packetid == (int)EnumBlockEntityPacketId.Close)
            {
                player.InventoryManager?.CloseInventory(Inventory);
            }

            if (packetid == (int)EnumBlockEntityPacketId.Open)
            {
                player.InventoryManager?.OpenInventory(Inventory);
            }


            if (packetid == 1337)
            {
                SealBarrel();
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);

            if (packetid == (int)EnumBlockEntityPacketId.Close)
            {
                (Api.World as IClientWorldAccessor).Player.InventoryManager.CloseInventory(Inventory);
                invDialog?.TryClose();
                invDialog?.Dispose();
                invDialog = null;
            }
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            Sealed = tree.GetBool("sealed");      // Update Sealed status before we generate the new mesh!
            if (Api?.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
                invDialog?.UpdateContents();
            }

            SealedSinceTotalHours = tree.GetDouble("sealedSinceTotalHours");

            if (Api != null)
            {
                FindMatchingRecipe();
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("sealed", Sealed);
            tree.SetDouble("sealedSinceTotalHours", SealedSinceTotalHours);
        }



        internal MeshData GenMesh()
        {
            if (ownBlock == null) return null;

            MeshData mesh = ownBlock.GenMesh(inventory[0].Itemstack, inventory[1].Itemstack, Sealed, Pos);

            if (mesh.CustomInts != null)
            {
                for (int i = 0; i < mesh.CustomInts.Count; i++)
                {
                    mesh.CustomInts.Values[i] |= 1 << 27; // Enable weak water wavy
                    mesh.CustomInts.Values[i] |= 1 << 26; // Enabled weak foam
                }
            }

            return mesh;
        }


        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            invDialog?.Dispose();
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            mesher.AddMeshData(currentMesh);
            return true;
        }
    }
}
