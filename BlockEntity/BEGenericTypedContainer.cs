using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockEntityGenericTypedContainer : BlockEntityOpenableContainer
    {
        internal InventoryGeneric inventory;
        public string type = "normal-generic";
        public string defaultType;

        public int quantitySlots = 16;
        public string inventoryClassName = "chest";
        public string dialogTitleLangCode = "chestcontents";
        public bool retrieveOnly = false;

        public virtual float MeshAngle { get; set; }

        MeshData ownMesh;

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
                    Inventory.LateInitialize(InventoryClassName + "-" + Pos.X + "/" + Pos.Y + "/" + Pos.Z, Api);
                    Inventory.ResolveBlocksOrItems();
                    Inventory.OnAcquireTransitionSpeed = Inventory_OnAcquireTransitionSpeed;
                    MarkDirty();
                }
            }

            base.OnBlockPlaced();
        }
       
        



        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
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
            }

            if (Api != null && Api.Side == EnumAppSide.Client)
            {
                ownMesh = null;
                MarkDirty(true);
            }

            base.FromTreeAtributes(tree, worldForResolving);
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
            //this.Block = block; - whats this good for? o.O
            block = this.Block;

            if (block?.Attributes != null)
            {
                inventoryClassName = block.Attributes["inventoryClassName"].AsString(inventoryClassName);

                dialogTitleLangCode = block.Attributes["dialogTitleLangCode"][type].AsString(dialogTitleLangCode);
                quantitySlots = block.Attributes["quantitySlots"][type].AsInt(quantitySlots);
                retrieveOnly = block.Attributes["retrieveOnly"][type].AsBool(false);
            }

            inventory = new InventoryGeneric(quantitySlots, null, null, null);
            inventory.BaseWeight = 1f;
            inventory.OnGetSuitability = (sourceSlot, targetSlot, isMerge) => (isMerge ? (inventory.BaseWeight + 3) : (inventory.BaseWeight + 1)) + (sourceSlot.Inventory is InventoryBasePlayer ? 1 : 0);
            inventory.OnGetAutoPullFromSlot = GetAutoPullFromSlot;


            if (block?.Attributes != null)
            {
                if (block.Attributes["spoilSpeedMulByFoodCat"][type].Exists == true)
                {
                    inventory.PerishableFactorByFoodCategory = block.Attributes["spoilSpeedMulByFoodCat"][type].AsObject<Dictionary<EnumFoodCategory, float>>();
                }

                if (block.Attributes["transitionSpeedMulByType"][type].Exists == true)
                {
                    inventory.TransitionableSpeedMulByType = block.Attributes["transitionSpeedMulByType"][type].AsObject<Dictionary<EnumTransitionType, float>>();
                }
            }

            inventory.PutLocked = retrieveOnly;
            inventory.OnInventoryClosed += OnInvClosed;
            inventory.OnInventoryOpened += OnInvOpened;
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
        }

        protected virtual void OnInvClosed(IPlayer player)
        {
            inventory.PutLocked = retrieveOnly;

            // This is already handled elsewhere and also causes a stackoverflowexception, but seems needed somehow?
            var inv = invDialog;
            invDialog = null; // Weird handling because to prevent endless recursion
            if (invDialog?.IsOpened() == true) inv?.TryClose();
            inv?.Dispose();
        }

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative)
            {
                inventory.PutLocked = false;
            }

            if (inventory.PutLocked && inventory.IsEmpty) return false;

            if (Api.World is IServerWorldAccessor)
            {
                byte[] data;

                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryWriter writer = new BinaryWriter(ms);
                    writer.Write("BlockEntityInventory");
                    writer.Write(DialogTitle);
                    writer.Write((byte)4);
                    TreeAttribute tree = new TreeAttribute();
                    inventory.ToTreeAttributes(tree);
                    tree.ToBytes(writer);
                    data = ms.ToArray();
                }

                ((ICoreServerAPI)Api).Network.SendBlockEntityPacket(
                    (IServerPlayer)byPlayer,
                    Pos.X, Pos.Y, Pos.Z,
                    (int)EnumBlockContainerPacketId.OpenInventory,
                    data
                );

                byPlayer.InventoryManager.OpenInventory(inventory);
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
            
            string key = "typedContainerMeshes" + Block.FirstCodePart() + block.Subtype;

            Dictionary<string, MeshData> meshes = ObjectCacheUtil.GetOrCreate(Api, key, () =>
            {
                return new Dictionary<string, MeshData>();
            });

            MeshData mesh;
            
            if (meshes.TryGetValue(type + block.Subtype, out mesh))
            {
                return mesh;
            }

            string shapename = Block.Attributes?["shape"][type].AsString();
            if (shapename == null)
            {
                return null;
            }
            
            return meshes[type + block.Subtype] = block.GenMesh(Api as ICoreClientAPI, type, shapename, tesselator, new Vec3f());
        }



        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (ownMesh == null)
            {
                ownMesh = GenMesh(tesselator);
                if (ownMesh == null) return false;
            }

            mesher.AddMeshData(ownMesh.Clone().Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, MeshAngle, 0));

            /*string facing = ownBlock.LastCodePart();
            if (facing == "north") { mesher.AddMeshData(ownMesh.Clone().Rotate(new API.MathTools.Vec3f(0.5f, 0.5f, 0.5f), 0, 1 * GameMath.PIHALF, 0)); }
            if (facing == "east") { mesher.AddMeshData(ownMesh.Clone().Rotate(new API.MathTools.Vec3f(0.5f, 0.5f, 0.5f), 0, 0 * GameMath.PIHALF, 0)); }
            if (facing == "south") { mesher.AddMeshData(ownMesh.Clone().Rotate(new API.MathTools.Vec3f(0.5f, 0.5f, 0.5f), 0, 3 * GameMath.PIHALF, 0)); }
            if (facing == "west") { mesher.AddMeshData(ownMesh.Clone().Rotate(new API.MathTools.Vec3f(0.5f, 0.5f, 0.5f), 0, 2 * GameMath.PIHALF, 0)); }*/

            return true;
        }
    }
}
