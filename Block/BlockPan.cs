using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class PanningDrop
    {
        public EnumItemClass Type;
        public string Code;
        public NatFloat Chance;

        public ItemStack ResolvedStack;
    }

    public class BlockPan : Block, ITexPositionSource
    {
        public int AtlasSize { get; set; }

        ITexPositionSource ownTextureSource;
        TextureAtlasPosition matTexPosition;
        
        ILoadedSound sound;
        PanningDrop[] drops;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            drops = Attributes["panningDrops"].AsObject<PanningDrop[]>();

            for (int i = 0; i < drops.Length; i++)
            {
                if (drops[i].Code.Contains("{rocktype}")) continue;
                drops[i].ResolvedStack = Resolve(drops[i].Type, drops[i].Code);
                
            }
        }

        private ItemStack Resolve(EnumItemClass type, string code)
        {
            if (type == EnumItemClass.Block)
            {
                Block block = api.World.GetBlock(new AssetLocation(code));
                if (block == null)
                {
                    api.World.Logger.Error("Failed resolving panning block drop with code {0}. Will skip.", code);
                    return null;
                }
                return new ItemStack(block);

            }
            else
            {
                Item item = api.World.GetItem(new AssetLocation(code));
                if (item == null)
                {
                    api.World.Logger.Error("Failed resolving panning item drop with code {0}. Will skip.", code);
                    return null;
                }
                return new ItemStack(item);
            }
        }

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (textureCode == "material") return matTexPosition;
                return ownTextureSource[textureCode];
            }
        }

        public string GetBlockMaterialCode(ItemStack stack)
        {
            return stack.Attributes.GetString("materialBlockCode", null);
        }

        public void SetMaterial(ItemSlot slot, Block block)
        {
            slot.Itemstack.Attributes.SetString("materialBlockCode", block.Code.ToShortString());
        }

        public void RemoveMaterial(ItemSlot slot)
        {
            slot.Itemstack.Attributes.RemoveAttribute("materialBlockCode");
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            string blockMaterialCode = GetBlockMaterialCode(itemstack);

            if (blockMaterialCode == null) return;

            string key = "pan-filled-" + blockMaterialCode;

            renderinfo.ModelRef = ObjectCacheUtil.GetOrCreate<MeshRef>(capi, key, () =>
            {
                AssetLocation shapeloc = new AssetLocation("shapes/block/wood/pan/filled.json");
                Shape shape = capi.Assets.TryGet(shapeloc).ToObject<Shape>();
                MeshData meshdata;

                Block block = capi.World.GetBlock(new AssetLocation(blockMaterialCode));
                AtlasSize = capi.BlockTextureAtlas.Size;
                matTexPosition = capi.BlockTextureAtlas.GetPosition(block, "up");
                ownTextureSource = capi.Tesselator.GetTexSource(this);

                capi.Tesselator.TesselateShape("filledpan", shape, out meshdata, this);

                return capi.Render.UploadMesh(meshdata);
            });
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            handling = EnumHandHandling.PreventDefault;

            if (!firstEvent)
            {
                return;
            }

            string blockMatCode = GetBlockMaterialCode(slot.Itemstack);

            if (!byEntity.FeetInLiquid && api.Side == EnumAppSide.Client && blockMatCode != null)
            {
                (api as ICoreClientAPI).TriggerIngameError(this, "notinwater", Lang.Get("ingameerror-panning-notinwater"));
                return;
            }

            if (blockMatCode == null && blockSel != null)
            {
                TryTakeMaterial(slot, byEntity, blockSel.Position);
                slot.Itemstack.TempAttributes.SetBool("canpan", false);
                return;
            }

            if (blockMatCode != null)
            {
                slot.Itemstack.TempAttributes.SetBool("canpan", true);
            }
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            string blockMaterialCode = GetBlockMaterialCode(slot.Itemstack);
            if (blockMaterialCode == null || !slot.Itemstack.TempAttributes.GetBool("canpan")) return false;
            
            Vec3d pos = byEntity.Pos.AheadCopy(0.4f).XYZ;
            pos.Y += byEntity.EyeHeight - 0.4f;

            if (secondsUsed > 0.5f && (int)(30 * secondsUsed) % 7 == 1)
            {
                Block block = api.World.GetBlock(new AssetLocation(blockMaterialCode));
                byEntity.World.SpawnCubeParticles(pos, new ItemStack(block), 0.3f, 4, 0.35f, (byEntity as EntityPlayer)?.Player);

            }


            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();

                tf.EnsureDefaultValues();

                tf.Origin.Set(0f, 0, 0f);

                if (secondsUsed > 0.5f)
                {
                    tf.Translation.X = Math.Min(0.25f, GameMath.Cos(10 * secondsUsed) / 4f);
                    tf.Translation.Y = Math.Min(0.15f, GameMath.Sin(10 * secondsUsed) / 6.666f);

                    if (sound == null)
                    {
                        sound = (api as ICoreClientAPI).World.LoadSound(new SoundParams()
                        {
                            Location = new AssetLocation("sounds/player/panning.ogg"),
                            ShouldLoop = false,
                            RelativePosition = true,
                            Position = new Vec3f(),
                            DisposeOnFinish = true,
                            Volume = 0.5f,
                            Range = 8
                        });

                        sound.Start();
                    }
                }

                tf.Translation.X -= Math.Min(1.6f, secondsUsed * 4 * 1.57f);
                tf.Translation.Y -= Math.Min(0.1f, secondsUsed * 2);
                tf.Translation.Z -= Math.Min(1f, secondsUsed * 180);

                tf.Scale = 1 + Math.Min(0.6f, 2 * secondsUsed);
                
                
                byEntity.Controls.UsingHeldItemTransformAfter = tf;

                return secondsUsed <= 4f;
            }

            // Let the client decide when he is done eating
            return true;
        }


        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            if (api.Side == EnumAppSide.Client)
            {
                sound?.Stop();
                sound = null;
            }
            return true;
        }


        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            sound?.Stop();
            sound = null;

            if (secondsUsed >= 3.4f)
            {
                string code = GetBlockMaterialCode(slot.Itemstack);

                if (api.Side == EnumAppSide.Server)
                {
                    CreateDrop(byEntity, code.Split('-')[1]);
                }

                RemoveMaterial(slot);
                slot.MarkDirty();
                (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();

                byEntity.GetBehavior<EntityBehaviorHunger>()?.ConsumeSaturation(3f);
            }
        }



        private void CreateDrop(EntityAgent byEntity, string rocktype)
        {
            IPlayer player = (byEntity as EntityPlayer)?.Player;

            for (int i = 0; i < drops.Length; i++)
            {
                double rnd = api.World.Rand.NextDouble();

                PanningDrop drop = drops[i];
                float val= drop.Chance.nextFloat();


                ItemStack stack = drop.ResolvedStack;

                if (drops[i].Code.Contains("{rocktype}"))
                {
                    stack = Resolve(drops[i].Type, drops[i].Code.Replace("{rocktype}", rocktype));
                }

                if (rnd < val && stack != null)
                {
                    stack = stack.Clone();
                    if (player == null || !player.InventoryManager.TryGiveItemstack(stack, true))
                    {
                        api.World.SpawnItemEntity(stack, byEntity.ServerPos.XYZ);
                    }
                    break;
                }

            }

        }




        private void TryTakeMaterial(ItemSlot slot, EntityAgent byEntity, BlockPos position)
        {
            Block block = api.World.BlockAccessor.GetBlock(position);
            if ((block.BlockMaterial == EnumBlockMaterial.Gravel || block.BlockMaterial == EnumBlockMaterial.Sand) && block.Variant.ContainsKey("rock"))
            {
                if (api.World.BlockAccessor.GetBlock(position.UpCopy()).Id != 0)
                {
                    if (api.Side == EnumAppSide.Client)
                    {
                        (api as ICoreClientAPI).TriggerIngameError(this, "noair", Lang.Get("ingameerror-panning-requireairabove"));
                    }
                    return;
                }

                if (block.Variant.ContainsKey("layer"))
                {
                    Block origblock = api.World.GetBlock(new AssetLocation(block.FirstCodePart() + "-" + block.Variant["rock"]));
                    SetMaterial(slot, origblock);
                    
                    string layer = block.Variant["layer"];

                    if (layer == "1")
                    {
                        api.World.BlockAccessor.SetBlock(0, position);
                    } else
                    {
                        Block reducedBlock = api.World.GetBlock(new AssetLocation(block.FirstCodePart() + "-" + block.Variant["rock"] + "-" + (int.Parse(layer) - 1)));
                        api.World.BlockAccessor.SetBlock(reducedBlock.BlockId, position);
                        api.World.BlockAccessor.TriggerNeighbourBlockUpdate(position);
                    }
                    
                } else
                {
                    SetMaterial(slot, block);
                    Block reducedBlock = api.World.GetBlock(new AssetLocation(block.FirstCodePart() + "-" + block.Variant["rock"] + "-7"));
                    api.World.BlockAccessor.SetBlock(reducedBlock.BlockId, position);
                    api.World.BlockAccessor.TriggerNeighbourBlockUpdate(position);
                }

                slot.MarkDirty();
                (byEntity as EntityPlayer)?.Player?.InventoryManager.BroadcastHotbarSlot();
            }
        }
    }
}
