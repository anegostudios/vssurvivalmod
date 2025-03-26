using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockSmeltedContainer : Block
    {
        public static SimpleParticleProperties smokeHeld;
        public static SimpleParticleProperties smokePouring;
        public static SimpleParticleProperties bigMetalSparks;

        static BlockSmeltedContainer()
        {
            smokeHeld = new SimpleParticleProperties(
                1, 1,
                ColorUtil.ToRgba(50, 180, 180, 180),
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
            smokeHeld.AddPos.Set(0.1, 0.1, 0.1);

            smokePouring = new SimpleParticleProperties(
                1, 2,
                ColorUtil.ToRgba(50, 180, 180, 180),
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
            smokePouring.AddPos.Set(0.3, 0.3, 0.3);

            bigMetalSparks = new SimpleParticleProperties(
                1, 1,
                ColorUtil.ToRgba(255, 255, 169, 83),
                new Vec3d(), new Vec3d(),
                new Vec3f(-3f, 1f, -3f),
                new Vec3f(3f, 8f, 3f),
                0.5f,
                1f,
                0.25f, 0.25f
            );
            bigMetalSparks.VertexFlags = 128;
        }

        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity)
        {
            return "pour";
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
            /*(if (!byPlayer.Entity.Controls.Sneak || world.BlockAccessor.GetBlockEntity(blockSel.Position.DownCopy()) is ILiquidMetalSink)
            {
                failureCode = "__ignore__";
                return false;
            }

            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode)) return false;

            if (world.BlockAccessor.GetBlock(blockSel.Position.DownCopy()).CanAttachBlockAt(world.BlockAccessor, this, blockSel.Position, BlockFacing.UP))
            {
                DoPlaceBlock(world, byPlayer, blockSel, itemstack);

                BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
                if (be is BlockEntitySmeltedContainer)
                {
                    BlockEntitySmeltedContainer belmc = (BlockEntitySmeltedContainer)be;
                    KeyValuePair<ItemStack, int> contents = GetContents(world, itemstack);
                    contents.Key.Collectible.SetTemperature(world, contents.Key, GetTemperature(world, itemstack));
                    belmc.contents = contents.Key.Clone();
                    belmc.units = contents.Value;
                }
                return true;
            }

            failureCode = "requiresolidground";

            return false;*/
        }


        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            if (byEntity.World is IClientWorldAccessor && byEntity.World.Rand.NextDouble() < 0.02)
            {
                KeyValuePair<ItemStack, int> contents = GetContents(byEntity.World, slot.Itemstack);

                if (contents.Key != null && !HasSolidifed(slot.Itemstack, contents.Key, byEntity.World))
                {
                    Vec3d pos =
                        byEntity.Pos.XYZ.Add(byEntity.LocalEyePos.X, byEntity.LocalEyePos.Y - 0.5f, byEntity.LocalEyePos.Z)
                        .Ahead(0.3f, byEntity.Pos.Pitch, byEntity.Pos.Yaw)
                        .Ahead(0.47f, 0, byEntity.Pos.Yaw + GameMath.PIHALF)
                    ;

                    smokeHeld.MinPos = pos.AddCopy(-0.05, -0.05, -0.05);
                    byEntity.World.SpawnParticles(smokeHeld);
                } 
            }
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null) return;

            ILiquidMetalSink be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as ILiquidMetalSink;

            if (be != null)
            {
                handHandling = EnumHandHandling.PreventDefault;
            }

            if (be != null && be.CanReceiveAny)
            {
                KeyValuePair<ItemStack, int> contents = GetContents(byEntity.World, slot.Itemstack);

                if (contents.Key == null)
                {
                    string emptiedCode = Attributes["emptiedBlockCode"].AsString();

                    slot.Itemstack = new ItemStack(byEntity.World.GetBlock(AssetLocation.Create(emptiedCode, Code.Domain)));
                    slot.MarkDirty();
                    handHandling = EnumHandHandling.PreventDefault;
                    return;
                }
                

                if (HasSolidifed(slot.Itemstack, contents.Key, byEntity.World))
                {
                    handHandling = EnumHandHandling.NotHandled;
                    return;
                }

                if (contents.Value <= 0) return;
                if (!be.CanReceive(contents.Key)) return;
                be.BeginFill(blockSel.HitPosition);

                byEntity.World.RegisterCallback((world, pos, dt) =>
                {
                    if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemInteract)
                    {
                        IPlayer byPlayer = null;
                        if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

                        world.PlaySoundAt(new AssetLocation("sounds/pourmetal"), byEntity, byPlayer);
                    }
                }, blockSel.Position, 666);

                handHandling = EnumHandHandling.PreventDefault;
            }

            if (handHandling == EnumHandHandling.NotHandled)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
            }
        }


        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return false;

            ILiquidMetalSink be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as ILiquidMetalSink; 
            if (be == null) return false;

            if (!be.CanReceiveAny) return false;
            KeyValuePair<ItemStack, int> contents = GetContents(byEntity.World, slot.Itemstack);
            if (!be.CanReceive(contents.Key)) return false;

            float speed = 1.5f;
            float temp = GetTemperature(byEntity.World, slot.Itemstack);

            EntityPlayer eplr = byEntity as EntityPlayer;
            var player = eplr.Player;

            if (secondsUsed > 1 / speed)
            {
                if (!slot.Itemstack.Attributes.HasAttribute("nowPouringEntityId"))
                {
                    slot.Itemstack.Attributes.SetLong("nowPouringEntityId", byEntity.EntityId);
                    slot.MarkDirty();
                }

                if ((int)(30 * secondsUsed) % 3 == 1)
                {
                    Vec3d pos = 
                        byEntity.Pos.XYZ
                        .Ahead(0.1f, byEntity.Pos.Pitch, byEntity.Pos.Yaw)
                        .Ahead(1.0f, byEntity.Pos.Pitch, byEntity.Pos.Yaw - GameMath.PIHALF)
                    ;
                    pos.Y += byEntity.LocalEyePos.Y - 0.4f;

                    smokePouring.MinPos = pos.AddCopy(-0.15, -0.15, -0.15);

                    Vec3d blockpos = blockSel.Position.ToVec3d().Add(0.5, 0.2, 0.5);

                    bigMetalSparks.MinQuantity = Math.Max(0.2f, 1 - (secondsUsed - 1) / 4);


                    float y2 = 0;
                    Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
                    Cuboidf[] collboxs = block.GetCollisionBoxes(byEntity.World.BlockAccessor, blockSel.Position);
                    for (int i = 0; collboxs != null && i < collboxs.Length; i++)
                    {
                        y2 = Math.Max(y2, collboxs[i].Y2);
                    }

                    // Metal Spark on the mold
                    bigMetalSparks.MinVelocity.Set(-2, 1, -2);
                    bigMetalSparks.AddVelocity.Set(4, 5, 4);
                    bigMetalSparks.MinPos = blockpos.AddCopy(-0.25, y2 - 2/16f, -0.25);
                    bigMetalSparks.AddPos.Set(0.5, 0, 0.5);
                    bigMetalSparks.VertexFlags = (byte)GameMath.Clamp((int)temp - 770, 48, 128);
                    byEntity.World.SpawnParticles(bigMetalSparks, player);

                    // Smoke on the mold
                    byEntity.World.SpawnParticles(
                        Math.Max(1, 12 - (secondsUsed-1) * 6),
                        ColorUtil.ToRgba(50, 180, 180, 180),
                        blockpos.AddCopy(-0.5, y2 - 2 / 16f, -0.5),
                        blockpos.Add(0.5, y2 - 2 / 16f + 0.15, 0.5),
                        new Vec3f(-0.5f, 0f, -0.5f),
                        new Vec3f(0.5f, 0f, 0.5f),
                        1.5f,
                        -0.05f,
                        0.4f,
                        EnumParticleModel.Quad,
                        player
                    );

                }

                int transferedAmount = Math.Min(2, contents.Value);

                
                be.ReceiveLiquidMetal(contents.Key, ref transferedAmount, temp);

                int newAmount = Math.Max(0, contents.Value - (2 - transferedAmount));
                slot.Itemstack.Attributes.SetInt("units", newAmount);
                

                if (newAmount <= 0 && byEntity.World is IServerWorldAccessor)
                {
                    string emptiedCode = Attributes["emptiedBlockCode"].AsString();
                    slot.Itemstack = new ItemStack(byEntity.World.GetBlock(AssetLocation.Create(emptiedCode, Code.Domain)));
                    slot.MarkDirty();
                    // Since we change the item stack we have to call this ourselves
                    OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
                    return false;
                }

                return true;
            }
            
            return true;
        }


        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);

            if (target == EnumItemRenderTarget.HandTp || target == EnumItemRenderTarget.HandTpOff)
            {
                long eid = itemstack.Attributes.GetLong("nowPouringEntityId");
                if (eid != 0)
                {
                    var entity = capi.World.GetEntityById(eid) as EntityAgent;
                    if (entity == null) return;

                    if (eid != capi.World.Player.Entity.EntityId || capi.World.Player.CameraMode != EnumCameraMode.FirstPerson || capi.Settings.Bool["immersiveFpMode"])
                    {
                        SpawnPouringParticles(entity);
                    }
                }
            }
        }

        void SpawnPouringParticles(EntityAgent byEntity)
        {
            EntityPlayer eplr = byEntity as EntityPlayer;
            var player = eplr.Player;

            AttachmentPointAndPose apap = byEntity.AnimManager.Animator.GetAttachmentPointPose("RightHand");
            if (apap != null && api.World.Rand.NextDouble() < 0.25)
            {
                AttachmentPoint ap = apap.AttachPoint;

                float bodyYaw = eplr.BodyYaw;
                float rotX = eplr.Properties.Client.Shape != null ? eplr.Properties.Client.Shape.rotateX : 0;
                float rotY = eplr.Properties.Client.Shape != null ? eplr.Properties.Client.Shape.rotateY : 0;
                float rotZ = eplr.Properties.Client.Shape != null ? eplr.Properties.Client.Shape.rotateZ : 0;
                float bodyPitch = eplr.WalkPitch;


                var mat = new Matrixf()
                    .RotateX(eplr.SidedPos.Roll + rotX * GameMath.DEG2RAD)
                    .RotateY(bodyYaw + (180 + rotY) * GameMath.DEG2RAD)
                    .RotateZ(bodyPitch + rotZ * GameMath.DEG2RAD)
                    .Scale(eplr.Properties.Client.Size, eplr.Properties.Client.Size, eplr.Properties.Client.Size)
                    .Translate(-0.5f, 0, -0.5f)
                    .RotateX(eplr.sidewaysSwivelAngle)
                    .Translate(ap.PosX / 16f, ap.PosY / 16f, ap.PosZ / 16f)
                    .Mul(apap.AnimModelMatrix)
                    .Translate(-0.15f, 0.0f, 0.15f)
                ;

                float[] pos = new float[4] { 0, 0f, 0, 1 };
                float[] endVec = Mat4f.MulWithVec4(mat.Values, pos);

                bigMetalSparks.GravityEffect = 0.5f;
                bigMetalSparks.Bounciness = 0.6f;
                bigMetalSparks.MinQuantity = 1;
                bigMetalSparks.AddQuantity = 1;
                bigMetalSparks.MinPos = new Vec3d(eplr.Pos.X + endVec[0], eplr.Pos.Y + endVec[1], eplr.Pos.Z + endVec[2]);
                bigMetalSparks.AddPos.Set(0, 0, 0);
                bigMetalSparks.MinSize = 0.75f;

                float dx = (float)Math.Sin(bodyYaw + GameMath.PIHALF) / 2f;
                float dz = (float)Math.Cos(bodyYaw + GameMath.PIHALF) / 2f;

                bigMetalSparks.MinVelocity.Set(-0.1f + dx, -1, -0.1f + dz);
                bigMetalSparks.AddVelocity.Set(0.2f + dz, 1, 0.2f + dz);
                byEntity.World.SpawnParticles(bigMetalSparks, eplr?.Player);

                byEntity.World.SpawnParticles(
                    4,
                    ColorUtil.ToRgba(50, 180, 180, 180),
                    bigMetalSparks.MinPos,
                    bigMetalSparks.MinPos.AddCopy(dx / 5f, -0.3, dz / 5f),
                    new Vec3f(-0.5f, 0f, -0.5f),
                    new Vec3f(0.5f, 0f, 0.5f),
                    1.5f,
                    -0.05f,
                    0.4f,
                    EnumParticleModel.Quad,
                    eplr.Player
                );
            }
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            slot.Itemstack?.Attributes.RemoveAttribute("nowPouringEntityId");
            slot.MarkDirty();

            if (blockSel == null) return;
                 
            ILiquidMetalSink be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as ILiquidMetalSink;
            be?.OnPourOver();
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            slot.Itemstack?.Attributes.RemoveAttribute("nowPouringEntityId");

            return base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return GetDrops(world, pos, null)[0];
        }

        
        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return GetHandbookDropsFromBreakDrops(handbookStack, forPlayer);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            ItemStack[] stacks = base.GetDrops(world, pos, byPlayer);

            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntitySmeltedContainer)
            {
                BlockEntitySmeltedContainer belmc = (BlockEntitySmeltedContainer)be;
                ItemStack contents = belmc.contents.Clone();
                SetContents(stacks[0], contents, belmc.units);
                belmc.contents?.ResolveBlockOrItem(world);
                stacks[0].Collectible.SetTemperature(world, stacks[0], belmc.contents.Collectible.GetTemperature(world, contents));
            }

            return stacks;
        }


        public override string GetHeldItemName(ItemStack itemStack)
        {
            KeyValuePair<ItemStack, int> contents = GetContents(api.World, itemStack);

            string mat = contents.Key?.Collectible?.Variant["metal"];
            string contentsLocalized = mat == null ? contents.Key?.GetName() : Lang.Get("material-" + mat);

            if (HasSolidifed(itemStack, contents.Key, api.World))
            {
                return Lang.Get("Crucible (Contains solidified {0})", contentsLocalized);
            } else
            {
                return Lang.Get("Crucible (Contains molten {0})", contentsLocalized);
            }
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntitySmeltedContainer)
            {
                BlockEntitySmeltedContainer belmc = (BlockEntitySmeltedContainer)be;
                belmc.contents.ResolveBlockOrItem(world);

                var metal = BlockSmeltingContainer.GetMetal(belmc.contents);
                return Lang.Get("blocksmeltedcontainer-contents", belmc.units, metal, (int)belmc.Temperature);
            }

            return base.GetPlacedBlockInfo(world, pos, forPlayer);
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            KeyValuePair<ItemStack, int> contents = GetContents(world, inSlot.Itemstack);

            if (contents.Key != null)
            {
                var metal = BlockSmeltingContainer.GetMetal(contents.Key);

                dsc.Append(Lang.Get("item-unitdrop", contents.Value, metal));

                if (HasSolidifed(inSlot.Itemstack, contents.Key, world))
                {
                    dsc.Append(Lang.Get("metalwork-toocold"));
                }
            }
            
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }


        public bool HasSolidifed(ItemStack ownStack, ItemStack contentstack, IWorldAccessor world)
        {
            if (ownStack?.Collectible == null || contentstack == null) return false;

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
