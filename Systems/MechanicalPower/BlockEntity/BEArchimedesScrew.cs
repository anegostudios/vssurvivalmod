using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockEntityArchimedesScrew : BlockEntityItemFlow
    {
        public override float ItemFlowRate {
            get {
                var bh = GetBehavior<BEBehaviorMPArchimedesScrew>();
                if (bh?.Network == null) return 0;

                return bh.Network.Speed * itemFlowRate;
            }
        }

    }
}
