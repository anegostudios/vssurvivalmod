using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorReinforcable : BlockBehavior
    {
        public BlockBehaviorReinforcable(Block block) : base(block)
        {
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref EnumHandling handling)
        {
            ModSystemBlockReinforcement modBre;

            if (byPlayer?.WorldData.CurrentGameMode == EnumGameMode.Creative)
            {
                if (world.Side == EnumAppSide.Server)
                {
                    modBre = world.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>();
                    modBre.ClearReinforcement(pos);
                }
                return;
            }


            modBre = world.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>();
            BlockReinforcement bre = modBre.GetReinforcment(pos);
            
            if (bre != null && bre.Strength > 0)
            {
                handling = EnumHandling.PreventDefault;
                
                world.PlaySoundAt(new AssetLocation("sounds/tool/breakreinforced"), pos.X, pos.Y, pos.Z, byPlayer);

                modBre.ConsumeStrength(pos, 1);

                world.BlockAccessor.MarkBlockDirty(pos);
            }
        }


        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            ModSystemBlockReinforcement modBre = world.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>();

            if (modBre != null)
            {
                BlockReinforcement bre = modBre.GetReinforcment(pos);
                if (bre == null) return null;

                if (bre.Locked)
                {
                    return Lang.Get("Has been locked and reinforced by {0}.", bre.LastPlayername) + "\n" + Lang.Get("Strength: {0}", bre.Strength) + "\n";
                } else
                {
                    return Lang.Get("Has been reinforced by {0}.", bre.LastPlayername) + "\n" + Lang.Get("Strength: {0}", bre.Strength) + "\n";
                }
            }

            return null;
        }

    }
}
