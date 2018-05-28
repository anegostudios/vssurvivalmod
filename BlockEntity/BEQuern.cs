using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockEntityQuern : BlockEntityContainer, IBlockShapeSupplier
    {
        ILoadedSound ambientSound;

        internal InventoryQuern inventory;
        
        // For how long the current ore has been grinding
        public float inputGrindTime;
        public float prevInputGrindTime;


        IGuiDialog clientDialog;
        
        Block ownBlock;
        QuernTopRenderer renderer;

        public string Material
        {
            get { return ownBlock.LastCodePart(); }
        }

        // Server side only
        List<IPlayer> playersGrinding = new List<IPlayer>();
        // Client and serverside
        int quantityPlayersGrinding;

        public bool IsGrinding
        {
            get { return quantityPlayersGrinding > 0; }
        }

        public void SetPlayerGrinding(IPlayer player, bool active)
        {
            
            if (active != IsGrinding)
            {
                api.World.BlockAccessor.MarkBlockDirty(pos, OnRetesselated);
                if (active)
                {
                    ambientSound?.Start();
                } else
                {
                    ambientSound?.Stop();
                }
            }

            if (active)
            {
                if (!playersGrinding.Contains(player))
                {
                    playersGrinding.Add(player);
                }
            } else
            {
                playersGrinding.Remove(player);
            }

            quantityPlayersGrinding = playersGrinding.Count;

            if (renderer != null)
            {
                //renderer.ShouldRender = IsGrinding;
                if (IsGrinding) renderer.GrindStartMs = api.World.ElapsedMilliseconds;
            }
        }


        MeshData baseOnlyMesh
        {
            get
            {
                object value = null;
                api.ObjectCache.TryGetValue("quernmesh-" + Material, out value);
                return (MeshData)value;
            }
            set { api.ObjectCache["quernmesh-" + Material] = value; }
        }



        #region Config

        public virtual float SoundLevel
        {
            get { return 1f; }
        }        

        // seconds it requires to melt the ore once beyond melting point
        public virtual float maxGrindingTime()
        {
            return 4;
        }

        public override string InventoryClassName
        {
            get { return "quern"; }
        }

        public virtual string DialogTitle
        {
            get { return "Quern"; }
        }

        public override InventoryBase Inventory
        {
            get { return inventory; }
        }

        #endregion


        private void OnRetesselated()
        {
            renderer.ShouldRender = IsGrinding;
        }



        public BlockEntityQuern()
        {
            inventory = new InventoryQuern(null, null);
            inventory.SlotModified += OnSlotModifid;
        }



        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            ownBlock = api.World.BlockAccessor.GetBlock(pos);

            inventory.LateInitialize("quern-1", api);
            Inventory.AfterBlocksLoaded(api.World);


            RegisterGameTickListener(Every100ms, 100);
            RegisterGameTickListener(Every500ms, 500);

            if (ambientSound == null && api.Side == EnumAppSide.Client)
            {
                ambientSound = ((IClientWorldAccessor)api.World).LoadSound(new SoundParams()
                {
                    Location = new AssetLocation("sounds/block/quern.ogg"),
                    ShouldLoop = true,
                    Position = pos.ToVec3f().Add(0.5f, 0.25f, 0.5f),
                    DisposeOnFinish = false,
                    Volume = SoundLevel
                });
            }

            if (api is ICoreClientAPI)
            {
                renderer = new QuernTopRenderer(api as ICoreClientAPI, pos, GenMesh("top"));

                (api as ICoreClientAPI).Event.RegisterRenderer(renderer, EnumRenderStage.Opaque);

                if (baseOnlyMesh == null) baseOnlyMesh = GenMesh();
            }

        }


        private void OnSlotModifid(int slotid)
        {
            if (api is ICoreClientAPI && clientDialog != null)
            {
                SetDialogValues(clientDialog.Attributes);
            }
        }

        internal MeshData GenMesh(string type = "base")
        {
            Block block = api.World.BlockAccessor.GetBlock(pos);
            if (block.BlockId == 0) return null;

            MeshData mesh;
            ITesselatorAPI mesher = ((ICoreClientAPI)api).Tesselator;

            mesher.TesselateShape(block, api.Assets.TryGet("shapes/block/stone/quern/"+type+".json").ToObject<Shape>(), out mesh);

            return mesh;
        }

        
        private void Every100ms(float dt)
        {
            // Only tick on the server and merely sync to client
            if (api is ICoreClientAPI) return;

            // Use up fuel
            if (CanGrind() && IsGrinding)
            {
                inputGrindTime += dt;

                if (inputGrindTime >= maxGrindingTime())
                {
                    grindInput();
                    inputGrindTime = 0;
                }
            }
        }

        private void grindInput()
        {
            ItemStack grindedStack = InputGrindProps.GrindedStack.ResolvedItemstack;

            if (OutputSlot.Itemstack == null)
            {
                OutputSlot.Itemstack = grindedStack.Clone();
            }
            else
            {
                OutputSlot.Itemstack.StackSize += grindedStack.StackSize;
            }

            InputSlot.TakeOut(1);
            InputSlot.MarkDirty();
            OutputSlot.MarkDirty();
        }


        // Sync to client every 500ms
        private void Every500ms(float dt)
        {
            if (api is ICoreServerAPI && (IsGrinding || prevInputGrindTime != inputGrindTime))
            {
                MarkDirty();
            }

            prevInputGrindTime = inputGrindTime;
        }        

        

        public bool CanGrind()
        {
            GrindingProperties grindProps = InputGrindProps;
            if (grindProps == null) return false;
            if (OutputSlot.Itemstack != null) return true;

            int mergableQuantity = OutputSlot.Itemstack.Collectible.GetMergableQuantity(OutputSlot.Itemstack, grindProps.GrindedStack.ResolvedItemstack);

            return mergableQuantity >= grindProps.GrindedStack.ResolvedItemstack.StackSize;
        }
        
        


        #region Events

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel.SelectionBoxIndex == 1) return false;

            if (api.World is IServerWorldAccessor)
            {
                byte[] data;

                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryWriter writer = new BinaryWriter(ms);
                    writer.Write("BlockEntityQuern");
                    writer.Write(DialogTitle);
                    TreeAttribute tree = new TreeAttribute();
                    inventory.ToTreeAttributes(tree);
                    tree.ToBytes(writer);
                    data = ms.ToArray();
                }

                ((ICoreServerAPI)api).Network.SendBlockEntityPacket(
                    (IServerPlayer)byPlayer,
                    pos.X, pos.Y, pos.Z,
                    (int)EnumBlockStovePacket.OpenGUI,
                    data
                );

                byPlayer.InventoryManager.OpenInventory(inventory);
            }

            return true;
        }


        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);
            Inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));

            if (api != null)
            {
                Inventory.AfterBlocksLoaded(api.World);
            }


            inputGrindTime = tree.GetFloat("inputGrindTime");
            quantityPlayersGrinding = tree.GetInt("quantityPlayersGrinding");


            if (api?.Side == EnumAppSide.Client && clientDialog != null)
            {
                SetDialogValues(clientDialog.Attributes);
            }
        }


        void SetDialogValues(ITreeAttribute dialogTree)
        {
            dialogTree.SetFloat("inputGrindTime", inputGrindTime);
            dialogTree.SetFloat("maxGrindingTime", maxGrindingTime());            
        }




        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            Inventory.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;

            tree.SetFloat("inputGrindTime", inputGrindTime);
            tree.SetInt("quantityPlayersGrinding", quantityPlayersGrinding);
        }




        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (api.World is IServerWorldAccessor)
            {
                Inventory.DropAll(pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }

            if (ambientSound != null)
            {
                ambientSound.Stop();
                ambientSound.Dispose();
            }

            if (renderer != null)
            {
                renderer.Unregister();
                renderer = null;
            }
        }

        ~BlockEntityQuern()
        {
            if (ambientSound != null) ambientSound.Dispose();
        }

        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            if (packetid < 1000)
            {
                Inventory.InvNetworkUtil.HandleClientPacket(player, packetid, data);

                // Tell server to save this chunk to disk again
                api.World.BlockAccessor.GetChunkAtBlockPos(pos.X, pos.Y, pos.Z).MarkModified();

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

                    IClientWorldAccessor clientWorld = (IClientWorldAccessor)api.World;

                    SyncedTreeAttribute dtree = new SyncedTreeAttribute();
                    SetDialogValues(dtree);

                    clientDialog = clientWorld.OpenDialog(dialogClassName, dialogTitle, Inventory, pos, dtree);
                }
            }

            if (packetid == (int)EnumBlockContainerPacketId.CloseInventory)
            {
                IClientWorldAccessor clientWorld = (IClientWorldAccessor)api.World;
                clientWorld.Player.InventoryManager.CloseInventory(Inventory);
            }
        }

        #endregion

        #region Helper getters


        public IItemSlot InputSlot
        {
            get { return inventory.GetSlot(0); }
        }

        public IItemSlot OutputSlot
        {
            get { return inventory.GetSlot(1); }
        }

        public ItemStack InputStack
        {
            get { return inventory.GetSlot(0).Itemstack; }
            set { inventory.GetSlot(0).Itemstack = value; inventory.GetSlot(0).MarkDirty(); }
        }

        public ItemStack OutputStack
        {
            get { return inventory.GetSlot(1).Itemstack; }
            set { inventory.GetSlot(1).Itemstack = value; inventory.GetSlot(1).MarkDirty(); }
        }


        public GrindingProperties InputGrindProps
        {
            get {
                IItemSlot slot = inventory.GetSlot(0);
                if (slot.Itemstack == null) return null;
                return slot.Itemstack.Collectible.GrindingProps;
            }
        }
        
        #endregion


        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            int q = Inventory.QuantitySlots;
            for (int i = 0; i < q; i++)
            {
                ItemSlot slot = Inventory.GetSlot(i);
                if (slot.Itemstack == null) continue;

                if (slot.Itemstack.Class == EnumItemClass.Item)
                {
                    itemIdMapping[slot.Itemstack.Item.Id] = slot.Itemstack.Item.Code;
                }
                else
                {
                    blockIdMapping[slot.Itemstack.Block.BlockId] = slot.Itemstack.Block.Code;
                }
            }
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping)
        {
            int q = Inventory.QuantitySlots;
            for (int i = 0; i < q; i++)
            {
                ItemSlot slot = Inventory.GetSlot(i);
                if (slot.Itemstack == null) continue;
                slot.Itemstack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve);
            }
        }



        public bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (ownBlock == null || !IsGrinding) return false;

            mesher.AddMeshData(this.baseOnlyMesh);
            return true;
        }


        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            renderer?.Unregister();
        }

    }
}
