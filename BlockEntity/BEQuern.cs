using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockEntityQuern : BlockEntityContainer, IBlockShapeSupplier
    {
        static SimpleParticleProperties FlourParticles;

        static BlockEntityQuern()
        {
            FlourParticles = new SimpleParticleProperties(1, 3, ColorUtil.ColorFromArgb(40, 220, 220, 220), new Vec3d(), new Vec3d(), new Vec3f(-0.25f, -0.25f, -0.25f), new Vec3f(0.25f, 0.25f, 0.25f), 1, 1, 0.1f, 0.3f, EnumParticleModel.Quad);
            FlourParticles.addPos.Set(1 + 2 / 32f, 0, 1 + 2 / 32f);
            FlourParticles.addQuantity = 20;
            FlourParticles.minVelocity.Set(-0.25f, 0, -0.25f);
            FlourParticles.addVelocity.Set(0.5f, 1, 0.5f);
            FlourParticles.WithTerrainCollision = true;
            FlourParticles.model = EnumParticleModel.Cube;
            FlourParticles.lifeLength = 1.5f;
            FlourParticles.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -0.4f);
        }

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

        public void SetPlayerGrinding(IPlayer player, bool playerGrinding)
        {
            bool beforeGrinding = IsGrinding;

            if (playerGrinding)
            {
                if (!playersGrinding.Contains(player))
                {
                    playersGrinding.Add(player);
                }
                //Console.WriteLine("added player");
            }
            else
            {
                playersGrinding.Remove(player);
                //Console.WriteLine("removed player");
            }

            quantityPlayersGrinding = playersGrinding.Count;

            bool nowGrinding = IsGrinding;

            if (nowGrinding != beforeGrinding)
            {
                if (renderer != null)
                {
                    renderer.ShouldRotate = nowGrinding;
                }

                api.World.BlockAccessor.MarkBlockDirty(pos, OnRetesselated);

                if (nowGrinding)
                {
                    ambientSound?.Start();
                } else
                {
                    ambientSound?.Stop();
                }
            }
        }


        MeshData quernBaseMesh
        {
            get
            {
                object value = null;
                api.ObjectCache.TryGetValue("quernbasemesh-" + Material, out value);
                return (MeshData)value;
            }
            set { api.ObjectCache["quernbasemesh-" + Material] = value; }
        }

        MeshData quernTopMesh
        {
            get
            {
                object value = null;
                api.ObjectCache.TryGetValue("querntopmesh-" + Material, out value);
                return (MeshData)value;
            }
            set { api.ObjectCache["querntopmesh-" + Material] = value; }
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
            if (renderer == null) return; // Maybe already disposed

            //Console.WriteLine("did retesselate now, players using: {0}, grind flag: {1}", playersGrinding.Count, IsGrinding);
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

                if (quernBaseMesh == null)
                {
                    quernBaseMesh = GenMesh("base");
                }
                if (quernTopMesh == null)
                {
                    quernTopMesh = GenMesh("top");
                }
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
            if (api.Side == EnumAppSide.Client)
            {
                if (IsGrinding)
                {
                    if (InputStack.Class == EnumItemClass.Block)
                    {
                        FlourParticles.color = CollectibleParticleProperties.RandomBlockPixel(api as ICoreClientAPI, InputStack.Block, BlockFacing.UP, pos);
                    }
                    else
                    {
                        FlourParticles.color = CollectibleParticleProperties.RandomItemPixel(api as ICoreClientAPI, InputStack.Item, BlockFacing.UP, pos);
                    }

                    FlourParticles.minPos.Set(pos.X - 1/32f, pos.Y + 11 / 16f, pos.Z - 1 / 32f);

                    api.World.SpawnParticles(FlourParticles);
                }
                return;
            }


            // Only tick on the server and merely sync to client

            // Use up fuel
            if (CanGrind() && IsGrinding)
            {
                inputGrindTime += dt;

                if (inputGrindTime >= maxGrindingTime())
                {
                    grindInput();
                    inputGrindTime = 0;
                    MarkDirty();
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
            if (OutputSlot.Itemstack == null) return true;

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

            if (worldForResolving.Side == EnumAppSide.Client)
            {
                List<int> clientIds = new List<int>((tree["clientIdsGrinding"] as IntArrayAttribute).value);
                for (int i = 0; i < playersGrinding.Count; i++)
                {
                    IPlayer plr = playersGrinding[i];
                    if (!clientIds.Contains(plr.ClientId))
                    {
                        playersGrinding.Remove(plr);
                        i--;
                    } else
                    {
                        clientIds.Remove(plr.ClientId);
                    }
                }
                
                for (int i = 0; i < clientIds.Count; i++)
                {
                    IPlayer plr = worldForResolving.AllPlayers.FirstOrDefault(p => p.ClientId == clientIds[i]);
                    if (plr != null) playersGrinding.Add(plr);
                }
                
            }
            


            if (api?.Side == EnumAppSide.Client && clientDialog != null)
            {
                SetDialogValues(clientDialog.Attributes);
            }
        }


        void SetDialogValues(ITreeAttribute dialogTree)
        {
            dialogTree.SetFloat("inputGrindTime", inputGrindTime);
            dialogTree.SetFloat("maxGrindTime", maxGrindingTime());            
        }




        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            Inventory.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;

            tree.SetFloat("inputGrindTime", inputGrindTime);
            tree["clientIdsGrinding"] = new IntArrayAttribute(playersGrinding.Select(p => p.ClientId).ToArray());
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
            if (ownBlock == null) return false;

            //Console.WriteLine("call to ontesseleation. Isgrinding={0}", IsGrinding);

            mesher.AddMeshData(this.quernBaseMesh);
            if (!IsGrinding)
            {
                mesher.AddMeshData(
                    this.quernTopMesh.Clone()
                    .Rotate(new API.MathTools.Vec3f(0.5f, 0.5f, 0.5f), 0, renderer.Angle * GameMath.DEG2RAD, 0)
                    .Translate(0 / 16f, 11 / 16f, 0 / 16f)
                );
            }
            

            return true;
        }


        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            renderer?.Unregister();
        }

    }
}
