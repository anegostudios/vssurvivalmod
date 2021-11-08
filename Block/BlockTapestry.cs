using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class TapestryTextureSource : ITexPositionSource
    {
        public bool rotten;
        public string type;
        int rotVariant;
        ICoreClientAPI capi;

        public TapestryTextureSource(ICoreClientAPI capi, bool rotten, string type, int rotVariant = 0)
        {
            this.capi = capi;
            this.rotten = rotten;
            this.type = type;
            this.rotVariant = rotVariant;
        }

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                AssetLocation texturePath;

                if (textureCode == "ropedcloth" || type == null || type == "") texturePath = new AssetLocation("block/cloth/ropedcloth");
                else texturePath = new AssetLocation("block/cloth/tapestry/" + type);

                AssetLocation cachedPath = texturePath.Clone();

                AssetLocation rotLoc = null;

                if (rotten)
                {
                    rotLoc = new AssetLocation("block/cloth/tapestryoverlay/rotten" + rotVariant);
                    cachedPath.Path += "++" + rotLoc.Path;
                }

                TextureAtlasPosition texpos = capi.BlockTextureAtlas[cachedPath];
                
                if (texpos == null)
                {
                    IAsset texAsset = capi.Assets.TryGet(texturePath.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                    if (texAsset != null)
                    {
                        BitmapRef bmp = texAsset.ToBitmap(capi);

                        if (rotten)
                        {
                            BakedBitmap bakedBmp = new BakedBitmap() { Width = bmp.Width, Height = bmp.Height };
                            bakedBmp.TexturePixels = bmp.Pixels;

                            int[] texturePixelsOverlay = capi.Assets.TryGet(rotLoc.WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"))?.ToBitmap(capi)?.Pixels;
                            if (texturePixelsOverlay == null)
                            {
                                throw new Exception("Texture file " + rotLoc + " is missing");
                            }

                            for (int p = 0; p < bakedBmp.TexturePixels.Length; p++)
                            {
                                bakedBmp.TexturePixels[p] = ColorUtil.ColorOver(texturePixelsOverlay[p], bakedBmp.TexturePixels[p]);
                            }

                            capi.BlockTextureAtlas.InsertTextureCached(cachedPath, bakedBmp, out _, out texpos);
                        }
                        else
                        {
                            capi.BlockTextureAtlas.InsertTextureCached(cachedPath, bmp, out _, out texpos);
                        }


                    }
                    else
                    {
                        capi.World.Logger.Warning("Tapestry type '{0}' defined texture '{1}', but no such texture found.", type, texturePath);
                    }
                }

                if (texpos == null)
                {
                    return capi.BlockTextureAtlas.UnknownTexturePosition;
                }

                return texpos;
            }
        }
    }

    public class TVec2i : Vec2i
    {
        public string IntComp;

        public TVec2i(int x, int y, string intcomp) : base(x,y)
        {
            this.IntComp = intcomp;
        }
    }

    public class BlockTapestry : Block
    {
        ICoreClientAPI capi;
        BlockFacing orientation;

        static Vec2i left = new Vec2i(-1, 0);
        static Vec2i right = new Vec2i(1, 0);
        static Vec2i up = new Vec2i(0, 1);
        static Vec2i down = new Vec2i(0, -1);

        static Dictionary<string, TVec2i[]> neighbours2x1 = new Dictionary<string, TVec2i[]>()
        {
            { "1", new TVec2i[] { new TVec2i(1, 0, "2") } },
            { "2", new TVec2i[] { new TVec2i(-1, 0, "1") } }
        };

        static Dictionary<string, TVec2i[]> neighbours1x2 = new Dictionary<string, TVec2i[]>()
        {
            { "1", new TVec2i[] { new TVec2i(0, -1, "2") } },
            { "2", new TVec2i[] { new TVec2i(0, 1, "1") } }
        };


        static Dictionary<string, TVec2i[]> neighbours3x1 = new Dictionary<string, TVec2i[]>()
        {
            { "1", new TVec2i[] { new TVec2i(1, 0, "2"), new TVec2i(2, 0, "3") } },
            { "2", new TVec2i[] { new TVec2i(-1, 0, "1"), new TVec2i(1, 0, "3") } },
            { "3", new TVec2i[] { new TVec2i(-2, 0, "1"), new TVec2i(-1, 0, "2") } }
        };

        static Dictionary<string, TVec2i[]> neighbours2x2 = new Dictionary<string, TVec2i[]>()
        {
            // yx
            { "11", new TVec2i[] { new TVec2i(1, 0, "12"), new TVec2i(0, -1, "21"), new TVec2i(1, -1, "22") } },
            { "12", new TVec2i[] { new TVec2i(-1, 0, "11"), new TVec2i(0, -1, "22"), new TVec2i(-1, -1, "21") } },
            { "21", new TVec2i[] { new TVec2i(0, 1, "11"), new TVec2i(1, 0, "22"), new TVec2i(1, 1, "12") } },
            { "22", new TVec2i[] { new TVec2i(0, 1, "12"), new TVec2i(-1, 0, "21"), new TVec2i(-1, 1, "11") } },
        };


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            capi = api as ICoreClientAPI;
            orientation = BlockFacing.FromCode(Variant["side"]);
        }



        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);

            Dictionary<string, MeshRef> tapestryMeshes;
            tapestryMeshes = ObjectCacheUtil.GetOrCreate(capi, "tapestryMeshesInventory", () => new Dictionary<string, MeshRef>());
            renderinfo.NormalShaded = false;
            MeshRef meshref;

            string type = itemstack.Attributes.GetString("type", "");

            if (!tapestryMeshes.TryGetValue(type, out meshref))
            {
                MeshData mesh = genMesh(false, type, 0, true);
                meshref = capi.Render.UploadMesh(mesh);
                tapestryMeshes[type] = meshref;
            }

            renderinfo.ModelRef = meshref;
        }


        public static string GetBaseCode(string type)
        {
            int substr = 0;
            if (char.IsDigit(type[type.Length - 1])) substr++;
            if (char.IsDigit(type[type.Length - 2])) substr++;

            return type.Substring(0, type.Length - substr);
        }


        public override void OnBeingLookedAt(IPlayer byPlayer, BlockSelection blockSel, bool firstTick)
        {
            if (firstTick && api.Side == EnumAppSide.Server)
            {
                BlockEntityTapestry beTas = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityTapestry;
                if (beTas.Rotten) return;

                string baseCode = GetBaseCode(beTas.Type);
                string size = Attributes["sizes"][baseCode].AsString();
                Dictionary<string, TVec2i[]> neighbours;

                switch (size)
                {
                    case "2x1":
                        neighbours = neighbours2x1;
                        break;
                    case "1x2":
                        neighbours = neighbours1x2;
                        break;
                    case "3x1":
                        neighbours = neighbours3x1;
                        break;
                    case "2x2":
                        neighbours = neighbours2x2;
                        break;
                    default:
                        throw new Exception("invalid tapestry json config - missing size attribute for size '" + size + "'");
                }

                string intComp = beTas.Type.Substring(baseCode.Length);
                TVec2i[] vecs = neighbours[intComp];

                if (isComplete(blockSel.Position, baseCode, vecs)) {

                    ModJournal jour = api.ModLoader.GetModSystem<ModJournal>();

                    if (baseCode == "schematic-c-bloody") baseCode = "schematic-c";

                    if (!jour.DidDiscoverLore(byPlayer.PlayerUID, LoreCode, GetLoreChapterId(baseCode)))
                    {
                        var splr = byPlayer as IServerPlayer;
                        jour.DiscoverLore(new LoreDiscovery() { Code = LoreCode, ChapterIds = new List<int>() { GetLoreChapterId(baseCode) } }, splr);
                    }
                }
            }
        }

        public string LoreCode
        {
            get
            {
                return "tapestry";
            }
        }


        public int GetLoreChapterId(string baseCode)
        {
            if (!Attributes["loreChapterIds"][baseCode].Exists) throw new Exception("incomplete tapestry json configuration - missing lore piece id");
            return Attributes["loreChapterIds"][baseCode].AsInt();
        }

        private bool isComplete(BlockPos position, string baseCode, TVec2i[] vecs)
        {
            foreach (var vec in vecs)
            {
                Vec3i offs; 
                switch(orientation.Index)
                {
                    // n
                    case 0: offs = new Vec3i(vec.X, vec.Y, 0); break;
                    // e
                    case 1: offs = new Vec3i(0, vec.Y, vec.X); break;
                    // s
                    case 2: offs = new Vec3i(-vec.X, vec.Y, 0); break;
                    // w
                    case 3: offs = new Vec3i(0, vec.Y, -vec.X); break;

                    default: return false;
                }

                BlockEntityTapestry bet = api.World.BlockAccessor.GetBlockEntity(position.AddCopy(offs.X, offs.Y, offs.Z)) as BlockEntityTapestry;
                
                if (bet == null) return false;
                string nbaseCode = GetBaseCode(bet.Type);
                if (nbaseCode != baseCode) return false;
                if (bet.Rotten) return false;

                string intComp = bet.Type.Substring(nbaseCode.Length);

                if (intComp != vec.IntComp) return false;
            }

            return true;
        }

        public MeshData genMesh(bool rotten, string type, int rotVariant, bool inventory = false)
        {
            MeshData mesh;

            TapestryTextureSource txs = new TapestryTextureSource(capi, rotten, type, rotVariant);
            Shape shape = capi.TesselatorManager.GetCachedShape(inventory ? ShapeInventory.Base : Shape.Base);
            capi.Tesselator.TesselateShape("tapestryblock", shape, out mesh, txs);

            return mesh;
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            ItemStack[] stacks = base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);

            BlockEntityTapestry bet = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityTapestry;

            if (bet.Rotten) return new ItemStack[0];

            stacks[0].Attributes.SetString("type", bet?.Type);

            return stacks;
        }


        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            BlockEntityTapestry bet = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityTapestry;

            ItemStack stack = new ItemStack(this);

            stack.Attributes.SetString("type", bet?.Type);
            stack.Attributes.SetBool("rotten", bet?.Rotten == true);

            return stack;
        }
        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            return base.GetPlacedBlockInfo(world, pos, forPlayer);
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            string type = itemStack.Attributes.GetString("type", "");
            return Lang.Get("tapestry-name", Lang.GetMatching("tapestry-" + type));
        }

        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            BlockEntityTapestry bet = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityTapestry;
            if (bet?.Rotten == true) return Lang.Get("Rotten Tapestry");

            string type = bet?.Type;
            return Lang.Get("tapestry-name", Lang.GetMatching("tapestry-" + type));
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            string type = inSlot.Itemstack.Attributes.GetString("type", "");

            dsc.AppendLine(GetWordedSection(inSlot, world));

            if (withDebugInfo)
            {
                dsc.AppendLine(type);
            }
        }


        public string GetWordedSection(ItemSlot slot, IWorldAccessor world)
        {
            string type = slot.Itemstack.Attributes.GetString("type", "");
            string baseCode = GetBaseCode(type);
            string size = Attributes["sizes"][baseCode].AsString();
            string intComp = type.Substring(baseCode.Length);

            switch (size)
            {
                case "2x1":
                    switch (intComp)
                    {
                        case "1": return Lang.Get("Section: Left Half");
                        case "2": return Lang.Get("Section: Right Half");
                        default: return "unknown";
                    }
                case "1x2":
                    switch (intComp)
                    {
                        case "1": return Lang.Get("Section: Top Half");
                        case "2": return Lang.Get("Section: Bottom Half");
                        default: return "unknown";
                    }
                case "3x1":
                    switch (intComp)
                    {
                        case "1": return Lang.Get("Section: Left third");
                        case "2": return Lang.Get("Section: Center third");
                        case "3": return Lang.Get("Section: Right third");
                        default: return "unknown";
                    }
                case "1x3":
                    switch (intComp)
                    {
                        case "1": return Lang.Get("Section: Top third");
                        case "2": return Lang.Get("Section: Middle third");
                        case "3": return Lang.Get("Section: Bottom third");
                        default: return "unknown";
                    }
                case "4x1":
                    switch (intComp)
                    {
                        case "1": return Lang.Get("Section: Top quarter");
                        case "2": return Lang.Get("Section: Top middle quarter");
                        case "3": return Lang.Get("Section: Bottom middle quarter");
                        case "4": return Lang.Get("Section: Bottom quarter");
                        default: return "unknown";
                    }
                case "2x2":
                    switch (intComp)
                    {
                        case "11": return Lang.Get("Section: Top Left Quarter");
                        case "21": return Lang.Get("Section: Bottom Left Quarter");
                        case "12": return Lang.Get("Section: Top Right Quarter");
                        case "22": return Lang.Get("Section: Bottom Right Quarter");
                        default: return "unknown";
                    }
                default:
                    throw new Exception("invalid tapestry json config - missing size attribute for size '" + size + "'");
            }


            return "";
        }


    }
}
