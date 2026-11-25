using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    // Every material of one texture on the same atlas page so instances of a shared mesh only differ by a UV offset.
    public class MechanicalMaterialPalette
    {
        readonly CompositeTexture[] tiles;
        readonly int tileWidth;
        readonly int tileHeight;
        readonly int columns;
        readonly TextureAtlasPosition palettePos;

        public Dictionary<string, TextureAtlasPosition> Positions { get; } = [];

        public TextureAtlasPosition ReferencePosition { get; }

        public MechanicalMaterialPalette(ICoreClientAPI capi, CompositeTexture defaultTexture, IReadOnlyList<KeyValuePair<string, CompositeTexture>> materialTextures)
        {
            List<CompositeTexture> uniqueTiles = [bake(capi, defaultTexture)];
            Dictionary<string, int> tileByName = new() { [uniqueTiles[0].Baked.BakedName] = 0 };
            Dictionary<string, int> tileByMaterial = [];
            foreach (var val in materialTextures)
            {
                CompositeTexture texture = bake(capi, val.Value);
                if (!tileByName.TryGetValue(texture.Baked.BakedName, out int tile))
                {
                    tileByName[texture.Baked.BakedName] = tile = uniqueTiles.Count;
                    uniqueTiles.Add(texture);
                }
                tileByMaterial[val.Key] = tile;
            }
            tiles = uniqueTiles.ToArray();

            ITextureAtlasAPI atlas = capi.BlockTextureAtlas;
            TextureAtlasPosition[] sourcePositions = insertTiles(atlas);
            try
            {
                tileWidth = getTileSize(atlas, sourcePositions[0], out tileHeight);
                validateTileSizes(atlas, sourcePositions);

                if (tileWidth > atlas.Size.Width || tileHeight > atlas.Size.Height
                    || tiles.Length > (long)(atlas.Size.Width / tileWidth) * (atlas.Size.Height / tileHeight))
                {
                    throw new InvalidOperationException($"Mechanical material palette of {tiles[0].Base} with {tiles.Length} tiles of {tileWidth}x{tileHeight} does not fit in the {atlas.Size.Width}x{atlas.Size.Height} atlas");
                }

                columns = Math.Min(tiles.Length, atlas.Size.Width / tileWidth);
                int rows = (tiles.Length - 1) / columns + 1;
                if (!atlas.AllocateTextureSpace(tileWidth * columns, tileHeight * rows, out int textureSubId, out palettePos))
                {
                    throw new InvalidOperationException($"Could not allocate atlas space for the mechanical material palette of {tiles[0].Base}");
                }

                try
                {
                    TextureAtlasPosition[] tilePositions = new TextureAtlasPosition[tiles.Length];
                    for (int i = 0; i < tiles.Length; i++) tilePositions[i] = getTilePosition(atlas, i, sourcePositions[i]);
                    ReferencePosition = tilePositions[0];
                    foreach (var val in tileByMaterial) Positions[val.Key] = tilePositions[val.Value];

                    renderTiles(capi, sourcePositions);
                }
                catch
                {
                    atlas.FreeTextureSpace(textureSubId);
                    throw;
                }
            }
            finally
            {
                releaseTiles(atlas, sourcePositions.Length);
            }
        }

        public void Redraw(ICoreClientAPI capi)
        {
            ITextureAtlasAPI atlas = capi.BlockTextureAtlas;
            TextureAtlasPosition[] sourcePositions = insertTiles(atlas);
            try
            {
                validateTileSizes(atlas, sourcePositions);
                renderTiles(capi, sourcePositions);
            }
            finally
            {
                releaseTiles(atlas, sourcePositions.Length);
            }
        }

        static CompositeTexture bake(ICoreClientAPI capi, CompositeTexture texture)
        {
            CompositeTexture baked = texture.Clone();
            baked.Bake(capi.Assets);
            return baked;
        }

        TextureAtlasPosition[] insertTiles(ITextureAtlasAPI atlas)
        {
            TextureAtlasPosition[] positions = new TextureAtlasPosition[tiles.Length];
            int inserted = 0;
            try
            {
                for (; inserted < tiles.Length; inserted++)
                {
                    if (!atlas.InsertTextureOrAddReference(tiles[inserted], out _, out positions[inserted]))
                    {
                        throw new InvalidOperationException($"Could not load mechanical material texture {tiles[inserted].Base}");
                    }
                }
                return positions;
            }
            catch
            {
                releaseTiles(atlas, inserted);
                throw;
            }
        }

        void releaseTiles(ITextureAtlasAPI atlas, int count)
        {
            for (int i = 0; i < count; i++) atlas.ReleaseTextureReference(tiles[i].Baked.BakedName);
        }

        void validateTileSizes(ITextureAtlasAPI atlas, TextureAtlasPosition[] sourcePositions)
        {
            for (int i = 0; i < sourcePositions.Length; i++)
            {
                if (getTileSize(atlas, sourcePositions[i], out int height) != tileWidth || height != tileHeight)
                {
                    throw new InvalidOperationException($"Mechanical material texture {tiles[i].Base} does not match the palette tile size of {tileWidth}x{tileHeight}");
                }
            }
        }

        void renderTiles(ICoreClientAPI capi, TextureAtlasPosition[] sourcePositions)
        {
            ITextureAtlasAPI atlas = capi.BlockTextureAtlas;
            Size2i atlasSize = atlas.Size;
            // Copy through a separate texture to avoid framebuffer feedback, which is still undefined on OpenGL 4.1.
            RawTexture tileTexture = new() { Width = tileWidth, Height = tileHeight, MinFilter = EnumTextureFilter.Nearest, MagFilter = EnumTextureFilter.Nearest };
            capi.Render.GenTexture(tileTexture);
            using LoadedTexture tileCopy = new(capi, tileTexture.TextureId, tileWidth, tileHeight);

            for (int i = 0; i < tiles.Length; i++)
            {
                TextureAtlasPosition sourcePos = sourcePositions[i];

                capi.Render.RenderTextureIntoTexture(
                    atlas.AtlasTextures[sourcePos.atlasNumber],
                    MathF.Round(sourcePos.x1 * atlasSize.Width),
                    MathF.Round(sourcePos.y1 * atlasSize.Height),
                    tileWidth,
                    tileHeight,
                    tileCopy,
                    0,
                    0,
                    -1
                );
                atlas.RenderTextureIntoAtlas(
                    palettePos.atlasTextureId,
                    tileCopy,
                    0,
                    0,
                    tileWidth,
                    tileHeight,
                    palettePos.x1 * atlasSize.Width + i % columns * tileWidth,
                    palettePos.y1 * atlasSize.Height + i / columns * tileHeight,
                    -1
                );
            }

            atlas.RegenMipMaps(palettePos.atlasNumber);
        }

        static int getTileSize(ITextureAtlasAPI atlas, TextureAtlasPosition pos, out int height)
        {
            // Sources may have raw pixel bounds or subpixel-inset UVs.
            height = (int)MathF.Round(pos.y2 * atlas.Size.Height) - (int)MathF.Round(pos.y1 * atlas.Size.Height);
            return (int)MathF.Round(pos.x2 * atlas.Size.Width) - (int)MathF.Round(pos.x1 * atlas.Size.Width);
        }

        // Allocated space comes back without the padding that baked atlas positions already include.
        TextureAtlasPosition getTilePosition(ITextureAtlasAPI atlas, int tile, TextureAtlasPosition sourcePos)
        {
            float atlasWidth = atlas.Size.Width;
            float atlasHeight = atlas.Size.Height;
            int column = tile % columns;
            int row = tile / columns;
            return new TextureAtlasPosition
            {
                atlasTextureId = palettePos.atlasTextureId,
                atlasNumber = palettePos.atlasNumber,
                reloadIteration = palettePos.reloadIteration,
                AvgColor = sourcePos.AvgColor,
                RndColors = sourcePos.RndColors,
                x1 = palettePos.x1 + column * tileWidth / atlasWidth + atlas.SubPixelPaddingX,
                y1 = palettePos.y1 + row * tileHeight / atlasHeight + atlas.SubPixelPaddingY,
                x2 = palettePos.x1 + (column + 1) * tileWidth / atlasWidth - atlas.SubPixelPaddingX,
                y2 = palettePos.y1 + (row + 1) * tileHeight / atlasHeight - atlas.SubPixelPaddingY
            };
        }
    }

    public class MechanicalMaterialTextureSource(ITexPositionSource baseSource, string materialTextureCode, TextureAtlasPosition referencePosition) : ITexPositionSource
    {
        public TextureAtlasPosition this[string textureCode] => textureCode == materialTextureCode ? referencePosition : baseSource[textureCode];

        public Size2i AtlasSize => baseSource.AtlasSize;
    }
}
