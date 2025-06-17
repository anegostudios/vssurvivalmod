using Cairo;
using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.GameContent
{
    public class OwnedEntityMapLayer : MarkerMapLayer
    {
        Dictionary<long, OwnedEntityMapComponent> MapComps = new Dictionary<long, OwnedEntityMapComponent>();
        ICoreClientAPI capi;

        LoadedTexture otherTexture;

        public override string Title => "Owned Creatures";
        public override EnumMapAppSide DataSide => EnumMapAppSide.Client;

        public override string LayerGroupCode => "ownedcreatures";

        public OwnedEntityMapLayer(ICoreAPI api, IWorldMapManager mapsink) : base(api, mapsink)
        {
            capi = api as ICoreClientAPI;
        }

        public void Reload()
        {
            Dispose();
            OnMapOpenedClient();
        }

        public override void OnMapOpenedClient()
        {
            int size = (int)GuiElement.scaled(32);

            if (otherTexture == null)
            {
                ImageSurface surface = new ImageSurface(Format.Argb32, size, size);
                Context ctx = new Context(surface);
                ctx.SetSourceRGBA(0, 0, 0, 0);
                ctx.Paint();
                capi.Gui.Icons.DrawMapPlayer(ctx, 0, 0, size, size, new double[] { 0.3, 0.3, 0.3, 1 }, new double[] { 0.95, 0.95, 0.95, 1 });
                otherTexture = new LoadedTexture(capi, capi.Gui.LoadCairoTexture(surface, false), size / 2, size / 2);
                ctx.Dispose();
                surface.Dispose();
            }

            var mseo = capi.ModLoader.GetModSystem<ModSystemEntityOwnership>();

            foreach (var cmp in MapComps.Values)
            {
                cmp?.Dispose();
            }
            MapComps.Clear();

            foreach (var eo in mseo.SelfOwnerShips)
            {
                MapComps[eo.Value.EntityId] = new OwnedEntityMapComponent(capi, otherTexture, eo.Value, eo.Value.Color);
            }
        }


        public override void Render(GuiElementMap mapElem, float dt)
        {
            if (!Active) return;
            foreach (var val in MapComps) val.Value.Render(mapElem, dt);
        }

        public override void OnMouseMoveClient(MouseEvent args, GuiElementMap mapElem, StringBuilder hoverText)
        {
            if (!Active) return;
            foreach (var val in MapComps) val.Value.OnMouseMove(args, mapElem, hoverText);
        }

        public override void OnMouseUpClient(MouseEvent args, GuiElementMap mapElem)
        {
            if (!Active) return;
            foreach (var val in MapComps) val.Value.OnMouseUpOnElement(args, mapElem);
        }

        public override void Dispose()
        {
            foreach (var val in MapComps)
            {
                val.Value?.Dispose();
            }

            otherTexture?.Dispose();
            otherTexture = null;
        }
    }
}
