using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

#nullable disable

namespace Vintagestory.ServerMods;

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
        sapi.ChatCommands.GetOrCreate("we")
            .BeginSubCommand("microblock")
            .WithDescription("Recalculate microblocks")
            .RequiresPrivilege("worldedit")
            .BeginSubCommand("recalc")
            .WithDescription("Recalc")
            .RequiresPlayer()
            .HandleWith(OnMicroblockCmd)
            .EndSubCommand()
            .EndSubCommand();
    }

    private TextCommandResult OnMicroblockCmd(TextCommandCallingArgs args)
    {
        var wmod = sapi.ModLoader.GetModSystem<WorldEdit.WorldEdit>();
        var workspace = wmod.GetWorkSpace(args.Caller.Player.PlayerUID);

        if (workspace == null || workspace.StartMarker == null || workspace.EndMarker == null)
        {
            return TextCommandResult.Success("Select an area with worldedit first");
        }

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

        return TextCommandResult.Success(i + " microblocks recalced");
    }
}
