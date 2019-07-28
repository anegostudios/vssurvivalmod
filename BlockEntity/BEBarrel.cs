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
    public class BlockEntityBarrel : BlockEntityContainer, IBlockShapeSupplier
    {
        public int CapacityLitres
        {
            get { return 100; }
        }

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

        public BlockEntityBarrel()
        {
            inventory = new InventoryGeneric(2, null, null, (id, self) =>
            {
                if (id == 0) return new ItemSlotBarrelInput(self);
                else return new ItemSlotLiquidOnly(self);
            });


            inventory.OnAcquireTransitionSpeed = (type, stack, basemul) =>
            {
                // Don't spoil while sealed
                if (Sealed && CurrentRecipe != null && CurrentRecipe.SealHours > 0) return 0;

                return type == EnumTransitionType.Perish ? 1 : 0;
            };

            inventory.SlotModified += Inventory_SlotModified;

            
        }



        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            ownBlock = api.World.BlockAccessor.GetBlock(pos) as BlockBarrel;

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
                if (api?.Side == EnumAppSide.Client)
                {
                    currentMesh = GenMesh();
                    MarkDirty(true);
                }
            
                FindMatchingRecipe();
            }

        }


        private void FindMatchingRecipe()
        {
            ItemStack[] inputstacks = new ItemStack[] { inventory[0].Itemstack, inventory[1].Itemstack };
            CurrentRecipe = null;

            foreach (var recipe in api.World.BarrelRecipes)
            {
                int outsize;


                if (recipe.Matches(api.World, inputstacks, out outsize))
                {
                    ignoreChange = true;

                    if (recipe.SealHours > 0)
                    {
                        CurrentRecipe = recipe;
                        CurrentOutSize = outsize;

                    } else
                    {
                        ItemStack mixedStack = recipe.Output.ResolvedItemstack.Clone();
                        mixedStack.StackSize = outsize;

                        if (BlockLiquidContainerBase.GetStackProps(mixedStack) != null)
                        {
                            inventory[0].Itemstack = null;
                            inventory[1].Itemstack = mixedStack;
                        } else
                        {
                            inventory[1].Itemstack = null;
                            inventory[0].Itemstack = mixedStack;
                        }
                        

                        inventory[0].MarkDirty();
                        inventory[1].MarkDirty();
                        MarkDirty(true);
                        api.World.BlockAccessor.MarkBlockEntityDirty(pos);
                    }
                    

                    invDialog?.UpdateContents();
                    if (api?.Side == EnumAppSide.Client)
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

            if (CurrentRecipe != null && Sealed && CurrentRecipe.SealHours > 0)
            {
                if (api.World.Calendar.TotalHours - SealedSinceTotalHours > CurrentRecipe.SealHours)
                {
                    ItemStack mixedStack = CurrentRecipe.Output.ResolvedItemstack.Clone();
                    mixedStack.StackSize = CurrentOutSize;

                    // Carry over freshness
                    TransitionableProperties perishProps = mixedStack.Collectible.GetTransitionableProperties(api.World, mixedStack, null)?[0];

                    if (perishProps != null)
                    {
                        ItemSlot[] slots = new ItemSlot[inventory.Count];
                        for (int i = 0; i < inventory.Count; i++) slots[i] = inventory[i];
                        BlockCookingContainer.CarryOverFreshness(api, slots, new ItemStack[] { mixedStack }, perishProps);
                    }

                    if (BlockLiquidContainerBase.GetStackProps(mixedStack) != null)
                    {
                        inventory[0].Itemstack = null;
                        inventory[1].Itemstack = mixedStack;
                    }
                    else
                    {
                        inventory[1].Itemstack = null;
                        inventory[0].Itemstack = mixedStack;
                    }

                    

                    inventory[0].MarkDirty();
                    inventory[1].MarkDirty();

                    MarkDirty(true);
                    api.World.BlockAccessor.MarkBlockEntityDirty(pos);
                    Sealed = false;
                }
            } else
            {
                Sealed = false;
                MarkDirty(true);
            }
        }



        public override void OnBlockBroken()
        {
            // Don't drop inventory contents

            invDialog?.TryClose();
        }


        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
        {
            if (packetid <= 1000)
            {
                inventory.InvNetworkUtil.HandleClientPacket(fromPlayer, packetid, data);
            }

            if (packetid == (int)EnumBlockContainerPacketId.CloseInventory)
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

            if (packetid == (int)EnumBlockContainerPacketId.CloseInventory)
            {
                (api.World as IClientWorldAccessor).Player.InventoryManager.CloseInventory(Inventory);
                invDialog?.TryClose();
                invDialog?.Dispose();
                invDialog = null;
            }
        }

        public void SealBarrel()
        {
            if (Sealed) return;

            Sealed = true;
            SealedSinceTotalHours = api.World.Calendar.TotalHours;
            MarkDirty(true);
        }


        public void OnBlockInteract(IPlayer byPlayer)
        {
            if (Sealed) return;

            FindMatchingRecipe();

            if (api.Side == EnumAppSide.Client)
            {
                if (invDialog == null)
                {
                    invDialog = new GuiDialogBarrel("Barrel", Inventory, pos, api as ICoreClientAPI);
                    invDialog.OnClosed += () =>
                    {
                        (api as ICoreClientAPI).Network.SendBlockEntityPacket(pos.X, pos.Y, pos.Z, (int)EnumBlockContainerPacketId.CloseInventory, null);
                        byPlayer.InventoryManager.CloseInventory(inventory);
                    };
                }

                invDialog.TryOpen();
                
                (api as ICoreClientAPI).Network.SendPacketClient(inventory.Open(byPlayer));
            } else
            {
                byPlayer.InventoryManager.OpenInventory(inventory);
            }
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);

            if (api?.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
                invDialog?.UpdateContents();
            }

            Sealed = tree.GetBool("sealed");
            SealedSinceTotalHours = tree.GetDouble("sealedSinceTotalHours");

            if (api != null)
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

            return ownBlock.GenMesh(inventory[0].Itemstack, inventory[1].Itemstack, Sealed, pos);
        }


        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            invDialog?.Dispose();
        }


        public bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            mesher.AddMeshData(currentMesh);
            return true;
        }
    }
}
