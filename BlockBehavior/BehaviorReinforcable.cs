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
            if (byPlayer == null) return;  // Fast return path for no player (although normally OnBlockBroken will specify a player)

            ModSystemBlockReinforcement modBre;

            modBre = world.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>();
            BlockReinforcement bre = modBre.GetReinforcment(pos);

            if (bre != null && bre.Strength > 0)
            {
                handling = EnumHandling.PreventDefault;   // This prevents the block from breaking normally, while it any amount of reinforcement left

                world.PlaySoundAt(new AssetLocation("sounds/tool/breakreinforced"), pos.X, pos.Y, pos.Z, byPlayer);

                if (!byPlayer.HasPrivilege("denybreakreinforced"))
                {
                    modBre.ConsumeStrength(pos, 1);

                    world.BlockAccessor.MarkBlockDirty(pos);
                }
            }
        }


        public override float GetMiningSpeedModifier(IWorldAccessor world, BlockPos pos, IPlayer byPlayer)
        {
            ModSystemBlockReinforcement modBre = world.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>();
            BlockReinforcement bre = modBre.GetReinforcment(pos);
            if (bre != null && bre.Strength > 0 && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                return 0.6f;
            }
            return 1.0f;
        }


        public override void OnBlockPlaced(IWorldAccessor world, BlockPos pos, ref EnumHandling handling)
        {
            if (world.Side == EnumAppSide.Server)
            {
                // Clear any existing reinforcement

                // This is necessary in case a previous block at this position at one time had reinforcement which was not cleared by that block
                // e.g. if a block changed from being previously having BehaviorReinforcable, to *not* having this behavior
                // Specifically, a lot of blocks changed in this way from 1.14 to 1.15

                ModSystemBlockReinforcement modBre;
                modBre = world.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>();
                modBre.ClearReinforcement(pos);
            }
        }


        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            ModSystemBlockReinforcement modBre = world.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>();

            if (modBre != null)
            {
                BlockReinforcement bre = modBre.GetReinforcment(pos);
                if (bre == null) return null;

                StringBuilder sb = new StringBuilder();

                if (bre.GroupUid != 0)
                {
                    sb.AppendLine(Lang.Get(bre.Locked ? "Has been locked and reinforced by group {0}." : "Has been reinforced by group {0}.", bre.LastGroupname));
                } else
                {
                    sb.AppendLine(Lang.Get(bre.Locked ? "Has been locked and reinforced by {0}." : "Has been reinforced by {0}.", bre.LastPlayername));
                }

                sb.AppendLine(Lang.Get("Strength: {0}", bre.Strength));

                return sb.ToString();
            }

            return null;
        }


        /// <summary>
        /// Prevent right-click pickup in survival mode, for blocks which have any level of reinforcement on them
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        /// <param name="byPlayer"></param>
        /// <returns>True if pickup is allowed; false if pickup is denied</returns>
        static public bool AllowRightClickPickup(IWorldAccessor world, BlockPos pos, IPlayer byPlayer)
        {
            ModSystemBlockReinforcement modBre;

            modBre = world.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>();
            BlockReinforcement bre = modBre.GetReinforcment(pos);

            if (bre != null && bre.Strength > 0)
            {
                return false;
            }

            return true;
        }
    }
}
