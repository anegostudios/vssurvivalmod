using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ChunkMapComponent : MapComponent
    {
        public float renderZ = 50;
        public Vec2i chunkCoord;
        public LoadedTexture Texture;

        Vec3d worldPos;
        Vec2f viewPos = new Vec2f();

        public ChunkMapComponent(ICoreClientAPI capi, Vec2i chunkCoord) : base(capi)
        {
            this.chunkCoord = chunkCoord;
            int chunksize = capi.World.BlockAccessor.ChunkSize;

            worldPos = new Vec3d(chunkCoord.X * chunksize, 0, chunkCoord.Y * chunksize);
        }


        public override void Render(GuiElementMap map, float dt)
        {
            map.TranslateWorldPosToViewPos(worldPos, ref viewPos);

            capi.Render.Render2DTexture(
                Texture.TextureId,
                (int)(map.Bounds.renderX + viewPos.X),
                (int)(map.Bounds.renderY + viewPos.Y),
                (int)(Texture.Width * map.ZoomLevel),
                (int)(Texture.Height * map.ZoomLevel),
                renderZ
            );
        }
        
    }

    public abstract class MapComponent
    {
        public ICoreClientAPI capi;

        public MapComponent(ICoreClientAPI capi)
        {
            this.capi = capi;
        }
        


        public virtual void Render(GuiElementMap map, float dt)
        {
            
        }


        public virtual void Dispose()
        {

        }

        public virtual void OnMouseMove(MouseEvent args, GuiElementMap mapElem, StringBuilder hoverText)
        {
            
        }
    }
}
