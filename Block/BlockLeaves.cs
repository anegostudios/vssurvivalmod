using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockLeaves : Block
    {
        public override bool ShouldReceiveServerGameTicks(IWorldAccessor world, BlockPos pos, out object extra)
        {
            extra = null;
            return world.Rand.NextDouble() < 0.1;
        }

        public override void OnServerGameTick(IWorldAccessor world, BlockPos pos, object extra = null)
        {
            TreeAttribute tree = new TreeAttribute();
            tree.SetInt("x", pos.X);
            tree.SetInt("y", pos.Y);
            tree.SetInt("z", pos.Z);
            world.Api.Event.PushEvent("testForDecay", tree);
        }

        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing)
        {
            BakedCompositeTexture tex = Textures?.First().Value?.Baked;
            int color = capi.BlockTextureAtlas.GetRandomPixel(tex.TextureSubId);
            color = capi.ApplyColorTintOnRgba(1, color, pos.X, pos.Y, pos.Z);

            return color;
        }

        public override int GetColor(ICoreClientAPI capi, BlockPos pos)
        {
            return capi.ApplyColorTintOnRgba(1, base.GetColor(capi, pos), pos.X, pos.Y, pos.Z, false);
        }
    }
}
