using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    public class BlockEntityTransient : BlockEntity
    {
        double transitionAtTotalDays = -1;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            Block block = api.World.BlockAccessor.GetBlock(pos);

            if (transitionAtTotalDays <= 0)
            {
                float hours = block.Attributes["inGameHours"].AsFloat(24);
                transitionAtTotalDays = api.World.Calendar.TotalDays + hours / 24;
            }

            if (api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(CheckTransition, 2000);
            }
        }

        private void CheckTransition(float dt)
        {
            if (transitionAtTotalDays > api.World.Calendar.TotalDays) return;

            Block block = api.World.BlockAccessor.GetBlock(pos);

            if (block.Attributes == null) return;

            AssetLocation blockCode = block.WildCardPop(
                new AssetLocation(block.Attributes["convertFrom"].AsString()), 
                new AssetLocation(block.Attributes["convertTo"].AsString())
            );

            Block tblock = api.World.GetBlock(blockCode);
            if (tblock == null) return;

            api.World.BlockAccessor.SetBlock(tblock.BlockId, pos);
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);

            transitionAtTotalDays = tree.GetDouble("transitionAtTotalDays");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetDouble("transitionAtTotalDays", transitionAtTotalDays);
        }
    }
}
