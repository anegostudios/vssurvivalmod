using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class MessageTexture
    {
        public LoadedTexture tex;
        public string message;
        public long receivedTime;
    }

    public class EntityShapeRenderer : EntityRenderer
    {
        static Dictionary<string, Animation[]> AnimationsByShape;
        static bool handlerAssigned;
        static void OnRetesselatedStatic(ICoreClientAPI capi)
        {
            AnimationsByShape.Clear();

            foreach (EntityType type in capi.World.EntityTypes)
            {
                if (type.Client?.Renderer == "Shape" || type.Client?.Renderer == "SkinnableShape")
                {
                    type.Client.LoadShape(type, capi);
                }
            }
        }
        
        LoadedTexture nameTagTexture = null;
        LoadedTexture debugTagTexture = null;

        MeshRef meshRefOpaque;
        MeshRef meshRefOit;

        Vec4f color = new Vec4f(1, 1, 1, 1);
        long lastDebugInfoChangeMs = 0;
        float headYaw;
        float bodyYaw;
        bool opposite;
        bool isSpectator;
        IPlayer player;

        public Vec3f OriginPos = new Vec3f();
        public float[] ModelMat = Mat4f.Create();
        float[] tmpMvMat = Mat4f.Create();

        public bool rotateTpYawNow;
        public BlendEntityAnimator curAnimator;


        public bool HeadControl;
        public bool DoRenderHeldItem;
        public bool DisplayChatMessages;

        List<MessageTexture> messageTextures = null;

        static void OnGameExit()
        {
            AnimationsByShape = null;
        }

        public EntityShapeRenderer(Entity entity, ICoreClientAPI api) : base(entity, api)
        {
            HeadControl = entity is EntityPlayer;
            DoRenderHeldItem = entity is EntityPlayer;
            DisplayChatMessages = entity is EntityPlayer;

            if (AnimationsByShape == null)
            {
                AnimationsByShape = new Dictionary<string, Animation[]>();
                api.Event.RegisterOnLeaveWorld(OnGameExit);
            }

            TesselateShape();

            entity.WatchedAttributes.OnModified.Add(new TreeModifiedListener() { path = "nametag", listener = OnNameChanged });
            OnNameChanged();
            api.Event.RegisterGameTickListener(UpdateDebugInfo, 250);
            OnDebugInfoChanged();

            if (DisplayChatMessages)
            {
                messageTextures = new List<MessageTexture>();
                api.Event.OnChatMessage += OnChatMessage;
            }
            
            if (!handlerAssigned)
            {
                handlerAssigned = true;
                api.Event.OnReloadShapes += () => OnRetesselatedStatic(api);
            }

            api.Event.OnReloadShapes += TesselateShape;
        }

        private void OnChatMessage(int groupId, string message, EnumChatType chattype, string data)
        {
            if (data != null && data.StartsWith("From:") && entity.Pos.SquareDistanceTo(capi.World.Player.Entity.Pos.XYZ) < 20*20 && message.Length > 0)
            {
                int entityid = 0;
                int.TryParse(data.Split(new string[] { ":" }, StringSplitOptions.None)[1], out entityid);
                if (entity.Entityid == entityid)
                {
                    string name = GetNameTagName();
                    if (name != null && message.StartsWith(name + ": "))
                    {
                        message = message.Substring((name + ": ").Length);
                    }

                    LoadedTexture tex = capi.Gui.Text.GenTextTexture(
                        message,
                        capi.Render.GetFont(30, capi.Render.StandardFontName, ColorUtil.WhiteArgbDouble),
                        350,
                        new TextBackground() { color = ElementGeometrics.DialogLightBgColor, padding = 3, radius = ElementGeometrics.ElementBGRadius },
                        EnumTextOrientation.Center
                    );

                    messageTextures.Insert(0, new MessageTexture()
                    {
                        tex = tex,
                        message = message,
                        receivedTime = capi.World.ElapsedMilliseconds
                    });

                }
            }
        }



        public void TesselateShape()
        {
            CompositeShape compositeShape = entity.Type?.Client?.Shape;
            Shape entityShape = entity.Type?.Client?.LoadedShape;
            if (entityShape == null)
            {
                return;
            }

            ShapeElement headElement = null;
            // Only for player entity for now
            if (entityShape.Elements != null && HeadControl)
            {
                headElement = FindHead(entityShape.Elements);
            }
            
            entityShape.ResolveAndLoadJoints(headElement);

            
            ITexPositionSource texSource = GetTextureSource();
            MeshData meshdata;

            if (entity.Type.Client != null && entity.Type.Client.Shape.VoxelizeTexture)
            {
                int altTexNumber = entity.WatchedAttributes.GetInt("textureIndex", 0);

                TextureAtlasPosition pos = texSource["all"];
                CompositeTexture tex = altTexNumber == 0 ? entity.Type.Client.FirstTexture : entity.Type.Client.FirstTexture.Alternates[altTexNumber - 1];
                meshdata = capi.Tesselator.VoxelizeTexture(tex, capi.EntityTextureAtlas.Size, pos);
                for (int i = 0; i < meshdata.xyz.Length; i+=3)
                {
                    meshdata.xyz[i] -= 0.125f;
                    meshdata.xyz[i + 1] -= 0.5f;
                    meshdata.xyz[i + 2] += 0.125f / 2;
                }

                curAnimator = new BlendEntityAnimator(entity, new Animation[0], new ShapeElement[0], new Dictionary<int, AnimationJoint>());
            } else
            {
                string animDictkey = (entity.Type.Code + entity.Type.Client.Shape.Base.ToString());

                try
                {
                    capi.Tesselator.TesselateShapeWithJointIds("entity", entityShape, out meshdata, texSource, new Vec3f(), compositeShape.QuantityElements, compositeShape.SelectiveElements);
                } catch (Exception e)
                {
                    capi.World.Logger.Fatal("Failed tesselating entity {0} with id {1}. Entity will probably be invisible!. The teselator threw {2}", entity.Type.Code, entity.EntityId, e);
                    curAnimator = new BlendEntityAnimator(entity, entityShape.Animations, entityShape.Elements, entityShape.JointsById, headElement);
                    return;
                }
                

                // We cache animations because they are cpu intensive to calculate
                if (AnimationsByShape.ContainsKey(animDictkey))
                {
                    entityShape.Animations = AnimationsByShape[animDictkey];
                } else
                {
                    for (int i = 0; entityShape.Animations != null && i < entityShape.Animations.Length; i++)
                    {
                        entityShape.Animations[i].GenerateAllFrames(entityShape.Elements, entityShape.JointsById);
                    }

                    AnimationsByShape[animDictkey] = entityShape.Animations;
                }

                curAnimator = new BlendEntityAnimator(entity, entityShape.Animations, entityShape.Elements, entityShape.JointsById, headElement);
            }

            meshdata.Rgba2 = null;

            if (meshRefOpaque != null)
            {
                capi.Render.DeleteMesh(meshRefOpaque);
                meshRefOpaque = null;
            }
            if (meshRefOit != null)
            {
                capi.Render.DeleteMesh(meshRefOit);
                meshRefOit = null;
            }

            MeshData opaqueMesh = meshdata.Clone().Clear();
            MeshData oitMesh = meshdata.Clone().Clear();

            opaqueMesh.AddMeshData(meshdata, EnumChunkRenderPass.Opaque);
            oitMesh.AddMeshData(meshdata, EnumChunkRenderPass.Transparent);

            if (opaqueMesh.VerticesCount > 0)
            {
                meshRefOpaque = capi.Render.UploadMesh(opaqueMesh);
            }

            if (oitMesh.VerticesCount > 0)
            {
                meshRefOit = capi.Render.UploadMesh(oitMesh);
            }
            
        }


        protected virtual ITexPositionSource GetTextureSource()
        {
            int altTexNumber = entity.WatchedAttributes.GetInt("textureIndex", 0);
            return capi.Tesselator.GetTextureSource(entity.Type, altTexNumber);
        }
        
        



        ShapeElement FindHead(ShapeElement[] elements)
        {
            foreach (ShapeElement elem in elements)
            {
                if (elem.Name.ToLowerInvariant() == "head") return elem;
                if (elem.Children != null)
                {
                    ShapeElement foundElem = FindHead(elem.Children);
                    if (foundElem != null) return foundElem;
                }

            }

            return null;
        }


        private void UpdateDebugInfo(float dt)
        {
            OnDebugInfoChanged();

            entity.DebugAttributes.MarkClean();
        }



        private void OnDebugInfoChanged()
        {
            bool showDebuginfo = capi.Settings.Bool["showEntityDebugInfo"];

            if (showDebuginfo && !entity.DebugAttributes.AllDirty && !entity.DebugAttributes.PartialDirty && debugTagTexture != null) return;

            if (debugTagTexture != null)
            {
                debugTagTexture.Dispose();
                debugTagTexture = null;
            }

            if (!showDebuginfo) return;


            StringBuilder text = new StringBuilder();
            foreach (KeyValuePair<string, IAttribute> val in entity.DebugAttributes)
            {
                text.AppendLine(val.Key +": " + val.Value.ToString());
            }

            debugTagTexture = capi.Gui.Text.GenUnscaledTextTexture(
                text.ToString(), 
                capi.Render.GetFont(20, capi.Render.StandardFontName, ColorUtil.WhiteArgbDouble), 
                new TextBackground() { color = ElementGeometrics.DialogDefaultBgColor, padding = 3, radius = ElementGeometrics.ElementBGRadius }
            );

            lastDebugInfoChangeMs = entity.World.ElapsedMilliseconds;
        }

        private void OnNameChanged()
        {
            if (nameTagTexture != null)
            {
                nameTagTexture.Dispose();
                nameTagTexture = null;
            }

            string name = GetNameTagName();
            if (name != null)
            {
                nameTagTexture = capi.Gui.Text.GenUnscaledTextTexture(
                    name,
                    capi.Render.GetFont(30, capi.Render.StandardFontName, ColorUtil.WhiteArgbDouble),
                    new TextBackground() { color = ElementGeometrics.DialogLightBgColor, padding = 3, radius = ElementGeometrics.ElementBGRadius }
                );
            }
        }

        public string GetNameTagName()
        {
            EntityBehaviorNameTag behavior = entity.GetBehavior<EntityBehaviorNameTag>();
            return behavior?.DisplayName;
        }

        public override void BeforeRender(float dt)
        {
            if (meshRefOpaque == null && meshRefOit == null) return;
            if (capi.IsGamePaused) return;

            if (HeadControl && player == null && entity is EntityPlayer)
            {
                player = capi.World.PlayerByUid((entity as EntityPlayer).PlayerUID);
            }

            isSpectator = player != null && player.WorldData.CurrentGameMode == EnumGameMode.Spectator;
            if (isSpectator) return;


            curAnimator.FastMode = !DoRenderHeldItem && !capi.Settings.Bool["highQualityAnimations"];

            if (DisplayChatMessages && messageTextures.Count > 0)
            {
                MessageTexture tex = messageTextures.Last();
                if (capi.World.ElapsedMilliseconds > tex.receivedTime + 3500 + 100 * (tex.message.Length - 10))
                {
                    messageTextures.RemoveAt(messageTextures.Count - 1);
                    tex.tex.Dispose();

                    if (messageTextures.Count > 0)
                    {
                        tex = messageTextures[messageTextures.Count - 1];
                        long msvisible = tex.receivedTime + 3500 + 100 * (tex.message.Length - 10) - capi.World.ElapsedMilliseconds;
                        tex.receivedTime += Math.Max(0, 1000 - msvisible);
                    }
                }
            }




            if (HeadControl)
            {
                /*if (player == api.World.Player && api.Render.CameraType == EnumCameraMode.FirstPerson)
                {
                    AttachmentPointAndPose apap = null;
                    curAnimator.AttachmentPointByCode.TryGetValue("Eyes", out apap);
                    float[] tmpMat = Mat4f.Create();

                    for (int i = 0; i < 16; i++) tmpMat[i] = ModelMat[i];
                    AttachmentPoint ap = apap.AttachPoint;

                    float[] mat = apap.Pose.AnimModelMatrix;
                    Mat4f.Mul(tmpMat, tmpMat, mat);

                    Mat4f.Translate(tmpMat, tmpMat, (float)ap.PosX / 16f, (float)ap.PosY / 16f, (float)ap.PosZ / 16f);
                    Mat4f.RotateX(tmpMat, tmpMat, (float)(ap.RotationX) * GameMath.DEG2RAD);
                    Mat4f.RotateY(tmpMat, tmpMat, (float)(ap.RotationY) * GameMath.DEG2RAD);
                    Mat4f.RotateZ(tmpMat, tmpMat, (float)(ap.RotationZ) * GameMath.DEG2RAD);
                    float[] vec = new float[] { 0,0,0, 0 };
                    float[] outvec = Mat4f.MulWithVec4(tmpMat, vec);

                    api.Render.CameraOffset.Translation.Set(outvec[0], outvec[1] + 1, outvec[2]);
                }*/


                float diff = GameMath.AngleRadDistance(bodyYaw, entity.Pos.Yaw);

                if (Math.Abs(diff) > GameMath.PIHALF * 1.2f) opposite = true;
                if (opposite)
                {
                    if (Math.Abs(diff) < GameMath.PIHALF * 0.9f) opposite = false;
                    else diff = 0;
                }

                headYaw += (diff - headYaw) * dt * 6;
                headYaw = GameMath.Clamp(headYaw, -0.75f, 0.75f);

                curAnimator.HeadYaw = headYaw;
                curAnimator.HeadPitch = GameMath.Clamp((entity.Pos.Pitch - GameMath.PI) * 0.75f, -1.2f, 1.2f);


                if (capi.World.Player.CameraMode == EnumCameraMode.Overhead || capi.World.Player.Entity.MountedOn != null)
                {
                    bodyYaw = entity.Pos.Yaw;
                } else
                {
                    float yawDist = GameMath.AngleRadDistance(bodyYaw, entity.Pos.Yaw);
                    if (Math.Abs(yawDist) > 1f - (capi.World.Player.Entity.Controls.TriesToMove ? 0.99f : 0) || rotateTpYawNow)
                    {
                        bodyYaw += GameMath.Clamp(yawDist, -dt * 3, dt * 3);
                        rotateTpYawNow = Math.Abs(yawDist) > 0.01f;
                    }
                }
                

            }
            else
            {
                curAnimator.HeadYaw = entity.Pos.Yaw;
                curAnimator.HeadPitch = entity.Pos.Pitch;
            }

            curAnimator.OnFrame(dt);
        }


        

        public override void DoRender3DOpaque(float dt, bool isShadowPass)
        {
            if (isSpectator) return;

            if (HeadControl)
            {
                bool isSelf = capi.World.Player.Entity.EntityId == entity.EntityId;
                loadModelMatrixForPlayer(entity, isSelf);
                if (isSelf) OriginPos.Set(0, 0, 0);
            }
            else
            {
                loadModelMatrix(entity, isShadowPass);
                Vec3d camPos = capi.World.Player.Entity.CameraPos;
                OriginPos.Set((float)(entity.Pos.X - camPos.X), (float)(entity.Pos.Y - camPos.Y), (float)(entity.Pos.Z - camPos.Z));
            }

            if (DoRenderHeldItem)
            {
                RenderHeldItem(isShadowPass);
            }
        }


        void RenderHeldItem(bool isShadowPass)
        {
            IRenderAPI rpi = capi.Render;
            ItemStack stack = (entity as IEntityAgent).RightHandItemSlot?.Itemstack;

            BlendEntityAnimator bea = curAnimator as BlendEntityAnimator;
            AttachmentPointAndPose apap = null;

            bea.AttachmentPointByCode.TryGetValue("RightHand", out apap);

            if (apap == null || stack == null) return;
            
            for (int i = 0; i < 16; i++) tmpMvMat[i] = ModelMat[i];

            AttachmentPoint ap = apap.AttachPoint;

            float[] mat = apap.Pose.AnimModelMatrix;
            float[] orig = new float[16];
            for (int i = 0; i < 16; i++) orig[i] = (float)capi.Render.CameraMatrixOrigin[i];

            if(!isShadowPass) Mat4f.Mul(tmpMvMat, orig, tmpMvMat);
            Mat4f.Mul(tmpMvMat, tmpMvMat, mat);

            ItemRenderInfo renderInfo = rpi.GetItemStackRenderInfo(stack, EnumItemRenderTarget.HandTp);
            IStandardShaderProgram prog = null;

            if (isShadowPass)
            {
                // Throws invalid op O_O
                //rpi.CurrentActiveShader.Uniform("tex2d", renderInfo.TextureId);
                rpi.CurrentActiveShader.BindTexture2D("tex2d", renderInfo.TextureId, 0);
            } else
            {
                //prog = rpi.PreparedStandardShader((int)entity.Pos.X, (int)entity.Pos.Y, (int)entity.Pos.Z);
                prog = rpi.StandardShader;
                prog.Use();
                prog.RgbaAmbientIn = rpi.AmbientColor;
                prog.RgbaBlockIn = ColorUtil.WhiteArgbVec;
                prog.RgbaFogIn = rpi.FogColor;
                prog.FogMinIn = rpi.FogMin;
                prog.FogDensityIn = rpi.FogDensity;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
                prog.ExtraGlow = 0;
                prog.RgbaTint = ColorUtil.WhiteArgbVec;

                prog.Tex2D = renderInfo.TextureId;
                prog.AlphaTest = stack.Collectible.RenderAlphaTest;
                
                Vec4f lightrgbs = capi.World.BlockAccessor.GetLightRGBs(
                    (int)entity.Pos.X,
                    (int)(entity.Pos.Y + entity.EyeHeight()),
                    (int)entity.Pos.Z
                );

                int temp = (int)stack.Collectible.GetTemperature(capi.World, stack);
                float[] glowColor = ColorUtil.getIncandescenceColorAsColor4f(temp);
                lightrgbs.R += 2 * glowColor[0];
                lightrgbs.G += 2 * glowColor[1];
                lightrgbs.B += 2 * glowColor[2];

                prog.RgbaLightIn = lightrgbs;
                prog.ExtraGlow = GameMath.Clamp((temp - 500) / 4, 0, 255);
            }


            Mat4f.Translate(tmpMvMat, tmpMvMat, renderInfo.Transform.Origin.X, renderInfo.Transform.Origin.Y, renderInfo.Transform.Origin.Z);

            Mat4f.Scale(tmpMvMat, tmpMvMat, renderInfo.Transform.ScaleXYZ.X, renderInfo.Transform.ScaleXYZ.Y, renderInfo.Transform.ScaleXYZ.Z);

            Mat4f.Translate(tmpMvMat, tmpMvMat, (float)ap.PosX / 16f + renderInfo.Transform.Translation.X, (float)ap.PosY / 16f + renderInfo.Transform.Translation.Y, (float)ap.PosZ / 16f + renderInfo.Transform.Translation.Z);
            Mat4f.RotateX(tmpMvMat, tmpMvMat, (float)(ap.RotationX + renderInfo.Transform.Rotation.X) * GameMath.DEG2RAD);
            Mat4f.RotateY(tmpMvMat, tmpMvMat, (float)(ap.RotationY + renderInfo.Transform.Rotation.Y) * GameMath.DEG2RAD);
            Mat4f.RotateZ(tmpMvMat, tmpMvMat, (float)(ap.RotationZ + renderInfo.Transform.Rotation.Z) * GameMath.DEG2RAD);

            Mat4f.Translate(tmpMvMat, tmpMvMat, -(renderInfo.Transform.Origin.X), -(renderInfo.Transform.Origin.Y), -(renderInfo.Transform.Origin.Z));

            if (isShadowPass)
            {
                Mat4f.Mul(tmpMvMat, capi.Render.CurrentShadowProjectionMatrix, tmpMvMat);
                capi.Render.CurrentActiveShader.UniformMatrix("mvpMatrix", tmpMvMat);
                capi.Render.CurrentActiveShader.Uniform("origin", OriginPos);
            } else
            {
                prog.ModelViewMatrix = tmpMvMat;
            }

            capi.Render.RenderMesh(renderInfo.ModelRef);

            if (!isShadowPass) prog.Stop();
        }

        public override void PrepareForGuiRender(float dt, double posX, double posY, double posZ, float yawDelta, float size, out MeshRef meshRef, out float[] modelviewMatrix)
        {
            loadModelMatrixForGui(entity, posX, posY, posZ, yawDelta, size);
            modelviewMatrix = ModelMat;
            meshRef = this.meshRefOpaque;
        }




        public override void DoRender3DOpaqueBatched(float dt, bool isShadowPass)
        {
            if (isSpectator || meshRefOpaque == null) return;

            if (isShadowPass)
            {
                Mat4f.Mul(tmpMvMat, capi.Render.CurrentModelviewMatrix, ModelMat);
                capi.Render.CurrentActiveShader.UniformMatrix("modelViewMatrix", tmpMvMat);
            }
            else
            {
                Vec4f lightrgbs = capi.World.BlockAccessor.GetLightRGBs((int)entity.Pos.X, (int)entity.Pos.Y, (int)entity.Pos.Z);
                capi.Render.CurrentActiveShader.Uniform("rgbaLightIn", lightrgbs);
                capi.Render.CurrentActiveShader.Uniform("extraGlow", entity.Type.Client.GlowLevel);
                capi.Render.CurrentActiveShader.UniformMatrix("modelMatrix", ModelMat);
                capi.Render.CurrentActiveShader.UniformMatrix("viewMatrix", capi.Render.CurrentModelviewMatrix);

                color[0] = (entity.RenderColor >> 16 & 0xff) / 255f;
                color[1] = ((entity.RenderColor >> 8) & 0xff) / 255f;
                color[2] = ((entity.RenderColor >> 0) & 0xff) / 255f;
                color[3] = ((entity.RenderColor >> 24) & 0xff) / 255f;

                capi.Render.CurrentActiveShader.Uniform("renderColor", color);
            }

            capi.Render.CurrentActiveShader.UniformMatrices("elementTransforms", GlobalConstants.MaxAnimatedElements, curAnimator.Matrices);
            capi.Render.RenderMesh(meshRefOpaque);

            if (meshRefOit != null)
            {
                capi.Render.RenderMesh(meshRefOit);
            }
        }


        public override void DoRender3DOITBatched(float dt)
        {
            /*if (isSpectator || meshRefOit == null) return;

            Vec4f lightrgbs = api.Render.GetLightRGBs((int)entity.Pos.X, (int)entity.Pos.Y, (int)entity.Pos.Z);
            api.Render.CurrentActiveShader.Uniform("rgbaLightIn", lightrgbs);
            api.Render.CurrentActiveShader.UniformMatrix("modelMatrix", ModelMat);
            api.Render.CurrentActiveShader.UniformMatrix("viewMatrix", api.Render.CurrentModelviewMatrix);

            color[0] = (entity.RenderColor >> 16 & 0xff) / 255f;
            color[1] = ((entity.RenderColor >> 8) & 0xff) / 255f;
            color[2] = ((entity.RenderColor >> 0) & 0xff) / 255f;
            color[3] = ((entity.RenderColor >> 24) & 0xff) / 255f;

            api.Render.CurrentActiveShader.Uniform("renderColor", color);
            api.Render.CurrentActiveShader.UniformMatrices("elementTransforms", GlobalConstants.MaxAnimatedElements, curAnimator.Matrices);
            api.Render.RenderMesh(meshRefOit);*/
        }


        public override void DoRender2D(float dt)
        {
            if (isSpectator || (nameTagTexture == null && debugTagTexture == null)) return;

            IRenderAPI rapi = capi.Render;
            EntityPlayer entityPlayer = capi.World.Player.Entity;
            Vec3d aboveHeadPos;

            if (capi.World.Player.Entity.EntityId == entity.EntityId) {
                if (rapi.CameraType == EnumCameraMode.FirstPerson) return;
                aboveHeadPos = new Vec3d(entityPlayer.CameraPos.X, entityPlayer.CameraPos.Y + entity.CollisionBox.Y2 + 0.5, entityPlayer.CameraPos.Z);
            } else
            {
                aboveHeadPos = new Vec3d(entity.Pos.X, entity.Pos.Y + entity.CollisionBox.Y2 + 0.5, entity.Pos.Z);
            }
            

            
            Vec3d pos = MatrixToolsd.Project(aboveHeadPos, rapi.PerspectiveProjectionMat, rapi.PerspectiveViewMat, rapi.FrameWidth, rapi.FrameHeight);

            // Z negative seems to indicate that the name tag is behind us \o/
            if (pos.Z < 0) return;

            float scale = 4f / Math.Max(1, (float)pos.Z);

            float cappedScale = Math.Min(1f, scale);
            if (cappedScale > 0.75f) cappedScale = 0.75f + (cappedScale - 0.75f) / 2;

            float offY = 0;

            if (nameTagTexture != null)
            {
                float posx = (float)pos.X - cappedScale * nameTagTexture.Width / 2;
                float posy = rapi.FrameHeight - (float)pos.Y + (nameTagTexture.Height * Math.Max(0, scale - 1));

                rapi.Render2DTexture(
                    nameTagTexture.TextureId, posx, posy, cappedScale * nameTagTexture.Width, cappedScale * nameTagTexture.Height, 20
                );
            }

            if (debugTagTexture != null)
            {
                float posx = (float)pos.X - cappedScale * debugTagTexture.Width / 2;
                float posy = rapi.FrameHeight - (float)pos.Y - (debugTagTexture.Height * Math.Max(0, cappedScale));

                rapi.Render2DTexture(
                    debugTagTexture.TextureId, posx, posy - offY, cappedScale * debugTagTexture.Width, cappedScale * debugTagTexture.Height, 20
                );
            }

            if (messageTextures != null)
            {
                offY += 0;

                foreach (MessageTexture mt in messageTextures)
                {
                    offY += (mt.tex.Height * cappedScale) + 4;

                    float posx = (float)pos.X - cappedScale * mt.tex.Width / 2;
                    float posy = (float)pos.Y + offY;
                    

                    rapi.Render2DTexture(
                        mt.tex.TextureId, posx, rapi.FrameHeight - posy, cappedScale * mt.tex.Width, cappedScale * mt.tex.Height, 20
                    );
                    // - cappedScale * mt.tex.height

                    
                }
            }
        }

        public override void Dispose()
        {
            if (meshRefOpaque != null)
            {
                meshRefOpaque.Dispose();
                meshRefOpaque = null;
            }

            if (meshRefOit != null)
            {
                meshRefOit.Dispose();
                meshRefOit = null;
            }

            if (nameTagTexture != null)
            {
                nameTagTexture.Dispose();
                nameTagTexture = null;
            }

            if (debugTagTexture != null)
            {
                debugTagTexture.Dispose();
                debugTagTexture = null;
            }

            capi.Event.OnReloadShapes -= TesselateShape;
        }



        public void loadModelMatrix(Entity entity, bool isShadowPass)
        {
            IEntityPlayer entityPlayer = capi.World.Player.Entity;

            Mat4f.Identity(ModelMat);
            //if (!isShadowPass) - why was this here? it causes creature shadows to render on the player
            {
                Mat4f.Translate(ModelMat, ModelMat, (float)(entity.Pos.X - entityPlayer.CameraPos.X), (float)(entity.Pos.Y - entityPlayer.CameraPos.Y), (float)(entity.Pos.Z - entityPlayer.CameraPos.Z));
            }

            float rotX = entity.Type?.Client?.Shape != null ? entity.Type.Client.Shape.rotateX : 0;
            float rotY = entity.Type?.Client?.Shape != null ? entity.Type.Client.Shape.rotateY : 0;
            float rotZ = entity.Type?.Client?.Shape != null ? entity.Type.Client.Shape.rotateZ : 0;

            Mat4f.Translate(ModelMat, ModelMat, 0, entity.CollisionBox.Y2 / 2, 0);

            double[] quat = Quaternion.Create();
            Quaternion.RotateX(quat, quat, entity.Pos.Pitch + rotX * GameMath.DEG2RAD);
            Quaternion.RotateY(quat, quat, entity.Pos.Yaw + (rotY + 90) * GameMath.DEG2RAD);
            Quaternion.RotateZ(quat, quat, entity.Pos.Roll + rotZ * GameMath.DEG2RAD);
            
            float[] qf = new float[quat.Length];
            for (int i = 0; i < quat.Length; i++) qf[i] = (float)quat[i];
            Mat4f.Mul(ModelMat, ModelMat, Mat4f.FromQuat(Mat4f.Create(), qf));
            
            float scale = entity.Type?.Client == null ? 1f : entity.Type.Client.Size;
            Mat4f.Scale(ModelMat, ModelMat, new float[] { scale, scale, scale });
            Mat4f.Translate(ModelMat, ModelMat, -0.5f, -entity.CollisionBox.Y2 / 2, -0.5f);
        }


        public void loadModelMatrixForPlayer(Entity entity, bool isSelf)
        {
            EntityPlayer entityPlayer = capi.World.Player.Entity;

            Mat4f.Identity(ModelMat);

            if (!isSelf)
            {
                Mat4f.Translate(ModelMat, ModelMat, (float)(entity.Pos.X - entityPlayer.CameraPos.X), (float)(entity.Pos.Y - entityPlayer.CameraPos.Y), (float)(entity.Pos.Z - entityPlayer.CameraPos.Z));
            }
            float rotX = entity.Type?.Client?.Shape != null ? entity.Type.Client.Shape.rotateX : 0;
            float rotY = entity.Type?.Client?.Shape != null ? entity.Type.Client.Shape.rotateY : 0;
            float rotZ = entity.Type?.Client?.Shape != null ? entity.Type.Client.Shape.rotateZ : 0;

            Mat4f.RotateX(ModelMat, ModelMat, entity.Pos.Roll + rotX * GameMath.DEG2RAD);
            Mat4f.RotateY(ModelMat, ModelMat, bodyYaw + (180 + rotY) * GameMath.DEG2RAD);
            Mat4f.RotateZ(ModelMat, ModelMat, entityPlayer.WalkPitch + rotZ * GameMath.DEG2RAD);

            float scale = entity.Type?.Client == null ? 1f : entity.Type.Client.Size;
            Mat4f.Scale(ModelMat, ModelMat, new float[] { scale, scale, scale });
            Mat4f.Translate(ModelMat, ModelMat, -0.5f, 0, -0.5f);
        }



        void loadModelMatrixForGui(Entity entity, double posX, double posY, double posZ, double yawDelta, float size)
        {
            IEntityPlayer entityPlayer = capi.World.Player.Entity;

            Mat4f.Identity(ModelMat);
            Mat4f.Translate(ModelMat, ModelMat, (float)posX, (float)posY, (float)posZ);

            Mat4f.Translate(ModelMat, ModelMat, size, 2 * size, 0);

            float rotX = entity.Type?.Client?.Shape != null ? entity.Type.Client.Shape.rotateX : 0;
            float rotY = entity.Type?.Client?.Shape != null ? entity.Type.Client.Shape.rotateY : 0;
            float rotZ = entity.Type?.Client?.Shape != null ? entity.Type.Client.Shape.rotateZ : 0;

            Mat4f.RotateX(ModelMat, ModelMat, GameMath.PI + rotX * GameMath.DEG2RAD);
            Mat4f.RotateY(ModelMat, ModelMat, (float)yawDelta + rotY * GameMath.DEG2RAD);
            Mat4f.RotateZ(ModelMat, ModelMat, rotZ * GameMath.DEG2RAD);

            float scale = entity.Type?.Client == null ? size : entity.Type.Client.Size * size;
            Mat4f.Scale(ModelMat, ModelMat, new float[] { scale, scale, scale });
            Mat4f.Translate(ModelMat, ModelMat, -0.5f, 0f, -0.5f);
        }

    }
}
