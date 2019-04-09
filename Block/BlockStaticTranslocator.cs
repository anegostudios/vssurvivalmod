using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockStaticTranslocator : Block
    {
        public SimpleParticleProperties idleParticles;
        public SimpleParticleProperties insideParticles;

        public bool On => LastCodePart() == "on";


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            idleParticles = new SimpleParticleProperties(
                1, 1,
                ColorUtil.ToRgba(150, 34, 47, 44),
                new Vec3d(),
                new Vec3d(),
                new Vec3f(-0.1f, -0.1f, -0.1f),
                new Vec3f(0.1f, 0.1f, 0.1f),
                1.5f,
                0,
                0.5f,
                0.75f,
                EnumParticleModel.Cube
            );

            idleParticles.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -0.6f);
            idleParticles.addPos.Set(1, 2, 1);
            idleParticles.addLifeLength = 0.5f;


            insideParticles = new SimpleParticleProperties(
                1, 1,
                ColorUtil.ToRgba(150, 92, 111, 107),
                new Vec3d(),
                new Vec3d(),
                new Vec3f(-0.2f, -0.2f, -0.2f),
                new Vec3f(0.2f, 0.2f, 0.2f),
                1.5f,
                0,
                0.5f,
                0.75f,
                EnumParticleModel.Cube
            );

            insideParticles.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -0.6f);
            insideParticles.addPos.Set(1, 2, 1);
            insideParticles.addLifeLength = 0.5f;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (On)
            {

            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            base.OnEntityCollide(world, entity, pos, facing, collideSpeed, isImpact);

            BlockEntityTeleporter be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityTeleporter;
            if (be == null) return;
            be.OnEntityCollide(entity);
        }

    }
}