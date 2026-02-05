using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockLakeIce : BlockForFluidsLayer
    {
        protected int waterBlock;
        protected bool stayIceWhenBroken;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            waterBlock = api.World.GetBlock(new AssetLocation("water-still-7")).BlockId;
            stayIceWhenBroken = Attributes?["stayIceWhenBroken"].AsBool(false) ?? false;
        }


        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            bool preventDefault = false;
            foreach (BlockBehavior behavior in BlockBehaviors)
            {
                EnumHandling handled = EnumHandling.PassThrough;

                behavior.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier, ref handled);
                if (handled == EnumHandling.PreventDefault) preventDefault = true;
                if (handled == EnumHandling.PreventSubsequent) return;
            }

            if (preventDefault) return;

            if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
            {
                ItemStack[] drops = GetDrops(world, pos, byPlayer, dropQuantityMultiplier);

                if (drops != null)
                {
                    for (int i = 0; i < drops.Length; i++)
                    {
                        if (SplitDropStacks)
                        {
                            for (int k = 0; k < drops[i].StackSize; k++)
                            {
                                ItemStack stack = drops[i].Clone();
                                stack.StackSize = 1;
                                world.SpawnItemEntity(stack, pos, null);
                            }
                        }

                    }
                }

                if (Sounds != null) world.PlaySoundAt(Sounds.GetBreakSound(byPlayer), pos, -0.5, byPlayer);
            }

            SpawnBlockBrokenParticles(pos);

            if (stayIceWhenBroken)
            {
                world.BlockAccessor.SetBlock(0, pos, BlockLayersAccess.Fluid);
                return;
            }

            world.BlockAccessor.SetBlock(waterBlock, pos, BlockLayersAccess.Fluid);
        }

        public override bool ShouldReceiveServerGameTicks(IWorldAccessor world, BlockPos pos, Random offThreadRandom, out object extra)
        {
            extra = null;
            float temperature = world.BlockAccessor.GetClimateAt(pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, api.World.Calendar.TotalDays).Temperature;
            float chance = GameMath.Clamp((temperature - 2f) / 20f, 0, 1);
            return offThreadRandom.NextDouble() < chance;
        }

        public override void OnServerGameTick(IWorldAccessor world, BlockPos pos, object extra = null)
        {
            world.BlockAccessor.SetBlock(waterBlock, pos, BlockLayersAccess.Fluid);
        }

        public override bool ShouldMergeFace(int facingIndex, Block neighbourIce, int intraChunkIndex3d)
        {
            return BlockMaterial == neighbourIce.BlockMaterial;
        }

        BlockPos tmpPos = new BlockPos(API.Config.Dimensions.WillSetLater);
        public override void CoolNow(ItemSlot slot, Vec3d pos, float dt, bool playSizzle = true)
        {
            var world = api.World;
            if (!slot.Itemstack.TempAttributes.HasAttribute("thawStart"))
            {
                slot.Itemstack.TempAttributes.SetLong("thawStart", world.Calendar.ElapsedSeconds);
            }

            float stackTemp = slot.Itemstack.Collectible.GetTemperature(world, slot.Itemstack);
            if (stackTemp <= coolingMediumTemperature) return;

            var nextTemperature = Math.Max(20, stackTemp - 2) * dt * 50;

            if (slot.Empty) return;
            slot.Itemstack.Collectible.SetTemperature(world, slot.Itemstack, nextTemperature);

            float tempDiff = stackTemp - coolingMediumTemperature;

            if (tempDiff > 90)
            {
                double width = 0.0; // EntityItem SelectionBox.XSize;
                Entity.SplashParticleProps.BasePos.Set(pos.X - width / 2, pos.Y - 0.75, pos.Z - width / 2);
                Entity.SplashParticleProps.AddVelocity.Set(0, 0, 0);
                Entity.SplashParticleProps.QuantityMul = 0.1f;
                world.SpawnParticles(Entity.SplashParticleProps);
            }

            long lastPlayedSizzlesTotalMs = slot.Itemstack.TempAttributes.GetLong("lastPlayedSizzlesTotalMs", -99999);

            if (playSizzle && tempDiff > 200 && world.Side == EnumAppSide.Client && world.ElapsedMilliseconds - lastPlayedSizzlesTotalMs > 10000)
            {
                world.PlaySoundAt(new AssetLocation("sounds/sizzle"), pos.X, pos.Y, pos.Z);
                slot.Itemstack.TempAttributes.SetLong("lastPlayedSizzlesTotalMs", world.ElapsedMilliseconds);
            }

            if (api.Side == EnumAppSide.Client) return;
            long secondsPassed = world.Calendar.ElapsedSeconds - slot.Itemstack.TempAttributes.GetLong("thawStart");
            if (secondsPassed < 120) return;
            slot.Itemstack.TempAttributes.RemoveAttribute("thawStart");

            tmpPos.Set(pos.XInt, pos.YInt, pos.ZInt);
            if (world.BlockAccessor.GetBlock(tmpPos) is not BlockLakeIce) tmpPos.Down();
            if (world.BlockAccessor.GetBlock(tmpPos) is BlockLakeIce)
            {
                world.BlockAccessor.SetBlock(waterBlock, tmpPos, BlockLayersAccess.Fluid);
            }
        }

        public override bool CanCool(ItemSlot slot, Vec3d pos) => slot is EntityItemSlot;
    }
}
