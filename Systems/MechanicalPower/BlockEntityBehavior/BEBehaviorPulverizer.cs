using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBehaviorMPPulverizer : BEBehaviorMPBase
    {
        static SimpleParticleProperties bitsParticles;
        static SimpleParticleProperties dustParticles;
        SimpleParticleProperties slideDustParticles;


        AssetLocation hitSound = new AssetLocation("sounds/effect/crusher-impact");
        AssetLocation crushSound = new AssetLocation("sounds/effect/stonecrush");

        public float prevProgressLeft;
        public float prevProgressRight;
        public int leftDir;
        public int rightDir;

        Vec4f leftOffset;
        Vec4f rightOffset;
        public BEPulverizer bepu;

        Vec3d leftSlidePos;
        Vec3d rightSlidePos;
        static BEBehaviorMPPulverizer()
        {
            float dustMinQ = 1;
            float dustAddQ = 5;
            float flourPartMinQ = 1;
            float flourPartAddQ = 20;

            // 1..20 per tick
            bitsParticles = new SimpleParticleProperties(1, 3, ColorUtil.ToRgba(40, 220, 220, 220), new Vec3d(), new Vec3d(), new Vec3f(-0.25f, -0.25f, -0.25f), new Vec3f(0.25f, 0.25f, 0.25f), 1, 1, 0.1f, 0.4f, EnumParticleModel.Quad);
            bitsParticles.AddPos.Set(2 / 32f, 1 / 32f, 2 / 32f);
            bitsParticles.AddQuantity = 20;
            bitsParticles.MinVelocity.Set(-1f, 0, -1f);
            bitsParticles.AddVelocity.Set(2f, 2, 2f);
            bitsParticles.WithTerrainCollision = false;
            bitsParticles.ParticleModel = EnumParticleModel.Cube;
            bitsParticles.LifeLength = 1.5f;
            bitsParticles.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -0.4f);
            bitsParticles.AddQuantity = flourPartAddQ;
            bitsParticles.MinQuantity = flourPartMinQ;

            // 1..5 per tick
            dustParticles = new SimpleParticleProperties(1, 3, ColorUtil.ToRgba(40, 220, 220, 220), new Vec3d(), new Vec3d(), new Vec3f(-0.25f, -0.25f, -0.25f), new Vec3f(0.25f, 0.25f, 0.25f), 1, 1, 0.1f, 0.3f, EnumParticleModel.Quad);
            dustParticles.AddPos.Set(2 / 32f, 1 / 32f, 2 / 32f);
            dustParticles.AddQuantity = 5;
            dustParticles.MinVelocity.Set(-0.1f, 0, -0.1f);
            dustParticles.AddVelocity.Set(0.2f, 0.1f, 0.2f);
            dustParticles.WithTerrainCollision = false;
            dustParticles.ParticleModel = EnumParticleModel.Quad;
            dustParticles.LifeLength = 1.5f;
            dustParticles.SelfPropelled = true;
            dustParticles.GravityEffect = 0;
            dustParticles.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, 0.4f);
            dustParticles.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -16f);
            dustParticles.MinQuantity = dustMinQ;
            dustParticles.AddQuantity = dustAddQ;
        }


        public BEBehaviorMPPulverizer(BlockEntity blockentity) : base(blockentity)
        {
        }


        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            bepu = Blockentity as BEPulverizer;
            

            Matrixf mat = bepu.mat;
            
            leftOffset = mat.TransformVector(new Vec4f(4.5f / 16f - 0.5f, 4 / 16f, -4.5f / 16f, 0f));
            rightOffset = mat.TransformVector(new Vec4f(11.5f / 16f - 0.5f, 4 / 16f, -4.5f / 16f, 0f));


            slideDustParticles = new SimpleParticleProperties(1, 3, ColorUtil.ToRgba(40, 220, 220, 220), new Vec3d(), new Vec3d(), new Vec3f(-0.25f, -0.25f, -0.25f), new Vec3f(0.25f, 0.25f, 0.25f), 1, 1, 0.1f, 0.2f, EnumParticleModel.Quad);
            slideDustParticles.AddPos.Set(2 / 32f, 1 / 32f, 2 / 32f);
            slideDustParticles.WithTerrainCollision = false;
            slideDustParticles.ParticleModel = EnumParticleModel.Quad;
            slideDustParticles.LifeLength = 0.75f;
            slideDustParticles.SelfPropelled = true;
            slideDustParticles.GravityEffect = 0f;
            slideDustParticles.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, 0.4f);
            slideDustParticles.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -16f);
            slideDustParticles.MinQuantity = 1;
            slideDustParticles.AddQuantity = 3;

            Vec4f vec = mat.TransformVector(new Vec4f(-0.1f, -0.1f, 0.2f, 0f));
            slideDustParticles.MinVelocity.Set(vec.X, vec.Y, vec.Z);

            vec = mat.TransformVector(new Vec4f(0.2f, -0.05f, 0.2f, 0f));
            slideDustParticles.AddVelocity.Set(vec.X, vec.Y, vec.Z);

            leftSlidePos = mat.TransformVector(new Vec4f(4.5f / 16f - 0.5f, 4 / 16f, -2.5f / 16f, 0f)).XYZ.ToVec3d().Add(Position).Add(0.5, 0, 0.5);
            rightSlidePos = mat.TransformVector(new Vec4f(11.5f / 16f - 0.5f, 4 / 16f, -2.5f / 16f, 0f)).XYZ.ToVec3d().Add(Position).Add(0.5, 0, 0.5);
        }

        public override float GetResistance()
        {
            bepu = Blockentity as BEPulverizer;
            return bepu.hasAxle ? 0.085f : 0.005f;
        }

        public override void JoinNetwork(MechanicalNetwork network)
        {
            base.JoinNetwork(network);

            //Speed limit when joining to an existing network: this is to prevent crazy bursts of speed on first connection if the network was spinning fast (with low resistances)
            // (if the network has enough torque to drive faster than this - which is going to be uncommon - then the network speed can increase after this is joined to the network)
            float speed = network == null ? 0f : Math.Abs(network.Speed * this.GearedRatio) * 1.6f;
            if (speed > 1f)
            {
                network.Speed /= speed;
                network.clientSpeed /= speed;
            }
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            base.OnTesselation(mesher, tesselator);

            switch (BlockFacing.FromCode(Block.Variant["side"]).Index)
            {
                case 0:
                    AxisSign = new int[] { -1, 0, 0 };
                    break;
                case 2:
                    AxisSign = new int[] { 1, 0, 0 };
                    break;
                case 3:
                    break;
                default:
                    break;
            }
            return true;
        }

        Vec3d hitPos = new Vec3d();

        internal void OnClientSideImpact(bool right)
        {
            if (!bepu.IsComplete) return;

            Vec4f offset = right ? rightOffset : leftOffset;
            int slotid = right ? 0 : 1;

            hitPos.Set(Position.X + 0.5f + offset.X, Position.Y + offset.Y, Position.Z + 0.5f + offset.Z);

            Api.World.PlaySoundAt(hitSound, hitPos.X, hitPos.Y, hitPos.Z, null, true, 8);

            if (!bepu.Inventory[slotid].Empty)
            {
                ItemStack stack = bepu.Inventory[slotid].Itemstack;

                Api.World.PlaySoundAt(crushSound, hitPos.X, hitPos.Y, hitPos.Z, null, true, 8);

                dustParticles.Color = bitsParticles.Color = stack.Collectible.GetRandomColor(Api as ICoreClientAPI, stack);
                dustParticles.Color &= 0xffffff;
                dustParticles.Color |= (200 << 24);

                dustParticles.MinPos.Set(hitPos.X - 1 / 32f, hitPos.Y, hitPos.Z - 1 / 32f);
                bitsParticles.MinPos.Set(hitPos.X - 1 / 32f, hitPos.Y, hitPos.Z - 1 / 32f);

                slideDustParticles.MinPos.Set(right ? rightSlidePos : leftSlidePos);
                slideDustParticles.Color = dustParticles.Color;

                Api.World.SpawnParticles(bitsParticles);
                Api.World.SpawnParticles(dustParticles);
                Api.World.SpawnParticles(slideDustParticles);
            }
        }
    }
}
