using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockEntityMoldRack : BlockEntityDisplay
    {
        public override InventoryBase Inventory => inv;
        public override string InventoryClassName => "moldrack";
        public override string AttributeTransformCode => "onmoldrackTransform";

        InventoryGeneric inv;
        Block block;
        Matrixf mat = new Matrixf();

        public BlockEntityMoldRack()
        {
            inv = new InventoryGeneric(5, "moldrack-0", null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            block = api.World.BlockAccessor.GetBlock(Pos);
            base.Initialize(api);

            if (api is ICoreClientAPI)
            {
                mat.RotateYDeg(block.Shape.rotateY);
                api.Event.RegisterEventBusListener(OnEventBusEvent);
            }
        }


        private void OnEventBusEvent(string eventname, ref EnumHandling handling, IAttribute data)
        {
            if (eventname != "genjsontransform" && eventname != "oncloseedittransforms" &&
                eventname != "onapplytransforms") return;
            if (Inventory.Empty) return;

            for (var i = 0; i < DisplayedItems; i++)
            {
                if (Inventory[i].Empty) continue;
                var key = getMeshCacheKey(Inventory[i]);
                MeshCache.Remove(key);
            }

            MarkMeshesDirty();
            Api.World.BlockAccessor.MarkBlockDirty(Pos);   // always redraw on client after updating meshes
        }

        internal bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;

            CollectibleObject colObj = slot.Itemstack?.Collectible;
            bool rackable = colObj?.Attributes != null && colObj.Attributes["moldrackable"].AsBool(false) == true;

            if (slot.Empty || !rackable)
            {
                if (TryTake(byPlayer, blockSel))
                {
                    return true;
                }
                return false;
            }
            else
            {
                if (rackable)
                {
                    SoundAttributes? sound = slot.Itemstack?.Block?.Sounds?.Place;

                    var stackName = slot.Itemstack?.Collectible.Code;
                    if (TryPut(slot, blockSel))
                    {
                        Api.World.PlaySoundAt(sound ?? GlobalConstants.DefaultBuildSound, byPlayer.Entity, byPlayer);
                        Api.World.Logger.Audit("{0} Put 1x{1} into Rack at {2}.",
                            byPlayer.PlayerName,
                            stackName,
                            Pos
                        );
                        return true;
                    }

                    return false;
                }
            }


            return false;
        }



        private bool TryPut(ItemSlot slot, BlockSelection blockSel)
        {
            int index = blockSel.SelectionBoxIndex;

            for (int i = 0; i < inv.Count; i++)
            {
                int slotnum = (index + i) % inv.Count;
                if (inv[slotnum].Empty)
                {
                    int moved = slot.TryPutInto(Api.World, inv[slotnum]);
                    MarkDirty();
                    return moved > 0;
                }
            }

            return false;
        }

        private bool TryTake(IPlayer byPlayer, BlockSelection blockSel)
        {
            int index = blockSel.SelectionBoxIndex;

            if (!inv[index].Empty)
            {
                ItemStack stack = inv[index].TakeOut(1);
                if (byPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    SoundAttributes? sound = stack.Block?.Sounds?.Place;
                    Api.World.PlaySoundAt(sound ?? GlobalConstants.DefaultBuildSound, byPlayer.Entity, byPlayer);
                }

                if (stack.StackSize > 0)
                {
                    Api.World.SpawnItemEntity(stack, Pos);
                }
                Api.World.Logger.Audit("{0} Took 1x{1} from Rack at {2}.",
                    byPlayer.PlayerName,
                    stack.Collectible.Code,
                    Pos
                );

                MarkDirty();
                return true;
            }

            return false;
        }

        protected override float[][] genTransformationMatrices()
        {
            float[][] tfMatrices = new float[Inventory.Count][];

            for (int index = 0; index < tfMatrices.Length; index++)
            {
                float x = 3 / 16f + 3 / 16f * index - 1;
                float y = 0;
                float z = 0;

                tfMatrices[index] =
                    new Matrixf()
                    .Translate(0.5f, 0, 0.5f)
                    .RotateYDeg(block.Shape.rotateY)
                    .Translate(x, y, z)
                    .Translate(-0.5f, 0, -0.5f)
                    .Values
                ;
            }

            return tfMatrices;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            if (forPlayer.CurrentBlockSelection == null)
            {
                base.GetBlockInfo(forPlayer, sb);
                return;
            }

            int index = forPlayer.CurrentBlockSelection.SelectionBoxIndex;
            ItemSlot slot = inv[index];
            if (slot.Empty)
            {
                sb.AppendLine(Lang.Get("Empty"));
            }
            else
            {
                sb.AppendLine(slot.Itemstack.GetName());
            }
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            // Do this last!!!
            RedrawAfterReceivingTreeAttributes(worldForResolving);     // Redraw on client after we have completed receiving the update from server
        }
    }
}
