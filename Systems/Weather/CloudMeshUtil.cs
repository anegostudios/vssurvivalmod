using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class CloudMeshUtil : CubeMeshUtil
    {
        public static MeshData GetCubeModelDataForClouds(float scaleH, float scaleV, Vec3f translate)
        {
            MeshData modeldata = new MeshData();
            modeldata.xyz = new float[3 * 4 * 6];
            modeldata.Normals = new int[4 * 6];
            for (int i = 0; i < 4 * 6; i++)
            {
                modeldata.xyz[3 * i + 0] = CubeVertices[3 * i] * scaleH + translate.X;
                modeldata.xyz[3 * i + 1] = CubeVertices[3 * i + 1] * scaleV + translate.Y;
                modeldata.xyz[3 * i + 2] = CubeVertices[3 * i + 2] * scaleH + translate.Z;
            }

            modeldata.SetVerticesCount(4 * 6);

            modeldata.SetIndices(CubeVertexIndices);
            modeldata.SetIndicesCount(3 * 2 * 6);

            modeldata.Rgba2 = null;

            return modeldata;
        }


        // More effficient than below method
        public static void AddIndicesWithSides(MeshData model, int offset, byte sideFlags)
        {
            int quantity = 12 + 6 * (sideFlags & 1) + 6 * ((sideFlags >> 1) & 1) + 6 * ((sideFlags >> 2) & 1) + 6 * ((sideFlags >> 3) & 1);

            if (model.IndicesCount + quantity >= model.IndicesMax)
            {
                model.GrowIndexBuffer(model.IndicesCount + quantity);
            }

            // Top Face
            model.Indices[model.IndicesCount++] = offset + 16;
            model.Indices[model.IndicesCount++] = offset + 17;
            model.Indices[model.IndicesCount++] = offset + 18;
            model.Indices[model.IndicesCount++] = offset + 16;
            model.Indices[model.IndicesCount++] = offset + 18;
            model.Indices[model.IndicesCount++] = offset + 19;
            // Bottom Face
            model.Indices[model.IndicesCount++] = offset + 20;
            model.Indices[model.IndicesCount++] = offset + 21;
            model.Indices[model.IndicesCount++] = offset + 22;
            model.Indices[model.IndicesCount++] = offset + 20;
            model.Indices[model.IndicesCount++] = offset + 22;
            model.Indices[model.IndicesCount++] = offset + 23;


            if ((sideFlags & BlockFacing.NORTH.Flag) > 0)
            {
                model.Indices[model.IndicesCount++] = offset + 0;
                model.Indices[model.IndicesCount++] = offset + 1;
                model.Indices[model.IndicesCount++] = offset + 2;
                model.Indices[model.IndicesCount++] = offset + 0;
                model.Indices[model.IndicesCount++] = offset + 2;
                model.Indices[model.IndicesCount++] = offset + 3;
            }

            if ((sideFlags & BlockFacing.EAST.Flag) > 0)
            {
                model.Indices[model.IndicesCount++] = offset + 4;
                model.Indices[model.IndicesCount++] = offset + 5;
                model.Indices[model.IndicesCount++] = offset + 6;
                model.Indices[model.IndicesCount++] = offset + 4;
                model.Indices[model.IndicesCount++] = offset + 6;
                model.Indices[model.IndicesCount++] = offset + 7;
            }

            if ((sideFlags & BlockFacing.SOUTH.Flag) > 0)
            {
                model.Indices[model.IndicesCount++] = offset + 8;
                model.Indices[model.IndicesCount++] = offset + 9;
                model.Indices[model.IndicesCount++] = offset + 10;
                model.Indices[model.IndicesCount++] = offset + 8;
                model.Indices[model.IndicesCount++] = offset + 10;
                model.Indices[model.IndicesCount++] = offset + 11;
            }

            if ((sideFlags & BlockFacing.WEST.Flag) > 0)
            {
                model.Indices[model.IndicesCount++] = offset + 12;
                model.Indices[model.IndicesCount++] = offset + 13;
                model.Indices[model.IndicesCount++] = offset + 14;
                model.Indices[model.IndicesCount++] = offset + 12;
                model.Indices[model.IndicesCount++] = offset + 14;
                model.Indices[model.IndicesCount++] = offset + 15;
            }

        }


        // Returns only the indicies of supplied faces, but always returns top and bottom face
        public static int[] GetIndicesWithSides(int offset, byte faceFlags)
        {
            int[] indices = new int[12 + 6 * (faceFlags & 1) + 6 * ((faceFlags >> 1) & 1) + 6 * ((faceFlags >> 2) & 1) + 6 * ((faceFlags >> 3) & 1)];
            int i = 0;

            // Top Face
            indices[i++] = offset + 16;
            indices[i++] = offset + 17;
            indices[i++] = offset + 18;
            indices[i++] = offset + 16;
            indices[i++] = offset + 18;
            indices[i++] = offset + 19;
            // Bottom Face
            indices[i++] = offset + 20;
            indices[i++] = offset + 21;
            indices[i++] = offset + 22;
            indices[i++] = offset + 20;
            indices[i++] = offset + 22;
            indices[i++] = offset + 23;


            if ((faceFlags & BlockFacing.NORTH.Flag) > 0)
            {
                indices[i++] = offset + 0;
                indices[i++] = offset + 1;
                indices[i++] = offset + 2;
                indices[i++] = offset + 0;
                indices[i++] = offset + 2;
                indices[i++] = offset + 3;
            }

            if ((faceFlags & BlockFacing.EAST.Flag) > 0)
            {
                indices[i++] = offset + 4;
                indices[i++] = offset + 5;
                indices[i++] = offset + 6;
                indices[i++] = offset + 4;
                indices[i++] = offset + 6;
                indices[i++] = offset + 7;
            }

            if ((faceFlags & BlockFacing.SOUTH.Flag) > 0)
            {
                indices[i++] = offset + 8;
                indices[i++] = offset + 9;
                indices[i++] = offset + 10;
                indices[i++] = offset + 8;
                indices[i++] = offset + 10;
                indices[i++] = offset + 11;
            }

            if ((faceFlags & BlockFacing.WEST.Flag) > 0)
            {
                indices[i++] = offset + 12;
                indices[i++] = offset + 13;
                indices[i++] = offset + 14;
                indices[i++] = offset + 12;
                indices[i++] = offset + 14;
                indices[i++] = offset + 15;
            }

            return indices;
        }

    }
}
