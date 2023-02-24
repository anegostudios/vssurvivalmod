using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BeamPlacerWorkSpace {
        public BlockPos startPos;
        public BlockFacing onFacing;
        public Vec3f startOffset;
        public Vec3f endOffset;
        public MeshData currentMesh;
        public MeshRef currentMeshRef;
        public bool nowBuilding;
        public Block block;

        public int GridSize = 4;
    }


    public class ModSystemSupportBeamPlacer : ModSystem, IRenderer
    {
        public override bool ShouldLoad(EnumAppSide forSide) => true;

        protected Dictionary<Block, MeshData> origBeamMeshes = new Dictionary<Block, MeshData>();
        Dictionary<string, BeamPlacerWorkSpace> workspaceByPlayer = new Dictionary<string, BeamPlacerWorkSpace>();
        
        ICoreAPI api;
        ICoreClientAPI capi;
        public Matrixf ModelMat = new Matrixf();

        public double RenderOrder => 0.5;
        public int RenderRange => 12;

        public override void Start(ICoreAPI api)
        {
            this.api = api;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            api.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "beamplacer");
        }


        public bool CancelPlace(BlockSupportBeam blockSupportBeam, EntityAgent byEntity)
        {
            var ws = getWorkSpace(byEntity);
            if (ws.nowBuilding)
            {
                ws.nowBuilding = false;
                ws.startOffset = null;
                ws.endOffset = null;
                return true;
            }

            return false;
        }

        public Vec3f snapToGrid(IVec3 pos, int gridSize)
        {
            var gs = gridSize;
            return new Vec3f(
                (int)Math.Round(pos.XAsFloat * gs) / (float)gs,
                (int)Math.Round(pos.YAsFloat * gs) / (float)gs,
                (int)Math.Round(pos.ZAsFloat * gs) / (float)gs
            );
        }

        public void OnInteract(Block block, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel)
        {
            if (blockSel == null)
            {
                return;
            }

            var ws = getWorkSpace(byEntity);

            if (!ws.nowBuilding)
            {
                if (byEntity.Controls.Sprint) ws.GridSize = 16;
                else ws.GridSize = 4;

                ws.currentMesh = getOrCreateBeamMesh(block);

                var be = api.World.BlockAccessor.GetBlockEntity(blockSel.Position);
                var beh = be?.GetBehavior<BEBehaviorSupportBeam>();
                if (beh == null)
                {
                    ws.startPos = blockSel.Position.AddCopy(blockSel.Face);
                    var face = blockSel.Face;
                    ws.startOffset = snapToGrid(blockSel.HitPosition, ws.GridSize).Sub(face.Normali);
                } else
                {
                    ws.startPos = blockSel.Position.Copy();
                    ws.startOffset = snapToGrid(blockSel.HitPosition, ws.GridSize);
                }
                
                ws.endOffset = null;
                ws.nowBuilding = true;
                ws.block = block;
                ws.onFacing = blockSel.Face;
            } else
            {
                ws.nowBuilding = false;
                var be = api.World.BlockAccessor.GetBlockEntity(ws.startPos);
                var beh = be?.GetBehavior<BEBehaviorSupportBeam>();
                if (beh == null)
                {
                    api.World.BlockAccessor.SetBlock(ws.block.Id, ws.startPos);
                    be = api.World.BlockAccessor.GetBlockEntity(ws.startPos);
                    beh = be?.GetBehavior<BEBehaviorSupportBeam>();
                }

                var eplr = (byEntity as EntityPlayer);

                Vec3f nowEndOffset = getEndOffset(eplr.Player, ws);

                if (eplr.Player.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    int len = (int)Math.Ceiling(nowEndOffset.DistanceTo(ws.startOffset));
                    if (slot.StackSize < len)
                    {
                        (api as ICoreClientAPI)?.TriggerIngameError(this, "notenoughitems", Lang.Get("You need {0} beams to place a beam at this lenth", len));
                        return;
                    }
                    slot.TakeOut(len);
                    slot.MarkDirty();
                }

                beh.AddBeam(ws.startOffset, nowEndOffset, ws.onFacing, ws.block);
                be.MarkDirty(true);
            }
        }

        public MeshData getOrCreateBeamMesh(Block block)
        {
            if (capi == null) return null;

            if (!origBeamMeshes.TryGetValue(block, out var meshData))
            {
                capi.Tesselator.TesselateShape(block, capi.TesselatorManager.GetCachedShape(block.Shape.Base), out meshData);
                origBeamMeshes[block] = meshData;
                return meshData;
            }

            return meshData;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            var ws = getWorkSpace(capi.World.Player.PlayerUID);

            if (!ws.nowBuilding) return;

            Vec3f nowEndOffset = getEndOffset(capi.World.Player, ws);

            if (ws.startOffset.DistanceTo(nowEndOffset) < 0.1) return;

            if (ws.endOffset != nowEndOffset)
            {
                ws.endOffset = nowEndOffset;
                reloadMeshRef();
            }
            if (ws.currentMeshRef == null) return;


            var prevprog = capi.Render.CurrentActiveShader;
            prevprog?.Stop();
            var prog = capi.Render.PreparedStandardShader((int)ws.startPos.X, (int)ws.startPos.Y, (int)ws.startPos.Z);
            Vec3d camPos = capi.World.Player.Entity.CameraPos;

            prog.Use();

            prog.Tex2D = capi.BlockTextureAtlas.AtlasTextures[0].TextureId;

            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(ws.startPos.X - camPos.X, ws.startPos.Y - camPos.Y, ws.startPos.Z - camPos.Z)
                .Values
            ;

            prog.ViewMatrix = capi.Render.CameraMatrixOriginf;
            prog.ProjectionMatrix = capi.Render.CurrentProjectionMatrix;

            capi.Render.RenderMesh(ws.currentMeshRef);

            prog.Stop();
            prevprog?.Use();
        }


        protected Vec3f getEndOffset(IPlayer player, BeamPlacerWorkSpace ws)
        {
            Vec3f nowEndOffset;
            Vec3d vec;
            
            if (player.CurrentBlockSelection != null)
            {
                var blockSel = player.CurrentBlockSelection;
                vec = blockSel.Position.ToVec3d().Sub(ws.startPos).Add(blockSel.HitPosition);
            }
            else
            {
                vec = player.Entity.SidedPos.AheadCopy(2).XYZ.Add(player.Entity.LocalEyePos).Sub(ws.startPos);
            }

            nowEndOffset = snapToGrid(vec, ws.GridSize);


            if (player.Entity.Controls.Sneak)
            {
                double dX = nowEndOffset.X - ws.startOffset.X;
                double dY = nowEndOffset.Y - ws.startOffset.Y;
                double dZ = nowEndOffset.Z - ws.startOffset.Z;
                double len = Math.Sqrt(dZ * dZ + dY * dY + dX * dX);
                double horlen = Math.Sqrt(dZ * dZ + dX * dX);

                float yaw = -GameMath.PIHALF - (float)Math.Atan2(-dX, -dZ);
                float pitch = (float)Math.Atan2(horlen, dY);

                float rotSnap = 15f;
                yaw = (float)Math.Round(yaw * GameMath.RAD2DEG / rotSnap) * (float)rotSnap * GameMath.DEG2RAD;
                pitch = (float)Math.Round(pitch * GameMath.RAD2DEG / rotSnap) * (float)rotSnap * GameMath.DEG2RAD;

                double cosYaw = Math.Cos(yaw);
                double sinYaw = Math.Sin(yaw);
                double cosPitch = Math.Cos(pitch);
                double sinPitch = Math.Sin(pitch);

                nowEndOffset = new Vec3f(
                    ws.startOffset.X + (float)(len * sinPitch * cosYaw),
                    ws.startOffset.Y + (float)(len * cosPitch),
                    ws.startOffset.Z + (float)(len * sinPitch * sinYaw)
                );
            }

            return nowEndOffset;
        }

        private void reloadMeshRef()
        {
            var ws = getWorkSpace(capi.World.Player.PlayerUID);
            ws.currentMeshRef?.Dispose();

            var mesh = generateMesh(ws.startOffset, ws.endOffset, ws.onFacing, ws.currentMesh);
            ws.currentMeshRef = capi.Render.UploadMesh(mesh);
        }

        public static MeshData generateMesh(Vec3f start, Vec3f end, BlockFacing facing, MeshData origMesh)
        {
            var outMesh = new MeshData(4, 6).WithRenderpasses().WithXyzFaces().WithColorMaps();

            float[] mat = new float[16];

            double dX = end.X - start.X;
            double dY = end.Y - start.Y;
            double dZ = end.Z - start.Z;
            double len = Math.Sqrt(dZ * dZ + dY * dY + dX * dX);
            double horlen = Math.Sqrt(dZ * dZ + dX * dX);

            var normalize = 1 / Math.Max(1, len);
            var dir = new Vec3f((float)(dX * normalize), (float)(dY * normalize), (float)(dZ * normalize));
            float yaw = (float)Math.Atan2(-dX, -dZ) + GameMath.PIHALF;
            float pitch = (float)Math.Atan2(horlen, -dY) + GameMath.PIHALF;

            // Sin and Cos are both 1 at 45 deg.
            float yawExtend = Math.Abs((float)(Math.Sin(yaw) * Math.Cos(yaw)));
            float pitchExtend = Math.Abs((float)(Math.Sin(pitch) * Math.Cos(pitch)));

            float distTo45Deg = Math.Max(yawExtend, pitchExtend);
            float extend = 1 / 16f * distTo45Deg * 4;

            len += extend;
            for (float r = -extend; r < len; r++)
            {
                double sectionLen = Math.Min(1, len - r);
                var sectionStart = start + r * dir;

                Mat4f.Identity(mat);
                Mat4f.Translate(mat, mat, sectionStart.X, sectionStart.Y, sectionStart.Z);
                Mat4f.RotateY(mat, mat, yaw);
                Mat4f.RotateZ(mat, mat, pitch);
                Mat4f.Scale(mat, mat, new float[] { (float)sectionLen, 1, 1 });
                Mat4f.Translate(mat, mat, -1f, -0.125f, -0.5f);

                var mesh = origMesh.Clone();
                mesh.MatrixTransform(mat);

                outMesh.AddMeshData(mesh);
            }

            return outMesh;
        }

        public static float[] GetAlignMatrix(IVec3 startPos, IVec3 endPos, BlockFacing facing)
        {
            double dX = startPos.XAsDouble - endPos.XAsDouble;
            double dY = startPos.YAsDouble - endPos.YAsDouble;
            double dZ = startPos.ZAsDouble - endPos.ZAsDouble;
            double len = Math.Sqrt(dZ * dZ + dY * dY + dX * dX);
            double horlen = Math.Sqrt(dZ * dZ + dX * dX);

            float yaw = (float)Math.Atan2(dX, dZ) + GameMath.PIHALF;
            float pitch = (float)Math.Atan2(horlen, dY) + GameMath.PIHALF;

            float[] mat = new float[16];
            Mat4f.Identity(mat);
            Mat4f.Translate(mat, mat, (float)startPos.XAsDouble, (float)startPos.YAsDouble, (float)startPos.ZAsDouble);
            Mat4f.RotateY(mat, mat, yaw);
            Mat4f.RotateZ(mat, mat, pitch);
            Mat4f.Scale(mat, mat, new float[] { (float)len, 1, 1 });
            Mat4f.Translate(mat, mat, -1f, -0.125f, -0.5f);
            return mat;
        }




        BeamPlacerWorkSpace getWorkSpace(EntityAgent forEntity)
        {
            return getWorkSpace((forEntity as EntityPlayer)?.PlayerUID);
        }

        private BeamPlacerWorkSpace getWorkSpace(string playerUID)
        {
            if (workspaceByPlayer.TryGetValue(playerUID, out var ws))
            {
                return ws;
            }
            return workspaceByPlayer[playerUID] = new BeamPlacerWorkSpace();
        }

        
    }

    public class BlockSupportBeam : Block
    {
        ModSystemSupportBeamPlacer bp;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            bp = api.ModLoader.GetModSystem<ModSystemSupportBeamPlacer>();
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            var be = api.World.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorSupportBeam>();
            if (be != null) return be.GetCollisionBoxes();

            return base.GetSelectionBoxes(blockAccessor, pos);
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            var be = api.World.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorSupportBeam>();
            if (be != null) return be.GetCollisionBoxes();

            return base.GetCollisionBoxes(blockAccessor, pos);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            handling = EnumHandHandling.PreventDefault;
            bp.OnInteract(this, slot, byEntity, blockSel);
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (bp.CancelPlace(this, byEntity))
            {
                handling = EnumHandHandling.PreventDefault;
                return;
            }

            base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
        }


        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            var be = api.World.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorSupportBeam>();
            if (be != null) return be.GetDrops(byPlayer);

            return base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
        }


        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            return false;
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = Lang.Get("Set Beam Start/End Point (Snap to 4x4 grid)"),
                    MouseButton = EnumMouseButton.Right
                },
                new WorldInteraction()
                {
                    ActionLangCode = Lang.Get("Set Beam Start/End Point (Snap to 16x16 grid)"),
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "sprint"
                },
                new WorldInteraction()
                {
                    ActionLangCode = Lang.Get("Cancel placement"),
                    MouseButton = EnumMouseButton.Left
                },
            };
        }
    }
}
