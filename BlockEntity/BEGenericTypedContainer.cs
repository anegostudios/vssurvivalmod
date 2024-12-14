﻿using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockEntityGenericTypedContainer : BlockEntityOpenableContainer, IRotatable
    {
        internal InventoryGeneric inventory;
        public string type = "normal-generic";
        public string defaultType;
        public int quantitySlots = 16;
        public int quantityColumns = 4;
        public string inventoryClassName = "chest";
        public string dialogTitleLangCode = "chestcontents";
        public bool retrieveOnly = false;

        float meshangle;
        public virtual float MeshAngle
        {
            get { return meshangle; }
            set
            {
                meshangle = value;
                rendererRot.Y = value * GameMath.RAD2DEG;
            }
        }

        MeshData ownMesh;
        public Cuboidf[] collisionSelectionBoxes;

        public virtual string DialogTitle
        {
            get { return Lang.Get(dialogTitleLangCode); }
        }

        public override InventoryBase Inventory
        {
            get { return inventory; }
        }

        public override string InventoryClassName
        {
            get { return inventoryClassName; }
        }

        BlockEntityAnimationUtil animUtil
        {
            get { return GetBehavior<BEBehaviorAnimatable>()?.animUtil; }
        }
        Vec3f rendererRot = new Vec3f();

        public BlockEntityGenericTypedContainer() : base()
        {
        }

        public override void Initialize(ICoreAPI api)
        {
            defaultType = Block.Attributes?["defaultType"]?.AsString("normal-generic");
            if (defaultType == null) defaultType = "normal-generic";
            // Newly placed
            if (inventory == null)
            {
                InitInventory(Block);
            }

            base.Initialize(api);
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            if (byItemStack?.Attributes != null)
            {
                string nowType = byItemStack.Attributes.GetString("type", defaultType);

                if (nowType != type)
                {
                    this.type = nowType;
                    InitInventory(Block);
                    LateInitInventory();
                }
            }

            base.OnBlockPlaced();
        }





        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            string prevType = type;
            type = tree.GetString("type", defaultType);
            MeshAngle = tree.GetFloat("meshAngle", MeshAngle);

            if (inventory == null)
            {
                if (tree.HasAttribute("forBlockId"))
                {
                    InitInventory(worldForResolving.GetBlock((ushort)tree.GetInt("forBlockId")));
                }
                else if (tree.HasAttribute("forBlockCode"))
                {
                    InitInventory(worldForResolving.GetBlock(new AssetLocation(tree.GetString("forBlockCode"))));
                }
                else
                {
                    ITreeAttribute inventroytree = tree.GetTreeAttribute("inventory");
                    int qslots = inventroytree.GetInt("qslots");
                    // Must be a basket
                    if (qslots == 8)
                    {
                        quantitySlots = 8;
                        inventoryClassName = "basket";
                        dialogTitleLangCode = "basketcontents";
                        if (type == null) type = "reed";
                    }

                    InitInventory(null);
                }
            } else if (type != prevType)
            {
                InitInventory(Block);

                if (Api == null) this.Api = worldForResolving.Api; // LateInitInventory needs the api
                LateInitInventory();
            }


            if (Api != null && Api.Side == EnumAppSide.Client)
            {
                ownMesh = null;
                MarkDirty(true);
            }

            base.FromTreeAttributes(tree, worldForResolving);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            if (Block != null) tree.SetString("forBlockCode", Block.Code.ToShortString());

            if (type == null) type = defaultType; // No idea why. Somewhere something has no type. Probably some worldgen ruins

            tree.SetString("type", type);
            tree.SetFloat("meshAngle", MeshAngle);
        }

        protected virtual void InitInventory(Block block)
        {
            if (block?.Attributes != null)
            {
                collisionSelectionBoxes = block.Attributes["collisionSelectionBoxes"]?[type]?.AsObject<Cuboidf[]>();

                inventoryClassName = block.Attributes["inventoryClassName"].AsString(inventoryClassName);

                dialogTitleLangCode = block.Attributes["dialogTitleLangCode"][type].AsString(dialogTitleLangCode);
                quantitySlots = block.Attributes["quantitySlots"][type].AsInt(quantitySlots);
                quantityColumns = block.Attributes["quantityColumns"][type].AsInt(4);

                retrieveOnly = block.Attributes["retrieveOnly"][type].AsBool(false);

                if (block.Attributes["typedOpenSound"][type].Exists)
                {
                    OpenSound = AssetLocation.Create(block.Attributes["typedOpenSound"][type].AsString(OpenSound.ToShortString()), block.Code.Domain);
                }
                if (block.Attributes["typedCloseSound"][type].Exists)
                {
                    CloseSound = AssetLocation.Create(block.Attributes["typedCloseSound"][type].AsString(CloseSound.ToShortString()), block.Code.Domain);
                }
            }

            inventory = new InventoryGeneric(quantitySlots, null, null, null);
            inventory.BaseWeight = 1f;
            inventory.OnGetSuitability = (sourceSlot, targetSlot, isMerge) => (isMerge ? (inventory.BaseWeight + 3) : (inventory.BaseWeight + 1)) + (sourceSlot.Inventory is InventoryBasePlayer ? 1 : 0);
            inventory.OnGetAutoPullFromSlot = GetAutoPullFromSlot;
            container.Reset();

            if (block?.Attributes != null)
            {
                if (block.Attributes["spoilSpeedMulByFoodCat"][type].Exists == true)
                {
                    inventory.PerishableFactorByFoodCategory = block.Attributes["spoilSpeedMulByFoodCat"][type].AsObject<Dictionary<EnumFoodCategory, float>>();
                }

                if (block.Attributes["transitionSpeedMul"][type].Exists == true)
                {
                    inventory.TransitionableSpeedMulByType = block.Attributes["transitionSpeedMul"][type].AsObject<Dictionary<EnumTransitionType, float>>();
                }
            }

            inventory.PutLocked = retrieveOnly;
            inventory.OnInventoryClosed += OnInvClosed;
            inventory.OnInventoryOpened += OnInvOpened;
        }


        public virtual void LateInitInventory()
        {
            Inventory.LateInitialize(InventoryClassName + "-" + Pos.X + "/" + Pos.Y + "/" + Pos.Z, Api);
            Inventory.ResolveBlocksOrItems();
            container.LateInit();
            MarkDirty();
        }


        private ItemSlot GetAutoPullFromSlot(BlockFacing atBlockFace)
        {
            if (atBlockFace == BlockFacing.DOWN)
            {
                return inventory.FirstOrDefault(slot => !slot.Empty);
            }

            return null;
        }

        protected virtual void OnInvOpened(IPlayer player)
        {
            inventory.PutLocked = retrieveOnly && player.WorldData.CurrentGameMode != EnumGameMode.Creative;

            if (Api.Side == EnumAppSide.Client)
            {
                OpenLid();
            }
        }

        public void OpenLid()
        {
            if (animUtil?.activeAnimationsByAnimCode.ContainsKey("lidopen") == false)
            {
                animUtil?.StartAnimation(new AnimationMetaData()
                {
                    Animation = "lidopen",
                    Code = "lidopen",
                    AnimationSpeed = 1.8f,
                    EaseOutSpeed = 6,
                    EaseInSpeed = 15
                });
            }
        }

        public void CloseLid()
        {
            if (animUtil?.activeAnimationsByAnimCode.ContainsKey("lidopen") == true)
            {
                animUtil?.StopAnimation("lidopen");
            }
        }

        protected virtual void OnInvClosed(IPlayer player)
        {
            if (LidOpenEntityId.Count == 0)
            {
                CloseLid();
            }
            inventory.PutLocked = retrieveOnly;

            // This is already handled elsewhere and also causes a stackoverflowexception, but seems needed somehow?
            var inv = invDialog;
            invDialog = null; // Weird handling because to prevent endless recursion
            if (inv?.IsOpened() == true) inv?.TryClose();
            inv?.Dispose();
        }

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative)
            {
                inventory.PutLocked = false;
            }

            if (inventory.PutLocked && inventory.Empty) return false;

            if (Api.World is IServerWorldAccessor)
            {
                var data = BlockEntityContainerOpen.ToBytes("BlockEntityInventory", Lang.Get(dialogTitleLangCode), (byte)quantityColumns, inventory);
                ((ICoreServerAPI)Api).Network.SendBlockEntityPacket(
                    (IServerPlayer)byPlayer,
                    Pos,
                    (int)EnumBlockContainerPacketId.OpenInventory,
                    data
                );

                byPlayer.InventoryManager.OpenInventory(inventory);
                data = SerializerUtil.Serialize(new OpenContainerLidPacket(byPlayer.Entity.EntityId, LidOpenEntityId.Count > 0));
                ((ICoreServerAPI)Api).Network.BroadcastBlockEntityPacket(
                    Pos,
                    (int)EnumBlockContainerPacketId.OpenLidOthers,
                    data,
                    (IServerPlayer)byPlayer
                );
            }

            return true;
        }

        private MeshData GenMesh(ITesselatorAPI tesselator)
        {
            BlockGenericTypedContainer block = Block as BlockGenericTypedContainer;
            if (Block == null)
            {
                block = Api.World.BlockAccessor.GetBlock(Pos) as BlockGenericTypedContainer;
                Block = block;
            }
            if (block == null) return null;
            int rndTexNum = Block.Attributes?["rndTexNum"][type]?.AsInt(0) ?? 0;

            string key = "typedContainerMeshes" + Block.Code.ToShortString();
            Dictionary<string, MeshData> meshes = ObjectCacheUtil.GetOrCreate(Api, key, () =>
            {
                return new Dictionary<string, MeshData>();
            });
            MeshData mesh;

            string shapename = Block.Attributes?["shape"][type].AsString();
            if (shapename == null)
            {
                return null;
            }

            Shape shape=null;
            if (animUtil != null)
            {
                string skeydict = "typedContainerShapes";
                Dictionary<string, Shape> shapes = ObjectCacheUtil.GetOrCreate(Api, skeydict, () =>
                {
                    return new Dictionary<string, Shape>();
                });
                string skey = Block.FirstCodePart() + type + block.Subtype + "-" + "-" + shapename + "-" + rndTexNum;
                if (!shapes.TryGetValue(skey, out shape))
                {
                    shapes[skey] = shape = block.GetShape(Api as ICoreClientAPI, shapename);
                }
            }

            string meshKey = type + block.Subtype + "-" + rndTexNum;
            if (meshes.TryGetValue(meshKey, out mesh))
            {
                if (animUtil != null && animUtil.renderer == null)
                {
                    animUtil.InitializeAnimator(type + "-" + key + "-" + block.Subtype, mesh, shape, rendererRot);
                }

                return mesh;
            }


            if (rndTexNum > 0) rndTexNum = GameMath.MurmurHash3Mod(Pos.X, Pos.Y, Pos.Z, rndTexNum);

            if (animUtil != null)
            {
                if (animUtil.renderer == null)
                {
                    var texSource = new GenericContainerTextureSource()
                    {
                        blockTextureSource = tesselator.GetTextureSource(Block, rndTexNum),
                        curType = type
                    };

                    mesh = animUtil.InitializeAnimator(type + "-" + key + "-" + block.Subtype, shape, texSource, rendererRot);
                }

                return meshes[meshKey] = mesh;
            } else
            {
                mesh = block.GenMesh(Api as ICoreClientAPI, type, shapename, tesselator, new Vec3f(), rndTexNum);

                return meshes[meshKey] = mesh;
            }
        }



        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            bool skipmesh = base.OnTesselation(mesher, tesselator);

            if (!skipmesh)
            {
                if (ownMesh == null)
                {
                    ownMesh = GenMesh(tesselator);
                    if (ownMesh == null) return false;
                }

                mesher.AddMeshData(ownMesh.Clone().Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, MeshAngle, 0));
            }

            return true;
        }

        public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int degreeRotation, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, EnumAxis? flipAxis)
        {
            MeshAngle = tree.GetFloat("meshAngle");
            MeshAngle -= degreeRotation * GameMath.DEG2RAD;
            tree.SetFloat("meshAngle", MeshAngle);
        }
    }
}
