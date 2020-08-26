using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Vintagestory.ServerMods
{
    
    public class ChiselBlockBulkSetMaterial : ModSystem
    {
        ICoreServerAPI sapi;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            //sapi.RegisterCommand("chiselsetmat", "", "", onChiselSetMatCmd, "worldedit");
        }

        private void onChiselSetMatCmd(IServerPlayer player, int groupId, CmdArgs args)
        {
            var wmod = sapi.ModLoader.GetModSystem<WorldEdit.WorldEdit>();

            var workspace = wmod.GetWorkSpace(player.PlayerUID);

            if (workspace == null || workspace.StartMarker == null || workspace.EndMarker == null)
            {
                player.SendMessage(groupId, "Select an area with worldedit first", EnumChatType.CommandError);
                return;
            }

            int startx = Math.Min(workspace.StartMarker.X, workspace.EndMarker.X);
            int endx = Math.Max(workspace.StartMarker.X, workspace.EndMarker.X);
            int starty = Math.Min(workspace.StartMarker.Y, workspace.EndMarker.Y);
            int endy = Math.Max(workspace.StartMarker.Y, workspace.EndMarker.Y);
            int startz = Math.Min(workspace.StartMarker.Z, workspace.EndMarker.Z);
            int endZ = Math.Max(workspace.StartMarker.Z, workspace.EndMarker.Z);

            for (int x = startx; x < endx; x++)
            {
                for (int y = starty; y < endy; y++)
                {
                    for (int z = startz; z < endZ; z++)
                    {

                    }
                }
            }


        }
    }
}
