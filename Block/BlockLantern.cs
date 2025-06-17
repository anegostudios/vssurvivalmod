using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockLantern : Block, ITexPositionSource, IAttachableToEntity
    {
        IAttachableToEntity attrAtta;
        #region IAttachableToEntity
        public int RequiresBehindSlots { get; set; } = 0;
        string IAttachableToEntity.GetCategoryCode(ItemStack stack) => attrAtta?.GetCategoryCode(stack);
        CompositeShape IAttachableToEntity.GetAttachedShape(ItemStack stack, string slotCode) => attrAtta.GetAttachedShape(stack, slotCode);
        string[] IAttachableToEntity.GetDisableElements(ItemStack stack) => attrAtta.GetDisableElements(stack);
        string[] IAttachableToEntity.GetKeepElements(ItemStack stack) => attrAtta.GetKeepElements(stack);
        string IAttachableToEntity.GetTexturePrefixCode(ItemStack stack) => attrAtta.GetTexturePrefixCode(stack);

        void IAttachableToEntity.CollectTextures(ItemStack itemstack, Shape intoShape, string texturePrefixCode, Dictionary<string, CompositeTexture> intoDict)
        {
            string material = itemstack.Attributes.GetString("material");
            string lining = itemstack.Attributes.GetString("lining");
            string glassMaterial = itemstack.Attributes.GetString("glass", "quartz");

            Block glassBlock = api.World.GetBlock(new AssetLocation("glass-" + glassMaterial));

            intoShape.Textures["glass"] = glassBlock.Textures["material"].Base;
            intoShape.Textures["material"] = this.Textures[material].Base;
            intoShape.Textures["lining"] = this.Textures[(lining == null || lining == "plain") ? material : lining].Base;
            intoShape.Textures["material-deco"] = this.Textures["deco-" + material].Base;
        }

        public bool IsAttachable(Entity toEntity, ItemStack itemStack) => true;
        #endregion

        public Size2i AtlasSize { get; set; }
        string curMat, curLining;
        ITexPositionSource glassTextureSource;
        ITexPositionSource tmpTextureSource;

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (textureCode == "material") return tmpTextureSource[curMat];
                if (textureCode == "material-deco") return tmpTextureSource["deco-" + curMat];
                if (textureCode == "lining") return tmpTextureSource[curLining == "plain" ? curMat : curLining];
                if (textureCode == "glass") return glassTextureSource["material"];
                return tmpTextureSource[textureCode];
            }
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            attrAtta = IAttachableToEntity.FromAttributes(this);
        }

        public override string GetHeldTpIdleAnimation(ItemSlot activeHotbarSlot, Entity forEntity, EnumHand hand)
        {
            IPlayer player = (forEntity as EntityPlayer)?.Player;

            if (forEntity.AnimManager.IsAnimationActive("sleep", "wave", "cheer", "shrug", "cry", "nod", "facepalm", "bow", "laugh", "rage", "scythe", "bowaim", "bowhit", "spearidle"))
            {
                return null;
            }

            if (player?.InventoryManager?.ActiveHotbarSlot != null && !player.InventoryManager.ActiveHotbarSlot.Empty && hand == EnumHand.Left)
            {
                ItemStack stack = player.InventoryManager.ActiveHotbarSlot.Itemstack;
                if (stack?.Collectible?.GetHeldTpIdleAnimation(player.InventoryManager.ActiveHotbarSlot, forEntity, EnumHand.Right) != null) return null;

                if (player?.Entity?.Controls.LeftMouseDown == true && stack?.Collectible?.GetHeldTpHitAnimation(player.InventoryManager.ActiveHotbarSlot, forEntity) != null) return null;
            }

            return hand == EnumHand.Left ? "holdinglanternlefthand" : "holdinglanternrighthand";
        }

        public override byte[] GetLightHsv(IBlockAccessor blockAccessor, BlockPos pos, ItemStack stack = null)
        {
            if (pos != null)
            {
                BELantern be = blockAccessor.GetBlockEntity(pos) as BELantern;
                if (be != null)
                {
                    return be.GetLightHsv();
                }
            }

            if (stack != null)
            {
                string lining = stack.Attributes.GetString("lining");
                string material = stack.Attributes.GetString("material");

                int v = this.LightHsv[2] + (lining != "plain" ? 2 : 0);

                byte[] lightHsv = new byte[] { this.LightHsv[0], this.LightHsv[1], (byte)v };
                BELantern.setLightColor(this.LightHsv, lightHsv, stack.Attributes.GetString("glass"));

                return lightHsv;
            }

            if (pos != null)  // This deals with the situation where a lantern at a pos was broken (so the BlockEntity is now null, as lighting updates are delayed) and we have no information about whether the lantern was lined or not: we return HSV *as if* it was lined, to ensure that lighting is fully cleared and no outer ring remains
            {
                int v = this.LightHsv[2] + 3;   // + 3 to match BELantern line 68
                return new byte[] { this.LightHsv[0], this.LightHsv[1], (byte)v };
            }

            return base.GetLightHsv(blockAccessor, pos, stack);
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            Dictionary<string, MultiTextureMeshRef> meshrefs = ObjectCacheUtil.GetOrCreate(capi, "blockLanternGuiMeshRefs", () =>
            {
                return new Dictionary<string, MultiTextureMeshRef>();
            });

            string material = itemstack.Attributes.GetString("material");
            string lining = itemstack.Attributes.GetString("lining");
            string glass = itemstack.Attributes.GetString("glass", "quartz");

            string key = material + "-" + lining + "-" + glass;
            if (!meshrefs.TryGetValue(key, out MultiTextureMeshRef meshref))
            {
                AssetLocation shapeloc = Shape.Base.CopyWithPathPrefixAndAppendixOnce("shapes/", ".json");
                Shape shape = API.Common.Shape.TryGet(capi, shapeloc);

                MeshData mesh = GenMesh(capi, material, lining, glass, shape);
                meshrefs[key] = meshref = capi.Render.UploadMultiTextureMesh(mesh);
            }

            renderinfo.ModelRef = meshref;
            renderinfo.CullFaces = false;
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            if (capi == null) return;

            if (capi.ObjectCache.TryGetValue("blockLanternGuiMeshRefs", out object obj))
            {
                Dictionary<string, MultiTextureMeshRef> meshrefs = obj as Dictionary<string, MultiTextureMeshRef>;

                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove("blockLanternGuiMeshRefs");
            }
        }


        public MeshData GenMesh(ICoreClientAPI capi, string material, string lining, string glassMaterial, Shape shape = null, ITesselatorAPI tesselator = null)
        {
            if (tesselator == null) tesselator = capi.Tesselator;

            tmpTextureSource = tesselator.GetTextureSource(this);

            if (shape == null)
            {
                shape = API.Common.Shape.TryGet(capi, "shapes/" + this.Shape.Base.Path + ".json");
            }

            if (shape == null)
            {
                return null;
            }

            this.AtlasSize = capi.BlockTextureAtlas.Size;
            curMat = material;
            curLining = lining;

            Block glassBlock = capi.World.GetBlock(new AssetLocation("glass-" + glassMaterial));
            glassTextureSource = tesselator.GetTextureSource(glassBlock);
            tesselator.TesselateShape("blocklantern", shape, out MeshData mesh, this, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));
            return mesh;
        }


        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool ok = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);
            if (!ok) return false;

            BELantern be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BELantern;
            if (be != null)
            {
                string material = byItemStack.Attributes.GetString("material");
                string lining = byItemStack.Attributes.GetString("lining");
                string glass = byItemStack.Attributes.GetString("glass");
                be.DidPlace(material, lining, glass);

                BlockPos targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
                double dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
                double dz = byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
                float angleHor = (float)Math.Atan2(dx, dz);

                float deg22dot5rad = GameMath.PIHALF / 4;
                float roundRad = ((int)Math.Round(angleHor / deg22dot5rad)) * deg22dot5rad;
                be.MeshAngle = roundRad;
            }

            return true;
        }



        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            ItemStack stack = new ItemStack(world.GetBlock(CodeWithParts("up")));

            BELantern be = world.BlockAccessor.GetBlockEntity(pos) as BELantern;
            if (be != null)
            {
                stack.Attributes.SetString("material", be.material);
                stack.Attributes.SetString("lining", be.lining);
                stack.Attributes.SetString("glass", be.glass);
            }
            else
            {
                stack.Attributes.SetString("material", "copper");
                stack.Attributes.SetString("lining", "plain");
                stack.Attributes.SetString("glass", "plain");
            }

            return stack;
        }

        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return new BlockDropItemStack[] { new BlockDropItemStack(handbookStack) };
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            bool preventDefault = false;
            foreach (BlockBehavior behavior in BlockBehaviors)
            {
                EnumHandling handled = EnumHandling.PassThrough;

                behavior.OnBlockBroken(world, pos, byPlayer, ref handled);
                if (handled == EnumHandling.PreventDefault) preventDefault = true;
                if (handled == EnumHandling.PreventSubsequent) return;
            }

            if (preventDefault) return;


            if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
            {
                ItemStack[] drops = new ItemStack[] { OnPickBlock(world, pos) };

                if (drops != null)
                {
                    for (int i = 0; i < drops.Length; i++)
                    {
                        world.SpawnItemEntity(drops[i], pos, null);
                    }
                }

                world.PlaySoundAt(Sounds.GetBreakSound(byPlayer), pos, -0.5, byPlayer);
            }

            if (EntityClass != null)
            {
                BlockEntity entity = world.BlockAccessor.GetBlockEntity(pos);
                if (entity != null)
                {
                    entity.OnBlockBroken(byPlayer);
                }
            }

            world.BlockAccessor.SetBlock(0, pos);
        }



        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!byPlayer.Entity.Controls.ShiftKey)
            {
                BELantern bel = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BELantern;
                if (bel.Interact(byPlayer))
                {
                    return true;
                }
                // if Interact returned false, the player had an empty slot so revert to base: right-click pickup
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            string material = itemStack.Attributes.GetString("material");

            return Lang.GetMatching(Code?.Domain + AssetLocation.LocationSeparator + "block-" + Code?.Path + "-" + material);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            string material = inSlot.Itemstack.Attributes.GetString("material");
            string lining = inSlot.Itemstack.Attributes.GetString("lining");
            string glass = inSlot.Itemstack.Attributes.GetString("glass");

            dsc.AppendLine(Lang.Get("Material: {0}", Lang.Get("material-" + material)));
            dsc.AppendLine(Lang.Get("Lining: {0}", lining == "plain" ? "-" : Lang.Get("material-" + lining)));
            if (glass != null) dsc.AppendLine(Lang.Get("Glass: {0}", Lang.Get("glass-" + glass)));
        }

        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
        {
            BELantern be = capi.World.BlockAccessor.GetBlockEntity(pos) as BELantern;
            if (be != null)
            {
                if (Textures.TryGetValue(be.material, out CompositeTexture tex)) return capi.BlockTextureAtlas.GetRandomColor(tex.Baked.TextureSubId, rndIndex);
            }

            return base.GetRandomColor(capi, pos, facing, rndIndex);
        }

        public override List<ItemStack> GetHandBookStacks(ICoreClientAPI capi)
        {
            if (Code == null) return null;

            bool inCreativeTab = CreativeInventoryTabs != null && CreativeInventoryTabs.Length > 0;
            bool inCreativeTabStack = CreativeInventoryStacks != null && CreativeInventoryStacks.Length > 0;
            bool explicitlyIncluded = Attributes?["handbook"]?["include"].AsBool() == true;
            bool explicitlyExcluded = Attributes?["handbook"]?["exclude"].AsBool() == true;

            if (explicitlyExcluded) return null;
            if (!explicitlyIncluded && !inCreativeTab && !inCreativeTabStack) return null;

            List<ItemStack> stacks = new List<ItemStack>();

            if (inCreativeTabStack)
            {
                for (int i = 0; i < CreativeInventoryStacks.Length; i++)
                {
                    for (int j = 0; j < CreativeInventoryStacks[i].Stacks.Length; j++)
                    {
                        ItemStack stack = CreativeInventoryStacks[i].Stacks[j].ResolvedItemstack;
                        stack.ResolveBlockOrItem(capi.World);

                        stack = stack.Clone();
                        stack.StackSize = stack.Collectible.MaxStackSize;

                        if (!stacks.Any((stack1) => stack1.Equals(stack)))
                        {
                            stacks.Add(stack);
                            ItemStack otherGlass = stack.Clone();
                            otherGlass.Attributes.SetString("glass", "plain");
                            stacks.Add(otherGlass);
                            ItemStack otherLiningSilver = stack.Clone();
                            ItemStack otherLiningGold = stack.Clone();
                            ItemStack otherLiningElectrum = stack.Clone();
                            otherLiningSilver.Attributes.SetString("lining", "silver");
                            otherLiningGold.Attributes.SetString("lining", "gold");
                            otherLiningElectrum.Attributes.SetString("lining", "electrum");
                            stacks.Add(otherLiningSilver);
                            stacks.Add(otherLiningGold);
                            stacks.Add(otherLiningElectrum);
                        }
                    }
                }
            }
            else
            {
                stacks.Add(new ItemStack(this));
            }

            return stacks;
        }

        
    }
}
