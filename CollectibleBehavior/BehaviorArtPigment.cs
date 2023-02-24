using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class CollectibleBehaviorArtPigment : CollectibleBehavior
    {
        private EnumBlockMaterial[] paintableOnBlockMaterials;

        private MeshRef[] meshes;
        TextureAtlasPosition texPos;
        SkillItem[] toolModes;
        List<Block> decorBlocks = new List<Block>();

        string[] onmaterialsStrTmp;
        AssetLocation[] decorCodesTmp;


        static int[] quadVertices = {
            -1, -1,  0,
             1, -1,  0,
             1,  1,  0,
            -1,  1,  0
        };

        static int[] quadTextureCoords = { 0, 0,  1, 0,  1, 1,  0, 1 };

        static int[] quadVertexIndices = { 0, 1, 2,   0, 2, 3 };

        

        public CollectibleBehaviorArtPigment(CollectibleObject collObj) : base(collObj)
        {
            this.collObj = collObj;
        }


        public override void Initialize(JsonObject properties)
        {
            onmaterialsStrTmp = properties["paintableOnBlockMaterials"].AsArray<string>(new string[0]);
            decorCodesTmp = properties["decorBlockCodes"].AsObject(new AssetLocation[0], collObj.Code.Domain);

            base.Initialize(properties);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            paintableOnBlockMaterials = new EnumBlockMaterial[onmaterialsStrTmp.Length];
            for (int i = 0; i < onmaterialsStrTmp.Length; i++)
            {
                if (onmaterialsStrTmp[i] == null) continue;
                try
                {
                    paintableOnBlockMaterials[i] = (EnumBlockMaterial)Enum.Parse(typeof(EnumBlockMaterial), onmaterialsStrTmp[i]);
                }
                catch (Exception)
                {
                    api.Logger.Warning("ArtPigment behavior for collectible {0}, paintable on material {1} is not a valid block material, will default to stone", collObj.Code, onmaterialsStrTmp[i]);
                    paintableOnBlockMaterials[i] = EnumBlockMaterial.Stone;
                }
            }
            onmaterialsStrTmp = null;

            var capi = api as ICoreClientAPI;

            foreach (var loc in decorCodesTmp)
            {
                if (loc.Path.Contains("*"))
                {
                    Block[] blocks = api.World.SearchBlocks(loc);
                    foreach (var block in blocks)
                    {
                        decorBlocks.Add(block);
                        //Console.WriteLine("\"/bir remapq " + block.Code.ToShortString() + " drawnart"+block.Variant["material"]+"-1-" + block.Variant["row"] +"-" + block.Variant["col"] + " force\",");
                    }
                    if (blocks.Length == 0)
                    {
                        api.Logger.Warning("ArtPigment behavior for collectible {0}, decor {1}, no such block using this wildcard found", collObj.Code, loc);
                    }

                }
                else
                {
                    Block block = api.World.GetBlock(loc);
                    if (block == null)
                    {
                        api.Logger.Warning("ArtPigment behavior for collectible {0}, decor {1} is not a loaded block", collObj.Code, loc);
                    }
                    else
                    {
                        decorBlocks.Add(block);
                    }

                }
            }

            if (api.Side == EnumAppSide.Client)
            {
                if (decorBlocks.Count > 0)
                {
                    BakedCompositeTexture tex = decorBlocks[0].Textures["up"].Baked;
                    texPos = capi.BlockTextureAtlas.Positions[tex.TextureSubId];
                }
                else
                {
                    texPos = capi.BlockTextureAtlas.UnknownTexturePosition;
                }
            }

            AssetLocation blockCode = collObj.Code;
            toolModes = new SkillItem[decorBlocks.Count];
            for (int i = 0; i < toolModes.Length; i++)
            {
                toolModes[i] = new SkillItem()
                {
                    Code = blockCode.CopyWithPath("art" + i),   // Unique code, it doesn't really matter what it is
                    Linebreak = i % GlobalConstants.CaveArtColsPerRow == 0,
                    Name = "",   // No name - alternatively each icon could be given a name? But discussed in meeting on 6/6/21 and decided it is better for players to assign their own meanings to the icons
                    Data = decorBlocks[i],
                    RenderHandler = (AssetLocation code, float dt, double atPosX, double atPosY) =>
                    {
                        float wdt = (float)GuiElement.scaled(GuiElementPassiveItemSlot.unscaledSlotSize);
                        string id = code.Path.Substring(3);
                        capi.Render.Render2DTexture(meshes[int.Parse(id)], texPos.atlasTextureId, (float)atPosX, (float)atPosY, wdt, wdt);
                    }
                };
            }


            if (capi != null)
            {
                meshes = new MeshRef[decorBlocks.Count];

                for (int i = 0; i < meshes.Length; i++)
                {
                    MeshData mesh = genMesh(i);
                    meshes[i] = capi.Render.UploadMesh(mesh);
                }
            }

        }

        public override void OnUnloaded(ICoreAPI api)
        {
            base.OnUnloaded(api);
            if (api is ICoreClientAPI && meshes != null)
            {
                for (int i = 0; i < meshes.Length; i++) meshes[i]?.Dispose();
            }
        }


        public MeshData genMesh(int index)
        {
            MeshData m = new MeshData(4, 6, false, true, false, false);

            float x1 = texPos.x1;
            float y1 = texPos.y1;
            float x2 = texPos.x2;
            float y2 = texPos.y2;


            float xSize = (x2 - x1) / GlobalConstants.CaveArtColsPerRow;
            float ySize = (y2 - y1) / GlobalConstants.CaveArtColsPerRow;
            x1 += (index % GlobalConstants.CaveArtColsPerRow) * xSize;
            y1 += (index / GlobalConstants.CaveArtColsPerRow) * ySize;

            for (int i = 0; i < 4; i++)
            {
                m.AddVertex(
                    quadVertices[i * 3],
                    quadVertices[i * 3 + 1],
                    quadVertices[i * 3 + 2],
                    x1 + (1 - quadTextureCoords[i * 2]) * xSize,
                    y1 + quadTextureCoords[i * 2 + 1] * ySize
                );
            }

            for (int i = 0; i < 6; i++)
            {
                m.AddIndex(quadVertexIndices[i]);
            }

            return m;
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            if (blockSel?.Position == null) return;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak)) return;
            if (byPlayer == null || !byPlayer.Entity.Controls.CtrlKey) return;
            if (!SuitablePosition(byEntity.World.BlockAccessor, blockSel)) return;

            handHandling = EnumHandHandling.PreventDefault;
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (blockSel?.Position == null) return false;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak)) return false;
            if (byPlayer == null || !byPlayer.Entity.Controls.CtrlKey) return false;
            if (!SuitablePosition(byEntity.World.BlockAccessor, blockSel)) return false;

            handling = EnumHandling.PreventSubsequent;
            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (blockSel?.Position == null) return;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak)) return;
            if (byPlayer == null || !byPlayer.Entity.Controls.CtrlKey) return;

            IBlockAccessor blockAccessor = byEntity.World.BlockAccessor;
            if (!SuitablePosition(blockAccessor, blockSel)) return;

            handling = EnumHandling.PreventDefault;
            DrawCaveArt(blockSel, blockAccessor, byPlayer);

            // 1/15 chance to consume the item
            if (byEntity.World.Side == EnumAppSide.Server && byEntity.World.Rand.NextDouble() < 1f/15)
            {
                slot.TakeOut(1);
                slot.MarkDirty();
            }

            byEntity.World.PlaySoundAt(new AssetLocation("sounds/player/chalkdraw"), blockSel.Position.X + blockSel.HitPosition.X, blockSel.Position.Y + blockSel.HitPosition.Y, blockSel.Position.Z + blockSel.HitPosition.Z, byPlayer, true, 8);
        }

        private void DrawCaveArt(BlockSelection blockSel, IBlockAccessor blockAccessor, IPlayer byPlayer)
        {
            int toolMode = GetToolMode(null, byPlayer, blockSel);
            Block blockToPlace = (Block)toolModes[toolMode].Data;
            blockAccessor.SetDecor(blockToPlace, blockSel.Position, blockSel.ToDecorIndex());
        }




        public static int BlockSelectionToSubPosition(BlockFacing face, Vec3i voxelPos)
        {
            int x = voxelPos.X;
            int y = 15 - voxelPos.Y;
            int z = voxelPos.Z;
            int offset = 0;

            switch (face.Index)
            {
                case 0:
                    offset = (15 - x) + y * 16;
                    break;
                case 1:
                    offset = (15 - z) + y * 16;
                    break;
                case 2:
                    offset = x + y * 16;
                    break;
                case 3:
                    offset = z + y * 16;
                    break;
                case 4:
                    offset = x + z * 16;
                    break;
                case 5:
                    offset = x + (15 - z) * 16;
                    break;
            }

            return face.Index + 6 * (1 + offset);
        }

        private bool SuitablePosition(IBlockAccessor blockAccessor, BlockSelection blockSel)
        {
            Block attachingBlock = blockAccessor.GetBlock(blockSel.Position);
            if (attachingBlock.SideSolid[blockSel.Face.Index] || (attachingBlock is BlockMicroBlock && (blockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityMicroBlock).sideAlmostSolid[blockSel.Face.Index]))
            {
                EnumBlockMaterial targetMaterial = attachingBlock.GetBlockMaterial(blockAccessor, blockSel.Position);
                for (int i = 0; i < paintableOnBlockMaterials.Length; i++) if (targetMaterial == paintableOnBlockMaterials[i]) return true;
            }
            return false;
        }

        public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        {
            if (blockSel == null) return null;

            IBlockAccessor blockAccessor = forPlayer.Entity.World.BlockAccessor;
            if (!SuitablePosition(blockAccessor, blockSel))
            {
                return null;
            }

            return toolModes;
        }

        public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer?.Entity == null) return 0;
            return byPlayer.Entity.WatchedAttributes.GetInt("toolModeCaveArt");
        }

        public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, int toolMode)
        {
             byPlayer?.Entity.WatchedAttributes.SetInt("toolModeCaveArt", toolMode);
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction
                {
                    HotKeyCode = "ctrl",
                    ActionLangCode = "heldhelp-draw",
                    MouseButton = EnumMouseButton.Right
                }
            }.Append(base.GetHeldInteractionHelp(inSlot, ref handling));

        }
    }
}