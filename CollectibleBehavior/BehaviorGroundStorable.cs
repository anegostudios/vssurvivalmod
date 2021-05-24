using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public enum EnumGroundStorageLayout
    {
        /// <summary>
        /// Single item in the center
        /// </summary>
        SingleCenter,
        /// <summary>
        /// One item left, one item right
        /// </summary>
        Halves,
        /// <summary>
        /// One item in each 4 corners
        /// </summary>
        Quadrants,
        /// <summary>
        /// A generic stack of items
        /// </summary>
        Stacking
    }


    public class GroundStorageProperties
    {
        public EnumGroundStorageLayout Layout = EnumGroundStorageLayout.SingleCenter;
        public AssetLocation PlaceRemoveSound = new AssetLocation("sounds/player/build");
        public bool RandomizeSoundPitch;
        public AssetLocation StackingModel;
        public int? TessQuantityElements;
        public Dictionary<string, AssetLocation> StackingTextures;
        public int MaxStackingHeight = 1;
        public int StackingCapacity = 1;
        public int TransferQuantity = 1;
        public int BulkTransferQuantity = 4;

        public Cuboidf CollisionBox;
        public float CbHeightMulFactor = 0;
    }


    public class CollectibleBehaviorGroundStorable : CollectibleBehavior
    {
        public GroundStorageProperties StorageProps { 
            get; 
            protected set; 
        }

        public CollectibleBehaviorGroundStorable(CollectibleObject collObj) : base(collObj)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            StorageProps = properties.AsObject<GroundStorageProperties>(null, collObj.Code.Domain);
        }


        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            Interact(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
        {
            handling = EnumHandling.PassThrough;
            return new WorldInteraction[]
            {
                new WorldInteraction
                {
                    HotKeyCode = "sneak",
                    ActionLangCode = "heldhelp-place",
                    MouseButton = EnumMouseButton.Right
                }
            };
        }



        public static void Interact(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            IWorldAccessor world = byEntity?.World;

            if (blockSel == null || world == null || !byEntity.Controls.Sneak) return;


            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = world.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            if (byPlayer == null) return;

            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                itemslot.MarkDirty();
                return;
            }

            BlockGroundStorage blockgs = world.GetBlock(new AssetLocation("groundstorage")) as BlockGroundStorage;
            if (blockgs == null) return;

            BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            BlockEntity beAbove = world.BlockAccessor.GetBlockEntity(blockSel.Position.UpCopy());
            if (be is BlockEntityGroundStorage || beAbove is BlockEntityGroundStorage)
            {
                if (((be as BlockEntityGroundStorage) ?? (beAbove as BlockEntityGroundStorage)).OnPlayerInteract(byPlayer, blockSel))
                {
                    handHandling = EnumHandHandling.PreventDefault;
                }
                return;
            }

            // Must be aiming at the up face
            if (blockSel.Face != BlockFacing.UP) return;
            Block onBlock = world.BlockAccessor.GetBlock(blockSel.Position);

            // Must have a support below
            if (!onBlock.CanAttachBlockAt(world.BlockAccessor, blockgs, blockSel.Position, BlockFacing.UP))
            {
                return;
            }

            // Must have empty space above
            BlockPos pos = blockSel.Position.AddCopy(blockSel.Face);
            if (world.BlockAccessor.GetBlock(pos).Replaceable < 6000) return;


            if (blockgs.CreateStorage(byEntity.World, blockSel, byPlayer))
            {
                handHandling = EnumHandHandling.PreventDefault;
            }
        }



    }
}
