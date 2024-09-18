using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BeamPlacerWorkSpace {
        public BlockPos startPos;
        public BlockFacing onFacing;
        public Vec3f startOffset;
        public Vec3f endOffset;
        public MeshData[] currentMeshes;
        public MultiTextureMeshRef currentMeshRef;
        public bool nowBuilding;
        public Block block;

        public int GridSize = 4;
    }
}
