using ProtoBuf;
using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class JuiceableProperties
    {
        public float? LitresPerItem;
        public float PressedDryRatio = 1f;
        public JsonItemStack LiquidStack;
        public JsonItemStack PressedStack;
    }

    public enum EnumFruitPressSection
    {
        Ground,
        MashContainer,
        Screw
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class FruitPressAnimPacket
    {
        public bool AnimationActive;
        public float AnimationSpeed;
    }


    public class BlockEntityFruitPress : BlockEntityContainer
    {
        const int PacketIdAnimUpdate = 1001;
        const int PacketIdScrewStart = 1002;
        const int PacketIdUnscrew = 1003;
        const int PacketIdScrewContinue = 1004;

        #region Particle
        static SimpleParticleProperties liquidParticles;
        static BlockEntityFruitPress()
        {
            liquidParticles = new SimpleParticleProperties()
            {
                MinVelocity = new Vec3f(-0.04f, 0, -0.04f),
                AddVelocity = new Vec3f(0.08f, 0, 0.08f),
                addLifeLength = 0.5f,
                LifeLength = 0.5f,
                MinQuantity = 0.25f,
                GravityEffect = 0.5f,
                SelfPropelled = true,
                MinSize = 0.1f,
                MaxSize = 0.2f
            };
        }

        #endregion

        // Slot 0: Berries / Mash
        // Slot 1: Bucket
        InventoryGeneric inv;
        public override InventoryBase Inventory => inv;
        public override string InventoryClassName => "fruitpress";
        BlockEntityAnimationUtil animUtil
        {
            get { return GetBehavior<BEBehaviorAnimatable>()?.animUtil; }
        }

        ICoreClientAPI capi;
        BlockFruitPress ownBlock;

        MeshData meshMovable;
        MeshData bucketMesh;
        MeshData bucketMeshTmp;

        FruitpressContentsRenderer renderer;


        AnimationMetaData compressAnimMeta = new AnimationMetaData()
        {
            Animation = "compress",
            Code = "compress",
            AnimationSpeed = 0.5f,
            EaseOutSpeed = 0.5f,
            EaseInSpeed = 3
        };


        long listenerId;
        double juiceableLitresCapacity = 10;
        bool squeezeSoundPlayed;

        public ItemSlot MashSlot => inv[0];
        public ItemSlot BucketSlot => inv[1];
        ItemStack mashStack => MashSlot.Itemstack;

        double juiceableLitresLeft
        {
            get
            {
                return mashStack?.Attributes.GetDouble("juiceableLitresLeft") ?? 0;
            }
            set
            {
                mashStack.Attributes.SetDouble("juiceableLitresLeft", value);
            }
        }

        double juiceableLitresTransfered
        {
            get
            {
                return mashStack.Attributes.GetDouble("juiceableLitresTransfered");
            }
            set
            {
                mashStack.Attributes.SetDouble("juiceableLitresTransfered", value);
            }
        }

        public bool CompressAnimFinished
        {
            get
            {
                RunningAnimation anim = animUtil.animator.GetAnimationState("compress");
                return anim.CurrentFrame >= anim.Animation.QuantityFrames - 1;
            }
        }
        public bool CompressAnimActive => animUtil.activeAnimationsByAnimCode.ContainsKey("compress") || animUtil.animator.GetAnimationState("compress")?.Active == true;


        public bool CanScrew => !CompressAnimActive || compressAnimMeta.AnimationSpeed == 0;
        public bool CanUnscrew => CompressAnimFinished;
        public bool CanFillRemoveItems => !CompressAnimActive;

        public BlockEntityFruitPress()
        {
            inv = new InventoryGeneric(2, "fruitpress-0", null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            this.ownBlock = Block as BlockFruitPress;
            capi = api as ICoreClientAPI;

            if (ownBlock != null)
            {
                Shape shape = Shape.TryGet(api, "shapes/block/wood/fruitpress/part-movable.json");

                if (api.Side == EnumAppSide.Client)
                {
                    capi.Tesselator.TesselateShape(ownBlock, shape, out meshMovable, new Vec3f(0, ownBlock.Shape.rotateY, 0));
                    animUtil.InitializeAnimator("fruitpress", shape, null, new Vec3f(0, ownBlock.Shape.rotateY, 0));
                } else
                {
                    animUtil.InitializeAnimatorServer("fruitpress", shape);
                }

                if (api.Side == EnumAppSide.Client)
                {
                    renderer = new FruitpressContentsRenderer(api as ICoreClientAPI, Pos, this);
                    (api as ICoreClientAPI).Event.RegisterRenderer(renderer, EnumRenderStage.Opaque, "fruitpress");

                    renderer.reloadMeshes(getJuiceableProps(mashStack), true);
                    genBucketMesh();
                }
            }
        }


        double lastLiquidTransferTotalHours;

        private void onTick25msClient(float dt)
        {
            double squeezeRel = mashStack?.Attributes.GetDouble("squeezeRel", 1) ?? 1;

            if (MashSlot.Empty || renderer.juiceTexPos == null || squeezeRel >= 1) return;

            var rand = Api.World.Rand;

            liquidParticles.MinQuantity = (float)juiceableLitresLeft / 10f;

            for (int i = 0; i < 4; i++)
            {
                BlockFacing face = BlockFacing.HORIZONTALS[i];

                liquidParticles.Color = capi.BlockTextureAtlas.GetRandomColor(renderer.juiceTexPos, rand.Next(TextureAtlasPosition.RndColorsLength));
                
                Vec3d minPos = face.Plane.Startd.Add(-0.5, 0, -0.5);
                Vec3d maxPos = face.Plane.Endd.Add(-0.5, 0, -0.5);

                minPos.Mul(8 / 16f);
                maxPos.Mul(8 / 16f);
                maxPos.Y = 5 / 16f - (1 - squeezeRel) * 0.25f;

                minPos.Add(face.Normalf.X * 1.2f / 16f, 0, face.Normalf.Z * 1.2f / 16f);
                maxPos.Add(face.Normalf.X * 1.2f / 16f, 0, face.Normalf.Z * 1.2f / 16f);

                liquidParticles.MinPos = minPos;
                liquidParticles.AddPos = maxPos.Sub(minPos);
                liquidParticles.MinPos.Add(Pos).Add(0.5, 1, 0.5);

                Api.World.SpawnParticles(liquidParticles);
            }

            if (squeezeRel < 0.9f)
            {
                liquidParticles.MinPos = Pos.ToVec3d().Add(6 / 16f, 0.7f, 6 / 16f);
                liquidParticles.AddPos.Set(4 / 16f, 0f, 4 / 16f);
                for (int i = 0; i < 3; i++)
                {
                    liquidParticles.Color = capi.BlockTextureAtlas.GetRandomColor(renderer.juiceTexPos, rand.Next(TextureAtlasPosition.RndColorsLength));
                    Api.World.SpawnParticles(liquidParticles);
                }
            }
        }

        private void onTick100msServer(float dt)
        {
            if (MashSlot.Empty) return;

            var juiceProps = getJuiceableProps(mashStack);
            double totalHours = Api.World.Calendar.TotalHours;

            double squeezeRel = mashStack.Attributes.GetDouble("squeezeRel", 1);
            double litresToTransfer = Math.Min(juiceableLitresLeft, (totalHours - lastLiquidTransferTotalHours) * 50f);

            if (Api.Side == EnumAppSide.Server && squeezeRel < 1 && !squeezeSoundPlayed && juiceableLitresLeft > 0)
            {
                Api.World.PlaySoundAt(new AssetLocation("sounds/player/wetclothsqueeze.ogg"), Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, null, false);
                squeezeSoundPlayed = true;
            }

            BlockLiquidContainerBase cntBlock = BucketSlot?.Itemstack?.Collectible as BlockLiquidContainerBase;

            if (Api.Side == EnumAppSide.Server && squeezeRel < 1 && totalHours - lastLiquidTransferTotalHours > 0.01)
            {
                ItemStack liquidStack = juiceProps.LiquidStack.ResolvedItemstack;
                liquidStack.StackSize = 999999;
                float actuallyTransfered;

                if (cntBlock != null)
                {
                    float beforelitres = cntBlock.GetCurrentLitres(BucketSlot.Itemstack);

                    if (litresToTransfer > 0)
                    {
                        cntBlock.TryPutLiquid(BucketSlot.Itemstack, liquidStack, (float)litresToTransfer);
                    }

                    float litres = cntBlock.GetCurrentLitres(BucketSlot.Itemstack);
                    actuallyTransfered = litres - beforelitres;
                } else
                {
                    actuallyTransfered = (float)litresToTransfer;
                }

                juiceableLitresLeft -= actuallyTransfered;
                juiceableLitresTransfered += actuallyTransfered;
                lastLiquidTransferTotalHours = totalHours;
                MarkDirty(true);
            }


            if (juiceableLitresLeft <= 0.01)
            {
                // Hack to fix rounding errors
                if (cntBlock != null)
                {
                    float litres = cntBlock.GetCurrentLitres(BucketSlot.Itemstack);
                    cntBlock.SetCurrentLitres(BucketSlot.Itemstack, (float)Math.Round(100 * litres) / 100f);
                }

                UnregisterGameTickListener(listenerId);
                listenerId = 0;

                int stacksize = GameMath.RoundRandom(Api.World.Rand, (float)juiceableLitresTransfered);
                mashStack.Attributes.RemoveAttribute("juiceableLitresTransfered");
                mashStack.Attributes.RemoveAttribute("juiceableLitresLeft");
                mashStack.StackSize = (int)(stacksize * juiceProps.PressedDryRatio);

                MarkDirty(true);
            }
        }


        // What needs to happen while squeezing:
        // 0..3 sec: Linearly squeeze mash mesh, stay squeezed
        // 0..3 sec: Spawn juice particles all around
        // 1..6 sec: Grow juice quad, spawn particles at the spout
        // 6..12 sec: Shrink juice quad
        // 1..12 sec: Add liquid to bucket
        public bool OnBlockInteractStart(IPlayer byPlayer, BlockSelection blockSel, EnumFruitPressSection section, bool firstEvent)
        {
            firstEvent |= Api.Side == EnumAppSide.Server;

            if (section == EnumFruitPressSection.MashContainer)
            {
                return InteractMashContainer(byPlayer, blockSel);
            }
            if (section == EnumFruitPressSection.Ground)
            {
                return InteractGround(byPlayer, blockSel);
            }
            if (section == EnumFruitPressSection.Screw)
            {
                return InteractScrew(byPlayer, blockSel, firstEvent);
            }

            return false;
        }

        private bool InteractScrew(IPlayer byPlayer, BlockSelection blockSel, bool firstEvent)
        {
            if (Api.Side == EnumAppSide.Server) return true; // We let the client control this

            // Start
            if (!CompressAnimActive && firstEvent)
            {
                compressAnimMeta.AnimationSpeed = 0.5f;
                animUtil.StartAnimation(compressAnimMeta);
                squeezeSoundPlayed = false;
                
                (Api as ICoreClientAPI).Network.SendBlockEntityPacket(Pos, PacketIdScrewStart);

                if (listenerId == 0)
                {
                    listenerId = RegisterGameTickListener(onTick25msClient, 25);
                }

                return true;
            }

            // Unscrew
            if (CanUnscrew && firstEvent)
            {
                compressAnimMeta.AnimationSpeed = 1.5f;
                animUtil.StopAnimation("compress");
                (Api as ICoreClientAPI).Network.SendBlockEntityPacket(Pos, PacketIdUnscrew);
                return true;
            }

            // Continue
            if (compressAnimMeta.AnimationSpeed == 0)
            {
                compressAnimMeta.AnimationSpeed = 0.5f;
                (Api as ICoreClientAPI).Network.SendBlockEntityPacket(Pos, PacketIdScrewContinue);
                return true;
            }


            return false;
        }

        private bool InteractMashContainer(IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot handslot = byPlayer.InventoryManager.ActiveHotbarSlot;
            ItemStack handStack = handslot.Itemstack;

            if (CompressAnimActive)
            {
                (Api as ICoreClientAPI)?.TriggerIngameError(this, "compressing", Lang.Get("Release the screw first to add/remove fruit"));
                return false;
            }

            // Put items
            if (!handslot.Empty)
            {
                var hprops = getJuiceableProps(handStack);
                if (hprops == null) return false;

                var pressedStack = hprops.PressedStack.ResolvedItemstack.Clone();
                if (MashSlot.Empty) MashSlot.Itemstack = pressedStack;
                else if (mashStack.StackSize >= 10)
                {
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, "fullcontainer", Lang.Get("Container is full, press out juice and remove the mash before adding more"));
                    return false;
                }

                if (!mashStack.Equals(Api.World, pressedStack, GlobalConstants.IgnoredStackAttributes.Append("juiceableLitresLeft", "juiceableLitresTransfered", "squeezeRel")))
                {
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, "fullcontainer", Lang.Get("Cannot mix fruit"));
                    return false;
                }


                float transferableLitres;
                int removeItems;
                if (hprops.LitresPerItem == null)
                {
                    var availableLitres = (float)handStack.Attributes.GetDecimal("juiceableLitresLeft");
                    transferableLitres = (float)Math.Min(availableLitres, juiceableLitresCapacity - juiceableLitresLeft);
                    // the juiceableLitresLeft is per item if we have a stack of multiple
                    // so we only remove one item and add the liters for one
                    removeItems = 1;
                } else
                {  
                    float desiredTransferSizeLitres = byPlayer.Entity.Controls.ShiftKey ? (float)hprops.LitresPerItem : Math.Min(handStack.StackSize, 4) * (float)hprops.LitresPerItem;
                    transferableLitres = (float)Math.Min(desiredTransferSizeLitres, juiceableLitresCapacity - juiceableLitresLeft);

                    removeItems = (int)(transferableLitres / hprops.LitresPerItem);
                }

                if (transferableLitres > 0) {
                    handslot.TakeOut(removeItems);

                    mashStack.Attributes.SetDouble("juiceableLitresLeft", juiceableLitresLeft += transferableLitres);
                    mashStack.StackSize = 1;
                    handslot.MarkDirty();
                    MarkDirty(true);
                    renderer?.reloadMeshes(hprops, true);
                    (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                }

                return true;
            }

            // Take out mash
            if (MashSlot.Empty) return false;

            if (!byPlayer.InventoryManager.TryGiveItemstack(mashStack, true))
            {
                Api.World.SpawnItemEntity(mashStack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }

            MashSlot.Itemstack = null;
            renderer?.reloadMeshes(null, true);

            if (Api.Side == EnumAppSide.Server)
            {
                MarkDirty(true);
            }

            return true;
        }

        private bool InteractGround(IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot handslot = byPlayer.InventoryManager.ActiveHotbarSlot;
            ItemStack handStack = handslot.Itemstack;

            if (handslot.Empty && !BucketSlot.Empty)
            {
                if (!byPlayer.InventoryManager.TryGiveItemstack(BucketSlot.Itemstack, true))
                {
                    Api.World.SpawnItemEntity(BucketSlot.Itemstack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }

                if (BucketSlot.Itemstack.Block != null) Api.World.PlaySoundAt(BucketSlot.Itemstack.Block.Sounds.Place, Pos.X + 0.5, Pos.Y, Pos.Z + 0.5, byPlayer);

                BucketSlot.Itemstack = null;
                MarkDirty(true);
                bucketMesh?.Clear();
            }

            else if (handStack != null && handStack.Collectible is BlockLiquidContainerBase blockLiqCont && blockLiqCont.AllowHeldLiquidTransfer && blockLiqCont.IsTopOpened && blockLiqCont.CapacityLitres < 20 && BucketSlot.Empty)
            {
                bool moved = handslot.TryPutInto(Api.World, BucketSlot, 1) > 0;
                if (moved)
                {
                    handslot.MarkDirty();
                    MarkDirty(true);
                    genBucketMesh();
                    Api.World.PlaySoundAt(handStack.Block.Sounds.Place, Pos.X + 0.5, Pos.Y, Pos.Z + 0.5, byPlayer);
                }
            }

            return true;
        }


        public bool OnBlockInteractStep(float secondsUsed, IPlayer byPlayer, EnumFruitPressSection section)
        {
            if (section != EnumFruitPressSection.Screw) return false;

            if (mashStack != null)
            {
                RunningAnimation anim = animUtil.animator.GetAnimationState("compress");
                if (anim != null)
                {
                    udpateSqueezeRel(anim);
                }
            }

            return CompressAnimActive && secondsUsed < 4f;
        }


        public void OnBlockInteractStop(float secondsUsed, IPlayer byPlayer)
        {
            RunningAnimation anim = animUtil.animator.GetAnimationState("compress");
            udpateSqueezeRel(anim);

            if (!CompressAnimActive) return;

            if (secondsUsed >= 4.8f)
            {
                compressAnimMeta.AnimationSpeed = 0f;
                // Fast forward the server
                anim.CurrentFrame = anim.Animation.QuantityFrames - 1;

                (Api as ICoreServerAPI)?.Network.BroadcastBlockEntityPacket(Pos, PacketIdAnimUpdate, new FruitPressAnimPacket() { AnimationActive = true, AnimationSpeed = 0f });
            }
        }


        private void udpateSqueezeRel(RunningAnimation anim)
        {
            if (anim == null || mashStack==null) return;

            double squeezeRel = GameMath.Clamp(1f - anim.CurrentFrame / anim.Animation.QuantityFrames / 2f, 0.1f, 1f);
            float selfHeight = (float)(juiceableLitresTransfered + juiceableLitresLeft) / 10f;

            squeezeRel += Math.Max(0, 0.9f - selfHeight);
            squeezeRel = GameMath.Clamp(Math.Min(mashStack.Attributes.GetDouble("squeezeRel", 1), squeezeRel), 0.1f, 1f);

            mashStack.Attributes.SetDouble("squeezeRel", squeezeRel);
        }


        public bool OnBlockInteractCancel(float secondsUsed, IPlayer byPlayer)
        {
            RunningAnimation anim = animUtil.animator.GetAnimationState("compress");
            udpateSqueezeRel(anim);

            if (CompressAnimActive)
            {
                compressAnimMeta.AnimationSpeed = 0f;
                (Api as ICoreServerAPI)?.Network.BroadcastBlockEntityPacket(Pos, PacketIdAnimUpdate, new FruitPressAnimPacket() { AnimationActive = true, AnimationSpeed = 0f });
            }

            return true;
        }


        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
        {
            switch (packetid)
            {
                case PacketIdScrewStart:
                    compressAnimMeta.AnimationSpeed = 1f; // This is supposed to be 0.5f, but for some reason, the server runs animations at half the speed?!
                    animUtil.StartAnimation(compressAnimMeta);
                    squeezeSoundPlayed = false;
                    lastLiquidTransferTotalHours = Api.World.Calendar.TotalHours;
                    if (listenerId == 0) listenerId = RegisterGameTickListener(onTick100msServer, 25);
                    (Api as ICoreServerAPI)?.Network.BroadcastBlockEntityPacket(Pos, PacketIdAnimUpdate, new FruitPressAnimPacket() { AnimationActive = true, AnimationSpeed = 0.5f });
                    break;
                case PacketIdScrewContinue:
                    compressAnimMeta.AnimationSpeed = 1f; // This is supposed to be 0.5f, but for some reason, the server runs animations at half the speed?!
                    (Api as ICoreServerAPI)?.Network.BroadcastBlockEntityPacket(Pos, PacketIdAnimUpdate, new FruitPressAnimPacket() { AnimationActive = true, AnimationSpeed = 0.5f });
                    break;
                case PacketIdUnscrew:
                    compressAnimMeta.AnimationSpeed = 1.5f;
                    animUtil.StopAnimation("compress");
                    (Api as ICoreServerAPI)?.Network.BroadcastBlockEntityPacket(Pos, PacketIdAnimUpdate, new FruitPressAnimPacket() { AnimationActive = false, AnimationSpeed = 1.5f });
                    break;
            }

            base.OnReceivedClientPacket(fromPlayer, packetid, data);
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            if (packetid == PacketIdAnimUpdate)
            {
                var packet = SerializerUtil.Deserialize<FruitPressAnimPacket>(data);

                compressAnimMeta.AnimationSpeed = packet.AnimationSpeed;

                if (packet.AnimationActive)
                {
                    if (!MashSlot.Empty && juiceableLitresLeft > 0 && !CompressAnimActive)
                    {
                        Api.World.PlaySoundAt(new AssetLocation("sounds/player/wetclothsqueeze.ogg"), Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, null, false);
                    }

                    animUtil.StartAnimation(compressAnimMeta);
                    if (listenerId == 0) listenerId = RegisterGameTickListener(onTick25msClient, 25);
                }
                else
                {
                    animUtil.StopAnimation("compress");
                    if (listenerId != 0) { UnregisterGameTickListener(listenerId); listenerId = 0; }
                }
            }

            base.OnReceivedServerPacket(packetid, data);
        }

        public JuiceableProperties getJuiceableProps(ItemStack stack)
        {
            var props = stack?.ItemAttributes?["juiceableProperties"].Exists == true ? stack.ItemAttributes["juiceableProperties"].AsObject<JuiceableProperties>(null, stack.Collectible.Code.Domain) : null;
            props?.LiquidStack?.Resolve(Api.World, "juiceable properties liquidstack", stack.Collectible.Code);
            props?.PressedStack?.Resolve(Api.World, "juiceable properties pressedstack", stack.Collectible.Code);

            return props;
        }


        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken();
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            renderer?.Dispose();
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            renderer?.Dispose();
        }



        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            bool wasEmpty = Inventory.Empty;
            ItemStack beforeStack = mashStack;

            base.FromTreeAttributes(tree, worldForResolving);

            if (worldForResolving.Side == EnumAppSide.Client)
            {
                if (listenerId > 0 && juiceableLitresLeft <= 0)
                {
                    UnregisterGameTickListener(listenerId);
                    listenerId = 0;
                }
                renderer?.reloadMeshes(getJuiceableProps(mashStack), wasEmpty != Inventory.Empty || (beforeStack != null && mashStack != null && !beforeStack.Equals(Api.World, mashStack, GlobalConstants.IgnoredStackAttributes)));
                genBucketMesh();
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            bool skip = base.OnTesselation(mesher, tessThreadTesselator);

            if (!skip) mesher.AddMeshData(meshMovable);

            mesher.AddMeshData(bucketMesh);

            return false;
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            if (!BucketSlot.Empty)
            {
                BlockLiquidContainerBase block = BucketSlot.Itemstack.Collectible as BlockLiquidContainerBase;
                dsc.Append(Lang.Get("Container: "));
                block.GetContentInfo(BucketSlot, dsc, Api.World);
                dsc.AppendLine();
            }

            if (!MashSlot.Empty)
            {
                if (juiceableLitresLeft > 0)
                {
                    dsc.AppendLine(Lang.Get("Mash produces {0:0.##} litres of juice when squeezed", juiceableLitresLeft));
                } else
                {
                    dsc.AppendLine(Lang.Get("Dry Mash"));
                }
            }
        }



        private void genBucketMesh()
        {
            if (BucketSlot.Empty || capi == null) return;

            var stack = BucketSlot.Itemstack;
            var meshSource = stack.Collectible as IContainedMeshSource;
            if (meshSource != null)
            {
                bucketMeshTmp = meshSource.GenMesh(stack, capi.BlockTextureAtlas, Pos);
                // Liquid mesh part
                bucketMeshTmp.CustomInts = new CustomMeshDataPartInt(bucketMeshTmp.FlagsCount);
                bucketMeshTmp.CustomInts.Count = bucketMeshTmp.FlagsCount;
                bucketMeshTmp.CustomInts.Values.Fill(0x4000000); // light foam only

                bucketMeshTmp.CustomFloats = new CustomMeshDataPartFloat(bucketMeshTmp.FlagsCount * 2);
                bucketMeshTmp.CustomFloats.Count = bucketMeshTmp.FlagsCount * 2;

                bucketMesh = bucketMeshTmp.Clone();
            }
        }
    }
}
    