using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Vintagestory.API;
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
    public enum EnumSkinnableType
    {
        Shape, 
        Texture, 
        Voice
    }

    public class SkinnablePart
    {
        public bool Colbreak;
        public bool UseDropDown;
        public string Code;
        public EnumSkinnableType Type;
        public string[] DisableElements;
        public CompositeShape ShapeTemplate;

        public SkinnablePartVariant[] Variants;
        public Vec2i TextureRenderTo = new Vec2i();
        public string TextureTarget;
        public AssetLocation TextureTemplate;

        public Dictionary<string, SkinnablePartVariant> VariantsByCode;
    }

    public class SkinnablePartVariant
    {
        public string Code;
        public CompositeShape Shape;
        public AssetLocation Texture;
        public AssetLocation Sound;
        public int Color;

        public AppliedSkinnablePartVariant AppliedCopy(string partCode)
        {
            return new AppliedSkinnablePartVariant()
            {
                Code = Code,
                Shape = Shape,
                Texture = Texture,
                Color = Color,
                PartCode = partCode
            };
        }
    }

    public class AppliedSkinnablePartVariant : SkinnablePartVariant
    {
        public string PartCode;
    }

    public class EntityBehaviorExtraSkinnable : EntityBehavior
    {
        public Dictionary<string, SkinnablePart> AvailableSkinPartsByCode = new Dictionary<string, SkinnablePart>();
        public SkinnablePart[] AvailableSkinParts;
        public string VoiceType = "altoflute";
        public string VoicePitch = "medium";


        public List<AppliedSkinnablePartVariant> appliedTemp = new List<AppliedSkinnablePartVariant>();

        ITreeAttribute skintree;
        public IReadOnlyList<AppliedSkinnablePartVariant> AppliedSkinParts
        {
            get
            {
                appliedTemp.Clear();

                ITreeAttribute appliedTree = skintree.GetTreeAttribute("appliedParts");
                if (appliedTree == null) return appliedTemp;

                foreach (var val in appliedTree)
                {
                    string partCode = val.Key;
                    if (!AvailableSkinPartsByCode.ContainsKey(partCode)) continue;

                    SkinnablePart part = AvailableSkinPartsByCode[partCode];
                    SkinnablePartVariant variant = null;

                    string code = (val.Value as StringAttribute).value;
                    if (part.VariantsByCode.TryGetValue(code, out variant))
                    {
                        appliedTemp.Add(variant.AppliedCopy(partCode));
                    }
                }

                return appliedTemp;
            }
        }


        public EntityBehaviorExtraSkinnable(Entity entity) : base(entity)
        {

        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            skintree = entity.WatchedAttributes.GetTreeAttribute("skinConfig");
            if (skintree == null)
            {
                entity.WatchedAttributes["skinConfig"] = skintree = new TreeAttribute();
            }

            entity.WatchedAttributes.RegisterModifiedListener("skinConfig", onSkinConfigChanged);
            entity.WatchedAttributes.RegisterModifiedListener("voicetype", onVoiceConfigChanged);
            entity.WatchedAttributes.RegisterModifiedListener("voicepitch", onVoiceConfigChanged);

            AvailableSkinParts = properties.Attributes["skinnableParts"].AsObject<SkinnablePart[]>();
            foreach (var val in AvailableSkinParts)
            {
                string partCode = val.Code;
                val.VariantsByCode = new Dictionary<string, SkinnablePartVariant>();

                AvailableSkinPartsByCode[val.Code] = val;

                if (val.Type == EnumSkinnableType.Texture && entity.Api.Side == EnumAppSide.Client)
                {
                    ICoreClientAPI capi = entity.Api as ICoreClientAPI;

                    LoadedTexture texture = new LoadedTexture(capi);
                    foreach (var variant in val.Variants)
                    {
                        AssetLocation textureLoc;

                        if (val.TextureTemplate != null)
                        {
                            textureLoc = val.TextureTemplate.Clone();
                            textureLoc.Path = textureLoc.Path.Replace("{code}", variant.Code);
                        }
                        else
                        {
                            textureLoc = variant.Texture;
                        }

                        IAsset asset = capi.Assets.TryGet(textureLoc.Clone().WithPathAppendixOnce(".png").WithPathPrefixOnce("textures/"), true);

                        int r = 0, g = 0, b = 0;
                        float c = 0;

                        BitmapRef bmp = asset.ToBitmap(capi);
                        for (int i = 0; i < 8; i++)
                        {
                            Vec2d vec = GameMath.R2Sequence2D(i);
                            Color col2 = bmp.GetPixelRel((float)vec.X, (float)vec.Y);
                            if (col2.A > 0.5)
                            {
                                r += col2.R;
                                g += col2.G;
                                b += col2.B;
                                c++;
                            }
                        }

                        bmp.Dispose();

                        c = Math.Max(1, c);
                        variant.Color = ColorUtil.ColorFromRgba((int)(b/c), (int)(g/c), (int)(r/c), 255);
                        val.VariantsByCode[variant.Code] = variant;
                    }
                } else
                {
                    foreach (var variant in val.Variants)
                    {
                        val.VariantsByCode[variant.Code] = variant;
                    }
                }
            }

            if (entity.Api.Side == EnumAppSide.Server && AppliedSkinParts.Count == 0)
            {
                foreach (var val in AvailableSkinParts)
                {
                    string partCode = val.Code;
                    string variantCode = val.Variants[entity.World.Rand.Next(val.Variants.Length)].Code;
                    selectSkinPart(partCode, variantCode, false, false);
                }
            }

            onVoiceConfigChanged();
        }

        private void onSkinConfigChanged()
        {
            skintree = entity.WatchedAttributes["skinConfig"] as ITreeAttribute;
           
            if (entity.World.Side == EnumAppSide.Client)
            {
                var essr = entity.Properties.Client.Renderer as EntitySkinnableShapeRenderer;
                essr.MarkShapeModified();
            }
        }


        private void onVoiceConfigChanged()
        {
            VoiceType = entity.WatchedAttributes.GetString("voicetype");
            VoicePitch = entity.WatchedAttributes.GetString("voicepitch");

            ApplyVoice(VoiceType, VoicePitch, false);
        }


        public override void OnEntityLoaded()
        {
            base.OnEntityLoaded();
            init();
        }

        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();
            init();
        }

        public override void OnEntityDespawn(EntityDespawnReason despawn)
        {
            base.OnEntityDespawn(despawn);

            var essr = entity.Properties.Client.Renderer as EntitySkinnableShapeRenderer;
            if (essr != null)
            {
                essr.OnReloadSkin -= Essr_OnReloadSkin;
                essr.OnTesselation -= Essr_OnTesselation;
            }
        }

        bool didInit = false;
        void init()
        {
            if (entity.World.Side != EnumAppSide.Client) return;

            if (!didInit)
            {
                var essr = entity.Properties.Client.Renderer as EntitySkinnableShapeRenderer;
                if (essr == null) throw new InvalidOperationException("The extra skinnable requires the entity to use the SkinnableShape renderer.");

                essr.OnReloadSkin += Essr_OnReloadSkin;
                essr.OnTesselation += Essr_OnTesselation;
                didInit = true;
            }
        }



        private void Essr_OnTesselation(ref Shape entityShape, string shapePathForLogging)
        {
            // Make a copy so we don't mess up the original
            Shape newShape = entityShape.Clone();
            newShape.ResolveAndLoadJoints("head");
            entityShape = newShape;

            foreach (var val in AppliedSkinParts)
            {
                SkinnablePart part;
                AvailableSkinPartsByCode.TryGetValue(val.PartCode, out part);
                
                if (part?.Type == EnumSkinnableType.Shape)
                {
                    entityShape = addSkinPart(val, entityShape, part.DisableElements, shapePathForLogging);
                }
            }

            foreach (var val in AppliedSkinParts)
            {
                SkinnablePart part;
                AvailableSkinPartsByCode.TryGetValue(val.PartCode, out part);

                if (part != null && part.Type == EnumSkinnableType.Texture && part.TextureTarget != null && part.TextureTarget != "seraph")
                {
                    AssetLocation textureLoc;
                    if (part.TextureTemplate != null)
                    {
                        textureLoc = part.TextureTemplate.Clone();
                        textureLoc.Path = textureLoc.Path.Replace("{code}", val.Code);
                    }
                    else
                    {
                        textureLoc = val.Texture;
                    }

                    string code = "skinpart-" + part.TextureTarget;
                    entityShape.TextureSizes.TryGetValue(code, out int[] sizes);
                    if (sizes != null)
                    {
                        loadTexture(entityShape, code, textureLoc, sizes[0], sizes[1], shapePathForLogging);
                    }
                    else
                    {
                        entity.Api.Logger.Error("Skinpart has no textureSize: " + code + "  in: " + shapePathForLogging);
                    }
                }
            }

            var inv = (entity as EntityAgent).GearInventory;
            if (inv != null)
            {
                foreach (var slot in inv)
                {
                    if (slot.Empty) continue;

                    if ((entity as EntityAgent).hideClothing)
                    {
                        continue;
                    }

                    ItemStack stack = slot.Itemstack;
                    JsonObject attrObj = stack.Collectible.Attributes;

                    string[] disableElements = attrObj?["disableElements"]?.AsArray<string>(null);
                    if (disableElements != null)
                    {
                        foreach (var val in disableElements)
                        {
                            entityShape.RemoveElementByName(val);
                        }
                    }
                }
            }
        }


        private void Essr_OnReloadSkin(LoadedTexture atlas, TextureAtlasPosition skinTexPos)
        {
            ICoreClientAPI capi = entity.World.Api as ICoreClientAPI;

            foreach (var val in AppliedSkinParts)
            {
                SkinnablePart part = AvailableSkinPartsByCode[val.PartCode];

                if (part.Type != EnumSkinnableType.Texture) continue;
                if (part.TextureTarget != null && part.TextureTarget != "seraph") continue;

                LoadedTexture texture = new LoadedTexture(capi);

                capi.Render.GetOrLoadTexture(val.Texture.Clone().WithPathAppendixOnce(".png"), ref texture);


                int posx = part.TextureRenderTo.X;
                int posy = part.TextureRenderTo.Y;

                capi.EntityTextureAtlas.RenderTextureIntoAtlas(
                    texture,
                    0,
                    0,
                    texture.Width,
                    texture.Height,
                    skinTexPos.x1 * capi.EntityTextureAtlas.Size.Width + posx,
                    skinTexPos.y1 * capi.EntityTextureAtlas.Size.Height + posy
                );
            }
        }


        public void selectSkinPart(string partCode, string variantCode, bool retesselateShape = true, bool playVoice = true)
        {
            AvailableSkinPartsByCode.TryGetValue(partCode, out var part);

            var essr = entity.Properties.Client.Renderer as EntitySkinnableShapeRenderer;
            ITreeAttribute appliedTree = skintree.GetTreeAttribute("appliedParts");
            if (appliedTree == null) skintree["appliedParts"] = appliedTree = new TreeAttribute();
            appliedTree[partCode] = new StringAttribute(variantCode);


            if (part?.Type == EnumSkinnableType.Voice)
            {
                entity.WatchedAttributes.SetString(partCode, variantCode);

                if (partCode == "voicetype")
                {
                    VoiceType = variantCode;
                }
                if (partCode == "voicepitch")
                {
                    VoicePitch = variantCode;
                }

                ApplyVoice(VoiceType, VoicePitch, playVoice);
                return;
            }

            if (retesselateShape) essr?.TesselateShape();
            return;
        }


        public void ApplyVoice(string voiceType, string voicePitch, bool testTalk)
        {
            if (!AvailableSkinPartsByCode.TryGetValue("voicetype", out var availVoices) || !AvailableSkinPartsByCode.TryGetValue("voicepitch", out var availPitches))
            {
                return;
            }

            VoiceType = voiceType;
            VoicePitch = voicePitch;

            if (entity is EntityPlayer plr && plr.talkUtil != null && voiceType != null) {

                if (!availVoices.VariantsByCode.ContainsKey(voiceType))
                {
                    voiceType = availVoices.Variants[0].Code;
                }

                plr.talkUtil.soundName = availVoices.VariantsByCode[voiceType].Sound;
                
                float pitchMod = 1;
                switch (VoicePitch)
                {
                    case "verylow": pitchMod = 0.6f; break;
                    case "low": pitchMod = 0.8f; break;
                    case "medium": pitchMod = 1f; break;
                    case "high": pitchMod = 1.2f; break;
                    case "veryhigh": pitchMod = 1.4f; break;
                }

                plr.talkUtil.pitchModifier = pitchMod;
                plr.talkUtil.chordDelayMul = 1.1f;

                if (testTalk)
                {
                    plr.talkUtil.Talk(EnumTalkType.Idle);
                }
            }
        }

        protected Shape addSkinPart(AppliedSkinnablePartVariant part, Shape entityShape, string[] disableElements, string shapePathForLogging)
        {
            if (AvailableSkinPartsByCode[part.PartCode].Type == EnumSkinnableType.Voice)
            {
                entity.WatchedAttributes.SetString("voicetype", part.Code);
                return entityShape;
            }

            if (disableElements != null)
            {
                foreach (var val in disableElements)
                {
                    entityShape.RemoveElementByName(val);
                }
            }

            ICoreClientAPI api = entity.World.Api as ICoreClientAPI;
            AssetLocation shapePath;
            CompositeShape tmpl = AvailableSkinPartsByCode[part.PartCode].ShapeTemplate;

            if (part.Shape == null && tmpl != null)
            {
                shapePath = tmpl.Base.CopyWithPath("shapes/" + tmpl.Base.Path + ".json");
                shapePath.Path = shapePath.Path.Replace("{code}", part.Code);
            }
            else
            {
                shapePath = part.Shape.Base.CopyWithPath("shapes/" + part.Shape.Base.Path + ".json");
            }


            Shape partShape = Shape.TryGet(api, shapePath);
            if (partShape == null)
            {
                api.World.Logger.Warning("Entity skin shape {0} defined in entity config {1} not found or errored, was supposed to be at {2}. Skin part will be invisible.", shapePath, entity.Properties.Code, shapePath);
                return null;
            }


            bool added = false;
            foreach (var val in partShape.Elements)
            {
                ShapeElement elem;

                if (val.StepParentName != null)
                {
                    elem = entityShape.GetElementByName(val.StepParentName, StringComparison.InvariantCultureIgnoreCase);
                    if (elem == null)
                    {
                        api.World.Logger.Warning("Skin part shape {0} defined in entity config {1} requires step parent element with name {2}, but no such element was found in shape {3}. Will not be visible.", shapePath, entity.Properties.Code, val.StepParentName, shapePathForLogging);
                        continue;
                    }
                }
                else
                {
                    api.World.Logger.Warning("Skin part shape element {0} in shape {1} defined in entity config {2} did not define a step parent element. Will not be visible.", val.Name, shapePath, entity.Properties.Code);
                    continue;
                }

                if (elem.Children == null)
                {
                    elem.Children = new ShapeElement[] { val };
                }
                else
                {
                    elem.Children = elem.Children.Append(val);
                }

                val.SetJointIdRecursive(elem.JointId);
                val.WalkRecursive((el) =>
                {
                    foreach (var face in el.FacesResolved)
                    {
                        if (face != null) face.Texture = "skinpart-" + face.Texture;
                    }
                });

                added = true;
            }

            if (added && partShape.Textures != null)
            {
                Dictionary<string, AssetLocation> newdict = new Dictionary<string, AssetLocation>();
                foreach (var val in partShape.Textures)
                {
                    if (val.Key == "seraph") continue;

                    newdict["skinpart-" + val.Key] = val.Value;
                }

                partShape.Textures = newdict;

                foreach (var val in partShape.Textures)
                {
                    if (val.Key == "seraph") continue;

                    loadTexture(entityShape, val.Key, val.Value, partShape.TextureWidth, partShape.TextureHeight, shapePathForLogging);
                }

                foreach (var val in partShape.TextureSizes)
                {
                    entityShape.TextureSizes[val.Key] = val.Value;
                }
            }

            return entityShape;
        }

        private void loadTexture(Shape entityShape, string code, AssetLocation location, int textureWidth, int textureHeight, string shapePathForLogging)
        {
            ICoreClientAPI api = entity.World.Api as ICoreClientAPI;

            CompositeTexture ctex = new CompositeTexture() { Base = location };

            entityShape.TextureSizes[code] = new int[] { textureWidth, textureHeight };

            AssetLocation shapeTexloc = location;

            // Weird backreference to the shaperenderer. Should be refactored.
            var texturesByLoc = (entity as EntityAgent).extraTextureByLocation;
            var texturesByName = (entity as EntityAgent).extraTexturesByTextureName;

            BakedCompositeTexture bakedCtex;


            if (!texturesByLoc.TryGetValue(shapeTexloc, out bakedCtex))
            {
                int textureSubId = 0;
                TextureAtlasPosition texpos;

                IAsset texAsset = api.Assets.TryGet(location.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"));
                if (texAsset != null)
                {
                    BitmapRef bmp = texAsset.ToBitmap(api);
                    api.EntityTextureAtlas.InsertTextureCached(location, bmp, out textureSubId, out texpos, -1);
                }
                else
                {
                    api.World.Logger.Warning("Skin part shape {0} defined texture {1}, no such texture found.", shapePathForLogging, location);
                }

                ctex.Baked = new BakedCompositeTexture() { BakedName = location, TextureSubId = textureSubId };

                texturesByName[code] = ctex;
                texturesByLoc[shapeTexloc] = ctex.Baked;
            }
            else
            {
                ctex.Baked = bakedCtex;
                texturesByName[code] = ctex;
            }
        }

        public override string PropertyName()
        {
            return "skinnableplayer";
        }


        
    }
}
