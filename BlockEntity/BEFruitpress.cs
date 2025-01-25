using System.Net.Sockets;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent;

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

// For server -> client, sent with current animation %.
public enum FruitPressPacket
{
    ScrewStart = 1001,
    ScrewStop = 1002,
    Unscrew = 1003,
    SyncAnimation = 1004
}

public class BlockEntityFruitPress : BlockEntityContainer
{
    // Slot 0: Berries / mash.
    // Slot 1: Bucket.
    private readonly InventoryGeneric inv;
    public override InventoryBase Inventory => inv;
    public override string InventoryClassName => "fruitpress";
    public BlockEntityAnimationUtil AnimUtil => GetBehavior<BEBehaviorAnimatable>()?.animUtil!;

    private ICoreClientAPI Capi => (ICoreClientAPI)Api;
    private MeshData? meshMovable;
    private MeshData? bucketMesh;
    private FruitPressContentsRenderer renderer = null!;

    // 0 = fully unscrewed, 1 = fully screwed.
    private const float SECONDS_TO_SCREW = 119 / 30f * 2f;
    private const float TIME_TO_SQUEEZE = 12;
    private float screwPercent;
    private long lastUnscrewTime;

    private bool soundPlayed = false;

    // How much juice in the stack (default 10).
    public double JuiceableLitresLeft
    {
        get => MashSlot.Itemstack?.Attributes.GetDouble("juiceableLitresLeft") ?? 0;
        set => MashSlot.Itemstack?.Attributes.SetDouble("juiceableLitresLeft", value);
    }

    // How much juice has been moved to the bucket.
    public double JuiceableLitresTransferred
    {
        get => MashSlot.Itemstack?.Attributes.GetDouble("juiceableLitresTransfered") ?? 0;
        set => MashSlot.Itemstack?.Attributes.SetDouble("juiceableLitresTransfered", value);
    }

    public ItemSlot MashSlot => inv[0];
    public ItemSlot BucketSlot => inv[1];

    // For client interactions.
    public bool CanScrew => screwPercent != 1;
    public bool CanFillRemoveItems => screwPercent == 0;

    // Will be played in reverse until finished when unscrewing.
    // Will be set to the current screw ratio and started when screwing begins.
    // When loaded on the client, will be set to the current screw ratio.
    public readonly AnimationMetaData compressAnimMeta = new()
    {
        Animation = "compress",
        Code = "compress",
        AnimationSpeed = 0.5f,
        EaseOutSpeed = 0.5f,
        EaseInSpeed = 3
    };

    public BlockEntityFruitPress()
    {
        inv = new InventoryGeneric(2, "fruitpress-0", null, null);
    }

    #region Particles

    static readonly SimpleParticleProperties liquidParticles;
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

    #region Interaction

    protected bool isPlayerInteracting; // Is a player interacting? Server-side.
    private long eventListenerId = -1; // Event listener id, different for client/server.

    /// <summary>
    /// When a player interacts with the block server-side.
    /// </summary>
    public void HandleInteraction(bool started, EnumFruitPressSection section, IServerPlayer player)
    {
        if (Api.World.ElapsedMilliseconds - lastUnscrewTime < 2500) return; // Finish animation.

        // Update screw.
        if (section == EnumFruitPressSection.Screw)
        {
            if ((player.Entity.Controls.CtrlKey || screwPercent == 1) && started)
            {
                UnregisterGameTickListener(eventListenerId);
                eventListenerId = -1;
                SendUpdateToClients(FruitPressPacket.Unscrew);
                screwPercent = 0;
                lastUnscrewTime = Api.World.ElapsedMilliseconds;
                soundPlayed = false;
                return;
            }

            if (started && eventListenerId == -1)
            {
                eventListenerId = RegisterGameTickListener(OnServerInteractTick, 50);
            }

            isPlayerInteracting = started;

            SendUpdateToClients(started ? FruitPressPacket.ScrewStart : FruitPressPacket.ScrewStop);
        }

        // Put mash into container.
        if (section == EnumFruitPressSection.MashContainer && started)
        {
            ItemSlot handSlot = player.InventoryManager.ActiveHotbarSlot;

            if (screwPercent != 0)
            {
                player.SendIngameError("compressing", Lang.Get("Release the screw first to add/remove fruit"));
                return;
            }

            if (!handSlot.Empty)
            {
                PutMashIn(handSlot, player);
            }
            else
            {
                TakeMashOut(player);
            }
        }

        if (section == EnumFruitPressSection.Ground && started)
        {
            InteractGround(player);
        }
    }

