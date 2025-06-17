using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{

    public class ModSystemSnowballs : ModSystem
    {
        ICoreAPI api;

        public override double ExecuteOrder() => 1;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            this.api = api;

            api.RegisterBlockBehaviorClass("Snowballable", typeof(BlockBehaviorSnowballable));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Event.ServerRunPhase(EnumServerRunPhase.WorldReady, addSnowballableBehavior);
        }

        private void addSnowballableBehavior()
        {
            foreach (Block block in api.World.Blocks)
            {
                if (block.Code == null || block.Id == 0) continue;

                if ((block.snowLevel == 1 && block.Variant.ContainsKey("height")) || block.snowLevel > 1 || (block.Attributes != null && block.Attributes.KeyExists("snowballableDecrementedBlockCode")))
                {
                    block.BlockBehaviors = block.BlockBehaviors.Append(new BlockBehaviorSnowballable(block));
                    block.CollectibleBehaviors = block.CollectibleBehaviors.Append(new BlockBehaviorSnowballable(block));
                }
            }
         }
    }

    public class BlockBehaviorSnowballable : BlockBehavior
    {
        public BlockBehaviorSnowballable(Block block) : base(block) { }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {   
            if (canPickSnowballFrom(block, blockSel.Position, byPlayer))
            {
                var stack = new ItemStack(world.GetItem(new AssetLocation("snowball-snow")), 2);
                if (!byPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    world.SpawnItemEntity(stack, byPlayer.Entity.Pos.XYZ.Add(0, 0.5, 0));
                }

                var harvestedblock = getDecrementedSnowLayerBlock(world, block);
                world.BlockAccessor.SetBlock(harvestedblock.Id, blockSel.Position);
                world.PlaySoundAt(new AssetLocation("sounds/block/snow"), byPlayer, byPlayer);

                handling = EnumHandling.PreventDefault;
                return true;
            }

            handling = EnumHandling.PassThrough;
            return false;
        }

        private Block getDecrementedSnowLayerBlock(IWorldAccessor world, Block block)
        {
            if (block.Attributes != null && block.Attributes.KeyExists("snowballableDecrementedBlockCode")) {
                return world.GetBlock(AssetLocation.Create(block.Attributes["snowballableDecrementedBlockCode"].AsString(), block.Code.Domain));
            }

            if (block.snowLevel > 3)
            {
                return world.GetBlock(block.CodeWithVariant("height", "" + (block.snowLevel - 1)));
            }

            return block == block.snowCovered3 ? block.snowCovered2 : (block == block.snowCovered2 ? block.snowCovered1 : world.Blocks[0]);
        }

        public static bool canPickSnowballFrom(Block block, BlockPos pos, IPlayer byPlayer)
        {
            var slot = byPlayer.Entity.RightHandItemSlot;

            bool hasSnow = 
                block.snowCovered2 != null || 
                (block.Attributes != null && block.Attributes.KeyExists("snowballableDecrementedBlockCode")) ||
                byPlayer.Entity.World.BlockAccessor.GetBlock(pos.DownCopy()).BlockMaterial == EnumBlockMaterial.Snow
            ;

            return 
                hasSnow
                && (block.snowLevel != 0 || byPlayer.Entity.World.BlockAccessor.GetBlock(pos.UpCopy()).BlockMaterial != EnumBlockMaterial.Snow) // Disallow when these are stacked snow blocks
                && byPlayer.Entity.Controls.ShiftKey && (slot.Empty || slot.Itemstack.Collectible is ItemSnowball);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handling)
        {
            handling = EnumHandling.Handled;
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-snow-takesnowball",
                    HotKeyCode = "shift",
                    MouseButton = EnumMouseButton.Right,
                    RequireFreeHand = true,
                    ShouldApply = (wi, bs, es) => (block.snowLevel != 0 || world.BlockAccessor.GetBlock(bs.Position.UpCopy()).BlockMaterial != EnumBlockMaterial.Snow) // Disallow when these are stacked snow blocks
                }
            };
        }
    }

}
