﻿using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

#nullable disable

namespace Vintagestory.GameContent
{

    public class BlockEntityQuern : BlockEntityOpenableContainer
    {
        static SimpleParticleProperties FlourParticles;
        static SimpleParticleProperties FlourDustParticles;

        static BlockEntityQuern()
        {
            // 1..20 per tick
            FlourParticles = new SimpleParticleProperties(1, 3, ColorUtil.ToRgba(40, 220, 220, 220), new Vec3d(), new Vec3d(), new Vec3f(-0.25f, -0.25f, -0.25f), new Vec3f(0.25f, 0.25f, 0.25f), 1, 1, 0.1f, 0.3f, EnumParticleModel.Quad);
            FlourParticles.AddPos.Set(1 + 2 / 32f, 0, 1 + 2 / 32f);
            FlourParticles.AddQuantity = 20;
            FlourParticles.MinVelocity.Set(-0.25f, 0, -0.25f);
            FlourParticles.AddVelocity.Set(0.5f, 1, 0.5f);
            FlourParticles.WithTerrainCollision = true;
            FlourParticles.ParticleModel = EnumParticleModel.Cube;
            FlourParticles.LifeLength = 1.5f;
            FlourParticles.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -0.4f);

            // 1..5 per tick
            FlourDustParticles = new SimpleParticleProperties(1, 3, ColorUtil.ToRgba(40, 220, 220, 220), new Vec3d(), new Vec3d(), new Vec3f(-0.25f, -0.25f, -0.25f), new Vec3f(0.25f, 0.25f, 0.25f), 1, 1, 0.1f, 0.3f, EnumParticleModel.Quad);
            FlourDustParticles.AddPos.Set(1 + 2 / 32f, 0, 1 + 2 / 32f);
            FlourDustParticles.AddQuantity = 5;
            FlourDustParticles.MinVelocity.Set(-0.05f, 0, -0.05f);
            FlourDustParticles.AddVelocity.Set(0.1f, 0.2f, 0.1f);
            FlourDustParticles.WithTerrainCollision = false;
            FlourDustParticles.ParticleModel = EnumParticleModel.Quad;
            FlourDustParticles.LifeLength = 1.5f;
            FlourDustParticles.SelfPropelled = true;
            FlourDustParticles.GravityEffect = 0;
            FlourDustParticles.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, 0.4f);
            FlourDustParticles.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -16f);
        }


        ILoadedSound ambientSound;

        internal InventoryQuern inventory;
        
        // For how long the current ore has been grinding
        public float inputGrindTime;
        public float prevInputGrindTime;


        GuiDialogBlockEntityQuern clientDialog;
        QuernTopRenderer renderer;
        bool automated;
        BEBehaviorMPConsumer mpc;
        private float prevSpeed = float.NaN;


        // Server side only
        Dictionary<string, long> playersGrinding = new Dictionary<string, long>();
        // Client and serverside
        int quantityPlayersGrinding;

        int nowOutputFace;

        #region Getters

        public string Material
        {
            get { return Block.LastCodePart(); }
        }

        public float GrindSpeed
        {
            get {
                if (quantityPlayersGrinding > 0) return 1f;

                if (automated && mpc.Network != null) return mpc.TrueSpeed;

                return 0;
            }
        }


        MeshData quernBaseMesh
        {
            get
            {
                Api.ObjectCache.TryGetValue("quernbasemesh-" + Material, out object value);
                return (MeshData)value;
            }
            set { Api.ObjectCache["quernbasemesh-" + Material] = value; }
        }

        MeshData quernTopMesh
        {
            get
            {
                Api.ObjectCache.TryGetValue("querntopmesh-" + Material, out object value);
                return (MeshData)value;
            }
            set { Api.ObjectCache["querntopmesh-" + Material] = value; }
        }

        #endregion

        #region Config

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
            get { return Lang.Get("Quern"); }
        }

        public override InventoryBase Inventory
        {
            get { return inventory; }
        }

        #endregion


        public BlockEntityQuern()
        {
            inventory = new InventoryQuern(null, null);
            inventory.SlotModified += OnSlotModifid;
        }



        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            inventory.LateInitialize("quern-" + Pos.X + "/" + Pos.Y + "/" + Pos.Z, api);

            RegisterGameTickListener(Every100ms, 100);
            RegisterGameTickListener(Every500ms, 500);

            if (api.Side == EnumAppSide.Client)
            {
                renderer = new QuernTopRenderer(api as ICoreClientAPI, Pos, GenMesh("top"));
                renderer.mechPowerPart = this.mpc;
                if (automated)
                {
                    renderer.ShouldRender = true;
                    renderer.ShouldRotateAutomated = true;
                }

                (api as ICoreClientAPI).Event.RegisterRenderer(renderer, EnumRenderStage.Opaque, "quern");

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



        public void updateSoundState(bool nowGrinding)
        {
            if (nowGrinding) startSound();
            else stopSound();
        }

        public void startSound()
        {
            if (ambientSound == null && Api?.Side == EnumAppSide.Client)
            {
                ambientSound = (Api as ICoreClientAPI).World.LoadSound(new SoundParams()
                {
                    Location = new AssetLocation("sounds/block/quern.ogg"),
                    ShouldLoop = true,
                    Position = Pos.ToVec3f().Add(0.5f, 0.25f, 0.5f),
                    DisposeOnFinish = false,
                    Volume = 0.75f
                });

                ambientSound.Start();
            }
        }

        public void stopSound()
        {
            if (ambientSound != null)
            {
                ambientSound.Stop();
                ambientSound.Dispose();
                ambientSound = null;
            }
        }


        public override void CreateBehaviors(Block block, IWorldAccessor worldForResolve)
        {
            base.CreateBehaviors(block, worldForResolve);

            mpc = GetBehavior<BEBehaviorMPConsumer>();
            if (mpc != null)
            {
                mpc.OnConnected = () => {
                    automated = true;
                    quantityPlayersGrinding = 0;
                    if (renderer != null)
                    {
                        renderer.ShouldRender = true;
                        renderer.ShouldRotateAutomated = true;
                    }
                };

                mpc.OnDisconnected = () =>  {
                    automated = false;
                    if (renderer != null)
                    {
                        renderer.ShouldRender = false;
                        renderer.ShouldRotateAutomated = false;
                    }
                };
            }
        }


        public void IsGrinding(IPlayer byPlayer)
        {
            SetPlayerGrinding(byPlayer, true);
        }

        private void Every100ms(float dt)
        {
            float grindSpeed = GrindSpeed;

            if (Api.Side == EnumAppSide.Client)
            {
                if (InputStack != null)
                {
                    float dustMinQ = 1 * grindSpeed;
                    float dustAddQ = 5 * grindSpeed;
                    float flourPartMinQ = 1 * grindSpeed;
                    float flourPartAddQ = 20 * grindSpeed;

                    FlourDustParticles.Color = FlourParticles.Color = InputStack.Collectible.GetRandomColor(Api as ICoreClientAPI, InputStack);
                    FlourDustParticles.Color &= 0xffffff;
                    FlourDustParticles.Color |= (200 << 24);
                    FlourDustParticles.MinQuantity = dustMinQ;
                    FlourDustParticles.AddQuantity = dustAddQ;
                    FlourDustParticles.MinPos.Set(Pos.X - 1 / 32f, Pos.Y + 11 / 16f, Pos.Z - 1 / 32f);
                    FlourDustParticles.MinVelocity.Set(-0.1f, 0, -0.1f);
                    FlourDustParticles.AddVelocity.Set(0.2f, 0.2f, 0.2f);

                    FlourParticles.MinPos.Set(Pos.X - 1 / 32f, Pos.Y + 11 / 16f, Pos.Z - 1 / 32f);
                    FlourParticles.AddQuantity = flourPartAddQ;
                    FlourParticles.MinQuantity = flourPartMinQ;

                    Api.World.SpawnParticles(FlourParticles);
                    Api.World.SpawnParticles(FlourDustParticles);
                }

                if (ambientSound != null && automated && mpc.TrueSpeed != prevSpeed)
                {
                    prevSpeed = mpc.TrueSpeed;
                    ambientSound.SetPitch((0.5f + prevSpeed) * 0.9f);
                    ambientSound.SetVolume(Math.Min(1f, prevSpeed * 3f));
                }
                else prevSpeed = float.NaN;

                return;
            }


            // Only tick on the server and merely sync to client
            if (CanGrind() && grindSpeed > 0)
            {
                inputGrindTime += dt * grindSpeed;
                
                if (inputGrindTime >= maxGrindingTime())
                {
                    grindInput();
                    inputGrindTime = 0;                    
                }

                MarkDirty();
            }
        }

        private void grindInput()
        {
            ItemStack grindedStack = InputGrindProps.GroundStack.ResolvedItemstack.Clone();

            if (OutputSlot.Itemstack == null)
            {
                OutputSlot.Itemstack = grindedStack;
            }
            else
            {
                int mergableQuantity = OutputSlot.Itemstack.Collectible.GetMergableQuantity(OutputSlot.Itemstack, grindedStack, EnumMergePriority.AutoMerge);

                if (mergableQuantity > 0)
                {
                    OutputSlot.Itemstack.StackSize += grindedStack.StackSize;
                } else
                {
                    BlockFacing face = BlockFacing.HORIZONTALS[nowOutputFace];
                    nowOutputFace = (nowOutputFace+1) % 4;

                    Block block = Api.World.BlockAccessor.GetBlock(this.Pos.AddCopy(face));
                    if (block.Replaceable < 6000) return;
                    Api.World.SpawnItemEntity(grindedStack, this.Pos.ToVec3d().Add(0.5 + face.Normalf.X * 0.7, 0.75, 0.5 + face.Normalf.Z * 0.7), new Vec3d(face.Normalf.X * 0.02f, 0, face.Normalf.Z * 0.02f));
                }
            }

            InputSlot.TakeOut(1);
            InputSlot.MarkDirty();
            OutputSlot.MarkDirty();
        }


        // Sync to client every 500ms
        private void Every500ms(float dt)
        {
            if (Api.Side == EnumAppSide.Server && (GrindSpeed > 0  || prevInputGrindTime != inputGrindTime) && inventory[0].Itemstack?.Collectible.GrindingProps != null)  //don't spam update packets when empty, as inputGrindTime is irrelevant when empty
            {
                MarkDirty();
            }

            prevInputGrindTime = inputGrindTime;

            
            foreach (var val in playersGrinding)
            {
                long ellapsedMs = Api.World.ElapsedMilliseconds;
                if (ellapsedMs - val.Value > 1000)
                {
                    playersGrinding.Remove(val.Key);
                    break;
                }
            }
        }





        public void SetPlayerGrinding(IPlayer player, bool playerGrinding)
        {
            if (!automated)
            {
                if (playerGrinding)
                {
                    playersGrinding[player.PlayerUID] = Api.World.ElapsedMilliseconds;
                }
                else
                {
                    playersGrinding.Remove(player.PlayerUID);
                }

                quantityPlayersGrinding = playersGrinding.Count;
            }

            updateGrindingState();
        }

        bool beforeGrinding;
        void updateGrindingState()
        {
            if (Api?.World == null) return;

            bool nowGrinding = quantityPlayersGrinding > 0 || (automated && mpc.TrueSpeed > 0f);

            if (nowGrinding != beforeGrinding)
            {
                if (renderer != null)
                {
                    renderer.ShouldRotateManual = quantityPlayersGrinding > 0;
                }

                Api.World.BlockAccessor.MarkBlockDirty(Pos, OnRetesselated);

                updateSoundState(nowGrinding);

                if (Api.Side == EnumAppSide.Server)
                {
                    MarkDirty();
                }
            }

            beforeGrinding = nowGrinding;
        }




        private void OnSlotModifid(int slotid)
        {
            if (Api is ICoreClientAPI)
            {
                clientDialog.Update(inputGrindTime, maxGrindingTime());
            }

            if (slotid == 0)
            {
                if (InputSlot.Empty)
                {
                    inputGrindTime = 0.0f; // reset the progress to 0 if the item is removed.
                }
                MarkDirty();

                if (clientDialog != null && clientDialog.IsOpened())
                {
                    clientDialog.SingleComposer.ReCompose();
                }
            }
        }


        private void OnRetesselated()
        {
            if (renderer == null) return; // Maybe already disposed

            renderer.ShouldRender = quantityPlayersGrinding > 0 || automated;
        }




        internal MeshData GenMesh(string type = "base")
        {
            Block block = Api.World.BlockAccessor.GetBlock(Pos);
            if (block.BlockId == 0) return null;

            ITesselatorAPI mesher = ((ICoreClientAPI)Api).Tesselator;

            mesher.TesselateShape(block, API.Common.Shape.TryGet(Api, "shapes/block/stone/quern/" + type + ".json"), out MeshData mesh);

            return mesh;
        }




        public bool CanGrind()
        {
            GrindingProperties grindProps = InputGrindProps;
            if (grindProps == null) return false;
            return true;
        }
        
        


        #region Events

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel.SelectionBoxIndex == 1) return false;

            if (Api.Side == EnumAppSide.Client)
            {
                toggleInventoryDialogClient(byPlayer, () => {
                    clientDialog = new GuiDialogBlockEntityQuern(DialogTitle, Inventory, Pos, Api as ICoreClientAPI);
                    clientDialog.Update(inputGrindTime, maxGrindingTime());
                    return clientDialog;
                });
            }

            return true;
        }


        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(player, packetid, data);
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
            Inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));

            if (Api != null)
            {
                Inventory.AfterBlocksLoaded(Api.World);
            }


            inputGrindTime = tree.GetFloat("inputGrindTime");
            nowOutputFace = tree.GetInt("nowOutputFace");

            if (worldForResolving.Side == EnumAppSide.Client)
            {
                List<int> clientIds = new List<int>((tree["clientIdsGrinding"] as IntArrayAttribute).value);

                quantityPlayersGrinding = clientIds.Count;
                
                string[] playeruids = playersGrinding.Keys.ToArray();

                foreach (var uid in playeruids)
                {
                    IPlayer plr = Api.World.PlayerByUid(uid);

                    if (!clientIds.Contains(plr.ClientId))
                    {
                        playersGrinding.Remove(uid);
                    } else
                    {
                        clientIds.Remove(plr.ClientId);
                    }
                }
                
                for (int i = 0; i < clientIds.Count; i++)
                {
                    IPlayer plr = worldForResolving.AllPlayers.FirstOrDefault(p => p.ClientId == clientIds[i]);
                    if (plr != null) playersGrinding.Add(plr.PlayerUID, worldForResolving.ElapsedMilliseconds);
                }
                
                updateGrindingState();
            }


            if (Api?.Side == EnumAppSide.Client && clientDialog != null)
            {
                clientDialog.Update(inputGrindTime, maxGrindingTime());
            }
        }



        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            Inventory.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;

            tree.SetFloat("inputGrindTime", inputGrindTime);
			tree.SetInt("nowOutputFace", nowOutputFace);
            List<int> vals = new List<int>();
            foreach (var val in playersGrinding)
            {
                IPlayer plr = Api.World.PlayerByUid(val.Key);
                if (plr == null) continue;
                vals.Add(plr.ClientId);
            }


            tree["clientIdsGrinding"] = new IntArrayAttribute(vals.ToArray());
        }




        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (ambientSound != null)
            {
                ambientSound.Stop();
                ambientSound.Dispose();
            }

            clientDialog?.TryClose();

            renderer?.Dispose();
            renderer = null;
        }

        ~BlockEntityQuern()
        {
            if (ambientSound != null) ambientSound.Dispose();
        }


        #endregion

        #region Helper getters


        public ItemSlot InputSlot
        {
            get { return inventory[0]; }
        }

        public ItemSlot OutputSlot
        {
            get { return inventory[1]; }
        }

        public ItemStack InputStack
        {
            get { return inventory[0].Itemstack; }
            set { inventory[0].Itemstack = value; inventory[0].MarkDirty(); }
        }

        public ItemStack OutputStack
        {
            get { return inventory[1].Itemstack; }
            set { inventory[1].Itemstack = value; inventory[1].MarkDirty(); }
        }


        public GrindingProperties InputGrindProps
        {
            get {
                ItemSlot slot = inventory[0];
                if (slot.Itemstack == null) return null;
                return slot.Itemstack.Collectible.GrindingProps;
            }
        }
        
        #endregion


        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            foreach (var slot in Inventory)
            {
                if (slot.Itemstack == null) continue;

                if (slot.Itemstack.Class == EnumItemClass.Item)
                {
                    itemIdMapping[slot.Itemstack.Item.Id] = slot.Itemstack.Item.Code;
                }
                else
                {
                    blockIdMapping[slot.Itemstack.Block.BlockId] = slot.Itemstack.Block.Code;
                }
                slot.Itemstack?.Collectible.OnStoreCollectibleMappings(Api.World, slot , blockIdMapping, itemIdMapping);
            }
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            foreach (var slot in Inventory)
            {
                if (slot.Itemstack == null) continue;
                if (!slot.Itemstack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve))
                {
                    slot.Itemstack = null;
                }
                slot.Itemstack?.Collectible.OnLoadCollectibleMappings(worldForResolve, slot , oldBlockIdMapping, oldItemIdMapping, resolveImports);
            }
        }



        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (Block == null) return false;
            
            mesher.AddMeshData(this.quernBaseMesh);
            if (quantityPlayersGrinding == 0 && !automated)
            {
                mesher.AddMeshData(
                    this.quernTopMesh.Clone()
                    .Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, renderer.AngleRad, 0)
                    .Translate(0 / 16f, 11 / 16f, 0 / 16f)
                );
            }
            

            return true;
        }


        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            renderer?.Dispose();

            if (ambientSound != null)
            {
                ambientSound.Stop();
                ambientSound.Dispose();
                ambientSound = null;
            }
        }

    }
}
