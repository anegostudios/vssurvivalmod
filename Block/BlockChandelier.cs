using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    class BlockChandelier : Block
    {
        public int CandleCount
        {
            get
            {
                switch (LastCodePart())
                {
                    case "candle0":
                        return 0;
                    case "candle1":
                        return 1;
                    case "candle2":
                        return 2;
                    case "candle3":
                        return 3;
                    case "candle4":
                        return 4;
                    case "candle5":
                        return 5;
                    case "candle6":
                        return 6;
                    case "candle7":
                        return 7;
                    case "candle8":
                        return 8;
                }
                return -1;
            }
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            int candlecount = CandleCount;
            ItemStack itemstack = byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack;

            if (itemstack != null && itemstack.Collectible.Code.Path == "candle" && CandleCount != 8)
            {
                if (byPlayer != null && byPlayer.WorldData.CurrentGameMode == EnumGameMode.Survival)
                {
                    byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                }

                Block block = world.GetBlock(CodeWithParts(GetNextCandleCount()));
                world.BlockAccessor.ExchangeBlock(block.BlockId, blockSel.Position);
                world.BlockAccessor.MarkBlockDirty(blockSel.Position);

                return true;
            }
            return false;
        }

        string GetNextCandleCount()
        {
            if (CandleCount != 8)
                return $"candle{CandleCount + 1}";
            else 
                return "";
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            if (CandleCount == 8) return null;

            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-chandelier-addcandle",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = new ItemStack[] { new ItemStack(world.GetItem(new AssetLocation("candle"))) }
                }
            }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
