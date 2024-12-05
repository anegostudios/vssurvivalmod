using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockJonasLensTower : Block
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "lensInteractions", () =>
            {
                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-lens-pickup",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                    }
                };
            });
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            GetBlockEntity<BEJonasLensTower>(blockSel)?.OnInteract(byPlayer);
            return true;
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            if (GetBlockEntity<BEJonasLensTower>(selection)?.RecentlyCollectedBy(forPlayer) == true)
            {
                return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
            }

            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }

    public class BEJonasLensTower : BlockEntity
    {
        Dictionary<string, double> totalDaysCollectedByPlrUid = new Dictionary<string, double>();

        double expireDays = 14;
        ICoreClientAPI capi;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            capi = api as ICoreClientAPI;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            totalDaysCollectedByPlrUid.Clear();
            var stree = tree.GetTreeAttribute("totalDaysCollectedByPlrUid");
            if (stree != null)
            {
                foreach (var val in stree)
                {
                    totalDaysCollectedByPlrUid[val.Key] = (val.Value as DoubleAttribute).value;
                }
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            TreeAttribute stree = new TreeAttribute();
            tree["totalDaysCollectedByPlrUid"] = stree;

            foreach (var val in totalDaysCollectedByPlrUid)
            {
                stree[val.Key] = new DoubleAttribute(val.Value);
            }
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (RecentlyCollectedBy(capi.World.Player)) return true; // Dont render when collected

            return base.OnTesselation(mesher, tessThreadTesselator);
        }


        internal void OnInteract(IPlayer byPlayer)
        {
            if (Api.ModLoader.GetModSystem<ModSystemDevastationEffects>().ErelAnnoyed)
            {
                if (RecentlyCollectedBy(byPlayer) || Api.Side == EnumAppSide.Client) return;

                totalDaysCollectedByPlrUid[byPlayer.PlayerUID] = Api.World.Calendar.TotalDays;
                MarkDirty(true);
                var stack = new ItemStack(Api.World.GetBlock("jonaslens-north"));
                if (!byPlayer.InventoryManager.TryGiveItemstack(stack, true))
                {
                    Api.World.SpawnItemEntity(stack, byPlayer.Entity.Pos.XYZ.Add(0, 0.5, 0));
                }
            } else
            {
                (Api as ICoreClientAPI)?.TriggerIngameError(this, "cantpicklens", Lang.Get("devastation-cantpicklens"));
            }
        }

        internal bool RecentlyCollectedBy(IPlayer forPlayer)
        {
            if (totalDaysCollectedByPlrUid.TryGetValue(forPlayer.PlayerUID, out var colldays))
            {
                return Api.World.Calendar.TotalDays - colldays < expireDays;
            }

            return false;
        }
    }
}
