using System;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public enum EnumCurdsBundleState
    {
        Bundled = 0,
        BundledStick = 1,
        Opened = 2,
        OpenedSalted = 3
    }

    public class BECheeseCurdsBundle : BlockEntityContainer
    {
        static SimpleParticleProperties props;

        static BECheeseCurdsBundle()
        {
            props = new SimpleParticleProperties(
                0.3f,
                1.3f,
                ColorUtil.ColorFromRgba(248, 243, 227, 255),
                new Vec3d(), new Vec3d(),
                new Vec3f(), new Vec3f(),
                2f,
                1f,
                0.05f,
                0.2f,
                EnumParticleModel.Quad
            );
        }

        InventoryGeneric inv;
        public override InventoryBase Inventory => inv;

        bool squeezed;
        EnumCurdsBundleState state;

        public override string InventoryClassName => "curdsbundle";

        public bool Squuezed => squeezed;
        public EnumCurdsBundleState State
        {
            get
            {
                return state;
            }
            set
            {
                state = value;
                MarkDirty(true);
            }
        }

        
        public BECheeseCurdsBundle()
        {
            inv = new InventoryGeneric(1, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            inv.LateInitialize("curdsbundle-" + Pos, api);

            if (api.Side == EnumAppSide.Client)
            {
                animUtil?.InitializeAnimator("curdbundle", null, (Block as BlockCheeseCurdsBundle).GetShape(EnumCurdsBundleState.BundledStick));
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            if (byItemStack != null)
            {
                inv[0].Itemstack = (byItemStack.Block as BlockCheeseCurdsBundle)?.GetContents(byItemStack);
            }
        }

        BlockEntityAnimationUtil animUtil
        {
            get { return GetBehavior<BEBehaviorAnimatable>()?.animUtil; }
        }

        long listenerId;

        internal void StartSqueeze(IPlayer byPlayer)
        {
            if (state != EnumCurdsBundleState.BundledStick || listenerId != 0) return;

            if (Api.Side == EnumAppSide.Client)
            {
                animUtil.StartAnimation(new AnimationMetaData()
                {
                    Animation = "twist",
                    Code = "twist",
                    AnimationSpeed = 0.25f,
                    EaseOutSpeed = 3,
                    EaseInSpeed = 3
                });
            }

            Api.World.PlaySoundAt(new AssetLocation("sounds/player/wetclothsqueeze.ogg"), Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, byPlayer, false);

            listenerId = Api.World.RegisterGameTickListener(onSqueezing, 20);
            secondsPassed = 0;
        }

        float secondsPassed;
        private void onSqueezing(float dt)
        {
            secondsPassed += dt;

            if (Api.Side == EnumAppSide.Client)
            {
                RunningAnimation anim = animUtil.animator.RunningAnimations[0];
            }

            if (secondsPassed > 5)
            {
                animUtil?.StopAnimation("twist");
                squeezed = true;
                Api.World.UnregisterGameTickListener(listenerId);
            }

            // 4/16f
            if (Api.Side == EnumAppSide.Server)
            {
                Vec3d pos = RandomParticlePos(BlockFacing.HORIZONTALS[Api.World.Rand.Next(4)]);

                props.MinPos.Set(pos.X, Pos.Y + 0.5f / 16f, pos.Z);
                props.AddPos.Set(0f, 6 / 16f, 0f);
                
                Api.World.SpawnParticles(props);
            }
        }


        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            Api.World.UnregisterGameTickListener(listenerId);
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);

            ItemStack stack = new ItemStack(Api.World.GetBlock(new AssetLocation("linen-normal-down")));
            Api.World.SpawnItemEntity(stack, Pos.ToVec3d().AddCopy(0.5, 0.25, 0.5));
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            squeezed = tree.GetBool("squeezezd");
            state = (EnumCurdsBundleState)tree.GetInt("state");

        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("squeezezd", squeezed);
            tree.SetInt("state", (int)state);
        }



        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            bool skipMesh = base.OnTesselation(mesher, tessThreadTesselator);

            if (!skipMesh)
            {
                mesher.AddMeshData((Block as BlockCheeseCurdsBundle).GetMesh(state));
            }

            return true;
        }



        public Vec3d RandomParticlePos(BlockFacing facing = null)
        {
            Random rand = Api.World.Rand;
            IBlockAccessor blockAccess = Api.World.BlockAccessor;
            Cuboidf box = Block.GetParticleBreakBox(blockAccess, Pos, facing);

            if (facing == null)
            {
                return new Vec3d(
                    Pos.X + box.X1 + 1 / 32f + rand.NextDouble() * (box.XSize - 1 / 16f),
                    Pos.Y + box.Y1 + 1 / 32f + rand.NextDouble() * (box.YSize - 1 / 16f),
                    Pos.Z + box.Z1 + 1 / 32f + rand.NextDouble() * (box.ZSize - 1 / 16f)
                );
            }
            else
            {
                bool haveBox = box != null;
                Vec3i facev = facing.Normali;

                Vec3d outpos = new Vec3d(
                    Pos.X + 0.5f + facev.X / 1.9f + (haveBox && facing.Axis == EnumAxis.X ? (facev.X > 0 ? box.X2 - 1 : box.X1) : 0),
                    Pos.Y + 0.5f + facev.Y / 1.9f + (haveBox && facing.Axis == EnumAxis.Y ? (facev.Y > 0 ? box.Y2 - 1 : box.Y1) : 0),
                    Pos.Z + 0.5f + facev.Z / 1.9f + (haveBox && facing.Axis == EnumAxis.Z ? (facev.Z > 0 ? box.Z2 - 1 : box.Z1) : 0)
                );

                outpos.Add(
                    (rand.NextDouble() - 0.5) * (1 - Math.Abs(facev.X)),
                    (rand.NextDouble() - 0.5) * (1 - Math.Abs(facev.Y)) - (facing == BlockFacing.DOWN ? 0.1f : 0f),
                    (rand.NextDouble() - 0.5) * (1 - Math.Abs(facev.Z))
                );

                return outpos;
            }
        }
    }
}
