using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockBunchOCandles : Block
    {
        static Random rand = new Random();

        int QuantityCandles;

        static Vec3f[] candleWickPositions = new Vec3f[]
        {
            new Vec3f(3 + 0.8f, 0 + 4, 3 + 0.8f),
            new Vec3f(7 + 0.8f, 0 + 7, 4 + 0.8f),
            new Vec3f(12 + 0.8f, 0 + 2, 1 + 0.8f),

            new Vec3f(4 + 0.8f, 0 + 5, 9 + 0.8f),
            new Vec3f(7 + 0.8f, 0 + 2, 8 + 0.8f),
            new Vec3f(12 + 0.8f, 0 + 6, 12 + 0.8f),
            new Vec3f(11 + 0.8f, 0 + 4, 6 + 0.8f),
            new Vec3f(1 + 0.8f, 0 + 1, 12 + 0.8f),
            new Vec3f(6 + 0.8f, 0 + 4, 13 + 0.8f)
        };

        static Vec3f[][] candleWickPositionsByRot = new Vec3f[4][];

        static BlockBunchOCandles()
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

            QuantityCandles = Variant["quantity"].ToInt();
        }

        public override void OnAsyncClientParticleTick(IAsyncParticleManager manager, BlockPos pos, float windAffectednessAtPos, float secondsTicking)
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


            if (ParticleProperties != null && ParticleProperties.Length > 0)
            {
                long rnd = 1 + GameMath.MurmurHash3Mod(pos.X, pos.Y, pos.Z, 3);
                Vec3f[] poses = candleWickPositionsByRot[rnd];

                for (int i = 0; i < ParticleProperties.Length; i++)
                {
                    AdvancedParticleProperties bps = ParticleProperties[i];
                    bps.WindAffectednesAtPos = windAffectednessAtPos;

                    

                    for (int j = 0; j < QuantityCandles; j++)
                    {
                        Vec3f dp = poses[j];

                        bps.basePos.X = pos.X + dp.X;
                        bps.basePos.Y = pos.Y + dp.Y;
                        bps.basePos.Z = pos.Z + dp.Z;
                        manager.Spawn(bps);
                    }
                }
            }
        }
    }
}
