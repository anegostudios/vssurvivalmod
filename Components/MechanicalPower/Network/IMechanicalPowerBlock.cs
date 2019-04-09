using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public interface IMechanicalPowerBlock
    {
        MechanicalNetwork GetNetwork(IWorldAccessor world, BlockPos pos);

        bool HasConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face);
        void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face);
    }

}
