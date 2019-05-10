using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockWateringCan : Block
    {
        public float CapacitySeconds = 16;


        public static SimpleParticleProperties WaterParticles;

        ILoadedSound pouringLoop;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            WaterParticles = new SimpleParticleProperties(
                1, 1, ColorUtil.WhiteArgb, new Vec3d(), new Vec3d(),
                new Vec3f(-1.5f, 0, -1.5f), new Vec3f(1.5f, 3f, 1.5f), 1f, 1f, 0.33f, 0.75f, EnumParticleModel.Cube
            );

            WaterParticles.addPos = new Vec3d(0.125 / 2, 2 / 16f, 0.125 / 2);
            WaterParticles.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -1f);
            WaterParticles.TintIndex = 2;
            WaterParticles.addQuantity = 1;
        }
        

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null) return;
            if (byEntity.Controls.Sneak) return;

            slot.Itemstack.TempAttributes.SetFloat("secondsUsed", 0);

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

            if (byEntity.World.BlockAccessor.GetBlock(blockSel.Position).LiquidCode == "water")
            {
                BlockPos pos = blockSel.Position;
                SetRemainingWateringSeconds(slot.Itemstack, CapacitySeconds);
                slot.Itemstack.TempAttributes.SetInt("refilled", 1);
                slot.MarkDirty();

                byEntity.World.PlaySoundAt(new AssetLocation("sounds/block/water"), pos.X, pos.Y, pos.Z, byPlayer);
                handHandling = EnumHandHandling.PreventDefault;
                return;
            }

            BlockBucket bucket = byEntity.World.BlockAccessor.GetBlock(blockSel.Position) as BlockBucket;
            if (bucket != null && bucket.GetContent(byEntity.World, blockSel.Position)?.Block?.LiquidCode == "water")
            {
                BlockPos pos = blockSel.Position;
                ItemStack takenWater = bucket.TryTakeContent(byEntity.World, blockSel.Position, 5);
                SetRemainingWateringSeconds(slot.Itemstack, CapacitySeconds * takenWater.StackSize / 5f);
                slot.Itemstack.TempAttributes.SetInt("refilled", 1);
                slot.MarkDirty();

                byEntity.World.PlaySoundAt(new AssetLocation("sounds/block/water"), pos.X, pos.Y, pos.Z, byPlayer);
                handHandling = EnumHandHandling.PreventDefault;
            }


            slot.Itemstack.TempAttributes.SetInt("refilled", 0);
            float remainingwater = GetRemainingWateringSeconds(slot.Itemstack);
            if (remainingwater <= 0) return;

            if (byEntity.World.Side == EnumAppSide.Client)
            {
                byEntity.World.RegisterCallback(After350ms, 350);
            }

            handHandling = EnumHandHandling.PreventDefault;
        }

        private void After350ms(float dt)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            IClientPlayer plr = capi.World.Player;
            EntityPlayer plrentity = plr.Entity;

            if (plrentity.Controls.HandUse == EnumHandInteract.HeldItemInteract)
            {
                capi.World.PlaySoundAt(new AssetLocation("sounds/effect/watering"), plrentity, plr);
            }

            if (plrentity.Controls.HandUse == EnumHandInteract.HeldItemInteract)
            {
                if (pouringLoop != null)
                {
                    pouringLoop.FadeIn(0.3f, null);
                    return;
                }

                pouringLoop = capi.World.LoadSound(new SoundParams()
                {
                    DisposeOnFinish = false,
                    Location = new AssetLocation("sounds/effect/watering-loop.ogg"),
                    Position = new Vec3f(),
                    RelativePosition = true,
                    ShouldLoop = true,
                    Range = 16,
                    Volume = 0.2f,
                    Pitch = 0.5f
                });

                pouringLoop.Start();
                pouringLoop.FadeIn(0.15f, null);
                return;
            }
        }


        public override void OnGroundIdle(EntityItem entityItem)
        {
            base.OnGroundIdle(entityItem);

            if (entityItem.FeetInLiquid)
            {
                SetRemainingWateringSeconds(entityItem.Itemstack, CapacitySeconds);
            }
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            return new ItemStack[] { OnPickBlock(world, pos) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            BlockEntityWateringCan becan = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityWateringCan;
            if (becan != null)
            {
                ItemStack stack = new ItemStack(this);
                SetRemainingWateringSeconds(stack, becan.SecondsWateringLeft);
                return stack;
            }

            return base.OnPickBlock(world, pos);
        }


        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return false;
            if (slot.Itemstack.TempAttributes.GetInt("refilled") > 0) return false;

            float prevsecondsused = slot.Itemstack.TempAttributes.GetFloat("secondsUsed");
            slot.Itemstack.TempAttributes.SetFloat("secondsUsed", secondsUsed);

            float remainingwater = GetRemainingWateringSeconds(slot.Itemstack);
            SetRemainingWateringSeconds(slot.Itemstack, remainingwater-= secondsUsed - prevsecondsused);
            

            if (remainingwater <= 0) return false;

            IWorldAccessor world = byEntity.World;

            BlockPos targetPos = blockSel.Position;

            Block facingBlock = world.BlockAccessor.GetBlock(blockSel.Position.AddCopy(blockSel.Face));
            if (facingBlock.Code.Path == "fire")
            {
                world.BlockAccessor.SetBlock(0, blockSel.Position.AddCopy(blockSel.Face));
            }

            Block block = world.BlockAccessor.GetBlock(blockSel.Position);
            bool notOnSolidblock = false;
            if ((block.CollisionBoxes == null || block.CollisionBoxes.Length == 0) && !block.IsLiquid())
            {
                notOnSolidblock = true;
                targetPos = targetPos.DownCopy();
            }

            BlockEntityFarmland be = world.BlockAccessor.GetBlockEntity(targetPos) as BlockEntityFarmland;
            if (be != null)
            {
                be.WaterFarmland(secondsUsed - prevsecondsused);
            }
            
            float speed = 4f;            

            if (world.Side == EnumAppSide.Client)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                tf.Origin.Set(0.5f, 0.2f, 0.5f);
                tf.Translation.Set(-Math.Min(0.25f, speed * secondsUsed / 4), 0, 0);
                
                tf.Rotation.Z = GameMath.Min(60, secondsUsed * 90 * speed, 80 - remainingwater * 5);
                byEntity.Controls.UsingHeldItemTransformBefore = tf;
            }

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);


            if (secondsUsed > 1 / speed)
            {
                Vec3d pos = blockSel.Position.ToVec3d().Add(blockSel.HitPosition);
                if (notOnSolidblock) pos.Y = (int)pos.Y + 0.05;
                WaterParticles.minPos = pos.Add(-0.125 / 2, 1 / 16f, -0.125 / 2);
                byEntity.World.SpawnParticles(WaterParticles, byPlayer);
            }

            return true;
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            return base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason);
        }


        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            pouringLoop?.Stop();
            pouringLoop?.Dispose();
            pouringLoop = null;

            slot.MarkDirty();
        }

        public float GetRemainingWateringSeconds(ItemStack stack)
        {
            return stack.Attributes.GetFloat("wateringSeconds", 0);
        }

        public void SetRemainingWateringSeconds(ItemStack stack, float seconds)
        {
            stack.Attributes.SetFloat("wateringSeconds", seconds);
        }

        public override void GetHeldItemInfo(ItemStack stack, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(stack, dsc, world, withDebugInfo);

            double perc = Math.Round(100 * GetRemainingWateringSeconds(stack) / CapacitySeconds);
            if (perc < 1)
            {
                dsc.AppendLine(Lang.Get("Empty"));
            } else
            {
                dsc.AppendLine(Lang.Get("{0}% full", perc));
            }
            
        }

    }
}
