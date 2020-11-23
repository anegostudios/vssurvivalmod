using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockBed : Block
    {
        public static IMountable GetMountable(IWorldAccessor world, TreeAttribute tree)
        {
            BlockPos pos = new BlockPos(tree.GetInt("posx"), tree.GetInt("posy"), tree.GetInt("posz"));
            Block block = world.BlockAccessor.GetBlock(pos);

            BlockFacing facing = BlockFacing.FromCode(block.LastCodePart());
            BlockEntityBed beBed = world.BlockAccessor.GetBlockEntity(block.LastCodePart(1) == "feet" ? pos.AddCopy(facing) : pos) as BlockEntityBed;

            return beBed;
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return false;
            }

            if (CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                BlockFacing[] horVer = SuggestedHVOrientation(byPlayer, blockSel);

                BlockPos secondPos = blockSel.Position.AddCopy(horVer[0]);

                BlockSelection secondBlockSel = new BlockSelection() { Position = secondPos, Face = BlockFacing.UP };
                if (!CanPlaceBlock(world, byPlayer, secondBlockSel, ref failureCode)) return false;

                string code = horVer[0].Opposite.Code;

                Block orientedBlock = world.BlockAccessor.GetBlock(CodeWithParts("head", code));
                orientedBlock.DoPlaceBlock(world, byPlayer, secondBlockSel, itemstack);

                AssetLocation feetCode = CodeWithParts("feet", code);
                orientedBlock = world.BlockAccessor.GetBlock(feetCode);
                orientedBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
                return true;
            }

            return false;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }

            BlockFacing facing = BlockFacing.FromCode(LastCodePart()).Opposite;
            BlockEntityBed beBed = world.BlockAccessor.GetBlockEntity(LastCodePart(1) == "feet" ? blockSel.Position.AddCopy(facing) : blockSel.Position) as BlockEntityBed;

            if (beBed == null) return false;
            if (beBed.MountedBy != null) return false;

            EntityBehaviorTiredness ebt = byPlayer.Entity.GetBehavior("tiredness") as EntityBehaviorTiredness;
            if (ebt != null && ebt.Tiredness <= 8)
            {
                if (world.Side == EnumAppSide.Client) (api as ICoreClientAPI).TriggerIngameError(this, "nottiredenough", Lang.Get("not-tired-enough"));
                return false;
            }

            return byPlayer.Entity.TryMount(beBed);
        }

        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
        {
            string headfoot = LastCodePart(1);

            BlockFacing facing = BlockFacing.FromCode(LastCodePart());
            if (LastCodePart(1) == "feet") facing = facing.Opposite;
            else
            {
                BlockEntityBed beBed = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityBed;
                beBed?.MountedBy?.TryUnmount();
            }

            Block secondPlock = world.BlockAccessor.GetBlock(pos.AddCopy(facing));

            if (secondPlock is BlockBed && secondPlock.LastCodePart(1) != headfoot)
            {
                world.BlockAccessor.SetBlock(0, pos.AddCopy(facing));
            }

            base.OnBlockRemoved(world, pos);
        }

        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return GetHandbookDropsFromBreakDrops(handbookStack, forPlayer);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            return new ItemStack[] { new ItemStack(world.BlockAccessor.GetBlock(CodeWithParts("head", "north"))) };
        }


        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            BlockFacing beforeFacing = BlockFacing.FromCode(LastCodePart());
            int rotatedIndex = GameMath.Mod(beforeFacing.HorizontalAngleIndex - angle / 90, 4);
            BlockFacing nowFacing = BlockFacing.HORIZONTALS_ANGLEORDER[rotatedIndex];

            return CodeWithParts(nowFacing.Code);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return new ItemStack(world.BlockAccessor.GetBlock(CodeWithParts("head", "north")));
        }

        public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis)
        {
            BlockFacing facing = BlockFacing.FromCode(LastCodePart());
            if (facing.Axis == axis)
            {
                return CodeWithParts(facing.Opposite.Code);
            }
            return Code;
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            double eff = inSlot.Itemstack.Collectible.Attributes["sleepEfficiency"].AsDouble();
            double sleephours = eff * world.Calendar.HoursPerDay / 2;

            dsc.AppendLine("\n" + Lang.Get("Lets you sleep for {0} hours a day", sleephours.ToString("#.#")));
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            float sleepEfficiency = 0.5f;
            if (Attributes?["sleepEfficiency"] != null) sleepEfficiency = Attributes["sleepEfficiency"].AsFloat(0.5f);

            return base.GetPlacedBlockInfo(world, pos, forPlayer) + Lang.Get("Lets you sleep for up to {0} hours", System.Math.Round(sleepEfficiency * world.Calendar.HoursPerDay / 2, 2));
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-bed-sleep",
                    MouseButton = EnumMouseButton.Right
                }
            }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }

        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            if (isImpact && facing.Axis == EnumAxis.Y)
            {
                if (Sounds?.Break != null && System.Math.Abs(collideSpeed.Y) > 0.2)
                {
                    world.PlaySoundAt(Sounds.Break, entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
                }
                entity.Pos.Motion.Y = GameMath.Clamp(-entity.Pos.Motion.Y * 0.8, -0.5, 0.5);
            }
        }
    }
}