    private void InteractGround(IServerPlayer fromPlayer)
    {
        ItemSlot handSlot = fromPlayer.InventoryManager.ActiveHotbarSlot;
        ItemStack handStack = handSlot.Itemstack;

        if (handSlot.Empty && !BucketSlot.Empty)
        {
            if (!fromPlayer.InventoryManager.TryGiveItemstack(BucketSlot.Itemstack, true))
            {
                Api.World.SpawnItemEntity(BucketSlot.Itemstack, Pos);
            }

            Api.World.Logger.Audit("{0} Took 1x{1} from Fruitpress at {2}.",
                fromPlayer.PlayerName,
                BucketSlot.Itemstack.Collectible.Code,
                Pos
            );

            if (BucketSlot.Itemstack.Block != null) Api.World.PlaySoundAt(BucketSlot.Itemstack.Block.Sounds.Place, Pos, -0.5);

            BucketSlot.Itemstack = null;
            handSlot.MarkDirty();
            MarkDirty(true);
        }

        else if (handStack != null && handStack.Collectible is BlockLiquidContainerBase blockLiqCont && blockLiqCont.AllowHeldLiquidTransfer && blockLiqCont.IsTopOpened && blockLiqCont.CapacityLitres < 20 && BucketSlot.Empty)
        {
            bool moved = handSlot.TryPutInto(Api.World, BucketSlot, 1) > 0;
            if (moved)
            {
                Api.World.Logger.Audit("{0} Put 1x{1} into Fruitpress at {2}.",
                    fromPlayer.PlayerName,
                    BucketSlot.Itemstack.Collectible.Code,
                    Pos
                );

                handSlot.MarkDirty();
                MarkDirty(true);
                Api.World.PlaySoundAt(handStack.Block.Sounds.Place, Pos, -0.5);
            }
        }
    }

    private const double LITRES_CAPACITY = 10;

