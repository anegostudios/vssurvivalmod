using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorHarvestable : BlockBehavior
    {
        float harvestTime;
        public BlockDropItemStack harvestedStack;

        public AssetLocation harvestingSound;

        AssetLocation harvestedBlockCode;
        Block harvestedBlock;
        string interactionHelpCode;

        public BlockBehaviorHarvestable(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            interactionHelpCode = properties["harvestTime"].AsString("blockhelp-harvetable-harvest");
            harvestTime = properties["harvestTime"].AsFloat(0);
            harvestedStack = properties["harvestedStack"].AsObject<BlockDropItemStack>(null);

            string code = properties["harvestingSound"].AsString("game:sounds/block/plant");
            if (code != null) {
                harvestingSound = AssetLocation.Create(code, block.Code.Domain);
            }

            code = properties["harvestedBlockCode"].AsString();
            if (code != null)
            {
                harvestedBlockCode = AssetLocation.Create(code, block.Code.Domain);
            }
            
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            harvestedStack?.Resolve(api.World, string.Format("harvestedStack of block {0}", block.Code));

            harvestedBlock = api.World.GetBlock(harvestedBlockCode);
            if (harvestedBlock == null)
            {
                api.World.Logger.Warning("Unable to resolve harvested block code '{0}' for block {1}. Will ignore.", harvestedBlockCode, block.Code);
            }
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }
            
            handling = EnumHandling.PreventDefault;

            if (harvestedStack != null)
            {
                world.PlaySoundAt(harvestingSound, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
                return true;
            }

            return false;
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handled)
        {
            if (blockSel == null) return false;

            handled = EnumHandling.PreventDefault;

            (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemAttack);

            if (world.Rand.NextDouble() < 0.1)
            {
                world.PlaySoundAt(harvestingSound, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
            }

            return world.Side == EnumAppSide.Client || secondsUsed < harvestTime;
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;


            if (secondsUsed > harvestTime - 0.05f && harvestedStack != null && world.Side == EnumAppSide.Server)
            {
                float dropRate = 1;

                if (block.Attributes?.IsTrue("forageStatAffected") == true)
                {
                    dropRate *= byPlayer.Entity.Stats.GetBlended("wildCropDropRate");
                }

                ItemStack stack = harvestedStack.GetNextItemStack(dropRate);

                if (!byPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    world.SpawnItemEntity(stack, blockSel.Position.ToVec3d().Add(0.5, 0.5, 0.5));
                }

                if (harvestedBlock != null)
                {
                    world.BlockAccessor.SetBlock(harvestedBlock.BlockId, blockSel.Position);
                }

                world.PlaySoundAt(harvestingSound, blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
            }
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handled)
        {
            if (harvestedStack != null)
            {
                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = interactionHelpCode,
                        MouseButton = EnumMouseButton.Right
                    }
                };
            }

            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer, ref handled);
        }
    }
}
