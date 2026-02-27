using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockChisel : BlockMicroBlock, IWrenchOrientable, IAttachableToEntity, IWearableShapeSupplier
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            interactions = new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-chisel-removedeco",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = ObjectCacheUtil.GetToolStacks(api, EnumTool.Knife),
                    GetMatchingStacks = (wi, bs, es) => {
                        var bec = GetBlockEntity<BlockEntityChisel>(bs.Position);
                        if (bec?.DecorIds != null && bec.DecorIds[bs.Face.Index] != 0)
                        {
                            return wi.Itemstacks;
                        }
                        return null;
                    }
                }
            };
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if ((inSlot.Itemstack.Attributes["materials"] as StringArrayAttribute)?.value.Length > 1 || (inSlot.Itemstack.Attributes["materials"] as IntArrayAttribute)?.value.Length > 1)
            {
                dsc.AppendLine(Lang.Get("<font color=\"lightblue\">Multimaterial chiseled block</font>"));
            }
        }

        public void Rotate(EntityAgent byEntity, BlockSelection blockSel, int dir)
        {
            var bechisel = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (byEntity.Controls.CtrlKey)
            {
                int rot = bechisel.DecorRotations;
                int bitshift = blockSel.Face.Index * 3;
                int facerot = rot >> bitshift & DecorBits.maskRotationData;
                rot &= ~(DecorBits.maskRotationData << bitshift);
                rot += ((facerot + 1) & DecorBits.maskRotationData) << bitshift;
                bechisel.DecorRotations = rot;
            }
            else bechisel.RotateModel(dir > 0 ? 90 : -90, null);

            bechisel.MarkDirty(true);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var bechisel = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (bechisel?.Interact(byPlayer, blockSel) == true) return true;

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityChisel bec && (api as ICoreClientAPI)?.World.Player.InventoryManager.ActiveTool == EnumTool.Chisel)
            {
                ((ICoreClientAPI)api).Network.SendBlockEntityPacket(pos, 1011);
                return null;
            }

            return base.OnPickBlock(world, pos);
        }

        public override bool TryToRemoveSoilFirst(IWorldAccessor world, BlockPos pos, IPlayer byPlayer)
        {
            return false;
        }

        public override bool IsSoilNonSoilMix(BlockEntityMicroBlock be)
        {
            return false;
        }

        public override bool IsSoilNonSoilMix(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return false;
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }

        public virtual void CollectTextures(ItemStack stack, Shape shape, string texturePrefixCode, Dictionary<string, CompositeTexture> intoDict)
        {
            int[] blockIds = BlockEntityMicroBlock.MaterialIdsFromAttributes(stack.Attributes, api.World);
            uint[] voxelCuboids = (stack.Attributes["cuboids"] as IntArrayAttribute)?.AsUint;

            if (voxelCuboids == null) return;

            CuboidWithMaterial cwm = new CuboidWithMaterial();

            for (int i = 0; i < voxelCuboids.Length; i++)
            {
                BlockEntityMicroBlock.FromUint(voxelCuboids[i], cwm);
                Block block = api.World.Blocks[blockIds[cwm.Material]];

                foreach (var facing in BlockFacing.ALLFACES)
                {
                    string texCode = texturePrefixCode + "-" + i + "-" + block.Code.ToShortString() + "-" + facing.Code;

                    if (intoDict.ContainsKey(texCode)) continue;

                    if (!block.Textures.TryGetValue(facing.Code, out CompositeTexture ctex))
                    {
                        if (facing.IsVertical) block.Textures.TryGetValue("vertical", out ctex);
                        else block.Textures.TryGetValue("horizontal", out ctex);

                        if (ctex == null) block.Textures.TryGetValue("all", out ctex);
                        if (ctex == null) block.Textures.TryGetValue("material", out ctex);

                        if (ctex == null && block.Textures.Count > 0)
                        {
                            ctex = block.Textures.First().Value;
                        }
                    }

                    if (ctex != null)
                    {
                        intoDict[texCode] = ctex.Clone();

                        intoDict[texCode].Base.Path = intoDict[texCode].Base.Path.Replace("*", "1");
                    }
                }
            }
        }

        public virtual Shape GetShape(ItemStack stack, Entity forEntity, string texturePrefixCode)
        {
            float scale = 1f;
            Vec3f offset = new Vec3f(-4f, 0f, -6f);

            int[] blockIds = BlockEntityMicroBlock.MaterialIdsFromAttributes(stack.Attributes, forEntity.World);
            uint[] voxelCuboids = (stack.Attributes["cuboids"] as IntArrayAttribute)?.AsUint;

            if (voxelCuboids == null) return null;

            Shape shape = new Shape()
            {
                Elements = new ShapeElement[voxelCuboids.Length],
                Textures = new Dictionary<string, AssetLocation>(),
                TextureWidth = 16,
                TextureHeight = 16
            };

            CuboidWithMaterial cwm = new CuboidWithMaterial();
            
            for (int i = 0; i < voxelCuboids.Length; i++)
            {
                BlockEntityMicroBlock.FromUint(voxelCuboids[i], cwm);
                Block block = forEntity.World.Blocks[blockIds[cwm.Material]];

                var elem = new ShapeElement()
                {
                    Name = "Cuboid" + i,
                    From =
                    [
                        offset.X + (cwm.X1 * scale),
                        offset.Y + (cwm.Y1 * scale),
                        offset.Z + (cwm.Z1 * scale)
                    ],
                    To = [
                        offset.X + (cwm.X2 * scale),
                        offset.Y + (cwm.Y2 * scale),
                        offset.Z + (cwm.Z2 * scale)
                    ],
                    FacesResolved = new ShapeElementFace[6]
                };

                foreach (var facing in BlockFacing.ALLFACES)
                {
                    float[] uv = facing.Index switch
                    {
                        0 => [16 - cwm.X2, 16 - cwm.Y2, 16 - cwm.X1, 16 - cwm.Y1],
                        1 => [16 - cwm.Z2, 16 - cwm.Y2, 16 - cwm.Z1, 16 - cwm.Y1],
                        2 => [cwm.X1, 16 - cwm.Y2, cwm.X2, 16 - cwm.Y1],
                        3 => [cwm.Z1, 16 - cwm.Y2, cwm.Z2, 16 - cwm.Y1],
                        4 => [16 - cwm.X1, 16 - cwm.Z1, 16 - cwm.X2, 16 - cwm.Z2],
                        5 => [cwm.X1, cwm.Z1, cwm.X2, cwm.Z2],
                        _ => [0, 0, 16, 16],
                    };

                    string texCode = texturePrefixCode + "-" + i + "-" + block.Code.ToShortString() + "-" + facing.Code;
                    elem.FacesResolved[facing.Index] = new ShapeElementFace()
                    {
                        Texture = texCode,
                        Uv = uv
                    };
                }

                shape.Elements[i] = elem;
            }

            return shape;
        }

        public int RequiresBehindSlots { get; set; } = 0;
        public CompositeShape GetAttachedShape(ItemStack stack, string slotCode) => null;
        public string GetCategoryCode(ItemStack stack) => "chiseled";
        public string[] GetDisableElements(ItemStack stack) => [];
        public string[] GetKeepElements(ItemStack stack) => [];
        public bool IsAttachable(Entity toEntity, ItemStack itemStack) => true;

        public string GetTexturePrefixCode(ItemStack stack)
        {
            int[] BlockIds = BlockEntityMicroBlock.MaterialIdsFromAttributes(stack.Attributes, api.World);
            uint[] VoxelCuboids = (stack.Attributes["cuboids"] as IntArrayAttribute)?.AsUint;
            string key = "chiseled-";
            key += "blockids:-" + string.Join("-", BlockIds);
            key += "cuboids:-" + string.Join("-", VoxelCuboids);
            return key;
        }
    }
}
