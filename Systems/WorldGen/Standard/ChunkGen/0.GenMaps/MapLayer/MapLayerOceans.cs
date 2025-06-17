using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.ServerMods
{
    class MapLayerOceans : MapLayerBase
    {
        NormalizedSimplexNoise noisegenX;
        NormalizedSimplexNoise noisegenY;
        float wobbleIntensity;
        NoiseOcean noiseOcean;

        public float landFormHorizontalScale = 1f;
        private List<XZ> requireLandAt;

        int spawnOffsX, spawnOffsZ;
        private float scale;
        /// <summary>
        /// This is related to ServerSystemSupplyChunks.LoadWorldgenHandlerAndSpawnChunks
        /// </summary>
        private readonly bool requiresSpawnOffset;

        public MapLayerOceans(long seed, float scale, float landCoverRate, List<XZ> requireLandAt, bool requiresSpawnOffset) : base(seed)
        {
            noiseOcean = new NoiseOcean(seed, scale, landCoverRate);
            this.requireLandAt = requireLandAt;
            this.scale = scale;
            int woctaves = 3;
            float wscale = scale;
            float wpersistence = 0.9f;
            wobbleIntensity = TerraGenConfig.oceanMapScale * scale / TerraGenConfig.oceanMapScale;
            noisegenX = NormalizedSimplexNoise.FromDefaultOctaves(woctaves, 1 / wscale, wpersistence, seed + 2);
            noisegenY = NormalizedSimplexNoise.FromDefaultOctaves(woctaves, 1 / wscale, wpersistence, seed + 1231296);
            this.requiresSpawnOffset = requiresSpawnOffset;
            var spawnCoord = requireLandAt[0];
            var offs = GetNoiseOffsetAt(spawnCoord.X, spawnCoord.Z);
            spawnOffsX = -offs.X;
            spawnOffsZ = -offs.Z;
        }

        public XZ GetNoiseOffsetAt(int xCoord, int zCoord)
        {
            int offsetX = (int)(wobbleIntensity * noisegenX.Noise(xCoord, zCoord) * 1.2f);
            int offsetY = (int)(wobbleIntensity * noisegenY.Noise(xCoord, zCoord) * 1.2f);
            return new XZ(offsetX, offsetY);
        }

        public override int[] GenLayer(int xCoord, int zCoord, int sizeX, int sizeZ)
        {
            if (requiresSpawnOffset)
            {
                xCoord += spawnOffsX;
                zCoord += spawnOffsZ;
            }
            var result = new int[sizeX * sizeZ];
            for (var x = 0; x < sizeX; x++)
            {
                for (var z = 0; z < sizeZ; z++)
                {
                    var nx = xCoord + x;
                    var nz = zCoord + z;
                    var offsetX = (int)(wobbleIntensity * noisegenX.Noise(nx, nz));
                    var offsetZ = (int)(wobbleIntensity * noisegenY.Noise(nx, nz));
                    var unscaledXpos = nx + offsetX;
                    var unscaledZpos = nz + offsetZ;
                    var oceanicity = noiseOcean.GetOceanIndexAt(unscaledXpos, unscaledZpos);

                    // if we have ocean check if we need to force land in case of story locations
                    if (oceanicity == 255)
                    {
                        if (requiresSpawnOffset)
                        {
                            var scaled = scale / 2;
                            for (var i = 0; i < requireLandAt.Count; i++)
                            {
                                var xz = requireLandAt[i];
                                if (Math.Abs(xz.X - unscaledXpos) <= scaled && Math.Abs(xz.Z - unscaledZpos) <= scaled)
                                {
                                    oceanicity = 0;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            for (var i = 0; i < requireLandAt.Count; i++)
                            {
                                var xz = requireLandAt[i];
                                if (xz.X == nx && xz.Z == nz)
                                {
                                    oceanicity = 0;
                                    break;
                                }
                            }
                        }
                    }

                    result[z * sizeX + x] = oceanicity;
                }
            }

            return result;
        }
    }
}
