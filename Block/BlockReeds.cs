using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockReeds : BlockPlant
    {
        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            Block block = world.BlockAccessor.GetBlock(blockSel.Position);

            Block blockToPlace = this;

            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                failureCode = "claimed";
                return false;
            }

            bool inWater = block.IsLiquid() && block.LiquidLevel == 7 && block.LiquidCode.Contains("water");

            if (inWater)
            {
                blockToPlace = world.GetBlock(CodeWithParts("water", LastCodePart()));
                if (blockToPlace == null) blockToPlace = this;
            }
            else
            {
                if (LastCodePart(1) != "free")
                {
                    failureCode = "requirefullwater";
                    return false;
                }
            }

            
            if (blockToPlace != null && blockToPlace.IsSuitablePosition(world, blockSel.Position, ref failureCode))
            {
                if (CanPlantStay(world.BlockAccessor, blockSel.Position))
                {
                    world.BlockAccessor.SetBlock(blockToPlace.BlockId, blockSel.Position);
                } else {
                    failureCode = "requirefertileground";
                    return false;
                }
                
                return true;
            }

            return false;
        }

        public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            if (LastCodePart() == "harvested") dt /= 2;
            else if (player.InventoryManager.ActiveTool != EnumTool.Knife)
            {
                dt /= 3;
            } else
            {
                float mul = 1f;
                if (itemslot.Itemstack.Collectible.MiningSpeed.TryGetValue(EnumBlockMaterial.Plant, out mul)) dt *= mul;
            }

            float resistance = RequiredMiningTier == 0 ? remainingResistance - dt : remainingResistance;

            if (counter % 5 == 0 || resistance <= 0)
            {
                double posx = blockSel.Position.X + blockSel.HitPosition.X;
                double posy = blockSel.Position.Y + blockSel.HitPosition.Y;
                double posz = blockSel.Position.Z + blockSel.HitPosition.Z;
                player.Entity.World.PlaySoundAt(resistance > 0 ? Sounds.GetHitSound(player) : Sounds.GetBreakSound(player), posx, posy, posz, player, true, 16, 1);
            }

            return resistance;
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            AssetLocation loc = CodeWithParts("free", "normal");
            Block block = world.GetBlock(loc);
            return new ItemStack(block);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
            {
                ItemStack drop = null;
                if (LastCodePart() == "normal")
                {
                    drop = new ItemStack(world.GetItem(new AssetLocation("cattailtops")));
                } else
                {
                    drop = new ItemStack(world.GetItem(new AssetLocation("cattailroot")));
                }

                if (drop != null)
                {
                    world.SpawnItemEntity(drop, new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5), null);
                }

                world.PlaySoundAt(Sounds.GetBreakSound(byPlayer), pos.X, pos.Y, pos.Z, byPlayer);
            }

            if (byPlayer != null && LastCodePart() == "normal" && byPlayer.InventoryManager.ActiveTool == EnumTool.Knife)
            {
                world.BlockAccessor.SetBlock(world.GetBlock(CodeWithParts("harvested")).BlockId, pos);
                return;
            }

            if (LastCodePart(1) != "free")
            {
                world.BlockAccessor.SetBlock(world.GetBlock(new AssetLocation("water-still-7")).BlockId, pos);
                world.BlockAccessor.GetBlock(pos).OnNeighourBlockChange(world, pos, pos);
            } else
            {
                world.BlockAccessor.SetBlock(0, pos);
            }
        }


        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace)
        {
            Block block = blockAccessor.GetBlock(pos);

            if (!block.IsReplacableBy(this))
            {
                return false;
            }

            Block belowBlock = blockAccessor.GetBlock(pos.X, pos.Y - 1, pos.Z);
            if (belowBlock.Fertility > 0)
            {
                if (block.LiquidCode == "water")
                {
                    return TryPlaceBlockInWater(blockAccessor, pos.UpCopy());
                }

                Block placingBlock = blockAccessor.GetBlock(CodeWithParts("free", "normal"));
                if (placingBlock == null) return false;
                blockAccessor.SetBlock(placingBlock.BlockId, pos);
                return true;
            }

            if (belowBlock.LiquidCode == "water")
            {
                return TryPlaceBlockInWater(blockAccessor, pos);
            }

            return false;
        }

        protected virtual bool TryPlaceBlockInWater(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Block belowBlock = blockAccessor.GetBlock(pos.X, pos.Y - 2, pos.Z);
            if (belowBlock.Fertility > 0)
            {
                blockAccessor.SetBlock(blockAccessor.GetBlock(CodeWithParts("water", "normal")).BlockId, pos.AddCopy(0, -1, 0));
                return true;
            }
            return false;
        }

        public override int GetRandomColor(ICoreClientAPI capi , BlockPos pos, BlockFacing facing)
        {
            return capi.ApplyColorTintOnRgba(1, capi.BlockTextureAtlas.GetRandomPixel(Textures.Last().Value.Baked.TextureSubId), pos.X, pos.Y, pos.Z);
        }

        
    }
}