    public void PutMashIn(ItemSlot handSlot, IServerPlayer fromPlayer)
    {
        JuiceableProperties? mashProps = GetJuiceableProps(handSlot.Itemstack);
        if (mashProps == null) return; // Item isn't juiceable.
        if (mashProps.LitresPerItem == null && !handSlot.Itemstack.Attributes.HasAttribute("juiceableLitresLeft")) return; // Item is dry mash.

        // If it has litres per item, it is not yet converted to mash. Get the mash stack.
        ItemStack pressedStack = mashProps.LitresPerItem != null ? mashProps.PressedStack.ResolvedItemstack.Clone() : handSlot.Itemstack.GetEmptyClone();

        // If there's nothing inside, put the pressed stack in. If the item being put in is a mash, return early.
        if (MashSlot.Empty)
        {
            MashSlot.Itemstack = pressedStack;

            // Directly transfer mash.
            if (mashProps.LitresPerItem == null)
            {
                MashSlot.Itemstack.StackSize = 1;
                handSlot.TakeOut(1);
                handSlot.MarkDirty();
                MarkDirty(true);
                return;
            }
        }
        else if (JuiceableLitresLeft + JuiceableLitresTransferred >= LITRES_CAPACITY)
        {
            // Before adding, container was already full.
            fromPlayer.SendIngameError("fullcontainer", Lang.Get("Container is full, press out juice and remove the mash before adding more"));
            return;
        }

        // Don't mix 2 items.
        if (!MashSlot.Itemstack.Equals(Api.World, pressedStack, GlobalConstants.IgnoredStackAttributes.Append("juiceableLitresLeft", "juiceableLitresTransfered", "squeezeRel")))
        {
            fromPlayer.SendIngameError("fullcontainer", Lang.Get("Cannot mix fruit"));
            return;
        }

        // Either a wet mash or more fruit will be transferred in now.
        ItemStack handStack = handSlot.Itemstack;

        int desiredTransferAmount = 0;

        if (mashProps.LitresPerItem == null) // Wet mash.
        {
            double roomInCurrentMash = LITRES_CAPACITY - (JuiceableLitresLeft + JuiceableLitresTransferred);
            double litresInHand = handStack.Attributes.GetDouble("juiceableLitresLeft");
            float amountToTransfer = (float)Math.Min(litresInHand, roomInCurrentMash);

            // Mix spoilage. The ratio will come from the source stack, and 1 - the ratio will come from the target stack.
            float ratio = (float)((amountToTransfer / JuiceableLitresLeft) + amountToTransfer);
            TransitionState[] sourceTransitionStates = handStack.Collectible.UpdateAndGetTransitionStates(Api.World, handSlot);
            TransitionState[] targetTransitionStates = MashSlot.Itemstack.Collectible.UpdateAndGetTransitionStates(Api.World, MashSlot);
            Dictionary<EnumTransitionType, TransitionState> targetStatesByType = new();
            foreach (TransitionState state in targetTransitionStates) targetStatesByType[state.Props.Type] = state;
            foreach (TransitionState sourceState in sourceTransitionStates)
            {
                TransitionState targetState = targetStatesByType[sourceState.Props.Type];
                MashSlot.Itemstack.Collectible.SetTransitionState(MashSlot.Itemstack, sourceState.Props.Type, (sourceState.TransitionedHours * ratio) + (targetState.TransitionedHours * (1 - ratio)));
            }

            handStack.Attributes.SetDouble("juiceableLitresLeft", litresInHand - amountToTransfer);
            JuiceableLitresLeft += amountToTransfer;

            if (litresInHand - amountToTransfer <= 0) handSlot.TakeOut(1);
            handSlot.MarkDirty();
            MarkDirty(true);
        }
        else // Fruit.
        {
            desiredTransferAmount = Math.Min(handStack.StackSize, fromPlayer.Entity.Controls.ShiftKey ? 1 : fromPlayer.Entity.Controls.CtrlKey ? handStack.Item.MaxStackSize : 4);
            while ((desiredTransferAmount * (float)mashProps.LitresPerItem!) + JuiceableLitresLeft + JuiceableLitresTransferred > LITRES_CAPACITY) desiredTransferAmount -= 1;

            if (desiredTransferAmount <= 0)
            {
                // Could also add, at minimum, one fruit.
                fromPlayer.SendIngameError("fullcontainer", Lang.Get("Container is full, press out juice and remove the mash before adding more"));
                return;
            }

            float transferrableLitres = desiredTransferAmount * (float)mashProps.LitresPerItem;

            handSlot.TakeOut(desiredTransferAmount);
            handSlot.MarkDirty();

            JuiceableLitresLeft += transferrableLitres;
            MarkDirty(true);
        }

        Api.World.Logger.Audit("{0} Put {1}x{2} into Fruitpress at {3}.",
                        fromPlayer.PlayerName,
                        desiredTransferAmount,
                        handStack.Collectible.Code,
                        Pos
                    );
    }

