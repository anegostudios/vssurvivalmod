using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
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
            sapi.RegisterCommand("microblock", "", "", onMicroblockCmd, "worldedit");
        }

        private void onMicroblockCmd(IServerPlayer player, int groupId, CmdArgs args)
        {
            string subcmd = args.PopWord();

            var wmod = sapi.ModLoader.GetModSystem<WorldEdit.WorldEdit>();
            var workspace = wmod.GetWorkSpace(player.PlayerUID);

            if (workspace == null || workspace.StartMarker == null || workspace.EndMarker == null)
            {
                player.SendMessage(groupId, "Select an area with worldedit first", EnumChatType.CommandError);
                return;
            }


            switch (subcmd)
            {
                case "recalc":
                    int i = 0;
                    sapi.World.BlockAccessor.WalkBlocks(workspace.StartMarker, workspace.EndMarker, (block, x, y, z) =>
                    {
                        if (block is BlockMicroBlock)
                        {
                            BlockEntityMicroBlock bemc = sapi.World.BlockAccessor.GetBlockEntity(new BlockPos(x, y, z)) as BlockEntityMicroBlock;
                            if (bemc != null)
                            {
                                bemc.RebuildCuboidList();
                                bemc.MarkDirty(true);
                                i++;
                            }
                        }
                    });

                    player.SendMessage(groupId, i + " microblocks recalced", EnumChatType.CommandError);

                    break;
            }
        }
    }
}
