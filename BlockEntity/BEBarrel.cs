using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockEntityBarrel : BlockEntityContainer
    {
        public int CapacityLitres { get; set; } = 50;

        GuiDialogBarrel invDialog;
        
        // Slot 0: Input/Item slot
        // Slot 1: Liquid slot

        internal InventoryGeneric inventory;

        public override InventoryBase Inventory => inventory;
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
            inventory.OnGetSuitability = (sourceSlot, targetSlot, isMerge) => (isMerge ? (inventory.BaseWeight + 3) : (inventory.BaseWeight + 1)) + (sourceSlot.Inventory is InventoryBasePlayer ? 1 : 0);


            inventory.SlotModified += Inventory_SlotModified;
        }


        protected override float Inventory_OnAcquireTransitionSpeed(EnumTransitionType transType, ItemStack stack, float baseMul)
        {
            // Don't spoil while sealed
            if (Sealed && CurrentRecipe != null && CurrentRecipe.SealHours > 0) return 0;

            return base.Inventory_OnAcquireTransitionSpeed(transType, stack, baseMul);
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

            foreach (var recipe in Api.World.BarrelRecipes)
            {
                int outsize;

                if (recipe.Matches(Api.World, inputSlots, out outsize))
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



        public override void OnBlockBroken()
        {
            if (!Sealed)
            {
                base.OnBlockBroken();
            }

            invDialog?.TryClose();
            invDialog = null;
        }


        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
        {
            if (packetid <= 1000)
            {
                inventory.InvNetworkUtil.HandleClientPacket(fromPlayer, packetid, data);
            }

            if (packetid == (int)EnumBlockEntityPacketId.Close)
            {
                if (fromPlayer.InventoryManager != null)
                {
                    fromPlayer.InventoryManager.CloseInventory(Inventory);
                }
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

        public void SealBarrel()
        {
            if (Sealed) return;

            Sealed = true;
            SealedSinceTotalHours = Api.World.Calendar.TotalHours;
            MarkDirty(true);
        }


        public void OnBlockInteract(IPlayer byPlayer)
        {
            if (Sealed) return;

            FindMatchingRecipe();

            if (Api.Side == EnumAppSide.Client)
            {
                if (invDialog == null)
                {
                    invDialog = new GuiDialogBarrel("Barrel", Inventory, Pos, Api as ICoreClientAPI);
                    invDialog.OnClosed += () =>
                    {
                        invDialog = null;
                        (Api as ICoreClientAPI).Network.SendBlockEntityPacket(Pos.X, Pos.Y, Pos.Z, (int)EnumBlockEntityPacketId.Close, null);
                        byPlayer.InventoryManager.CloseInventory(inventory);
                    };
                }

                invDialog.TryOpen();
                
                (Api as ICoreClientAPI).Network.SendPacketClient(inventory.Open(byPlayer));
            } else
            {
                byPlayer.InventoryManager.OpenInventory(inventory);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            if (Api?.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
                invDialog?.UpdateContents();
            }

            Sealed = tree.GetBool("sealed");
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
