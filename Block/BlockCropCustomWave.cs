using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockCropCustomWave : BlockCrop
    {
        private float windWaveStartHeight;
        private bool isCassava;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            windWaveStartHeight = Attributes?["windWaveStartHeight"].AsFloat(0.5f) ?? 0.5f;
            isCassava = Variant["type"] == "cassava";
        }


        protected override void setLeaveWaveFlags(MeshData sourceMesh, bool off)
        {
            int clearFlags = VertexFlags.clearWaveBits;
            int verticesCount = sourceMesh.VerticesCount;
            int blockSpecialFlag = (VertexFlags.All >> VertexFlags.GroundDistanceBitsShift) & 7;
            int blockFlag = VertexFlags.All & ~(7 << VertexFlags.GroundDistanceBitsShift);
            Vec3f normalf = isCassava ? new Vec3f() : null;

            // Iterate over each element face
            for (int vertexNum = 0; vertexNum < verticesCount; vertexNum++)
            {
                bool cassavaLeaf = false;
                float fy = sourceMesh.xyz[vertexNum * 3 + 1];

                int specialFlag = sourceMesh.Flags[vertexNum] >> VertexFlags.GroundDistanceBitsShift;
                if (specialFlag < 0)
                {
                    cassavaLeaf = true;
                    specialFlag &= 7;
                }
                if (specialFlag == 0)
                {
                    specialFlag = blockSpecialFlag;
                }
                float fx = sourceMesh.xyz[vertexNum * 3];
                float fz = sourceMesh.xyz[vertexNum * 3 + 2];
                // regular leaf motion for the leaves, swaying motion for the central stalk and fruit due to the FoliageWaveSpecial set in the shapes
                if (specialFlag == 4)
                {
                    // tweak the center vertices of the leaves below the fruit (calyx) so the centers stay close to the stalk (groundDistance [a.k.a. foliageWaveSpecial] 3) - no other good way to do this
                    float dist = (fx - 0.5f) * (fx - 0.5f) + (fz - 0.5f) * (fz - 0.5f);
                    if (dist < 0.04f) specialFlag = 3;
                }

                int flag = blockFlag;
                if (off || fy < windWaveStartHeight)  //this low  y threshold for windwave ensures even the outer tips of the bent-over big leaves, will windwave
                {
                    flag &= clearFlags;
                }
                else
                {
                    flag |= specialFlag << VertexFlags.GroundDistanceBitsShift;
                }

                if (isCassava)
                {
                    // Special normals for Cassava: we want the wood to be dark, and the leaves to be lit according to which side of the bush they are on
                    // (anything else looks weird, because one leaf plane is horizontal, i.e. normal is UP and brightly lit, and the rest are vertical and therefore not brightly lit)
                    int normal = BlockFacing.DOWN.NormalPackedFlags;
                    if (cassavaLeaf)
                    {
                        float xzRatio = Math.Abs((fx - 0.5f) / fz - 0.5f);
                        normalf.Set(xzRatio < 0.5 ? 0 : fx < 0.5 ? -1 : 1, 0, xzRatio > 2 ? 0 : fz < 0.5 ? -1 : 1);
                        normal = VertexFlags.NormalToPackedInt(normalf) << 15;
                    }
                    sourceMesh.Flags[vertexNum] = flag | normal;
                }
                else
                {
                    sourceMesh.Flags[vertexNum] |= flag;
                }
            }
        }
    }
}
