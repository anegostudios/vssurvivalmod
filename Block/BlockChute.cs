﻿using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Note Elbow Chute up/down names are inverted in the BlockType JSON - but can't fix without breaking existing worlds
    /// </summary>
    public class BlockChute : Block, IBlockItemFlow
    {
        public string Type { get; set; }
        public string Side { get; set; }
        public string Vertical { get; set; }

        public string[] PullFaces => Attributes["pullFaces"].AsArray<string>(System.Array.Empty<string>());
        public string[] PushFaces => Attributes["pushFaces"].AsArray<string>(System.Array.Empty<string>());
        public string[] AcceptFaces => Attributes["acceptFromFaces"].AsArray<string>(System.Array.Empty<string>());


        

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            this.Type = Variant["type"] is string t ? string.Intern(t) : null;
            this.Side = Variant["side"] is string s ? string.Intern(s) : null;
            this.Vertical = Variant["vertical"] is string v ? string.Intern(v) : null;
        }


        public bool HasItemFlowConnectorAt(BlockFacing facing)
        {
            return PullFaces.Contains(facing.Code) || PushFaces.Contains(facing.Code) || AcceptFaces.Contains(facing.Code);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            BlockChute blockToPlace = null;

            BlockFacing[] facings = OrientForPlacement(world.BlockAccessor, byPlayer, blockSel);

            if (Type == "elbow" || Type == "3way")
            {
                string vertical = facings[1] == BlockFacing.UP ? "down" : "up";  //all block shapes for elbow and 3way are the wrong way up!
                BlockFacing horizontal = facings[0];
                if (vertical == "up" && (Type == "3way" || horizontal == BlockFacing.NORTH || horizontal == BlockFacing.SOUTH)) horizontal = horizontal.Opposite;
                AssetLocation code = CodeWithVariants(new string[] { "vertical", "side" }, new string[] { vertical, horizontal.Code });
                blockToPlace = api.World.GetBlock(code) as BlockChute;

                int i = 0;
                while (blockToPlace != null && !blockToPlace.CanStay(world, blockSel.Position))
                {
                    if (i >= BlockFacing.HORIZONTALS.Length)
                    {
                        blockToPlace = null;
                        break;
                    }
                    blockToPlace = api.World.GetBlock(CodeWithVariants(new string[] { "vertical", "side" }, new string[] { vertical, BlockFacing.HORIZONTALS[i++].Code })) as BlockChute;
                }
            }
            else if (Type == "t")
            {
                string variant = facings[0].Axis == EnumAxis.X ? "we" : "ns";
                if (blockSel.Face.IsVertical)
                {
                    variant = "ud-" + facings[0].Opposite.Code[0];
                }

                blockToPlace = api.World.GetBlock(CodeWithVariant("side", variant)) as BlockChute;

                if (!blockToPlace.CanStay(world, blockSel.Position))
                {
                    blockToPlace = api.World.GetBlock(CodeWithVariant("side", facings[0].Axis == EnumAxis.X ? "we" : "ns")) as BlockChute;
                }
            }
            else if (Type == "straight")
            {
                string variant = facings[0].Axis == EnumAxis.X ? "we" : "ns";
                if (blockSel.Face.IsVertical)
                {
                    variant = "ud";
                }
                blockToPlace = api.World.GetBlock(CodeWithVariant("side", variant)) as BlockChute;
            }
            else if (Type == "cross")
            {
                string variant = facings[0].Axis != EnumAxis.X ? "ns" : "we";
                if (blockSel.Face.IsVertical)
                {
                    variant = "ground";
                }
                blockToPlace = api.World.GetBlock(CodeWithVariant("side", variant)) as BlockChute;
            }

            if (blockToPlace != null && blockToPlace.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode) && (blockToPlace as BlockChute).CanStay(world, blockSel.Position))
            {
                world.BlockAccessor.SetBlock(blockToPlace.BlockId, blockSel.Position);
                world.Logger.Audit("{0} placed a chute at {1}", byPlayer.PlayerName, blockSel.Position);
                return true;
            }

            if (Type == "cross")
            {
                blockToPlace = api.World.GetBlock(CodeWithVariant("side", "ground")) as BlockChute;
            }

            if (blockToPlace != null && blockToPlace.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode) && (blockToPlace as BlockChute).CanStay(world, blockSel.Position))
            {
                world.BlockAccessor.SetBlock(blockToPlace.BlockId, blockSel.Position);
                world.Logger.Audit("{0} placed a chute at {1}", byPlayer.PlayerName, blockSel.Position);
                return true;
            }

            return false;
        }

        protected virtual BlockFacing[] OrientForPlacement(IBlockAccessor worldmap, IPlayer player, BlockSelection bs)
        {
            BlockFacing[] facings = SuggestedHVOrientation(player, bs);
            BlockPos pos = bs.Position;
            BlockFacing horizontal = null;
            BlockFacing face = bs.Face.Opposite;
            BlockFacing vert = null;

            //If player is placing against a block horizontally, check that one and all other horizontals for connectors
            if (face.IsHorizontal)
            {
                if (HasConnector(worldmap, pos.AddCopy(face), bs.Face, out vert)) horizontal = face;
                else
                {
                    face = face.GetCW();
                    if (HasConnector(worldmap, pos.AddCopy(face), face.Opposite, out vert)) horizontal = face;
                    else if (HasConnector(worldmap, pos.AddCopy(face.Opposite), face, out vert)) horizontal = face.Opposite;
                    else if (HasConnector(worldmap, pos.AddCopy(bs.Face), bs.Face.Opposite, out vert)) horizontal = bs.Face;
                }
                //Special case: the 3way has two connectors but the directional attribute covers only one of them
                if (Type == "3way" && horizontal != null)
                {
                    face = horizontal.GetCW();
                    if (HasConnector(worldmap, pos.AddCopy(face), face.Opposite, out BlockFacing unused) && !HasConnector(worldmap, pos.AddCopy(face.Opposite), face, out unused)) horizontal = face;
                }
            }
            else
            {
                //Player is placing against a block vertically, use that as the vertical connection and check all horizontals for connectors
                vert = face;
                bool moreThanOne = false;
                horizontal = HasConnector(worldmap, pos.EastCopy(), BlockFacing.WEST, out vert) ? BlockFacing.EAST : null;
                if (HasConnector(worldmap, pos.WestCopy(), BlockFacing.EAST, out vert))
                {
                    moreThanOne = horizontal != null;
                    horizontal = BlockFacing.WEST;
                }
                if (HasConnector(worldmap, pos.NorthCopy(), BlockFacing.SOUTH, out vert))
                {
                    moreThanOne = horizontal != null;
                    horizontal = BlockFacing.NORTH;
                }
                if (HasConnector(worldmap, pos.SouthCopy(), BlockFacing.NORTH, out vert))
                {
                    moreThanOne = horizontal != null;
                    horizontal = BlockFacing.SOUTH;
                }
                if (moreThanOne) horizontal = null;
            }
            if (vert == null)
            {
                //If vertical orientation not already chosen, see whether there is an existing open connector up or down
                bool up = HasConnector(worldmap, pos.UpCopy(), BlockFacing.DOWN, out BlockFacing unused);
                bool down = HasConnector(worldmap, pos.DownCopy(), BlockFacing.UP, out unused);
                if (up && !down) vert = BlockFacing.UP;
                else if (down && !up) vert = BlockFacing.DOWN;
            }
            if (vert != null) facings[1] = vert;
            facings[0] = horizontal ?? facings[0].Opposite;
            return facings;
        }

        /// <summary>
        /// vert parameter 'suggests' an orientation for newly placed block to be opposite to existing chute
        /// </summary>
        protected virtual bool HasConnector(IBlockAccessor ba, BlockPos pos, BlockFacing face, out BlockFacing vert)
        {
            if (ba.GetBlock(pos) is BlockChute chute)
            {
                if (chute.HasItemFlowConnectorAt(BlockFacing.UP) && !chute.HasItemFlowConnectorAt(BlockFacing.DOWN)) vert = BlockFacing.DOWN;
                else if (chute.HasItemFlowConnectorAt(BlockFacing.DOWN) && !chute.HasItemFlowConnectorAt(BlockFacing.UP)) vert = BlockFacing.UP;
                else vert = null;
                return chute.HasItemFlowConnectorAt(face);
            }

            vert = null;
            return ba.GetBlock(pos).GetBlockEntity<BlockEntityContainer>(pos) != null;
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            if (!CanStay(world, pos))
            {
                world.BlockAccessor.BreakBlock(pos, null);
            }
        }

        bool CanStay(IWorldAccessor world, BlockPos pos)
        {
            BlockPos npos = new BlockPos();
            var ba = world.BlockAccessor;

            if (PullFaces != null)
            {
                foreach (var val in PullFaces)
                {
                    BlockFacing face = BlockFacing.FromCode(val);
                    Block block = world.BlockAccessor.GetBlock(npos.Set(pos).Add(face));
                    if (block.CanAttachBlockAt(world.BlockAccessor, this, pos, face) || (block as IBlockItemFlow)?.HasItemFlowConnectorAt(face.Opposite) == true || ba.GetBlock(pos).GetBlockEntity< BlockEntityContainer>(npos) != null) return true;
                }
            }

            if (PushFaces != null)
            {
                foreach (var val in PushFaces)
                {
                    BlockFacing face = BlockFacing.FromCode(val);
                    Block block = world.BlockAccessor.GetBlock(npos.Set(pos).Add(face));
                    if (block.CanAttachBlockAt(world.BlockAccessor, this, pos, face) || (block as IBlockItemFlow)?.HasItemFlowConnectorAt(face.Opposite) == true || ba.GetBlock(pos).GetBlockEntity<BlockEntityContainer>(npos) != null) return true;
                }
            }

            return false;
        }

        
        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return new[] { new BlockDropItemStack(handbookStack) };
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            Block block = null;

            if (Type == "elbow" || Type == "3way")
            {
                block = api.World.GetBlock(CodeWithVariants(new string[] { "vertical", "side" }, new string[] { "down", "east" }));
            }

            if (Type == "t" || Type == "straight")
            {
                block = api.World.GetBlock(CodeWithVariant("side", "ns"));
            }

            if (Type == "cross")
            {
                block = api.World.GetBlock(CodeWithVariant("side", "ground"));
            }

            return new ItemStack[] { new ItemStack(block) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return GetDrops(world, pos, null)[0];
        }


        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            var dir = GameMath.Mod(angle / 90, 4);
            switch (Type)
            {
                case  "elbow":
                {
                    var facing = BlockFacing.FromCode(Side);
                    return CodeWithVariant("side",
                        BlockFacing.HORIZONTALS[GameMath.Mod(facing.Index+dir+2, 4)].Code.ToLowerInvariant());
                }
                case "3way":
                {
                    var facing = BlockFacing.FromCode(Side);
                    return CodeWithVariant("side",
                        BlockFacing.HORIZONTALS[GameMath.Mod(facing.Index+dir, 4)].Code.ToLowerInvariant());
                }
                case "t":
                {
                    if ((Side.Equals("ns") || Side.Equals("we")) && (dir == 1 || dir == 3))
                    {
                        return CodeWithVariant("side",Side.Equals("ns") ? "we" : "ns");
                    }
                    var facing = Side switch
                    {
                        "ud-n" => BlockFacing.NORTH,
                        "ud-e" => BlockFacing.EAST,
                        "ud-s" => BlockFacing.SOUTH,
                        "ud-w" => BlockFacing.WEST,
                        _ => BlockFacing.NORTH
                    };
                    return CodeWithVariant("side","ud-"+
                        BlockFacing.HORIZONTALS[GameMath.Mod(facing.Index+dir, 4)].Code.ToLowerInvariant()[0]);
                }
                case "straight":
                {
                    if (Side.Equals("ud") || dir == 0 || dir == 2)
                    {
                        return Code;
                    }
                    return CodeWithVariant("side",Side.Equals("ns") ? "we" : "ns");
                }
                case "cross":
                {
                    if ((Side.Equals("ns") || Side.Equals("we")) && (dir == 1 || dir == 3))
                    {
                        return CodeWithVariant("side",Side.Equals("ns") ? "we" : "ns");
                    }
                    return Code;
                }
            }
            return Code;
        }

        public override AssetLocation GetVerticallyFlippedBlockCode()
        {
            return base.GetVerticallyFlippedBlockCode();
        }
    }
}
