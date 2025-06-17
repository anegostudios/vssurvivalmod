using System;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockEntityArchimedesScrew : BlockEntityItemFlow
    {
        public override float ItemFlowRate {
            get {
                var bh = GetBehavior<BEBehaviorMPArchimedesScrew>();
                if (bh?.Network == null) return 0;

                return Math.Abs(bh.Network.Speed) * itemFlowRate;
            }
        }

    }
}