    public void TakeMashOut(IServerPlayer fromPlayer)
    {
        if (MashSlot.Empty) return;

        // Remove liquid properties from the mash if it's empty.
        TryConvertMash();

        if (!fromPlayer.InventoryManager.TryGiveItemstack(MashSlot.Itemstack, true))
        {
            Api.World.SpawnItemEntity(MashSlot.Itemstack, Pos);
        }

        Api.World.Logger.Audit("{0} Took 1x{1} from Fruitpress at {2}.",
            fromPlayer.PlayerName,
            MashSlot.Itemstack.Collectible.Code,
            Pos
        );

        MashSlot.TakeOutWhole();

        MarkDirty(true);
    }

    /// <summary>
    /// Started when: interaction started.
    /// Stopped when: unscrewed.
    /// Called every 100ms.
    /// </summary>
    public void OnServerInteractTick(float dt)
    {
        if (isPlayerInteracting)
        {
            screwPercent = Math.Clamp(screwPercent + (dt / SECONDS_TO_SCREW), 0, 1);
            UpdateSqueezeRelOnServer();
        }

        if (screwPercent > 0.1f && MashSlot.Itemstack != null)
        {
            double litresTransferred = JuiceableLitresTransferred;
            float mashCapacity = (float)(JuiceableLitresLeft + litresTransferred);

            if (JuiceableLitresLeft / LITRES_CAPACITY < 1 - screwPercent) return; // Screw is not far down enough to transfer liquid.

            float litresPerSecond = mashCapacity / TIME_TO_SQUEEZE;

            TransferLiquid(litresPerSecond * dt);

            if (!soundPlayed)
            {
                Api.World.PlaySoundAt(new AssetLocation("sounds/player/wetclothsqueeze.ogg"), Pos, 0, null, false);
                soundPlayed = true;
            }
        }
    }

    /// <summary>
    /// Try to transfer from the mash to the bucket, returns amount transferred.
    /// If bucket is full will not transfer anything.
    /// </summary>
    public float TransferLiquid(float litres)
    {
        JuiceableProperties? juiceProps = GetJuiceableProps(MashSlot.Itemstack);
        if (juiceProps == null) return 0;

        ItemStack liquidStack = juiceProps.LiquidStack.ResolvedItemstack;
        liquidStack.StackSize = int.MaxValue;

        float litresTransferred = Math.Min(litres, (float)JuiceableLitresLeft);

        if (BucketSlot.Itemstack?.Collectible is BlockLiquidContainerBase bucket && !bucket.IsFull(BucketSlot.Itemstack))
        {
            float beforeLitres = bucket.GetCurrentLitres(BucketSlot.Itemstack);

            if (litres > 0)
            {
                bucket.TryPutLiquid(BucketSlot.Itemstack, liquidStack, litresTransferred);
            }

            float afterLitres = bucket.GetCurrentLitres(BucketSlot.Itemstack);
            litresTransferred = afterLitres - beforeLitres;
        }

        JuiceableLitresLeft = Math.Max(JuiceableLitresLeft - litresTransferred, 0);
        JuiceableLitresTransferred = Math.Min(JuiceableLitresTransferred + litresTransferred, LITRES_CAPACITY);

        MarkDirty(true);

        return litresTransferred;
    }

