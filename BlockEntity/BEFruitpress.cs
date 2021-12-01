using System;
using System.Collections.Generic;
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
    public class JuicableProperties
    {
        public int Quantity;
        public JsonItemStack LiquidStack;
        public JsonItemStack PressedStack;
    }

    public enum EnumFruitPressSection
    {
        Ground,
        MashContainer,
        Screw
    }

    public enum EnumFruitPressState
    {
        Prepare = 0,
        Compressing = 1,
        CompressedDraining = 2,
        CompressedDrained = 3,
        Uncompressing = 4
    }

    public class BlockEntityFruitPress : BlockEntityContainer, ITerrainMeshPool
    {
        #region particle
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
                GravityEffect = 1,
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


        ICoreClientAPI capi;
        BlockFruitPress ownBlock;

        MeshData meshMovable;
        MeshData bucketMesh;
        MeshData bucketMeshTmp;

        FruitpressContentsRenderer renderer;
        EnumFruitPressState state;
        float squeezeYScale = 1f;
        double compresssBeginTotalHours = 0f;


        AnimationMetaData compressAnimMeta = new AnimationMetaData()
        {
            Animation = "compress",
            Code = "compress",
            AnimationSpeed = 0.5f,
            EaseOutSpeed = 0.5f,
            EaseInSpeed = 3
        };


        BlockEntityAnimationUtil animUtil
        {
            get { return GetBehavior<BEBehaviorAnimatable>()?.animUtil; }
        }


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
                Shape shape = api.Assets.TryGet("shapes/block/wood/fruitpress/part-movable.json").ToObject<Shape>();

                if (api.Side == EnumAppSide.Client)
                {
                    capi.Tesselator.TesselateShape(ownBlock, shape, out meshMovable);
                    animUtil.InitializeAnimator("fruitpress", new Vec3f(0, ownBlock.Shape.rotateY, 0), shape);
                } else
                {
                    animUtil.InitializeAnimatorServer("fruitpress", shape);
                }

                if (api.Side == EnumAppSide.Client)
                {
                    renderer = new FruitpressContentsRenderer(api as ICoreClientAPI, Pos, this);
                    (api as ICoreClientAPI).Event.RegisterRenderer(renderer, EnumRenderStage.Opaque, "fruitpress");

                    renderer.reloadMeshes(getJuicableProps(inv[0].Itemstack), true);
                    genBucketMesh();
                }
            }
        }



        bool nowCompress;

        #region ITerrainMeshPool imp to get bucket mesh
        public void AddMeshData(MeshData data, int lodlevel = 1)
        {
            if (data == null) return;
            bucketMeshTmp.AddMeshData(data);
        }

        public void AddMeshData(MeshData data, ColorMapData colormapdata, int lodlevel = 1)
        {
            if (data == null) return;
            bucketMeshTmp.AddMeshData(data);
        }
        #endregion


        void StartOrContinuePressing()
        {
            setState(EnumFruitPressState.Compressing);
            compresssBeginTotalHours = Api.World.Calendar.TotalHours;
            prevLiquidStackSize = 0;
            if (!inv[1].Empty)
            {
                ItemStack bucketStack = inv[1].Itemstack;
                var containerBlock = bucketStack.Collectible as BlockLiquidContainerBase;
                ItemStack currentLiquidStack = containerBlock.GetContent(bucketStack);

                prevLiquidStackSize = currentLiquidStack?.StackSize ?? 0;
            }
            

            if (listenerId == 0) listenerId = RegisterGameTickListener(onTick50ms, Api.Side == EnumAppSide.Client ? 25 : 50);
            nowCompress = true;
        }


        long listenerId;
        float juiceAccumLitres;
        double totalFlowHours = 0.25f;
        int prevLiquidStackSize;

        private void onTick50ms(float dt)
        {
            var props = getJuicableProps(inv[0].Itemstack);
            double totalHours = Api.World.Calendar.TotalHours;

            double hoursPassed = totalHours - compresssBeginTotalHours;
            double juiceflowSpeed = (totalFlowHours - hoursPassed) * 1.5f;

            if (Api.Side == EnumAppSide.Server && state == EnumFruitPressState.CompressedDraining) {

                float berryFillLevelRel = (float)inv[0].StackSize / (9 * props.Quantity);
                
                juiceAccumLitres = (float)(berryFillLevelRel * hoursPassed / totalFlowHours * 10.5);
                juiceAccumLitres = GameMath.Clamp(juiceAccumLitres, 0, 10);

                ItemStack liquidStack = props.LiquidStack.ResolvedItemstack;
                var liquidProps = BlockLiquidContainerBase.GetInContainerProps(liquidStack);
                int juiceStackSize = (int)(liquidProps.ItemsPerLitre * juiceAccumLitres);

                if (!inv[1].Empty && juiceStackSize > 0)
                {
                    ItemStack bucketStack = inv[1].Itemstack;
                    var containerBlock = bucketStack.Collectible as BlockLiquidContainerBase;
                    ItemStack currentLiquidStack = containerBlock.GetContent(bucketStack);

                    if (currentLiquidStack == null) currentLiquidStack = liquidStack.Clone();

                    if (currentLiquidStack.Equals(Api.World, liquidStack, GlobalConstants.IgnoredStackAttributes))
                    {
                        currentLiquidStack.StackSize = prevLiquidStackSize + juiceStackSize;
                        containerBlock.SetContent(bucketStack, currentLiquidStack);
                        inv[1].MarkDirty();
                        MarkDirty(true);
                    }
                }
            }


            if (Api.Side == EnumAppSide.Client)
            {
                int[] cols = renderer.juiceTexPos.RndColors;
                var rand = Api.World.Rand;

                liquidParticles.MinQuantity = (float)juiceflowSpeed;

                for (int i = 0; i < 4; i++)
                {
                    BlockFacing face = BlockFacing.HORIZONTALS[i];

                    liquidParticles.Color = cols[rand.Next(cols.Length)];

                    Vec3d minPos = face.Plane.Startd.Add(-0.5, 0, -0.5);
                    Vec3d maxPos = face.Plane.Endd.Add(-0.5, 0, -0.5);

                    minPos.Mul(8 / 16f);
                    maxPos.Mul(8 / 16f);
                    maxPos.Y = 6/16f - (1 - squeezeYScale) * 0.25f;

                    minPos.Add(face.Normalf.X * 1.2f / 16f, 0, face.Normalf.Z * 1.2f / 16f);
                    maxPos.Add(face.Normalf.X * 1.2f / 16f, 0, face.Normalf.Z * 1.2f / 16f);

                    liquidParticles.MinPos = minPos;
                    liquidParticles.AddPos = maxPos.Sub(minPos);
                    liquidParticles.MinPos.Add(Pos).Add(0.5, 1, 0.5);

                    Api.World.SpawnParticles(liquidParticles);
                }

                if (squeezeYScale < 0.9f)
                {
                    liquidParticles.MinPos = Pos.ToVec3d().Add(6 / 16f, 0.7f, 6 / 16f);
                    liquidParticles.AddPos.Set(4 / 16f, 0f, 4 / 16f);
                    for (int i = 0; i < 3; i++)
                    {
                        liquidParticles.Color = cols[rand.Next(cols.Length)];
                        Api.World.SpawnParticles(liquidParticles);
                    }
                }
            }

            if (!animUtil.activeAnimationsByAnimCode.ContainsKey("compress") || Api.World.Calendar.TotalHours > compresssBeginTotalHours + totalFlowHours)
            {
                UnregisterGameTickListener(listenerId);
                listenerId = 0;
                if (Api.World.Calendar.TotalHours > compresssBeginTotalHours + totalFlowHours)
                {
                    setState(EnumFruitPressState.CompressedDrained);
                }
            }

            squeezeYScale = Math.Min(squeezeYScale, GetSqueezeYScale());
        }

        void setState(EnumFruitPressState state)
        {
            this.state = state;
            if (Api.Side == EnumAppSide.Server) MarkDirty(false);
        }

        public float GetSqueezeYScale()
        {
            RunningAnimation anim = animUtil.animator.GetAnimationState("compress");
            if (anim == null || !anim.Active) return squeezeYScale;

            float ys = GameMath.Clamp(1f - (float)anim.CurrentFrame / anim.Animation.QuantityFrames / 2f, 0.1f, 1f);

            return ys;
        }


        public JuicableProperties getJuicableProps(ItemStack stack)
        {
            var props = stack?.ItemAttributes?["juicableProperties"].Exists == true ? stack.ItemAttributes["juicableProperties"].AsObject<JuicableProperties>(null, stack.Collectible.Code.Domain) : null;
            props?.LiquidStack?.Resolve(Api.World, "juicable properties liquidstack");
            props?.PressedStack?.Resolve(Api.World, "juicable properties pressedstack");

            return props;
        }



        // What needs to happen while squeezing:
        // 0..3 sec: Linearly squeeze mash mesh, stay squeezed
        // 0..3 sec: Spawn juice particles all around
        // 1..6 sec: Grow juice quad, spawn particles at the spout
        // 6..12 sec: Shrink juice quad
        // 1..12 sec: Add liquid to bucket
        public bool OnBlockInteractStart(IPlayer byPlayer, BlockSelection blockSel, EnumFruitPressSection section)
        {
            ItemSlot handslot = byPlayer.InventoryManager.ActiveHotbarSlot;
            ItemStack handStack = handslot.Itemstack;
            ItemSlot contentSlot = inv[0];

            if (!animUtil.activeAnimationsByAnimCode.ContainsKey("compress") && (state == EnumFruitPressState.Uncompressing || animUtil.activeAnimationsByAnimCode.Count == 0))
            {
                setState(EnumFruitPressState.Prepare);
            }

            if (section == EnumFruitPressSection.MashContainer)
            {
                if (state != EnumFruitPressState.Prepare) return false;

                if (!handslot.Empty)
                {
                    var props = getJuicableProps(handStack);
                    if (props == null) return false;

                    int leftToPut = props == null ? 0 : props.Quantity * 9 - contentSlot.StackSize;

                    ItemStackMoveOperation op = new ItemStackMoveOperation(Api.World, EnumMouseButton.Left, 0, EnumMergePriority.DirectMerge, Math.Min(props.Quantity, leftToPut));

                    if (props != null && handStack.StackSize >= props.Quantity && handslot.TryPutInto(contentSlot, ref op) > 0 && leftToPut > 0)
                    {
                        handslot.MarkDirty();
                        MarkDirty(true);
                        renderer?.reloadMeshes(props, true);
                    }
                }
                else
                {
                    var props = getJuicableProps(inv[0].Itemstack);
                    ItemStack stack;

                    
                    if (squeezeYScale > 0.9f || props == null)
                    {
                        stack = contentSlot.TakeOut(props?.Quantity ?? 4);
                    }
                    else
                    {
                        stack = props.PressedStack.ResolvedItemstack.Clone();
                        stack.StackSize = contentSlot.StackSize / props.Quantity;
                        contentSlot.Itemstack = null;
                    }

                    if (!byPlayer.InventoryManager.TryGiveItemstack(stack, true))
                    {
                        Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                    }

                    renderer?.reloadMeshes(props, true);

                    if (Api.Side == EnumAppSide.Server)
                    {
                        if (inv[0].Empty) squeezeYScale = 1;
                        MarkDirty(true);
                    }
                }

                return true;
            }


            if (section == EnumFruitPressSection.Ground)
            {
                if (handslot.Empty && !inv[1].Empty)
                {
                    if (!byPlayer.InventoryManager.TryGiveItemstack(inv[1].Itemstack, true))
                    {
                        Api.World.SpawnItemEntity(inv[1].Itemstack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                    }
                    inv[1].Itemstack = null;
                    MarkDirty(true);
                    bucketMesh?.Clear();
                }

                else if (handStack != null && handStack.Collectible is BlockLiquidContainerBase blockLiqCont && blockLiqCont.CapacityLitres > 5 && blockLiqCont.CapacityLitres < 20 && inv[1].Empty)
                {
                    bool moved = handslot.TryPutInto(Api.World, inv[1], 1) > 0;
                    if (moved)
                    {
                        handslot.MarkDirty();
                        MarkDirty(true);
                        genBucketMesh();
                    }
                }

                return true;
            }


            if (state == EnumFruitPressState.CompressedDraining && listenerId != 0) return false;

            if (animUtil.activeAnimationsByAnimCode.ContainsKey("compress"))
            {
                RunningAnimation anim = animUtil.animator.GetAnimationState("compress");

                if (anim.CurrentFrame >= anim.Animation.QuantityFrames - 1)
                {
                    animUtil.StopAnimation("compress");
                    setState(EnumFruitPressState.Uncompressing);
                    return true;
                }
                else
                {
                    compressAnimMeta.AnimationSpeed = 0.5f;
                }

                StartOrContinuePressing();
                return true;
            }


            StartOrContinuePressing();

            
            if (animUtil.activeAnimationsByAnimCode.ContainsKey("compress"))
            {
                animUtil.StopAnimation("compress");
                setState(EnumFruitPressState.Uncompressing);
            }
            else
            {
                compressAnimMeta.AnimationSpeed = 0.5f;
                animUtil.StartAnimation(compressAnimMeta);

                if (!contentSlot.Empty)
                {
                    Api.World.PlaySoundAt(new AssetLocation("sounds/player/wetclothsqueeze.ogg"), Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, byPlayer, false);
                }
            }


            return true;
        }

        private void genBucketMesh()
        {
            if (inv[1].Empty) return;

            // Haxy, but works ¯\_(ツ)_/¯
            if (inv[1].Itemstack.Block?.EntityClass != null && Api.Side == EnumAppSide.Client)
            {
                if (bucketMeshTmp == null)
                {
                    bucketMeshTmp = new MeshData(4, 3, false, true, true, true);

                    // Liquid mesh
                    bucketMeshTmp.CustomInts = new CustomMeshDataPartInt(bucketMeshTmp.FlagsCount);
                    bucketMeshTmp.CustomInts.Count = bucketMeshTmp.FlagsCount;
                    bucketMeshTmp.CustomInts.Values.Fill(0x4000000); // light foam only

                    bucketMeshTmp.CustomFloats = new CustomMeshDataPartFloat(bucketMeshTmp.FlagsCount * 2);
                    bucketMeshTmp.CustomFloats.Count = bucketMeshTmp.FlagsCount * 2;
                }
                bucketMeshTmp.Clear();
                var be = Api.ClassRegistry.CreateBlockEntity(inv[1].Itemstack.Block.EntityClass);
                be.Pos = new BlockPos(0, 0, 0);
                be.Block = inv[1].Itemstack.Block;
                be.Initialize(Api);
                be.OnBlockPlaced(inv[1].Itemstack);
                be.OnTesselation(this, capi.Tesselator);
                be.OnBlockRemoved();
                bucketMesh = bucketMeshTmp.Clone();
            }
        }

        public bool OnBlockInteractStep(float secondsUsed, IPlayer byPlayer, EnumFruitPressSection section)
        {
            if (section != EnumFruitPressSection.Screw) return false;
            compresssBeginTotalHours = Api.World.Calendar.TotalHours;

            return nowCompress && secondsUsed < 4;
        }


        public void OnBlockInteractStop(float secondsUsed, IPlayer byPlayer)
        {
            if (secondsUsed >= 3 && !inv[0].Empty)
            {
                var props = getJuicableProps(inv[0].Itemstack);
                if (props != null)
                {
                    setState(EnumFruitPressState.CompressedDraining);
                }

                if (animUtil.activeAnimationsByAnimCode.ContainsKey("compress"))
                {
                    compressAnimMeta.AnimationSpeed = 0f;
                    // Fast forward the server
                    RunningAnimation anim = animUtil.animator.GetAnimationState("compress");
                    anim.CurrentFrame = anim.Animation.QuantityFrames - 1;
                }
            }
        }


        public bool OnBlockInteractCancel(float secondsUsed, IPlayer byPlayer)
        {
            if (animUtil.activeAnimationsByAnimCode.ContainsKey("compress"))
            {
                compressAnimMeta.AnimationSpeed = 0f;
            }

            return true;
        }


        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            //base.OnBlockBroken(); - don't drop contents
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
            state = (EnumFruitPressState)tree.GetInt("state", 0);
            squeezeYScale = tree.GetFloat("squeezeYScale", 1);

            base.FromTreeAttributes(tree, worldForResolving);

            if (worldForResolving.Side == EnumAppSide.Client)
            {
                renderer?.reloadMeshes(getJuicableProps(inv[0].Itemstack), wasEmpty != Inventory.Empty);
                genBucketMesh();
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetInt("state", (int)state);
            tree.SetFloat("squeezeYScale", squeezeYScale);
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

            if (!inv[1].Empty)
            {
                BlockLiquidContainerBase block = inv[1].Itemstack.Collectible as BlockLiquidContainerBase;
                dsc.Append("Bucket: ");
                block.GetContentInfo(inv[1], dsc, Api.World);
            }
        }

    }
}
