using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockEntityConditional : BlockEntityGuiConfigurableCommands, IWrenchOrientable
    {
        BlockFacing facing = BlockFacing.EAST;
        int prevState;
        bool Latching => Silent;   // Silent means enable latching, for a Conditional Block.

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            facing = BlockFacing.FromCode(Block.Code.EndVariant()) ?? BlockFacing.EAST;
        }

        public override void Execute(Caller caller, string commands)
        {
            if (Api.Side == EnumAppSide.Server)
            {
                int testResult = EvaluateConditionAsTrue();
                if (testResult == 0) return;
                if (Latching && testResult == prevState) return;
                prevState = testResult;
                BlockPos sidePosition = testResult == 2 ? this.Pos.AddCopy(facing) : this.Pos.AddCopy(facing.GetCCW());

                Api.World.BlockAccessor.GetBlock(sidePosition).Activate(Api.World, getCaller(), new BlockSelection() { Position = sidePosition });
            }
        }

        private int EvaluateConditionAsTrue()
        {
            string theCommands = Commands.Trim();
            TextCommandCallingArgs packedArgs = new TextCommandCallingArgs()
            {
                Caller = getCaller(),
                RawArgs = new CmdArgs(theCommands)
            };

            if (theCommands.StartsWith("isBlock"))
            {
                ICommandArgumentParser blockCondParser = new IsBlockArgParser("cond", Api, true);
                EnumParseResult bresult = blockCondParser.TryProcess(packedArgs);
                if (bresult != EnumParseResult.Good) return 0;
                return (bool)blockCondParser.GetValue() ? 2 : 1;
            }
            ICommandArgumentParser entityCondParser = new EntitiesArgParser("cond", Api, true);
            EnumParseResult result = entityCondParser.TryProcess(packedArgs);
            if (result != EnumParseResult.Good) return 0;

            return (entityCondParser.GetValue() as Entity[]).Length > 0 ? 2 : 1;
        }

        Caller getCaller()
        {
            return new Caller()
            {
                Type = EnumCallerType.Console,
                CallerRole = "admin",
                CallerPrivileges = new string[] { "*" },
                FromChatGroupId = GlobalConstants.ConsoleGroup,
                Pos = new Vec3d(0.5, 0.5, 0.5).Add(this.Pos)
            };
        }

        public override bool OnInteract(Caller caller)
        {
            if (caller.Player != null && CanEditCommandblocks(caller.Player))
            {
                if (Api.Side == EnumAppSide.Client && caller.Player.Entity.Controls.ShiftKey)
                {
                    if (clientDialog != null)
                    {
                        clientDialog.TryClose();
                        clientDialog.Dispose();
                        clientDialog = null;
                        return true;
                    }

                    clientDialog = new GuiDialogBlockEntityConditional(Pos, Commands, Latching, Api as ICoreClientAPI, "Conditional editor");
                    clientDialog.TryOpen();
                    clientDialog.OnClosed += () =>
                    {
                        clientDialog?.Dispose(); clientDialog = null;
                    };

                    return true;
                }
            }
            else
            {
                (Api as ICoreClientAPI)?.TriggerIngameError(this, "noprivilege", "Can only be modified in creative mode and with controlserver privlege");
            }

            return base.OnInteract(caller);
        }

        public void Rotate(EntityAgent byEntity, BlockSelection blockSel, int dir)
        {
            facing = dir > 0 ? facing.GetCCW() : facing.GetCW();
            Api.World.BlockAccessor.ExchangeBlock(Api.World.GetBlock(Block.CodeWithVariant("side", facing.Code)).Id, Pos);
            MarkDirty(true);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            prevState = tree.GetInt("prev");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("prev", prevState);
        }
    }
}
