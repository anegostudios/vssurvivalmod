using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockEntityWorldgenHook : BlockEntity
    {
        public string hook;
        public string param;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            hook = tree.GetString("hook");
            param = tree.GetString("param");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetString("hook", hook);
            if (param != null)
            {
                tree.SetString("param", param);
            }
        }

        public void SetHook(string hook, string param)
        {
            this.hook = hook;
            this.param = param;
            MarkDirty(false);
        }

        public string GetHook()
        {
            return this.param == null ? hook : hook + " " + param;
        }

        public override void OnPlacementBySchematic(ICoreServerAPI api, IBlockAccessor blockAccessor, BlockPos pos, Dictionary<int, Dictionary<int, int>> replaceBlocks, int centerrockblockid, Block layerBlock, bool resolveImports)
        {
            base.OnPlacementBySchematic(api, blockAccessor, pos, replaceBlocks, centerrockblockid, layerBlock, resolveImports);
            TriggerWorldgenHook(api, blockAccessor, pos, hook, param);
        }

        public static void TriggerWorldgenHook(ICoreServerAPI api, IBlockAccessor blockAccessor, BlockPos target, string hook, string param)
        {
            api.Event.TriggerWorldgenHook(param == null ? "genHookStructure" : hook, blockAccessor, target, param ?? hook);
        }
    }
}
