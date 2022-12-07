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
        public float CapacitySeconds = 32;


        public static SimpleParticleProperties WaterParticles;

        ILoadedSound pouringLoop;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            WaterParticles = new SimpleParticleProperties(
                1, 1, ColorUtil.WhiteArgb, new Vec3d(), new Vec3d(),
                new Vec3f(-1.5f, 0, -1.5f), new Vec3f(1.5f, 3f, 1.5f), 1f, 1f, 0.33f, 0.75f, EnumParticleModel.Cube
            );

            WaterParticles.AddPos = new Vec3d(0.125 / 2, 2 / 16f, 0.125 / 2);
            WaterParticles.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -1f);
            WaterParticles.ClimateColorMap = "climateWaterTint";
            WaterParticles.AddQuantity = 1;

            CapacitySeconds = Attributes?["capacitySeconds"].AsFloat(32) ?? 32;
        }       

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null)
            {
               base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
               return;
            }

            if (byEntity.Controls.ShiftKey) return;

            
            slot.Itemstack.TempAttributes.SetFloat("secondsUsed", 0);

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position, BlockLayersAccess.Fluid);
            if (block.LiquidCode == "water")
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
            Block contentBlock = bucket?.GetContent(blockSel.Position)?.Block;
            if (bucket != null && contentBlock?.LiquidCode == "water")
            {
                var liquidProps = contentBlock.Attributes["waterTightContainerProps"].AsObject<WaterTightContainableProps>(null, block.Code.Domain);
                int quantityItems = (int)(5 / liquidProps.ItemsPerLitre);
                float litres = bucket.GetCurrentLitres(blockSel.Position);

                BlockPos pos = blockSel.Position;
                ItemStack takenWater = bucket.TryTakeContent(blockSel.Position, quantityItems);
                SetRemainingWateringSeconds(slot.Itemstack, CapacitySeconds * takenWater.StackSize * liquidProps.ItemsPerLitre);
                slot.Itemstack.TempAttributes.SetInt("refilled", 1);
                slot.MarkDirty();

                byEntity.World.PlaySoundAt(new AssetLocation("sounds/block/water"), pos.X, pos.Y, pos.Z, byPlayer);
                handHandling = EnumHandHandling.PreventDefault;
                return;
            }


            slot.Itemstack.TempAttributes.SetInt("refilled", 0);
            float remainingwater = GetRemainingWateringSeconds(slot.Itemstack);
            if (remainingwater <= 0) return;

            if (byEntity.World.Side == EnumAppSide.Client)
            {
                byEntity.World.RegisterCallback(After350ms, 350);
            }

            handHandling = EnumHandHandling.PreventDefault;
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
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


        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return GetHandbookDropsFromBreakDrops(handbookStack, forPlayer);
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
            if (blockSel == null) return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel);
            if (slot.Itemstack.TempAttributes.GetInt("refilled") > 0) return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel);

            float prevsecondsused = slot.Itemstack.TempAttributes.GetFloat("secondsUsed");
            slot.Itemstack.TempAttributes.SetFloat("secondsUsed", secondsUsed);

            float remainingwater = GetRemainingWateringSeconds(slot.Itemstack);
            SetRemainingWateringSeconds(slot.Itemstack, remainingwater-= secondsUsed - prevsecondsused);
            

            if (remainingwater <= 0) return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel);

            IWorldAccessor world = byEntity.World;

            BlockPos targetPos = blockSel.Position;

            if (api.World.Side == EnumAppSide.Server)
            {
                var beburningBh = world.BlockAccessor.GetBlockEntity(blockSel.Position.AddCopy(blockSel.Face))?.GetBehavior<BEBehaviorBurning>();
                if (beburningBh != null) beburningBh.KillFire(false);

                beburningBh = world.BlockAccessor.GetBlockEntity(blockSel.Position)?.GetBehavior<BEBehaviorBurning>();
                if (beburningBh != null) beburningBh.KillFire(false);

                Vec3i voxelPos = new Vec3i();
                for (int dx = -2; dx < 2; dx++)
                {
                    for (int dy = -2; dy < 2; dy++)
                    {
                        for (int dz = -2; dz < 2; dz++)
                        {
                            int x = (int)(blockSel.HitPosition.X * 16);
                            int y = (int)(blockSel.HitPosition.Y * 16);
                            int z = (int)(blockSel.HitPosition.Z * 16);
                            if (x + dx < 0 || x + dx > 15 || y + dy < 0 || y + dy > 15 || z + dz < 0 || z + dz > 15) continue;

                            voxelPos.Set(x + dx, y + dy, z + dz);

                            int faceAndSubPosition = CollectibleBehaviorArtPigment.BlockSelectionToSubPosition(blockSel.Face, voxelPos);
                            Block decorblock = world.BlockAccessor.GetDecor(blockSel.Position, faceAndSubPosition);

                            if (decorblock?.FirstCodePart() == "caveart")
                            {
                                world.BlockAccessor.BreakDecor(blockSel.Position, blockSel.Face, faceAndSubPosition);
                            }
                        }
                    }
                }
            }

            Block block = world.BlockAccessor.GetBlock(blockSel.Position);
            bool notOnSolidblock = false;
            if (block.CollisionBoxes == null || block.CollisionBoxes.Length == 0)
            {
                block = world.BlockAccessor.GetBlock(blockSel.Position, BlockLayersAccess.Fluid);
                if ((block.CollisionBoxes == null || block.CollisionBoxes.Length == 0) && !block.IsLiquid())
                {
                    notOnSolidblock = true;
                    targetPos = targetPos.DownCopy();
                }
            }

            BlockEntityFarmland be = world.BlockAccessor.GetBlockEntity(targetPos) as BlockEntityFarmland;
            if (be != null)
            {
                be.WaterFarmland(secondsUsed - prevsecondsused);
            }
            
            float speed = 3f;            

            if (world.Side == EnumAppSide.Client)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                tf.Origin.Set(0.5f, 0.2f, 0.5f);
                tf.Translation.Set(-Math.Min(0.25f, speed * secondsUsed / 2), 0, 0);                
                tf.Rotation.Z = GameMath.Min(60, secondsUsed * 90 * speed, 120 - remainingwater * 4);
                byEntity.Controls.UsingHeldItemTransformBefore = tf;
            }

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);


            if (secondsUsed > 1 / speed)
            {
                Vec3d pos = blockSel.Position.ToVec3d().Add(blockSel.HitPosition);
                if (notOnSolidblock) pos.Y = (int)pos.Y + 0.05;
                WaterParticles.MinPos = pos.Add(-0.125 / 2, 1 / 16f, -0.125 / 2);
                byEntity.World.SpawnParticles(WaterParticles, byPlayer);
            }

            return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel);
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


        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool val = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (val)
            {
                BlockEntityWateringCan bect = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityWateringCan;
                if (bect != null)
                {
                    BlockPos targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
                    double dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
                    double dz = byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
                    float angleHor = (float)Math.Atan2(dx, dz);

                    float deg22dot5rad = GameMath.PIHALF / 4;
                    float roundRad = ((int)Math.Round(angleHor / deg22dot5rad)) * deg22dot5rad;
                    bect.MeshAngle = roundRad;
                }
            }

            return val;
        }





        public float GetRemainingWateringSeconds(ItemStack stack)
        {
            return stack.Attributes.GetFloat("wateringSeconds", 0);
        }

        public void SetRemainingWateringSeconds(ItemStack stack, float seconds)
        {
            stack.Attributes.SetFloat("wateringSeconds", seconds);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            
            dsc.AppendLine();            

            double perc = Math.Round(100 * GetRemainingWateringSeconds(inSlot.Itemstack) / CapacitySeconds);
            string colorn = ColorUtil.Int2Hex(GuiStyle.DamageColorGradient[(int)Math.Min(99, perc)]);

            if (perc < 1)
            {
                dsc.AppendLine(string.Format("<font color=\"{0}\">" + Lang.Get("Empty") + "</font>", colorn));
            } else
            {
                dsc.AppendLine(string.Format("<font color=\"{0}\">" + Lang.Get("{0}% full", perc) + "</font>", colorn));
            }
            
        }

    }
}
