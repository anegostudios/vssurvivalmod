using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;


namespace Vintagestory.GameContent
{
    public class BlockFruitTreePart : Block
    {
        SimpleParticleProperties foliageParticles;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            foliageParticles = new SimpleParticleProperties()
            {
                MinQuantity = 1,
                AddQuantity = 0,
                MinPos = new Vec3d(),
                AddPos = new Vec3d(1, 1, 1),
                LifeLength = 2,
                GravityEffect = 0.005f,
                MinSize = 0.1f,
                MaxSize = 0.2f,
                ParticleModel = EnumParticleModel.Quad,
                WindAffectednes = 2,
                ShouldSwimOnLiquid = true
            };
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var bebranch = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityFruitTreeBranch;
            if (bebranch?.RootOff?.Equals(Vec3i.Zero)==true && byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative)
            {
                if (world.Side == EnumAppSide.Server)
                {
                    bebranch?.InteractDebug();
                }
                return true;
            }

            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityFruitTreePart;
            if (be != null) return be.OnBlockInteractStart(byPlayer, blockSel);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityFruitTreePart;
            if (be != null) return be.OnBlockInteractStep(secondsUsed, byPlayer, blockSel);

            return base.OnBlockInteractStep(secondsUsed, world, byPlayer, blockSel);
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityFruitTreePart;
            if (be != null)
            {
                be.OnBlockInteractStop(secondsUsed, byPlayer, blockSel);
                return;
            }

            base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
        }


        public override void OnAsyncClientParticleTick(IAsyncParticleManager manager, BlockPos pos, float windAffectednessAtPos, float secondsTicking)
        {
            if (api.World.Rand.NextDouble() < 0.98 - GlobalConstants.CurrentWindSpeedClient.X / 10f) return;

            var be = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityFruitTreePart;

            if (be?.LeafParticlesColor == null) return;

            if (be.FruitTreeState == EnumFruitTreeState.EnterDormancy)
            {
                foliageParticles.Color = be.LeafParticlesColor[api.World.Rand.Next(25)];
                foliageParticles.Color = (api as ICoreClientAPI).World.ApplyColorMapOnRgba("climatePlantTint", SeasonColorMap, foliageParticles.Color, pos.X, pos.Y, pos.Z);

                foliageParticles.GravityEffect = 0.02f + 0.005f * GlobalConstants.CurrentWindSpeedClient.X;
                foliageParticles.MinSize = 0.4f;
            } else
            {
                foliageParticles.Color = be.BlossomParticlesColor[api.World.Rand.Next(25)];
                foliageParticles.MinSize = 0.1f;
                foliageParticles.GravityEffect = 0.005f + 0.005f * GlobalConstants.CurrentWindSpeedClient.X;
            }

            foliageParticles.LifeLength = 7f - GlobalConstants.CurrentWindSpeedClient.X * 3f;
            foliageParticles.WindAffectednes = 1f;
            foliageParticles.MinPos.Set(pos);
            manager.Spawn(foliageParticles);
        }

        public override bool ShouldReceiveClientParticleTicks(IWorldAccessor world, IPlayer player, BlockPos pos, out bool isWindAffected)
        {
            var be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityFruitTreePart;
            isWindAffected = true;
            var typeProps = be?.blockBranch?.TypeProps;
            if (be?.TreeType != null && typeProps?.ContainsKey(be.TreeType) == true)
            {
                return
                    be.fruitingSide > 0 && be.LeafParticlesColor != null && be.FruitTreeState == EnumFruitTreeState.EnterDormancy ||
                    (be.FruitTreeState == EnumFruitTreeState.Flowering && be.Progress > 0.5 && typeProps[be.TreeType].BlossomParticles)
                ;
            }

            return false;
        }

    }

}
