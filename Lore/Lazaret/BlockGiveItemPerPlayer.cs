using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockBehaviorGiveItemPerPlayer : BlockBehavior
    {
        string interactionHelpCode;
        public BlockBehaviorGiveItemPerPlayer(Block block) : base(block)
        {
        }
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            interactionHelpCode = this.block.Attributes["interactionHelpCode"].AsString();
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            block.GetBEBehavior<BEBehaviorGiveItemPerPlayer>(blockSel.Position)?.OnInteract(byPlayer);
            handling = EnumHandling.PreventDefault;
            return true;
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handling)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = interactionHelpCode,
                    MouseButton = EnumMouseButton.Right,
                }
            };
        }
    }

    public class BEBehaviorGiveItemPerPlayer : BlockEntityBehavior
    {
        public Dictionary<string, double> retrievedTotalDaysByPlayerUid = new Dictionary<string, double>();
        double resetDays;

        bool selfRetrieved = false;

        public BEBehaviorGiveItemPerPlayer(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
            resetDays = Block.Attributes?["resetAfterDays"].AsDouble(-1) ?? -1;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            var rtree = tree.GetTreeAttribute("retrievedTotalDaysByPlayerUid");
            if (rtree != null)
            {
                foreach (var val in rtree)
                {
                    retrievedTotalDaysByPlayerUid[val.Key] = (val.Value as DoubleAttribute).value;
                }
            }

            if (Api is ICoreClientAPI capi)
            {
                selfRetrieved = false;
                if (retrievedTotalDaysByPlayerUid.TryGetValue(capi.World.Player.PlayerUID, out var recievedTotalDays)) {
                    selfRetrieved = resetDays < 0 || (Api.World.Calendar.TotalDays - recievedTotalDays < resetDays);
                }                
            }

            base.FromTreeAttributes(tree, worldAccessForResolve);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            var rtree = new TreeAttribute();

            foreach (var val in retrievedTotalDaysByPlayerUid)
            {
                rtree.SetDouble(val.Key, val.Value);
            }

            tree["retrievedTotalDaysByPlayerUid"] = rtree;
            base.ToTreeAttributes(tree);
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (selfRetrieved)
            {
                var capi = Api as ICoreClientAPI;
                var cshape = Block.Attributes["lootedShape"].AsObject<CompositeShape>();
                if (cshape != null) {
                    var texSource = capi.Tesselator.GetTextureSource(Block);
                    capi.Tesselator.TesselateShape("lootedShape", cshape.Base, cshape, out var meshdata, texSource);
                    mesher.AddMeshData(meshdata);
                    return true;
                }
            }

            return base.OnTesselation(mesher, tessThreadTesselator);

        }

        public void OnInteract(IPlayer byPlayer)
        {
            if (byPlayer == null || Api.Side != EnumAppSide.Server) return;

            if (retrievedTotalDaysByPlayerUid.TryGetValue(byPlayer.PlayerUID, out var recievedTotalDays))
            {
                if (resetDays < 0 || Api.World.Calendar.TotalDays - recievedTotalDays < resetDays) return;
            }

            retrievedTotalDaysByPlayerUid[byPlayer.PlayerUID] = Api.World.Calendar.TotalDays;

            var jstack = Block.Attributes["giveItem"].AsObject<JsonItemStack>();
            if (jstack == null)
            {
                Api.Logger.Warning("Block code " + Block.Code + " attribute giveItem has GiveItemPerPlayer behavior but no giveItem defined");
                return;
            }
            if (!jstack.Resolve(Api.World, "Block code " + Block.Code + " attribute giveItem", true)) return;

            if (!byPlayer.InventoryManager.TryGiveItemstack(jstack.ResolvedItemstack))
            {
                Api.World.SpawnItemEntity(jstack.ResolvedItemstack, Pos);
            }

            Blockentity.MarkDirty(true);
        }
    }
}
