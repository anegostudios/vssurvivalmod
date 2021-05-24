using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorRopeTieable : BlockBehavior
    {

        ClothManager cm;

        public BlockBehaviorRopeTieable(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            cm = api.ModLoader.GetModSystem<ClothManager>();
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            ClothSystem cs = cm.GetClothSystemAttachedToBlock(blockSel.Position);
            if (cs != null)
            {
                Entity byEntity = byPlayer.Entity;

                Vec3d lpos = new Vec3d(0, byEntity.LocalEyePos.Y - 0.25f, 0);
                Vec3d aheadPos = lpos.AheadCopy(0.25f, byEntity.SidedPos.Pitch, byEntity.SidedPos.Yaw);

                // Already handled by ItemRope
                if (cs.FirstPoint.PinnedToEntity?.EntityId == byPlayer.Entity.EntityId || cs.LastPoint.PinnedToEntity?.EntityId == byPlayer.Entity.EntityId)
                {
                    return base.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);
                }

                ClothPoint targetPoint = cs.FirstPoint.PinnedToBlockPos == blockSel.Position ? cs.FirstPoint : cs.LastPoint;

                ItemStack stack = new ItemStack(world.GetItem(new AssetLocation("rope")));
                stack.Attributes.SetInt("clothId", cs.ClothId); 

                if (byPlayer.InventoryManager.TryGiveItemstack(stack, true))
                {
                    targetPoint.PinTo(byPlayer.Entity, aheadPos.ToVec3f());
                } else
                {
                    Entity ei = world.SpawnItemEntity(stack, blockSel.Position.ToVec3d().Add(0.5, 0.5, 0.5));
                    targetPoint.PinTo(ei, new Vec3f(0, 0.1f, 0));
                }

                handling = EnumHandling.PreventDefault;
                return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);


        }

    }
}

