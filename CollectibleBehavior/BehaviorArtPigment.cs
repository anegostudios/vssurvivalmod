using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class CollectibleBehaviorArtPigment : CollectibleBehavior
    {
        CollectibleObject thisObject;
        private int numberOfModes;
        private int countPerSide;
        private string decorBase;
        private string guiBase;
        private EnumBlockMaterial[] allowedMaterials;

        private MeshRef[] meshes;
        TextureAtlasPosition texPos;
        SkillItem[] toolModesForGUI;

        static int[] quadVertices = {
            // Front face
            -1, -1,  0,
             1, -1,  0,
             1,  1,  0,
            -1,  1,  0
        };

        static int[] quadTextureCoords = { 0, 0,  1, 0,  1, 1,  0, 1 };

        static int[] quadVertexIndices = { 0, 1, 2,   0, 2, 3 };

        public CollectibleBehaviorArtPigment(CollectibleObject collObj) : base(collObj)
        {
            thisObject = collObj;
        }

        public override void Initialize(JsonObject properties)
        {
            numberOfModes = properties["numberOfModes"].AsInt(6);
            countPerSide = properties["countPerSide"].AsInt(6);
            decorBase = properties["decorBase"].AsString();
            guiBase = properties["guiBase"].AsString(decorBase);
            string[] materials = properties["allowedMaterials"].AsArray<string>(new string[0]);
            allowedMaterials = new EnumBlockMaterial[materials.Length];
            try
            {
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    allowedMaterials[i] = (EnumBlockMaterial)Enum.Parse(typeof(EnumBlockMaterial), materials[i]);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error decoding allowed materials, in ArtPigment behavior for " + collObj.Code.ToShortString());
                string list = "[";
                for (int i = 0; i < materials.Length; i++)
                {
                    if (i > 0) list += ",";
                    list += materials[i] == null ? "null" : materials[i];
                }
                Console.WriteLine(list + "]");
                Console.WriteLine(e.Message);
            }


            base.Initialize(properties);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            if (api is ICoreClientAPI capi)
            {
                Block art = capi.World.BlockAccessor.GetBlock(new AssetLocation(guiBase + "-1-1"));
                BakedCompositeTexture tex = art.Textures["up"].Baked;
                texPos = capi.BlockTextureAtlas.Positions[tex.TextureSubId];

                meshes = new MeshRef[numberOfModes];
                toolModesForGUI = new SkillItem[numberOfModes];
                for (int i = 0; i < meshes.Length; i++)
                {
                    MeshData mesh = genMesh(capi, i);
                    meshes[i] = capi.Render.UploadMesh(mesh);
                }

                AssetLocation blockCode = thisObject.Code;
                for (int i = 0; i < toolModesForGUI.Length; i++)
                {
                    toolModesForGUI[i] = new SkillItem()
                    {
                        Code = blockCode.CopyWithPath("art" + i),   //unique code, it doesn't really matter what it is
                        Linebreak = i % countPerSide == 0,
                        Name = "",   // no name - alternatively each icon could be given a name? But discussed in meeting on 6/6/21 and decided it is better for players to assign their own meanings to the icons
                        Data = "" + i,
                        RenderHandler = (AssetLocation code, float dt, double atPosX, double atPosY) =>
                        {
                            float wdt = (float)GuiElement.scaled(GuiElementPassiveItemSlot.unscaledSlotSize);
                            string id = code.Path.Substring(3);
                            capi.Render.Render2DTexture(meshes[int.Parse(id)], texPos.atlasTextureId, (float)atPosX, (float)atPosY, wdt, wdt);
                        }
                    };
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


        public MeshData genMesh(ICoreClientAPI capi, int index)
        {
            MeshData m = new MeshData(4, 6, false, true, false, false);

            float x1 = texPos.x1;
            float y1 = texPos.y1;
            float x2 = texPos.x2;
            float y2 = texPos.y2;


            float xSize = (x2 - x1) / 6;
            float ySize = (y2 - y1) / 6;
            x1 += (index % 6) * xSize;
            y1 += (index / 6) * ySize;

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
            if (byPlayer == null || byPlayer.Entity.Controls.Sneak) return;  // Base behavior if shift is held
            if (!SuitablePosition(byEntity.World.BlockAccessor, blockSel)) return;

            handHandling = EnumHandHandling.PreventDefault;  // Custom behavior (onHeldInteractStop) if this pigment can be used here
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (blockSel?.Position == null) return false;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak)) return false;
            if (byPlayer == null || byPlayer.Entity.Controls.Sneak) return false;
            if (!SuitablePosition(byEntity.World.BlockAccessor, blockSel)) return false;

            handling = EnumHandling.PreventSubsequent;  // Ensure that this returns true, i.e. continue the interaction until Stop
            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (blockSel?.Position == null) return;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak)) return;
            if (byPlayer == null || byPlayer.Entity.Controls.Sneak) return;

            IBlockAccessor blockAccessor = byEntity.World.BlockAccessor;
            if (!SuitablePosition(blockAccessor, blockSel)) return;

            handling = EnumHandling.PreventDefault;
            DrawCaveArt(blockSel, blockAccessor, byPlayer);  // Now draw the art :)

            byEntity.World.PlaySoundAt(new AssetLocation("sounds/player/chalkdraw"), blockSel.Position.X + blockSel.HitPosition.X, blockSel.Position.Y + blockSel.HitPosition.Y, blockSel.Position.Z + blockSel.HitPosition.Z, byPlayer, true, 8);
        }

        private void DrawCaveArt(BlockSelection blockSel, IBlockAccessor blockAccessor, IPlayer byPlayer)
        {
            int xx = (int)(blockSel.HitPosition.X * 16);
            int yy = 15 - (int)(blockSel.HitPosition.Y * 16);
            int zz = (int)(blockSel.HitPosition.Z * 16);
            int offset = 0;
            switch (blockSel.Face.Index)
            {
                case 0:
                    offset = (15 - xx) + yy * 16;
                    break;
                case 1:
                    offset = (15 - zz) + yy * 16;
                    break;
                case 2:
                    offset = xx + yy * 16;
                    break;
                case 3:
                    offset = zz + yy * 16;
                    break;
                case 4:
                    offset = xx + zz * 16;
                    break;
                case 5:
                    offset = xx + (15 - zz) * 16;
                    break;
            }

            int toolMode = GetToolMode(null, byPlayer, blockSel);
            int ix = 1 + toolMode / countPerSide;
            int iz = 1 + toolMode % countPerSide;
            Block blockToPlace = blockAccessor.GetBlock(new AssetLocation(decorBase + "-" + ix + "-" + iz));
            blockAccessor.AddDecor(blockToPlace, blockSel.Position, blockSel.Face.Index + 6 * (1 + offset));
        }

        private bool SuitablePosition(IBlockAccessor blockAccessor, BlockSelection blockSel)
        {
            Block attachingBlock = blockAccessor.GetBlock(blockSel.Position);
            if (attachingBlock.SideSolid[blockSel.Face.Index] || (attachingBlock is BlockMicroBlock && (blockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityMicroBlock).sideAlmostSolid[blockSel.Face.Index]))
            {
                EnumBlockMaterial targetMaterial = attachingBlock.GetBlockMaterial(blockAccessor, blockSel.Position);
                for (int i = 0; i < allowedMaterials.Length; i++) if (targetMaterial == allowedMaterials[i]) return true;
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

            return toolModesForGUI;
        }

        public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer?.Entity == null) return 0;
            return byPlayer.Entity.WatchedAttributes.GetInt("toolModeArt-" + decorBase);
        }

        public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, int toolMode)
        {
             byPlayer?.Entity.WatchedAttributes.SetInt("toolModeArt-" + decorBase, toolMode % numberOfModes);
        }


    }
}
