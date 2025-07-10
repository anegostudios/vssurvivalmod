using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockBunchOCandles : Block
    {
        internal int QuantityCandles;

        internal Vec3f[] candleWickPositions = {
            new(3 + 0.8f, 0 + 4, 3 + 0.8f),
            new(7 + 0.8f, 0 + 7, 4 + 0.8f),
            new(12 + 0.8f, 0 + 2, 1 + 0.8f),

            new(4 + 0.8f, 0 + 5, 9 + 0.8f),
            new(7 + 0.8f, 0 + 2, 8 + 0.8f),
            new(12 + 0.8f, 0 + 6, 12 + 0.8f),
            new(11 + 0.8f, 0 + 4, 6 + 0.8f),
            new(1 + 0.8f, 0 + 1, 12 + 0.8f),
            new(6 + 0.8f, 0 + 4, 13 + 0.8f)
        };

        Vec3f[][] candleWickPositionsByRot = new Vec3f[4][];


        internal void initRotations()
        {
            for (int i = 0; i < 4; i++)
            {
                Matrixf m = new Matrixf();
                m.Translate(0.5f, 0.5f, 0.5f);
                m.RotateYDeg(i * 90);
                m.Translate(-0.5f, -0.5f, -0.5f);

                Vec3f[] poses = candleWickPositionsByRot[i] = new Vec3f[candleWickPositions.Length];
                for (int j = 0; j < poses.Length; j++)
                {
                    Vec4f rotated = m.TransformVector(new Vec4f(candleWickPositions[j].X / 16f, candleWickPositions[j].Y / 16f, candleWickPositions[j].Z / 16f, 1));
                    poses[j] = new Vec3f(rotated.X, rotated.Y, rotated.Z);
                }
            }
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            initRotations();
            QuantityCandles = Variant["quantity"].ToInt();
        }

        public override void OnAsyncClientParticleTick(IAsyncParticleManager manager, BlockPos pos, float windAffectednessAtPos, float secondsTicking)
        {
            if (ParticleProperties != null && ParticleProperties.Length > 0)
            {
                int rnd = GameMath.MurmurHash3Mod(pos.X, pos.Y, pos.Z, 4);
                Vec3f[] poses = candleWickPositionsByRot[rnd];

                for (int i = 0; i < ParticleProperties.Length; i++)
                {
                    AdvancedParticleProperties bps = ParticleProperties[i];
                    bps.WindAffectednesAtPos = windAffectednessAtPos;

                    

                    for (int j = 0; j < QuantityCandles; j++)
                    {
                        Vec3f dp = poses[j];

                        bps.basePos.X = pos.X + dp.X;
                        bps.basePos.Y = pos.InternalY + dp.Y;
                        bps.basePos.Z = pos.Z + dp.Z;
                        manager.Spawn(bps);
                    }
                }
            }
        }
    }
}
