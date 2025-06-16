using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    [ProtoContract]
    public class PlacedBeam
    {
        [ProtoMember(1)]
        public Vec3f Start;
        [ProtoMember(2)]
        public Vec3f End;
        [ProtoMember(3)]
        public int BlockId;
        [ProtoMember(4)]
        public int FacingIndex;

        private Block block;
        public Block Block
        {
            get { return block; }
            set
            {
                this.block = value;
                this.SlumpPerMeter = block.Attributes?["slumpPerMeter"].AsFloat(0) ?? 0;
            }
        }
        public float SlumpPerMeter;
    }
}