    /// <summary>
    /// Started when: Loaded and screw is down or when the server sends the first animation packet.
    /// Stopped when: Server sends unscrew packet.
    /// Spawns particles every 25ms.
    /// </summary>
    public void OnClientInteractTick(float dt)
    {
        // Check item stack's squeeze amount here.
        if (MashSlot.Empty || renderer.juiceTexPos == null || JuiceableLitresLeft < 0.01) return;

        double mashHeight = MashSlot.Itemstack?.Attributes.GetDouble("squeezeRel", 1) ?? 1;
        float selfHeight = (float)(JuiceableLitresTransferred + JuiceableLitresLeft) / 10f;

        RunningAnimation? anim = AnimUtil?.animator.GetAnimationState("compress");
        if (anim == null) return;
        float screwLevel = anim.CurrentFrame / (anim.Animation.QuantityFrames - 1);

        // Not enough to crush.
        if (JuiceableLitresLeft / LITRES_CAPACITY < 1 - screwLevel) return;

        liquidParticles.MinQuantity = (float)JuiceableLitresLeft / 10f;

        for (int i = 0; i < 4; i++)
        {
            BlockFacing face = BlockFacing.HORIZONTALS[i];

            liquidParticles.Color = Capi.BlockTextureAtlas.GetRandomColor(renderer.juiceTexPos, Api.World.Rand.Next(TextureAtlasPosition.RndColorsLength));

            Vec3d minPos = face.Plane.Startd.Add(-0.5, 0, -0.5);
            Vec3d maxPos = face.Plane.Endd.Add(-0.5, 0, -0.5);

            minPos.Mul(8 / 16f);
            maxPos.Mul(8 / 16f);
            maxPos.Y = (5 / 16f) - ((1 - mashHeight + Math.Max(0, 0.9f - selfHeight)) * 0.5f);

            minPos.Add(face.Normalf.X * 1.2f / 16f, 0, face.Normalf.Z * 1.2f / 16f);
            maxPos.Add(face.Normalf.X * 1.2f / 16f, 0, face.Normalf.Z * 1.2f / 16f);

            liquidParticles.MinPos = minPos;
            liquidParticles.AddPos = maxPos.Sub(minPos);
            liquidParticles.MinPos.Add(Pos).Add(0.5, 1, 0.5);

            Api.World.SpawnParticles(liquidParticles);
        }

        if (mashHeight < 0.9f)
        {
            liquidParticles.MinPos = Pos.ToVec3d().Add(6 / 16f, 0.7f, 6 / 16f);
            liquidParticles.AddPos.Set(4 / 16f, 0f, 4 / 16f);

            for (int i = 0; i < 3; i++)
            {
                liquidParticles.Color = Capi.BlockTextureAtlas.GetRandomColor(renderer.juiceTexPos, Api.World.Rand.Next(TextureAtlasPosition.RndColorsLength));
                Api.World.SpawnParticles(liquidParticles);
            }
        }
    }

    #endregion

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

        Shape shape = Shape.TryGet(api, "shapes/block/wood/fruitpress/part-movable.json");

