using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ModSystemShipConstructionSitePreview : ModSystem
    {
        ICoreClientAPI capi;
        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            api.Event.RegisterGameTickListener(onTick, 100);
        }

        private void onTick(float dt)
        {
            var slot = capi.World.Player.InventoryManager.ActiveHotbarSlot;

            if (slot.Itemstack?.Collectible is ItemRoller)
            {
                int orient = ItemRoller.GetOrient(capi.World.Player);
                var siteList = ItemRoller.siteListByFacing[orient];
                var waterEdgeList = ItemRoller.waterEdgeByFacing[orient];

                var c = ColorUtil.ColorFromRgba(0, 50, 150, 50);
                capi.World.HighlightBlocks(capi.World.Player, 941, siteList, EnumHighlightBlocksMode.AttachedToSelectedBlock, EnumHighlightShape.Cube);
                capi.World.HighlightBlocks(capi.World.Player, 942, waterEdgeList, new List<int>() { c }, EnumHighlightBlocksMode.AttachedToSelectedBlock, EnumHighlightShape.Cube);
            } else
            {
                capi.World.HighlightBlocks(capi.World.Player, 941, ItemRoller.emptyList, EnumHighlightBlocksMode.AttachedToSelectedBlock, EnumHighlightShape.Cube);
                capi.World.HighlightBlocks(capi.World.Player, 942, ItemRoller.emptyList, EnumHighlightBlocksMode.AttachedToSelectedBlock, EnumHighlightShape.Cube);
            }
        }
    }

    // Provides the base for shipbuilding
    public class ItemRoller : Item
    {
        public static List<BlockPos> emptyList = new List<BlockPos>();

        public static List<List<BlockPos>> siteListByFacing = new List<List<BlockPos>>();
        public static List<List<BlockPos>> waterEdgeByFacing = new List<List<BlockPos>>();

        public static List<BlockPos> siteListN = new List<BlockPos>() { new BlockPos(-5, -1, -2), new BlockPos(3, 2, 2) };
        public static List<BlockPos> waterEdgeListN = new List<BlockPos>() { new BlockPos(3, -1, -2), new BlockPos(4, 0, 2) };

        public SkillItem[] skillItems;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            siteListByFacing.Add(siteListN);
            waterEdgeByFacing.Add(waterEdgeListN);

            for (int i = 1; i < 4; i++)
            {
                siteListByFacing.Add(rotateList(siteListN, i));
                waterEdgeByFacing.Add(rotateList(waterEdgeListN, i));
            }

            skillItems = new SkillItem[]
            {
                new SkillItem() { Code = new AssetLocation("north"), Name = "North" },
                new SkillItem() { Code = new AssetLocation("east"), Name = "Easth" },
                new SkillItem() { Code = new AssetLocation("south"), Name = "South" },
                new SkillItem() { Code = new AssetLocation("west"), Name = "West" },
            };

            if (api is ICoreClientAPI capi)
            {
                skillItems[0].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("textures/icons/pointnorth.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
                skillItems[1].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("textures/icons/pointeast.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
                skillItems[2].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("textures/icons/pointsouth.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
                skillItems[3].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("textures/icons/pointwest.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
            }
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            if (skillItems != null)
            {
                foreach (var sk in skillItems) sk.Dispose();
            }
        }

        private static List<BlockPos> rotateList(List<BlockPos> startlist, int i)
        {
            Matrixf matrixf = new Matrixf();
            matrixf.RotateY(i * GameMath.PIHALF);

            List<BlockPos> list = new List<BlockPos>();
            var vec1 = matrixf.TransformVector(new Vec4f(startlist[0].X, startlist[0].Y, startlist[0].Z, 1));
            var vec2 = matrixf.TransformVector(new Vec4f(startlist[1].X, startlist[1].Y, startlist[1].Z, 1));

            var minpos = new BlockPos((int)Math.Round(Math.Min(vec1.X, vec2.X)), (int)Math.Round(Math.Min(vec1.Y, vec2.Y)), (int)Math.Round(Math.Min(vec1.Z, vec2.Z)));
            var maxpos = new BlockPos((int)Math.Round(Math.Max(vec1.X, vec2.X)), (int)Math.Round(Math.Max(vec1.Y, vec2.Y)), (int)Math.Round(Math.Max(vec1.Z, vec2.Z)));

            list.Add(minpos);
            list.Add(maxpos);

            return list;
        }

        public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection)
        {
            return GetOrient(byPlayer);
        }

        public static int GetOrient(IPlayer byPlayer)
        {
            return ObjectCacheUtil.GetOrCreate(byPlayer.Entity.Api, "rollerOrient-" + byPlayer.PlayerUID, () => 0); 
        }

        public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        {
            return skillItems;
        }

        public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, int toolMode)
        {
            api.ObjectCache["rollerOrient-" + byPlayer.PlayerUID] = toolMode;
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;
            var player = (byEntity as EntityPlayer)?.Player;

            if (slot.StackSize < 5)
            {
                (api as ICoreClientAPI)?.TriggerIngameError(this, "need5", "Need 5 rolles to place a boat construction site");
                return;
            }
            if (!suitableLocation(player, blockSel))
            {
                (api as ICoreClientAPI)?.TriggerIngameError(this, "unsuitableLocation", "Requires a suitable location near water to place a boat construction site");
                return;
            }

            string material = "oak";
            int orient = GetOrient(player);

            EntityProperties type = byEntity.World.GetEntityType(new AssetLocation("boatconstruction-sailed-" + material));
            var entity = byEntity.World.ClassRegistry.CreateEntity(type);
            entity.ServerPos.SetPos(blockSel.Position.ToVec3d().AddCopy(0.5, 1, 0.5));
            entity.ServerPos.Yaw = -GameMath.PIHALF + orient * GameMath.PIHALF;
            entity.Pos.SetFrom(entity.ServerPos);
            byEntity.World.SpawnEntity(entity);

            api.World.PlaySoundAt(new AssetLocation("sounds/block/planks"), byEntity, player);

            handling = EnumHandHandling.PreventDefault;
        }

        private bool suitableLocation(IPlayer forPlayer, BlockSelection blockSel)
        {
            int orient = GetOrient(forPlayer);
            var siteList = siteListByFacing[orient];
            var waterEdgeList = waterEdgeByFacing[orient];

            var ba = api.World.BlockAccessor;
            bool placeable = true;

            // 9 x 3 x 4
            var cpos = blockSel.Position;

            BlockPos mingPos = siteList[0].AddCopy(0, 1, 0).Add(cpos);
            BlockPos maxgPos = siteList[1].AddCopy(-1, 0, -1).Add(cpos);
            maxgPos.Y = mingPos.Y; // Only need to check 1 block ground


            // Below: Solid
            api.World.BlockAccessor.WalkBlocks(mingPos, maxgPos, (block, x, y, z) => {
                if (!block.SideIsSolid(new BlockPos(x, y, z), BlockFacing.UP.Index))
                {
                    placeable = false;
                }
            });
            if (!placeable) return false;

            // Above: Free
            BlockPos minPos = siteList[0].AddCopy(0, 2, 0).Add(cpos);
            BlockPos maxPos = siteList[1].AddCopy(-1, 1, -1).Add(cpos);
            api.World.BlockAccessor.WalkBlocks(minPos, maxPos, (block, x, y, z) => {
                var cboxes = block.GetCollisionBoxes(ba, new BlockPos(x, y, z));
                if (cboxes != null && cboxes.Length > 0) placeable = false;
            });

            // In front water
            BlockPos minlPos = waterEdgeList[0].AddCopy(0, 1, 0).Add(cpos);
            BlockPos maxlPos = waterEdgeList[1].AddCopy(-1, 0, -1).Add(cpos);
            WalkBlocks(minlPos, maxlPos, (block, x, y, z) => {
                //api.World.SpawnParticles(1, ColorUtil.WhiteArgb, new Vec3d(x+0.5, y+0.5, z+0.5), new Vec3d(x + 0.5, y + 0.5, z + 0.5), new Vec3f(), new Vec3f(), 1, 0, 0.2f);   
                if (!block.IsLiquid()) placeable = false;
            }, BlockLayersAccess.Fluid);

            return placeable;
        }



        public void WalkBlocks(BlockPos minPos, BlockPos maxPos, Action<Block, int, int, int> onBlock, int layer)
        {
            var ba = api.World.BlockAccessor;
            int chunksize = ba.ChunkSize;
            int minx = minPos.X;
            int miny = minPos.Y;
            int minz = minPos.Z;
            int maxx = maxPos.X;
            int maxy = maxPos.Y;
            int maxz = maxPos.Z;

            for (int x = minx; x <= maxx; x++)
            {
                for (int y = miny; y <= maxy; y++)
                {
                    for (int z = minz; z <= maxz; z++)
                    {
                        var block = ba.GetBlock(x, y, z);
                        onBlock(block, x, y, z);
                    }
                }
            }
        }
    }
}
