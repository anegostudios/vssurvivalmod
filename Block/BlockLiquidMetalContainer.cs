using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockLiquidMetalContainer : Block
    {
        public static SimpleParticleProperties smokeHeld;
        public static SimpleParticleProperties smokePouring;
        public static SimpleParticleProperties bigMetalSparks;

        static BlockLiquidMetalContainer()
        {
            smokeHeld = new SimpleParticleProperties(
                1, 1,
                ColorUtil.ColorFromArgb(50, 220, 220, 220),
                new Vec3d(),
                new Vec3d(),
                new Vec3f(-0.25f, 0.1f, -0.25f),
                new Vec3f(0.25f, 0.1f, 0.25f),
                1.5f,
                -0.075f,
                0.25f,
                0.25f,
                EnumParticleModel.Quad
            );
            smokeHeld.addPos.Set(0.1, 0.1, 0.1);

            smokePouring = new SimpleParticleProperties(
                1, 2,
                ColorUtil.ColorFromArgb(50, 220, 220, 220),
                new Vec3d(),
                new Vec3d(),
                new Vec3f(-0.5f, 0f, -0.5f),
                new Vec3f(0.5f, 0f, 0.5f),
                1.5f,
                -0.1f,
                0.75f,
                0.75f,
                EnumParticleModel.Quad
            );
            smokePouring.addPos.Set(0.3, 0.3, 0.3);

            bigMetalSparks = new SimpleParticleProperties(
                1, 1,
                ColorUtil.ColorFromArgb(255, 83, 233, 255),
                new Vec3d(), new Vec3d(),
                new Vec3f(-3f, 1f, -3f),
                new Vec3f(3f, 8f, 3f),
                0.5f,
                1f,
                0.25f, 0.25f
            );
            bigMetalSparks.glowLevel = 128;
        }


        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel)
        {
            if (!world.TestPlayerAccessBlock(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return false;
            }

            if (!byPlayer.Entity.Controls.Sneak) return false;
            if (IsSuitablePosition(world, blockSel.Position) && world.BlockAccessor.GetBlock(blockSel.Position.DownCopy()).SideSolid[BlockFacing.UP.Index])
            {
                DoPlaceBlock(world, blockSel.Position, blockSel.Face, itemstack);

                BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
                if (be is BlockEntityLiquidMetalContainer)
                {
                    BlockEntityLiquidMetalContainer belmc = (BlockEntityLiquidMetalContainer)be;
                    KeyValuePair<ItemStack, int> contents = GetContents(world, itemstack);
                    contents.Key.Collectible.SetTemperature(world, contents.Key, GetTemperature(world, itemstack));
                    belmc.contents = contents.Key;
                    belmc.units = contents.Value;
                }

                return true;
            }

            return false;
        }


        public override void OnHeldIdle(IItemSlot slot, IEntityAgent byEntity)
        {
            if (byEntity.World is IClientWorldAccessor && byEntity.World.Rand.NextDouble() < 0.02)
            {
                KeyValuePair<ItemStack, int> contents = GetContents(byEntity.World, slot.Itemstack);

                if (contents.Key != null && !HasSolidifed(slot.Itemstack, contents.Key, byEntity.World))
                {
                    Vec3d pos =
                        byEntity.Pos.XYZ.Add(0, byEntity.EyeHeight() - 0.5f, 0)
                        .Ahead(0.3f, byEntity.Pos.Pitch, byEntity.Pos.Yaw)
                        .Ahead(0.47f, 0, byEntity.Pos.Yaw + GameMath.PIHALF)
                    ;

                    smokeHeld.minPos = pos.AddCopy(-0.05, -0.05, -0.05);
                    byEntity.World.SpawnParticles(smokeHeld);
                } 
            }
        }

        public override bool OnHeldInteractStart(IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return false;

            ILiquidMetalSink be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as ILiquidMetalSink;

            if (be != null && be.CanReceiveAny)
            {
                KeyValuePair<ItemStack, int> contents = GetContents(byEntity.World, slot.Itemstack);

                if (HasSolidifed(slot.Itemstack, contents.Key, byEntity.World))
                {
                    return false;
                }

                if (contents.Value <= 0) return false;
                if (!be.CanReceive(contents.Key)) return false;
                be.BeginFill(blockSel.HitPosition);

                byEntity.World.RegisterCallback((world, pos, dt) =>
                {
                    if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemInteract)
                    {
                        IPlayer byPlayer = null;
                        if (byEntity is IEntityPlayer) byPlayer = byEntity.World.PlayerByUid(((IEntityPlayer)byEntity).PlayerUID);

                        world.PlaySoundAt(new AssetLocation("sounds/hotmetal"), byEntity, byPlayer);
                    }
                }, blockSel.Position, 666);
                
                return true;
            }

            return false;
        }


        public override bool OnHeldInteractStep(float secondsUsed, IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return false;

            ILiquidMetalSink be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as ILiquidMetalSink; 
            if (be == null) return false;

            if (!be.CanReceiveAny) return false;
            KeyValuePair<ItemStack, int> contents = GetContents(byEntity.World, slot.Itemstack);
            if (!be.CanReceive(contents.Key)) return false;

            float speed = 1.5f;
            float temp = GetTemperature(byEntity.World, slot.Itemstack);

            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                tf.Origin.Set(0.5f, 0.2f, 0.5f);
                tf.Translation.Set(0, 0, -Math.Min(0.25f, speed * secondsUsed / 4));
                tf.Scale = 1f + Math.Min(0.25f, speed * secondsUsed / 4);
                tf.Rotation.X = Math.Max(-110, -secondsUsed * 90 * speed);
                byEntity.Controls.UsingHeldItemTransform = tf;
            }

            IPlayer byPlayer = null;
            if (byEntity is IEntityPlayer) byPlayer = byEntity.World.PlayerByUid(((IEntityPlayer)byEntity).PlayerUID);


            if (secondsUsed > 1 / speed)
            {
                if ((int)(30 * secondsUsed) % 3 == 1)
                {
                    Vec3d pos = 
                        byEntity.Pos.XYZ
                        .Ahead(0.1f, byEntity.Pos.Pitch, byEntity.Pos.Yaw)
                        .Ahead(1.0f, byEntity.Pos.Pitch, byEntity.Pos.Yaw - GameMath.PIHALF)
                    ;
                    pos.Y += byEntity.EyeHeight() - 0.4f;

                    smokePouring.minPos = pos.AddCopy(-0.15, -0.15, -0.15);

                    Vec3d blockpos = blockSel.Position.ToVec3d().Add(0.5, 0.2, 0.5);

                    bigMetalSparks.minQuantity = Math.Max(0.2f, 1 - (secondsUsed - 1) / 4);

                    if ((int)(30 * secondsUsed) % 7 == 1)
                    {
                        bigMetalSparks.minPos = pos;
                        bigMetalSparks.minVelocity.Set(-2, -1, -2);
                        bigMetalSparks.addVelocity.Set(4, 1, 4);
                        byEntity.World.SpawnParticles(bigMetalSparks, byPlayer);

                        byEntity.World.SpawnParticles(smokePouring, byPlayer);
                    }

                    float y2 = 0;
                    Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
                    Cuboidf[] collboxs = block.GetCollisionBoxes(byEntity.World.BlockAccessor, blockSel.Position);
                    for (int i = 0; collboxs != null && i < collboxs.Length; i++)
                    {
                        y2 = Math.Max(y2, collboxs[i].Y2);
                    }

                    // Metal Spark on the mold
                    bigMetalSparks.minVelocity.Set(-2, 1, -2);
                    bigMetalSparks.addVelocity.Set(4, 5, 4);
                    bigMetalSparks.minPos = blockpos.AddCopy(-0.25, y2 - 2/16f, -0.25);
                    bigMetalSparks.addPos.Set(0.5, 0, 0.5);
                    bigMetalSparks.glowLevel = (byte)GameMath.Clamp((int)temp - 770, 48, 128);
                    byEntity.World.SpawnParticles(bigMetalSparks, byPlayer);

                    // Smoke on the mold
                    byEntity.World.SpawnParticles(
                        Math.Max(1, 12 - (secondsUsed-1) * 6),
                        ColorUtil.ColorFromArgb(50, 220, 220, 220),
                        blockpos.AddCopy(-0.5, y2 - 2 / 16f, -0.5),
                        blockpos.Add(0.5, y2 - 2 / 16f + 0.15, 0.5),
                        new Vec3f(-0.5f, 0f, -0.5f),
                        new Vec3f(0.5f, 0f, 0.5f),
                        1.5f,
                        -0.05f,
                        0.75f,
                        EnumParticleModel.Quad,
                        byPlayer
                    );

                }

                int transferedAmount = Math.Min(2, contents.Value);

                
                be.ReceiveLiquidMetal(contents.Key, ref transferedAmount, temp);

                int newAmount = Math.Max(0, contents.Value - (2 - transferedAmount));
                slot.Itemstack.Attributes.SetInt("units", newAmount);
                

                if (newAmount == 0 && byEntity.World is IServerWorldAccessor)
                {
                    slot.Itemstack = new ItemStack(byEntity.World.GetBlock(new AssetLocation("crucible-burned")));
                    slot.MarkDirty();
                    // Since we change the item stack we have to call this ourselves
                    OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
                    return false;
                }

                return true;
            }
            
            return true;
        }


        public override void OnHeldInteractStop(float secondsUsed, IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            slot.MarkDirty();

            if (blockSel == null) return;
                 
            ILiquidMetalSink be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as ILiquidMetalSink;
            be?.OnPourOver();
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return GetDrops(world, pos, null)[0];
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            ItemStack[] stacks = base.GetDrops(world, pos, byPlayer);

            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityLiquidMetalContainer)
            {
                BlockEntityLiquidMetalContainer belmc = (BlockEntityLiquidMetalContainer)be;
                SetContents(stacks[0], belmc.contents, belmc.units);
                belmc.contents?.ResolveBlockOrItem(world);
                stacks[0].Collectible.SetTemperature(world, stacks[0], belmc.contents.Collectible.GetTemperature(world, belmc.contents));
            }

            return stacks;
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityLiquidMetalContainer)
            {
                BlockEntityLiquidMetalContainer belmc = (BlockEntityLiquidMetalContainer)be;
                belmc.contents.ResolveBlockOrItem(world);

                return
                    "Units: " + (int)(belmc.units) + "\n" +
                    "Metal: " + belmc.contents.GetName() + "\n" +
                    "Temperature: " + (int)belmc.Temperature + " °C\n"
                ;
            }

            return base.GetPlacedBlockInfo(world, pos, forPlayer);
        }


        public override void GetHeldItemInfo(ItemStack stack, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            KeyValuePair<ItemStack, int> contents = GetContents(world, stack);

            if (contents.Key != null)
            {
                // cheap hax to get metal name
                string name = contents.Key.GetName();
                string metal = name.Substring(name.IndexOf("(") + 1, name.Length - 2 - name.IndexOf("("));

                dsc.Append(string.Format("{0} units of {1}\n", (int)(contents.Value), metal));

                if (HasSolidifed(stack, contents.Key, world))
                {
                    dsc.Append(string.Format("Solidified, reheat to melt again\n"));
                }
            }

            

            base.GetHeldItemInfo(stack, dsc, world, withDebugInfo);
        }


        public bool HasSolidifed(ItemStack ownStack, ItemStack contentstack, IWorldAccessor world)
        {
            return ownStack.Collectible.GetTemperature(world, ownStack) < 0.9 * contentstack.Collectible.GetMeltingPoint(world, null, null);
        }

        internal void SetContents(ItemStack stack, ItemStack output, int units)
        {
            stack.Attributes.SetItemstack("output", output);
            stack.Attributes.SetInt("units", units);
        }

        KeyValuePair<ItemStack, int> GetContents(IWorldAccessor world, ItemStack stack)
        {
            ItemStack outstack = stack.Attributes.GetItemstack("output");
            if (outstack != null)
            {
                outstack.ResolveBlockOrItem(world);
            }
            return new KeyValuePair<ItemStack, int>(
                outstack,
                stack.Attributes.GetInt("units")
            );
        }
    }
}
