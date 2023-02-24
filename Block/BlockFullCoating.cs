using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockSmoothTextureTransition : Block
    {
        Dictionary<string, MeshData> sludgeMeshByNESWFlag = null;
        ICoreClientAPI capi;

        public Dictionary<string, HashSet<string>> cornersByNEWSFlag = new Dictionary<string, HashSet<string>>()
        {
            { "flat", new HashSet<string>(new string[] { "wn", "ne", "es", "sw" }) },

            { "n", new HashSet<string>(new string[] { "es", "sw" }) },
            { "e", new HashSet<string>(new string[] { "wn", "sw"  }) },
            { "s", new HashSet<string>(new string[] { "wn", "ne" }) },
            { "w", new HashSet<string>(new string[] { "ne", "es" }) },

            { "sw", new HashSet<string>(new string[] { "ne" }) },
            { "nw", new HashSet<string>(new string[] { "se" }) },
            { "ne", new HashSet<string>(new string[] { "sw" }) },
            { "es", new HashSet<string>(new string[] { "nw" }) },
        };

        string[] cornerCodes = new string[] { "wn", "ne", "es", "sw" };
        int[] cornerOffest = new int[] { 
            -1 * TileSideEnum.MoveIndex[1] -1 * TileSideEnum.MoveIndex[2], 
            1 * TileSideEnum.MoveIndex[1] -1 * TileSideEnum.MoveIndex[2],
            1 * TileSideEnum.MoveIndex[1] + 1 * TileSideEnum.MoveIndex[2],
            -1 * TileSideEnum.MoveIndex[1] + 1 * TileSideEnum.MoveIndex[2]
        };

        public override void OnLoaded(ICoreAPI api)
        {
            capi = api as ICoreClientAPI;
            base.OnLoaded(api);
        }

        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
        {
            if (sludgeMeshByNESWFlag == null) genMeshes();

            string flags = getNESWFlag(chunkExtBlocks, extIndex3d);

            if (cornersByNEWSFlag.TryGetValue(flags, out var corners))
            {
                string cornerFlags = getCornerFlags(corners, chunkExtBlocks, extIndex3d);
                if (cornerFlags.Length > 0) flags += "-cornercut-" + cornerFlags;
            }

            if (sludgeMeshByNESWFlag.TryGetValue(flags, out var mesh))
            {
                sourceMesh = mesh;
                return;
            }

            base.OnJsonTesselation(ref sourceMesh, ref lightRgbsByCorner, pos, chunkExtBlocks, extIndex3d);
        }

        private string getCornerFlags(HashSet<string> corners, Block[] chunkExtBlocks, int extIndex3d)
        {
            string cornerflags = "";
            for (int i = 0; i < cornerOffest.Length; i++)
            {
                if (!corners.Contains(cornerCodes[i])) continue;

                Block cBlock = chunkExtBlocks[extIndex3d + cornerOffest[i]];
                if (!cBlock.SideSolid[BlockFacing.UP.Index])
                {
                    cornerflags += cornerCodes[i];
                }
            }

            return cornerflags;
        }

        private void genMeshes()
        {
            sludgeMeshByNESWFlag = new Dictionary<string, MeshData>();

            var sludgeShapeByNESWFlag = Attributes["shapeByOrient"].AsObject<Dictionary<string, CompositeShape>>();

            foreach (var val in sludgeShapeByNESWFlag)
            {
                var shape = capi.Assets.TryGet(val.Value.Base.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json")).ToObject<Shape>();
                if (shape == null)
                {
                    api.Logger.Warning("Smooth texture transition shape for block {0}: Shape {1} not found. Block will be invisible.", Code.Path, val.Value.Base);
                    continue;
                }

                capi.Tesselator.TesselateShape(this, shape, out var mesh, new Vec3f(val.Value.rotateX, val.Value.rotateY, val.Value.rotateZ));
                sludgeMeshByNESWFlag[val.Key] = mesh;
            }
        }

        public string getNESWFlag(Block[] chunkExtBlocks, int extIndex3d)
        {
            string flags = "";
            for (int i = 0; i < BlockFacing.ALLFACES.Length; i++)
            {
                var face = BlockFacing.ALLFACES[i];
                int moveindex = face.Normali.X * TileSideEnum.MoveIndex[1] + face.Normali.Z * TileSideEnum.MoveIndex[2];

                Block nblock = chunkExtBlocks[extIndex3d + moveindex];
                if (!nblock.SideSolid[BlockFacing.UP.Index])
                {
                    flags += face.Code[0];
                }
            }

            return flags.Length == 0 ? "flat" : flags;
        }
    }



    public class BlockFullCoating : Block
    {
        BlockFacing[] ownFacings;
        Cuboidf[] selectionBoxes;

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor world, BlockPos pos)
        {
            return selectionBoxes;
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            string facingletters = Variant["coating"];

            ownFacings = new BlockFacing[facingletters.Length];
            selectionBoxes = new Cuboidf[ownFacings.Length];

            for (int i = 0; i < facingletters.Length; i++)
            {
                ownFacings[i] = BlockFacing.FromFirstLetter(facingletters[i]);
                switch(facingletters[i])
                {
                    case 'n':
                        selectionBoxes[i] = new Cuboidf(0, 0, 0, 1, 1, 0.0625f);
                        break;
                    case 'e':
                        selectionBoxes[i] = new Cuboidf(0, 0, 0, 1, 1, 0.0625f).RotatedCopy(0, 270, 0, new Vec3d(0.5, 0.5, 0.5));
                        break;
                    case 's':
                        selectionBoxes[i] = new Cuboidf(0, 0, 0, 1, 1, 0.0625f).RotatedCopy(0, 180, 0, new Vec3d(0.5, 0.5, 0.5));
                        break;
                    case 'w':
                        selectionBoxes[i] = new Cuboidf(0, 0, 0, 1, 1, 0.0625f).RotatedCopy(0, 90, 0, new Vec3d(0.5, 0.5, 0.5));
                        break;
                    case 'u':
                        selectionBoxes[i] = new Cuboidf(0, 0, 0, 1, 0.0625f, 1).RotatedCopy(180, 0, 0, new Vec3d(0.5, 0.5, 0.5));
                        break;
                    case 'd':
                        selectionBoxes[i] = new Cuboidf(0, 0, 0, 1, 0.0625f, 1);
                        break;
                }
                
            }
        }


        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            return TryPlaceBlockForWorldGen(world.BlockAccessor, blockSel.Position, blockSel.Face);
        }

        
        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return GetHandbookDropsFromBreakDrops(handbookStack, forPlayer);
        }


        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            int quantity = 0;
            for (int i = 0; i < ownFacings.Length; i++) quantity += world.Rand.NextDouble() > Drops[0].Quantity.nextFloat() ? 1 : 0;

            ItemStack stack = Drops[0].ResolvedItemstack.Clone();
            stack.StackSize = Math.Max(1, (int)quantity);
            return new ItemStack[] { stack };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithVariant("coating", "d"));
            return new ItemStack(block);
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            string newFacingLetters = "";
            foreach (BlockFacing facing in ownFacings)
            {
                Block block = world.BlockAccessor.GetBlock(pos.X + facing.Normali.X, pos.Y + facing.Normali.Y, pos.Z + facing.Normali.Z);

                if (block.SideSolid[facing.Opposite.Index])
                {
                    newFacingLetters += facing.Code.Substring(0, 1);
                }
            }

            if (ownFacings.Length <= newFacingLetters.Length) return;

            if (newFacingLetters.Length == 0)
            {
                world.BlockAccessor.BreakBlock(pos, null);
                return;
            }

            int diff = newFacingLetters.Length - ownFacings.Length;
            for (int i = 0; i < diff; i++)
            {
                world.SpawnItemEntity(Drops[0].GetNextItemStack(), pos.ToVec3d().AddCopy(0.5, 0.5, 0.5));
            }

            Block newblock = world.GetBlock(CodeWithVariant("coating", newFacingLetters));
            world.BlockAccessor.SetBlock(newblock.BlockId, pos);
        }

        


        public override bool CanAttachBlockAt(IBlockAccessor world, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea = null)
        {
            return false;
        }


        public string getSolidFacesAtPos(IBlockAccessor blockAccessor, BlockPos pos)
        {
            string facings = "";

            foreach (BlockFacing facing in BlockFacing.ALLFACES)
            {
                Block block = blockAccessor.GetBlock(pos.X + facing.Normali.X, pos.Y + facing.Normali.Y, pos.Z + facing.Normali.Z);

                if (block.SideSolid[facing.Opposite.Index])
                {
                    facings += facing.Code.Substring(0, 1);
                }
            }

            return facings;
        }


        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldGenRand)
        {
            return TryPlaceBlockForWorldGen(blockAccessor, pos, onBlockFace);
        }

        public bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace)
        {
            float thup = 70f / 255 * api.World.BlockAccessor.MapSizeY;
            float thdown = 16f / 255 * api.World.BlockAccessor.MapSizeY;

            if (pos.Y < thdown || pos.Y > thup || blockAccessor.GetLightLevel(pos, EnumLightLevelType.OnlySunLight) > 15) return false;

            var hblock = blockAccessor.GetBlock(pos);
            if (hblock.Replaceable < 6000 || hblock.IsLiquid()) return false; // Don't place where there's solid blocks

            string facings = getSolidFacesAtPos(blockAccessor, pos);

            if (facings.Length > 0)
            {
                Block block = blockAccessor.GetBlock(CodeWithVariant("coating", facings));
                blockAccessor.SetBlock(block.BlockId, pos);
            }

            return true;
        }
    }
}
