using System;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public enum EnumBlockStovePacket
    {
        OpenGUI = 1000,
        CloseGUI = 1001,
        UpdateGUI = 1002
    }

    public class FurnaceSection : BlockEntityOpenableContainer
    {
        public int MaxTemperature = 900;

        internal InventorySmelting inventory;

        int state;
        int burntime;
        int receivedAirBlows;
        int airblowtimer;


        public override InventoryBase Inventory
        {
            get { return inventory; }
        }

        public override string InventoryClassName
        {
            get { return "furnacesection"; }
        }

        public FurnaceSection() : base()
        {
            // Have to initialize the inventory without anything at first, 
            // let's hope this won't explode in our faces in the future
            inventory = new InventorySmelting(null, null);
        }


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            RegisterGameTickListener(OnSlowTick, 100);
        }

        private void OnSlowTick(float dt)
        {
            if (airblowtimer > 0)
            {
                airblowtimer--;
            }

            if (burntime > 0)
            {
                burntime--;
            }
            else
            {
                return;
            }

            if (state != 1) return;


            if (Api is ICoreClientAPI)
            {
                // Client updates

                if (Api.World.Rand.Next(100) == 0)
                {
                    /*api.World.PlaySoundAt(
                        (double)((float)pos.X + 0.5F),
                        (double)((float)pos.Y + 0.5F),
                        (double)((float)pos.Z + 0.5F),
                        "fire.fire",
                        0.5F,
                        api.World.Rand.Next() * 0.5F + 0.3F,
                        false
                    );*/
                }

                float quantitysmoke = (2.5f * burntime * burntime) / (getTotalBurnTime() * getTotalBurnTime());

                //if (furnacetype == EnumFurnaceType.BLASTFURNACE) quantitysmoke /= 10;
                quantitysmoke += Math.Min(4, airblowtimer / 2);

                while (quantitysmoke > 0.001f)
                {
                    if (quantitysmoke < 1f && Api.World.Rand.NextDouble() > quantitysmoke) break;

                    double x = (double)Pos.X + 0.3f + Api.World.Rand.NextDouble() * 0.4f;
                    double y = (double)Pos.Y + 0.7f + Api.World.Rand.NextDouble() * 0.3D + 0.7D; // + (furnacetype == EnumFurnaceType.BLASTFURNACE ? 1D : 0D);
                    double z = (double)Pos.Z + 0.3f + Api.World.Rand.NextDouble() * 0.4f;

                    //worldObj.spawnParticle(EnumParticleTypes.SMOKE_LARGE, x, y, z, 0.0D, 0.0D, 0.0D, new int[0]);

                    quantitysmoke--;
                }



            }
            else
            {
                // Server updates

                if (burntime - receivedAirBlows * 20 <= 1)
                {
                    finishMelt();
                    return;
                }

                /*if (burntime % 30 == 0 && (furnacetype == EnumFurnaceType.BLOOMERY && !isValidBloomery()) || (furnacetype == EnumFurnaceType.BLASTFURNACE && !isValidBlastFurnace()))
                {
                    burntime = 0;
                    state = 0;
                    worldObj.markBlockForUpdate(pos);
                    receivedAirBlows = 0;
                    if (storage[0] != null)
                    {
                        storage[0].stackSize = 0;
                    }

                    markDirty();

                }*/

            }

        }


        public void finishMelt()
        {
            state = 2;

            Slot(0).Itemstack = null;
            Slot(2).Itemstack = getSmeltedOre(Slot(1));
            if (Slot(2).Itemstack != null)
            {
                Slot(2).Itemstack.StackSize = Slot(1).Itemstack.StackSize / getSmeltedRatio(Slot(1).Itemstack);  // ingots	
            }

            Slot(1).Itemstack.StackSize = 0; // ore

            if (Slot(2).Itemstack.StackSize == 0) state = 0;


            burntime = 0;
            receivedAirBlows = 0;
            Api.World.BlockAccessor.MarkBlockEntityDirty(Pos);
        }


        public ItemSlot Slot(int i)
        {
            return inventory[i];
        }

        public ItemStack getSmeltedOre(ItemSlot oreSlot)
        {
            if (oreSlot.Itemstack == null) return null;

            CombustibleProperties compustibleOpts = oreSlot.Itemstack.Collectible.CombustibleProps;

            if (compustibleOpts == null) return null;

            ItemStack smelted = compustibleOpts.SmeltedStack.ResolvedItemstack.Clone();

            if (compustibleOpts.MeltingPoint <= MaxTemperature)
            {
                return smelted;
            }

            return null;
        }


        public int getSmeltedRatio(IItemStack oreStack)
        {
            if (oreStack == null) return 0;
            CombustibleProperties compustibleOpts = oreStack.Collectible.CombustibleProps;

            return compustibleOpts.SmeltedStack.ResolvedItemstack.StackSize;
        }


        public int getTotalBurnTime()
        {
            /*if (furnacetype == EnumFurnaceType.BLASTFURNACE)
            {
                return 20 * 60 * 7; // 7 Minutes
            }*/

            return 20 * 60 * 5; // 5 Minutes
        }



        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            return true;
        }


        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            
            if (Api.World is IServerWorldAccessor)
            {
                Inventory.DropAll(Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
        }

        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            if (packetid < 1000)
            {
                Inventory.InvNetworkUtil.HandleClientPacket(player, packetid, data);

                // Tell server to save this chunk to disk again
                Api.World.BlockAccessor.GetChunkAtBlockPos(Pos).MarkModified();

                return;
            }

            if (packetid == (int)EnumBlockStovePacket.CloseGUI)
            {
                if (player.InventoryManager != null)
                {
                    player.InventoryManager.CloseInventory(Inventory);
                }
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            if (packetid == (int)EnumBlockStovePacket.OpenGUI)
            {
                using (MemoryStream ms = new MemoryStream(data))
                {
                    BinaryReader reader = new BinaryReader(ms);

                    string dialogClassName = reader.ReadString();
                    string dialogTitle = reader.ReadString();

                    TreeAttribute tree = new TreeAttribute();
                    tree.FromBytes(reader);
                    Inventory.FromTreeAttributes(tree);
                    Inventory.ResolveBlocksOrItems();

                    IClientWorldAccessor clientWorld = (IClientWorldAccessor)Api.World;

                    //clientWorld.OpenDialog(dialogClassName, dialogTitle, Inventory);
                }
            }

            if (packetid == (int)EnumBlockEntityPacketId.Close)
            {
                IClientWorldAccessor clientWorld = (IClientWorldAccessor)Api.World;
                clientWorld.Player.InventoryManager.CloseInventory(Inventory);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            Inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            Inventory.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;
        }

    }
}
