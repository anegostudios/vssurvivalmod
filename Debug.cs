﻿using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;


namespace Vintagestory.ServerMods
{
    /*public class NoNerdPoleSystem : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;
        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Event.CanPlaceOrBreakBlock += (IServerPlayer plr, BlockSelection bs, out string claimant) => {
                if (!plr.Entity.Collided) { claimant = "nonerdpoleing"; return false; }
                claimant = null; return true; 
            }
        }
    }*/


    /*public class DebugSystem : ModSystem
    {
        ICoreAPI api;


        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            
            // api.ChatCommands.GetOrCreate("debug")
            //     .BeginSubCommand("anvildebug")
            //         .WithDescription("Anvil debug info")
            //         .RequiresPrivilege(Privilege.controlserver)
            //         .HandleWith(OnAnvilDebug)
            //     .EndSubCommand();
        }

        private TextCommandResult OnAnvilDebug(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            if (player.CurrentBlockSelection?.Position != null)
            {
                BlockEntityAnvil bea = api.World.BlockAccessor.GetBlockEntity(player.CurrentBlockSelection.Position) as BlockEntityAnvil;

                if (bea == null)
                {
                    return TextCommandResult.Success("Not looking at an anvil");
                }

                return TextCommandResult.Success(bea.PrintDebugText());
            }
            return TextCommandResult.Success();
        }
    }*/
}
