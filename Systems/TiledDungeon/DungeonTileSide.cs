using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.ServerMods
{
    public struct DungeonTileSide
    {
        public BlockFacing Side;
        public BlockPos Pos;
        public string[] Constraints;
        public string Code;
        public int SizeX;
        public int SizeY;
        public int SizeZ;
    }
}
