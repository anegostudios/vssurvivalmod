using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockEntityBomb : BlockEntity
    {
        float remainingSeconds = 0;
        bool lit;
        Block block;
        float blastRadius;
        float injureRadius;

        EnumBlastType blastType;

        ILoadedSound fuseSound;
        public static SimpleParticleProperties smallSparks;

        static BlockEntityBomb()
        {
            smallSparks = new SimpleParticleProperties(
                1, 1,
                ColorUtil.ColorFromArgb(255, 0, 233, 255),
                new Vec3d(), new Vec3d(),
                new Vec3f(-3f, 5f, -3f),
                new Vec3f(3f, 8f, 3f),
                0.03f,
                1f,
                0.25f, 0.25f,
                EnumParticleModel.Quad
            );
            smallSparks.glowLevel = 64;
            smallSparks.addPos.Set(1 / 16f, 0, 1 / 16f);
            smallSparks.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.05f);
        }


        public virtual float FuseTimeSeconds
        {
            get { return 4; }
        }


        public virtual EnumBlastType BlastType
        {
            get { return blastType; }
        }

        public virtual float BlastRadius
        {
            get { return blastRadius; }
        }

        public virtual float InjureRadius
        {
            get { return injureRadius; }
        }


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            RegisterGameTickListener(OnTick, 50);
            

            if (fuseSound == null && api.Side == EnumAppSide.Client)
            {
                fuseSound = ((IClientWorldAccessor)api.World).LoadSound(new SoundParams()
                {
                    Location = new AssetLocation("sounds/effect/fuse"),
                    ShouldLoop = true,
                    Position = pos.ToVec3f().Add(0.5f, 0.25f, 0.5f),
                    DisposeOnFinish = false,
                    Volume = 0.1f,
                    Range = 16,
                });
            }

            block = api.World.BlockAccessor.GetBlock(pos);
            blastRadius = block.Attributes["blastRadius"].AsInt(4);
            injureRadius = block.Attributes["injureRadius"].AsInt(8);
            blastType = (EnumBlastType)block.Attributes["blastType"].AsInt((int)EnumBlastType.OreBlast);
        }

        private void OnTick(float dt)
        {
            if (lit)
            {
                remainingSeconds -= dt;

                if (api.Side == EnumAppSide.Server && remainingSeconds <= 0)
                {
                    Combust(dt);
                }

                if (api.Side == EnumAppSide.Client)
                {
                    smallSparks.minPos.Set(pos.X + 0.45, pos.Y + 0.5, pos.Z + 0.45);
                    api.World.SpawnParticles(smallSparks);
                }
            }
        }

        void Combust(float dt)
        {
            api.World.BlockAccessor.SetBlock(0, pos);
            ((IServerWorldAccessor)api.World).CreateExplosion(pos, BlastType, BlastRadius, InjureRadius);
        }

        internal void OnBlockExploded(BlockPos pos)
        {
            if (api.Side == EnumAppSide.Server)
            {
                if (!lit || remainingSeconds > 0.3)
                {
                    api.World.RegisterCallback(Combust, 250);
                }
                
            }
        }

        public bool IsLit
        {
            get { return lit; }
        }

        internal void OnIgnite(IPlayer byPlayer)
        {
            if (lit) return;

            if (api.Side == EnumAppSide.Client) fuseSound.Start();
            lit = true;
            remainingSeconds = FuseTimeSeconds;
            MarkDirty();
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);
            remainingSeconds = tree.GetFloat("remainingSeconds", 0);
            lit = tree.GetInt("lit") > 0;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetFloat("remainingSeconds", remainingSeconds);
            tree.SetInt("lit", lit ? 1 : 0);
        }



        ~BlockEntityBomb()
        {
            if (fuseSound != null)
            {
                fuseSound.Dispose();
            }
        }


        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            if (fuseSound != null) fuseSound.Stop();
        }
    }
}