        if (api.Side == EnumAppSide.Client)
        {
            Capi.Tesselator.TesselateShape(Block, shape, out meshMovable, new Vec3f(0, Block.Shape.rotateY, 0));
            AnimUtil.InitializeAnimator("fruitpress", shape, null, new Vec3f(0, Block.Shape.rotateY, 0));

            renderer = new FruitPressContentsRenderer(Pos, this);
            Capi.Event.RegisterRenderer(renderer, EnumRenderStage.Opaque, "fruitpress");

            if (screwPercent > 0)
            {
                // Animation sync.
                Capi.Network.SendBlockEntityPacket(Pos, 69);
            }

            renderer.ReloadMeshes();
            GenBucketMesh();
        }
    }

    /// <summary>
    /// Gets juiceable properties of the stack or null if it doesn't exit.
    /// </summary>
    public JuiceableProperties? GetJuiceableProps(ItemStack? stack)
    {
        if (stack == null) return null; // No stack.
        JuiceableProperties? props = stack.ItemAttributes["juiceableProperties"].Exists == true ? stack.ItemAttributes["juiceableProperties"].AsObject<JuiceableProperties>(null!, stack.Collectible.Code.Domain) : null;
        props?.LiquidStack?.Resolve(Api.World, "juiceable properties liquidstack", stack.Collectible.Code);
        props?.PressedStack?.Resolve(Api.World, "juiceable properties pressedstack", stack.Collectible.Code);
        return props;
    }

    public void SendUpdateToClients(FruitPressPacket packet)
    {
        ICoreServerAPI sapi = (ICoreServerAPI)Api;

        byte[]? data = SerializerUtil.Serialize(screwPercent);

        sapi.Network.BroadcastBlockEntityPacket(Pos, (int)packet, data);
    }

    public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetId, byte[] data)
    {
        base.OnReceivedClientPacket(fromPlayer, packetId, data);

        if (packetId == 69)
        {
            byte[]? screwData = SerializerUtil.Serialize(screwPercent);
            ((ICoreServerAPI)Api).Network.SendBlockEntityPacket((IServerPlayer)fromPlayer, Pos, (int)FruitPressPacket.SyncAnimation, screwData);
        }
    }

    public override void OnReceivedServerPacket(int packetId, byte[]? data)
    {
        base.OnReceivedServerPacket(packetId, data);

        if (data == null) return;

        FruitPressPacket packetEnum = (FruitPressPacket)packetId;
        screwPercent = SerializerUtil.Deserialize<float>(data);

        // Make sure animation is active and get it.
        RunningAnimation anim = AnimUtil.animator.GetAnimationState("compress");

        // Get what the current frame should be from the server.
        float currentFrame = screwPercent * (anim.Animation.QuantityFrames - 1);
        if (packetEnum == FruitPressPacket.ScrewStart)
        {
            anim.CurrentFrame = (int)currentFrame;
            AnimUtil.StartAnimation(compressAnimMeta);
        }

        // Sync animation once when client loads block.
        if (packetEnum == FruitPressPacket.SyncAnimation)
        {
            if (currentFrame > 0)
            {
                AnimUtil.StartAnimation(compressAnimMeta);
                compressAnimMeta.AnimationSpeed = 0.0001f;
                AnimUtil.OnRenderFrame(0.1f, EnumRenderStage.Before);
            }

            if (anim.CurrentFrame > 0 && anim.CurrentFrame < currentFrame)
            {
                while (anim.CurrentFrame < currentFrame && anim.CurrentFrame < anim.Animation.QuantityFrames - 1) anim.Progress(1f, 1f);
                compressAnimMeta.AnimationSpeed = 0f;
                anim.CurrentFrame = currentFrame;
            }

            return;
        }

        compressAnimMeta.AnimationSpeed = packetEnum switch
        {
            FruitPressPacket.ScrewStart => 0.5f,
            FruitPressPacket.ScrewStop => 0f,
            FruitPressPacket.Unscrew => 1.5f,
            _ => 0
        };

        if (packetEnum == FruitPressPacket.ScrewStart && eventListenerId == -1)
        {
            eventListenerId = RegisterGameTickListener(OnClientInteractTick, 25);
        }

        if (packetEnum == FruitPressPacket.Unscrew)
        {
            AnimUtil.StopAnimation("compress");
            // Reset percent for interactions.
            screwPercent = 0;

            if (eventListenerId != -1)
            {
                UnregisterGameTickListener(eventListenerId);
                eventListenerId = -1;
            }
        }
    }

    private void TryConvertMash()
    {
        if (MashSlot.Itemstack == null) return;

        JuiceableProperties? props = GetJuiceableProps(MashSlot.Itemstack);

        if (JuiceableLitresLeft == 0 && props != null)
        {
            ItemStack mashStack = MashSlot.Itemstack;
            double volume = JuiceableLitresLeft + JuiceableLitresTransferred;
            int stackSize = (int)(volume * props.PressedDryRatio);

            mashStack.StackSize = stackSize;

            mashStack.Attributes?.RemoveAttribute("juiceableLitresTransfered");
            mashStack.Attributes?.RemoveAttribute("juiceableLitresLeft");
            mashStack.Attributes?.RemoveAttribute("squeezeRel");
        }
    }

    public override void OnBlockBroken(IPlayer? byPlayer = null)
    {
        if (!MashSlot.Empty) TryConvertMash();
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
        base.FromTreeAttributes(tree, worldForResolving);

        screwPercent = tree.GetFloat("screwPercent");

        if (worldForResolving.Side == EnumAppSide.Client)
        {
            renderer?.ReloadMeshes();
            GenBucketMesh();
        }
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);

        tree.SetFloat("screwPercent", screwPercent);
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        bool skip = base.OnTesselation(mesher, tessThreadTesselator);
        if (!skip) mesher.AddMeshData(meshMovable);
        mesher.AddMeshData(bucketMesh);
        return false;
    }

    private void GenBucketMesh()
    {
        if (BucketSlot.Empty || Api == null)
        {
            bucketMesh = null;
            return;
        }

        ItemStack stack = BucketSlot.Itemstack;
        stack.ResolveBlockOrItem(Api.World);

        if (stack.Collectible == null) return; // Unable to resolve.

        IContainedMeshSource? meshSource = stack.Collectible.GetCollectibleInterface<IContainedMeshSource>();

        if (meshSource != null)
        {
            // Create a mesh of the item holding the stack holding liquid.
            bucketMesh = meshSource.GenMesh(stack, Capi.BlockTextureAtlas, Pos);

            // Liquid mesh part.
            bucketMesh.CustomInts = new CustomMeshDataPartInt(bucketMesh.FlagsCount)
            {
                Count = bucketMesh.FlagsCount
            };
            bucketMesh.CustomInts.Values.Fill(0x4000000); // Light foam.

            bucketMesh.CustomFloats = new CustomMeshDataPartFloat(bucketMesh.FlagsCount * 2)
            {
                Count = bucketMesh.FlagsCount * 2
            };

            bucketMesh = bucketMesh.Clone();
        }
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);

        if (!BucketSlot.Empty)
        {
            if (BucketSlot.Itemstack.Collectible is BlockLiquidContainerBase block)
            {
                dsc.Append($"{Lang.Get("Container:")} ");
                block.GetContentInfo(BucketSlot, dsc, Api.World);
                dsc.AppendLine();
            }
        }

        ItemStack? mashStack = MashSlot.Itemstack;

        if (mashStack != null)
        {
            int stackSize = MashSlot.StackSize;

            if (JuiceableLitresLeft > 0 && mashStack.Collectible.Code.Path != "rot")
            {
                string juiceName = GetJuiceableProps(mashStack)?.LiquidStack.ResolvedItemstack.GetName().ToLowerInvariant() ?? "None";
                dsc.AppendLine(Lang.GetWithFallback("fruitpress-litreswhensqueezed", "Mash produces {0:0.##} litres of juice when squeezed", JuiceableLitresLeft, juiceName));
            }
            else
            {
                JuiceableProperties? props = GetJuiceableProps(mashStack);

                if (props != null)
                {
                    double volume = JuiceableLitresLeft + JuiceableLitresTransferred;
                    stackSize *= (int)(volume * props.PressedDryRatio);
                }

                dsc.AppendLine(Lang.Get("{0}x {1}", stackSize, MashSlot.GetStackName().ToLowerInvariant()));
            }
        }
    }

    /// <summary>
    /// Update the squeeze amount server-side when ticking on the server.
    /// Updated every frame in the renderer.
    /// </summary>
    public void UpdateSqueezeRelOnServer()
    {
        ItemStack? mashStack = MashSlot.Itemstack;

        if (mashStack == null) return;

        // Up to 0.5f squeeze rel.
        double squeezeRel = Math.Clamp(1f - (screwPercent / 2f), 0.1f, 1f);
        float selfHeight = (float)(JuiceableLitresTransferred + JuiceableLitresLeft) / 10f;

        squeezeRel += Math.Max(0, 0.9f - selfHeight);
        squeezeRel = Math.Clamp(Math.Min(mashStack.Attributes.GetDouble("squeezeRel", 1), squeezeRel), 0.1f, 1f);

        mashStack.Attributes.SetDouble("squeezeRel", squeezeRel);
    }
}