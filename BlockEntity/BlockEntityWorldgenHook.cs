using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

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
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetString("hook", hook);
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

        public override void OnPlacementBySchematic(ICoreServerAPI api, IBlockAccessor blockAccessor, BlockPos pos)
        {
            base.OnPlacementBySchematic(api, blockAccessor, pos);
            TriggerWorldgenHook(api, blockAccessor, pos, hook, param);
        }

        public static void TriggerWorldgenHook(ICoreServerAPI api, IBlockAccessor blockAccessor, BlockPos target, string hook, string param)
        {
            api.Event.TriggerWorldgenHook(param == null ? "genHookStructure" : hook, blockAccessor, target, new AssetLocation(param ?? hook));
        }
    }
}
