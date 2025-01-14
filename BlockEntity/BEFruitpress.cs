using ProtoBuf;
using System;
using System.Collections.Generic;
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

    public enum EnumFruitPressAnimState
    {
        ScrewStart,
        Unscrew,
        ScrewContinue
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class FruitPressAnimPacket
    {
        public EnumFruitPressAnimState AnimationState;
        public float AnimationSpeed;
        public float CurrentFrame = 0;
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

        float? loadedFrame;
        bool serverListenerActive;

        long listenerId;
        double juiceableLitresCapacity = 10;
        double screwPercent;
        double squeezedLitresLeft;
        double pressSqueezeRel;
        bool squeezeSoundPlayed;
        int dryStackSize = 0;

        public ItemSlot MashSlot => inv[0];
        public ItemSlot BucketSlot => inv[1];
        ItemStack mashStack => MashSlot.Itemstack;

        double juiceableLitresLeft
        {
            get
            {
                return mashStack?.Attributes?.GetDouble("juiceableLitresLeft") ?? 0;
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
                return mashStack?.Attributes?.GetDouble("juiceableLitresTransfered") ?? 0;
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


        public bool CanScrew => !CompressAnimFinished;
        public bool CanUnscrew => CompressAnimFinished || CompressAnimActive;
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
                else if (serverListenerActive)
                {
                    if (loadedFrame > 0) animUtil.StartAnimation(compressAnimMeta);
                    if (listenerId == 0) listenerId = RegisterGameTickListener(onTick100msServer, 25);
                }
            }
        }


        double lastLiquidTransferTotalHours;

        private void onTick25msClient(float dt)
        {
            double squeezeRel = mashStack?.Attributes.GetDouble("squeezeRel", 1) ?? 1;
            float selfHeight = (float)(juiceableLitresTransfered + juiceableLitresLeft) / 10f;

            if (MashSlot.Empty || renderer.juiceTexPos == null || squeezeRel >= 1 || pressSqueezeRel > squeezeRel || squeezedLitresLeft < 0.01) return;

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
                maxPos.Y = 5 / 16f - (1 - squeezeRel + Math.Max(0, 0.9f - selfHeight)) * 0.5f;

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
            RunningAnimation anim = animUtil.animator.GetAnimationState("compress");
            if (serverListenerActive)
            {
                anim.CurrentFrame = loadedFrame ?? 0;
                updateSqueezeRel(animUtil.animator.GetAnimationState("compress"));
                serverListenerActive = false;
                loadedFrame = null;
                return;
            }
            else if (CompressAnimActive) (Api as ICoreServerAPI)?.Network.BroadcastBlockEntityPacket(Pos, PacketIdAnimUpdate, new FruitPressAnimPacket() { AnimationState = EnumFruitPressAnimState.ScrewContinue, AnimationSpeed = compressAnimMeta.AnimationSpeed, CurrentFrame = anim.CurrentFrame });
            if (MashSlot.Empty) return;

            var juiceProps = getJuiceableProps(mashStack);
            double totalHours = Api.World.Calendar.TotalHours;

            double squeezeRel = mashStack.Attributes.GetDouble("squeezeRel", 1);

            // First we need to calculate how squeezed down the mash has been and therefore how much we're allowed to take
            squeezedLitresLeft = Math.Max(Math.Max(0, squeezedLitresLeft), juiceableLitresLeft - ((juiceableLitresLeft + juiceableLitresTransfered) * screwPercent));
            double litresToTransfer = Math.Min(squeezedLitresLeft, Math.Round((totalHours - lastLiquidTransferTotalHours) * (CompressAnimActive ? GameMath.Clamp(squeezedLitresLeft * (1 - squeezeRel) * 500f, 25f, 100f) : 5f), 2));

            if (Api.Side == EnumAppSide.Server && CompressAnimActive && squeezeRel < 1 && pressSqueezeRel <= squeezeRel && !squeezeSoundPlayed && juiceableLitresLeft > 0)
            {
                Api.World.PlaySoundAt(new AssetLocation("sounds/player/wetclothsqueeze.ogg"), Pos, 0, null, false);
                squeezeSoundPlayed = true;
            }

            BlockLiquidContainerBase cntBlock = BucketSlot?.Itemstack?.Collectible as BlockLiquidContainerBase;

            if (Api.Side == EnumAppSide.Server && squeezedLitresLeft > 0)
            {
                ItemStack liquidStack = juiceProps.LiquidStack.ResolvedItemstack;
                liquidStack.StackSize = 999999;
                float actuallyTransfered;

                if (cntBlock != null && !cntBlock.IsFull(BucketSlot.Itemstack))
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
                squeezedLitresLeft -= pressSqueezeRel <= squeezeRel ? actuallyTransfered : (actuallyTransfered * 100); // Let the mash drain less if screw is released.
                juiceableLitresTransfered += actuallyTransfered;
                lastLiquidTransferTotalHours = totalHours;
                MarkDirty(true);
            }
            else if (Api.Side == EnumAppSide.Server && (!CompressAnimActive || juiceableLitresLeft <= 0))
            {
                UnregisterGameTickListener(listenerId);
                listenerId = 0;

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

            if (section == EnumFruitPressSection.MashContainer && firstEvent)
            {
                return InteractMashContainer(byPlayer, blockSel);
            }
            if (section == EnumFruitPressSection.Ground && firstEvent)
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
            if (!CompressAnimActive && !byPlayer.Entity.Controls.CtrlKey && firstEvent)
            {
                (Api as ICoreClientAPI).Network.SendBlockEntityPacket(Pos, PacketIdScrewStart);

                return true;
            }

            // Unscrew
            if (CanUnscrew && (byPlayer.Entity.Controls.CtrlKey || (CompressAnimFinished && !byPlayer.Entity.Controls.CtrlKey)) && firstEvent)
            {
                (Api as ICoreClientAPI).Network.SendBlockEntityPacket(Pos, PacketIdUnscrew);

                return true;
            }

            // Continue
            if (compressAnimMeta.AnimationSpeed == 0 && !byPlayer.Entity.Controls.CtrlKey)
            {
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

                // Don't try to put dry mash back into the machine
                if (hprops.LitresPerItem == null && !handStack.Attributes.HasAttribute("juiceableLitresLeft")) return false;

                var pressedStack = hprops.LitresPerItem != null ? hprops.PressedStack.ResolvedItemstack.Clone() : handStack.GetEmptyClone();
                if (MashSlot.Empty)
                {
                    MashSlot.Itemstack = pressedStack;

                    // Directly transfer the fruit mash to the machine if it's empty, but only one if it's a stack
                    if (hprops.LitresPerItem == null)
                    {
                        mashStack.StackSize = 1;
                        dryStackSize = (int)(GameMath.RoundRandom(Api.World.Rand, (float)juiceableLitresLeft + (float)juiceableLitresTransfered) * getJuiceableProps(mashStack).PressedDryRatio);
                        handslot.TakeOut(1);
                        MarkDirty(true);
                        renderer?.reloadMeshes(hprops, true);
                        (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                        return true;
                    }
                }
                else if (juiceableLitresLeft + juiceableLitresTransfered >= juiceableLitresCapacity)
                {
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, "fullcontainer", Lang.Get("Container is full, press out juice and remove the mash before adding more"));
                    return false;
                }

                if (!mashStack.Equals(Api.World, pressedStack, GlobalConstants.IgnoredStackAttributes.Append("juiceableLitresLeft", "juiceableLitresTransfered", "squeezeRel")))
                {
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, "fullcontainer", Lang.Get("Cannot mix fruit"));
                    return false;
                }

                float transferableLitres = (float)handStack.Attributes.GetDecimal("juiceableLitresLeft");
                float usedLitres = (float)handStack.Attributes.GetDecimal("juiceableLitresTransfered");
                int removeItems;
                if (hprops.LitresPerItem == null)
                {
                    // the juiceableLitresLeft and juiceableLitresTransfered are per item if we have a stack of multiple
                    // so we check if the press is full and only remove and add the liters for one item at a time
                    if (juiceableLitresLeft + juiceableLitresTransfered + transferableLitres + usedLitres > juiceableLitresCapacity)
                    {
                        (Api as ICoreClientAPI)?.TriggerIngameError(this, "fullcontainer", Lang.Get("Container is full, press out juice and remove the mash before adding more"));
                        return false;
                    }

                    // If we're adding mash we want to average the spoiling so players can't keep it fresh forever
                    TransitionState[] sourceTransitionStates = handStack.Collectible.UpdateAndGetTransitionStates(Api.World, handslot);
                    TransitionState[] targetTransitionStates = mashStack.Collectible.UpdateAndGetTransitionStates(Api.World, MashSlot);

                    Dictionary<EnumTransitionType, TransitionState> targetStatesByType = null;

                    targetStatesByType = new Dictionary<EnumTransitionType, TransitionState>();
                    foreach (var state in targetTransitionStates) targetStatesByType[state.Props.Type] = state;

                    // We're mixing based on total litres because we don't really have a stack size to compare
                    float t = (transferableLitres + usedLitres) / (transferableLitres + usedLitres + (float)juiceableLitresLeft + (float)juiceableLitresTransfered);

                    foreach (var sourceState in sourceTransitionStates)
                    {
                        TransitionState targetState = targetStatesByType[sourceState.Props.Type];
                        mashStack.Collectible.SetTransitionState(mashStack, sourceState.Props.Type, sourceState.TransitionedHours * t + targetState.TransitionedHours * (1 - t));
                    }

                    removeItems = 1;
                } else
                {
                    // In order to make sure we're always giving exactly the amount of juice that's appropriate for the number
                    // of items we take out of the inventory we start by counting up how many the player wants to add, be it
                    // 1, 4, or the whole stack, and then subtracting 1 from that total until we have an amount that can fit
                    int desiredTransferAmount = Math.Min(handStack.StackSize, byPlayer.Entity.Controls.ShiftKey ? 1 : byPlayer.Entity.Controls.CtrlKey ? handStack.Item.MaxStackSize : 4);

                    while (desiredTransferAmount * (float)hprops.LitresPerItem + juiceableLitresLeft + juiceableLitresTransfered > juiceableLitresCapacity) desiredTransferAmount -= 1;

                    if (desiredTransferAmount <= 0)
                    {
                        (Api as ICoreClientAPI)?.TriggerIngameError(this, "fullcontainer", Lang.Get("Container is full, press out juice and remove the mash before adding more"));
                        return false;
                    }

                    transferableLitres = desiredTransferAmount * (float)hprops.LitresPerItem;
                    removeItems = desiredTransferAmount;
                }

                if (removeItems > 0)
                {
                    var stackCode = handslot.Itemstack.Collectible.Code;
                    handslot.TakeOut(removeItems);


                    Api.World.Logger.Audit("{0} Put {1}x{2} into Fruitpress at {3}.",
                        byPlayer.PlayerName,
                        removeItems,
                        stackCode,
                        blockSel.Position
                    );

                    mashStack.Attributes.SetDouble("juiceableLitresLeft", juiceableLitresLeft += transferableLitres);
                    mashStack.Attributes.SetDouble("juiceableLitresTransfered", juiceableLitresTransfered += usedLitres);
                    mashStack.StackSize = 1;

                    // Calculate how large the stack of dry mash will be here so we can tell the player that amount is in
                    // the machine later and not have a random mismatch due to the use of RoundRandom in the calculation
                    dryStackSize = (int)(GameMath.RoundRandom(Api.World.Rand, (float)juiceableLitresLeft + (float)juiceableLitresTransfered) * getJuiceableProps(mashStack).PressedDryRatio);

                    handslot.MarkDirty();
                    MarkDirty(true);
                    renderer?.reloadMeshes(hprops, true);
                    (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                }

                return true;
            }

            // Take out mash
            if (MashSlot.Empty) return false;

            convertDryMash();

            if (!byPlayer.InventoryManager.TryGiveItemstack(mashStack, true))
            {
                Api.World.SpawnItemEntity(mashStack, Pos);
            }
            Api.World.Logger.Audit("{0} Took 1x{1} from Fruitpress at {2}.",
                byPlayer.PlayerName,
                mashStack.Collectible.Code,
                blockSel.Position
            );

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
                    Api.World.SpawnItemEntity(BucketSlot.Itemstack, Pos);
                }
                Api.World.Logger.Audit("{0} Took 1x{1} from Fruitpress at {2}.",
                    byPlayer.PlayerName,
                    BucketSlot.Itemstack.Collectible.Code,
                    blockSel.Position
                );

                if (BucketSlot.Itemstack.Block != null) Api.World.PlaySoundAt(BucketSlot.Itemstack.Block.Sounds.Place, Pos, -0.5, byPlayer);

                BucketSlot.Itemstack = null;
                MarkDirty(true);
                bucketMesh?.Clear();
            }

            else if (handStack != null && handStack.Collectible is BlockLiquidContainerBase blockLiqCont && blockLiqCont.AllowHeldLiquidTransfer && blockLiqCont.IsTopOpened && blockLiqCont.CapacityLitres < 20 && BucketSlot.Empty)
            {
                bool moved = handslot.TryPutInto(Api.World, BucketSlot, 1) > 0;
                if (moved)
                {
                    Api.World.Logger.Audit("{0} Put 1x{1} into Fruitpress at {2}.",
                        byPlayer.PlayerName,
                        BucketSlot.Itemstack.Collectible.Code,
                        blockSel.Position
                    );
                    handslot.MarkDirty();
                    MarkDirty(true);
                    genBucketMesh();
                    Api.World.PlaySoundAt(handStack.Block.Sounds.Place, Pos, -0.5, byPlayer);
                }
            }

            return true;
        }


        public bool OnBlockInteractStep(float secondsUsed, IPlayer byPlayer, EnumFruitPressSection section)
        {
            if (section != EnumFruitPressSection.Screw) return false;

            if (mashStack != null) updateSqueezeRel(animUtil.animator.GetAnimationState("compress"));

            return CompressAnimActive || (Block as BlockFruitPress).RightMouseDown;
        }


        public void OnBlockInteractStop(float secondsUsed, IPlayer byPlayer)
        {
            updateSqueezeRel(animUtil.animator.GetAnimationState("compress"));

            if (!CompressAnimActive) return;

            compressAnimMeta.AnimationSpeed = 0f;
            (Api as ICoreServerAPI)?.Network.BroadcastBlockEntityPacket(Pos, PacketIdAnimUpdate, new FruitPressAnimPacket() { AnimationState = EnumFruitPressAnimState.ScrewContinue, AnimationSpeed = 0f });
        }

        // Here we will calculate various things, like how squeezed down the mash is, how far down the screw is,
        // and a copy of squeezeRel for the press itself so that it can tell if the screw is touching the mash yet
        private void updateSqueezeRel(RunningAnimation anim)
        {
            if (anim == null || mashStack==null) return;

            double squeezeRel = GameMath.Clamp(1f - anim.CurrentFrame / anim.Animation.QuantityFrames / 2f, 0.1f, 1f);
            float selfHeight = (float)(juiceableLitresTransfered + juiceableLitresLeft) / 10f;

            squeezeRel += Math.Max(0, 0.9f - selfHeight);
            pressSqueezeRel = GameMath.Clamp(squeezeRel, 0.1f, 1f);
            squeezeRel = GameMath.Clamp(Math.Min(mashStack.Attributes.GetDouble("squeezeRel", 1), squeezeRel), 0.1f, 1f);

            mashStack.Attributes.SetDouble("squeezeRel", squeezeRel);

            screwPercent = GameMath.Clamp(1f - anim.CurrentFrame / (anim.Animation.QuantityFrames - 1), 0, 1f) / selfHeight;
        }


        private void convertDryMash()
        {
            if (juiceableLitresLeft < 0.01)
            {
                mashStack?.Attributes?.RemoveAttribute("juiceableLitresTransfered");
                mashStack?.Attributes?.RemoveAttribute("juiceableLitresLeft");
                mashStack?.Attributes?.RemoveAttribute("squeezeRel");
                if (mashStack?.Collectible.Code.Path != "rot") mashStack.StackSize = dryStackSize;
                dryStackSize = 0;
            }
        }


        public bool OnBlockInteractCancel(float secondsUsed, IPlayer byPlayer)
        {
            updateSqueezeRel(animUtil.animator.GetAnimationState("compress"));

            if (CompressAnimActive)
            {
                compressAnimMeta.AnimationSpeed = 0f;
                (Api as ICoreServerAPI)?.Network.BroadcastBlockEntityPacket(Pos, PacketIdAnimUpdate, new FruitPressAnimPacket() { AnimationState = EnumFruitPressAnimState.ScrewContinue, AnimationSpeed = 0f });
            }

            return true;
        }


        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
        {
            switch (packetid)
            {
                case PacketIdScrewStart:
                    compressAnimMeta.AnimationSpeed = 0.5f;
                    animUtil.StartAnimation(compressAnimMeta);
                    squeezeSoundPlayed = false;
                    lastLiquidTransferTotalHours = Api.World.Calendar.TotalHours;
                    if (listenerId == 0) listenerId = RegisterGameTickListener(onTick100msServer, 25);
                    (Api as ICoreServerAPI)?.Network.BroadcastBlockEntityPacket(Pos, PacketIdAnimUpdate, new FruitPressAnimPacket() { AnimationState = EnumFruitPressAnimState.ScrewStart, AnimationSpeed = 0.5f });
                    break;
                case PacketIdScrewContinue:
                    compressAnimMeta.AnimationSpeed = 0.5f;
                    (Api as ICoreServerAPI)?.Network.BroadcastBlockEntityPacket(Pos, PacketIdAnimUpdate, new FruitPressAnimPacket() { AnimationState = EnumFruitPressAnimState.ScrewContinue, AnimationSpeed = 0.5f });
                    break;
                case PacketIdUnscrew:
                    compressAnimMeta.AnimationSpeed = 1.5f;
                    animUtil.StopAnimation("compress");
                    (Api as ICoreServerAPI)?.Network.BroadcastBlockEntityPacket(Pos, PacketIdAnimUpdate, new FruitPressAnimPacket() { AnimationState = EnumFruitPressAnimState.Unscrew, AnimationSpeed = 1.5f });
                    animUtil.animator.GetAnimationState("compress").Stop(); // Without this the player is occasionally told to unscrew a second time
                    if (MashSlot.Empty && listenerId != 0) UnregisterGameTickListener(listenerId); // Unregister the tick listener here since the container is empty
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

                if (packet.AnimationState == EnumFruitPressAnimState.ScrewStart)
                {
                    animUtil.StartAnimation(compressAnimMeta);
                    squeezeSoundPlayed = false;
                    lastLiquidTransferTotalHours = Api.World.Calendar.TotalHours;
                    if (listenerId == 0) listenerId = RegisterGameTickListener(onTick25msClient, 25);
                }
                else if (packet.AnimationState == EnumFruitPressAnimState.ScrewContinue)
                {
                    // Since the game isn't set up to synchronize BlockEntity animations and the game logic of
                    // the fruit press relies on the animation progress we have to do something a little hacky
                    // to get the client to properly synchronize the visual animation with the server, so first
                    // we have to get the animation running for a moment if it hasn't been already
                    RunningAnimation anim = animUtil.animator.GetAnimationState("compress");
                    if (anim.CurrentFrame <= 0 && packet.CurrentFrame > 0)
                    {
                        compressAnimMeta.AnimationSpeed = 0.0001f;
                        animUtil.StartAnimation(compressAnimMeta);
                    }

                    // Then we have to fast forward the animation to the correct spot so the client will visually
                    // match where the server is in the animation. We do this by setting the speed very low and
                    // then progressing one step at a time in a while loop before resetting the speed back to
                    // where it was before we performed this action. We also make sure we don't do this unless
                    // the CurrentFrame is > 0 to make sure that the client is fully loaded and capable of playing
                    // animations otherwise it will crash with a null pointer exception
                    if (anim.CurrentFrame > 0 && anim.CurrentFrame < packet.CurrentFrame)
                    {
                        compressAnimMeta.AnimationSpeed = 0.0001f;
                        while (anim.CurrentFrame < packet.CurrentFrame && anim.CurrentFrame < anim.Animation.QuantityFrames - 1) anim.Progress(1f, 1f);
                        compressAnimMeta.AnimationSpeed = packet.AnimationSpeed;
                        anim.CurrentFrame = packet.CurrentFrame;

                        MarkDirty(true);
                        updateSqueezeRel(anim);
                    }

                    if (listenerId == 0) listenerId = RegisterGameTickListener(onTick25msClient, 25);
                }
                else if (packet.AnimationState == EnumFruitPressAnimState.Unscrew)
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
            if (!MashSlot.Empty) convertDryMash();

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
            squeezedLitresLeft = tree.GetDouble("squeezedLitresLeft");
            squeezeSoundPlayed = tree.GetBool("squeezeSoundPlayed");
            dryStackSize = tree.GetInt("dryStackSize");
            lastLiquidTransferTotalHours = tree.GetDouble("lastLiquidTransferTotalHours");

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
            else
            {
                if (listenerId == 0) serverListenerActive = tree.GetBool("ServerListenerActive");
                if (listenerId != 0 || serverListenerActive)
                {
                    loadedFrame = tree.GetFloat("CurrentFrame");
                    compressAnimMeta.AnimationSpeed = tree.GetFloat("AnimationSpeed", compressAnimMeta.AnimationSpeed);
                }
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetDouble("squeezedLitresLeft", squeezedLitresLeft);
            tree.SetBool("squeezeSoundPlayed", squeezeSoundPlayed);
            tree.SetInt("dryStackSize", dryStackSize);
            tree.SetDouble("lastLiquidTransferTotalHours", lastLiquidTransferTotalHours);

            if (Api.Side == EnumAppSide.Server)
            {
                if (listenerId != 0) tree.SetBool("ServerListenerActive", true);
                if (CompressAnimActive)
                {
                    tree.SetFloat("CurrentFrame", animUtil.animator.GetAnimationState("compress").CurrentFrame);
                    tree.SetFloat("AnimationSpeed", compressAnimMeta.AnimationSpeed);
                }
            }
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
                if (block != null)
                {
                    dsc.Append(Lang.Get("Container:") + " ");
                    block.GetContentInfo(BucketSlot, dsc, Api.World);
                    dsc.AppendLine();
                }
            }

            if (!MashSlot.Empty)
            {
                // Using the precalculated dryStackSize we fake the number of dry mash in the slot without converting,
                // and that means we don't have to worry about someone adding fruit to the machine when it still has
                // dry mash inside and overwriting the total stack size, making them lose some of their dry mash
                int stacksize = mashStack.Collectible.Code.Path != "rot" ? dryStackSize : MashSlot.StackSize;

                if (juiceableLitresLeft > 0 && mashStack.Collectible.Code.Path != "rot")
                {
                    string juicename = getJuiceableProps(mashStack).LiquidStack.ResolvedItemstack.GetName().ToLowerInvariant();
                    dsc.AppendLine(Lang.GetWithFallback("fruitpress-litreswhensqueezed", "Mash produces {0:0.##} litres of juice when squeezed", juiceableLitresLeft, juicename));
                } else
                {
                    dsc.AppendLine(Lang.Get("{0}x {1}", stacksize, MashSlot.GetStackName().ToLowerInvariant()));
                }
            }
        }



        private void genBucketMesh()
        {
            if (BucketSlot.Empty || capi == null) return;

            var stack = BucketSlot.Itemstack;
            IContainedMeshSource meshSource = stack.Collectible?.GetCollectibleInterface<IContainedMeshSource>();
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
