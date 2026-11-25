#nullable disable

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockOilLamp : BlockTorch, IGroundStoredParticleEmitter
    {
        protected BlockFacing facing;

        public override void OnLoaded(ICoreAPI api)
        {
            facing = BlockFacing.FromCode(Variant["orientation"]);
            base.OnLoaded(api);
        }

        public override void OnAsyncClientParticleTick(IAsyncParticleManager manager, BlockPos pos, float windAffectednessAtPos, float secondsTicking)
        {
            if (ParticleProperties != null && ParticleProperties.Length > 0)
            {
                for (int i = 0; i < ParticleProperties.Length; i++)
                {
                    AdvancedParticleProperties bps = ParticleProperties[i];
                    bps.WindAffectednesAtPos = windAffectednessAtPos;

                    bps.basePos.X = pos.X + 0.5f;
                    bps.basePos.Y = pos.InternalY + 7.5/16f + facing.Normalf.Y * 0.25f;
                    bps.basePos.Z = pos.Z + 0.5f;
                    manager.Spawn(bps);
                }
            }
        }


        public void DoSpawnGSParticles(IAsyncParticleManager manager, BlockPos pos, Vec3f offset)
        {
            var meshAngle = api.World.BlockAccessor.GetBlockEntity<BlockEntityGroundStorage>(pos)?.MeshAngle ?? 0;

            if (ParticleProperties != null && ParticleProperties.Length > 0)
            {
                Vec3f wickoffset = new Matrixf().RotateY(meshAngle).TransformVector(new Vec4f(-2.5f/16f, 0, -2.5f/16f, 1)).XYZ;

                for (int i = 0; i < ParticleProperties.Length; i++)
                {
                    AdvancedParticleProperties bps = ParticleProperties[i];
                    bps.WindAffectednesAtPos = 0; // windAffectednessAtPos;

                    bps.basePos.X = pos.X + 0.5f + offset.X + wickoffset.X;
                    bps.basePos.Y = pos.InternalY + 2.25 / 16f + offset.Y;
                    bps.basePos.Z = pos.Z + 0.5f + offset.Z + wickoffset.Z;
                    manager.Spawn(bps);
                }
            }
        }

        public bool ShouldSpawnGSParticles(IWorldAccessor world, ItemStack stack)
        {
            return true;
        }
    }
}
