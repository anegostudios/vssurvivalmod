using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public sealed class LinkageRenderer : IRenderer
    {
        readonly ICoreClientAPI capi;
        readonly ModSystemLinkage linkageSystem;
        readonly Dictionary<(int, float), LinkageMeshPool> pools = [];
        readonly Dictionary<int, List<(string? MaterialTextureCode, MeshData FirstEnd, MeshData SecondEnd, MeshData Shaft)>> sourceMeshes = [];
        readonly Vec2f materialUvOffset = new();
        readonly float[] modelMatrix = Mat4f.Create();
        readonly Vec3d startPin = new();
        readonly Vec3d endPin = new();
        readonly BlockPos previewTargetPos = new(0);

        IShaderProgram? shader;

        // Runs after AnimationUtil so a rod that ends on an animated block uses this frame's pose, not the last one.
        public double RenderOrder => 1.1;
        public int RenderRange => 128;

        public LinkageRenderer(ICoreClientAPI capi, ModSystemLinkage linkageSystem)
        {
            this.capi = capi;
            this.linkageSystem = linkageSystem;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            foreach (LinkageMeshPool pool in pools.Values) pool.Count = 0;

            foreach (LinkageConnection connection in linkageSystem.GetLoadedLinks())
            {
                ILinkageProducer? owner = linkageSystem.GetEndpoint(connection.OwnerPosition) as ILinkageProducer;
                if (owner == null || !owner.IsValidLinkagePoint(connection.OwnerPoint)) continue;

                ILinkageEndpoint? target = linkageSystem.GetEndpoint(connection.TargetPosition);
                if (target == null || !target.IsValidLinkagePoint(connection.TargetPoint)) continue;

                LinkageEndpointSelection ownerSelection = new(owner, connection.OwnerPoint);
                LinkageEndpointSelection targetSelection = new(target, connection.TargetPoint);
                if (ownerSelection.Facing.Axis != targetSelection.Facing.Axis) continue;

                ItemStack? stack = owner.LinkageStack;
                if (stack?.Item is not ItemLinkage linkage
                    || !linkage.CanAttachTo(owner, connection.OwnerPoint)
                    || !linkage.CanAttachTo(target, connection.TargetPoint))
                {
                    continue;
                }

                addLink(ownerSelection, targetSelection, linkage, stack, linkageSystem.GetRodLength(connection));
            }

            addPlacementPreview();
            renderPools();
        }

        public void Dispose()
        {
            foreach (LinkageMeshPool pool in pools.Values)
            {
                foreach (LinkageMeshPiece piece in pool.Pieces)
                {
                    foreach (MeshRef meshRef in piece.MeshRefs) meshRef.Dispose();
                }
            }
            pools.Clear();
            sourceMeshes.Clear();
        }

        void addLink(LinkageEndpointSelection start, LinkageEndpointSelection end, ItemLinkage linkage, ItemStack stack, float rodLength)
        {
            LinkageGeometry.OrderEndpoints(ref start, ref end);

            start.Endpoint.SetLinkagePin(startPin, start.Endpoint.LinkageAngleRad, start.Point);
            end.Endpoint.SetLinkagePin(endPin, end.Endpoint.LinkageAngleRad, end.Point);
            addLink(startPin, endPin, start.Position, start.Facing.Axis, linkage, stack, rodLength);
        }

        void addPlacementPreview()
        {
            LinkagePlacerWorkspace workspace = linkageSystem.GetClientWorkspace();
            if (!workspace.NowBuilding || workspace.StartPosition == null) return;
            if (linkageSystem.TryCancelPlace(capi.World.Player.Entity, onlyWhenTooFar: true)) return;

            ItemStack? stack = capi.World.Player.InventoryManager.ActiveHotbarSlot.Itemstack;
            if (stack?.Item is not ItemLinkage linkage) return;

            ILinkageEndpoint? startEndpoint = linkageSystem.GetEndpoint(workspace.StartPosition);
            if (startEndpoint == null || !startEndpoint.IsValidLinkagePoint(workspace.StartPoint)) return;

            LinkageEndpointSelection start = new(startEndpoint, workspace.StartPoint);
            if (!getPreviewTarget(start, linkage, out BlockPos? targetPos, out LinkageEndpointSelection? target) || targetPos == null) return;

            if (target != null)
            {
                addLink(start, target.Value, linkage, stack, linkageSystem.GetRodLength(start, target.Value));
                return;
            }

            // Until there is a target endpoint, show a stationary rod toward the selected cell.
            start.Endpoint.SetLinkagePinRestingPose(startPin, start.Point);
            endPin.SetWithDimension(targetPos).Add(0.5);
            if (start.Facing.Axis == EnumAxis.X) endPin.X = startPin.X;
            else endPin.Z = startPin.Z;
            bool startDrives = start.Endpoint is ILinkageProducer;
            addLink(startDrives ? startPin : endPin, startDrives ? endPin : startPin, startDrives ? start.Position : targetPos, start.Facing.Axis, linkage, stack, (float)startPin.DistanceTo(endPin));
        }

        bool getPreviewTarget(LinkageEndpointSelection start, ItemLinkage linkage, out BlockPos? targetPos, out LinkageEndpointSelection? target)
        {
            targetPos = null;
            target = null;
            BlockSelection? selection = capi.World.Player.CurrentBlockSelection;
            if (selection == null) return false;

            if (linkageSystem.TryGetEndpointSelection(selection, out var selected) && linkageSystem.TryValidatePreviewSpan(start, selected, linkage))
            {
                targetPos = selected.Position;
                target = selected;
                return true;
            }

            EnumAxis axis = start.Facing.Axis;
            if (axis == EnumAxis.Y) return false;

            BlockPos startPos = start.Position;
            int horizontalOffset = axis == EnumAxis.Z ? selection.Position.X - startPos.X : selection.Position.Z - startPos.Z;
            int verticalOffset = selection.Position.Y - startPos.Y;
            if (horizontalOffset == 0 && verticalOffset == 0) return false;

            int lengthSquared = horizontalOffset * horizontalOffset + verticalOffset * verticalOffset;
            if (lengthSquared > ModSystemLinkage.MaxLengthSquared)
            {
                int bestHorizontal = 0;
                int bestVertical = 0;
                int bestLengthSquared = 0;
                double bestDirection = double.MinValue;

                for (int horizontal = -ModSystemLinkage.MaxLength; horizontal <= ModSystemLinkage.MaxLength; horizontal++)
                {
                    for (int vertical = -ModSystemLinkage.MaxLength; vertical <= ModSystemLinkage.MaxLength; vertical++)
                    {
                        int candidateLengthSquared = horizontal * horizontal + vertical * vertical;
                        if (candidateLengthSquared == 0 || candidateLengthSquared > ModSystemLinkage.MaxLengthSquared) continue;

                        double direction = (horizontal * horizontalOffset + vertical * verticalOffset) / Math.Sqrt(candidateLengthSquared);
                        if (direction < bestDirection
                            || (direction == bestDirection && candidateLengthSquared <= bestLengthSquared))
                        {
                            continue;
                        }

                        bestDirection = direction;
                        bestLengthSquared = candidateLengthSquared;
                        bestHorizontal = horizontal;
                        bestVertical = vertical;
                    }
                }

                horizontalOffset = bestHorizontal;
                verticalOffset = bestVertical;
            }

            targetPos = previewTargetPos.Set(startPos, startPos.dimension);
            ref int horizontalCoordinate = ref (axis == EnumAxis.Z ? ref targetPos.X : ref targetPos.Z);
            horizontalCoordinate += horizontalOffset;
            targetPos.Y += verticalOffset;
            return true;
        }

        void addLink(Vec3d start, Vec3d end, BlockPos lightPosition, EnumAxis axleAxis, ItemLinkage linkage, ItemStack stack, float rodLength)
        {
            if (rodLength <= 0 || rodLength > ModSystemLinkage.MaxLength + 1) return;

            Mat4f.Identity(modelMatrix);
            if (!LinkageGeometry.TrySetRodOrientation(modelMatrix, start, end, axleAxis, out _)) return;

            LinkageMeshPool? pool = getPool(linkage, rodLength);
            if (pool == null) return;

            Vec3d cameraPos = capi.World.Player.Entity.CameraPos;
            modelMatrix[12] = (float)(start.X - cameraPos.X);
            modelMatrix[13] = (float)(start.Y - cameraPos.Y);
            modelMatrix[14] = (float)(start.Z - cameraPos.Z);
            Vec3f firstPin = linkage.MeshProperties.FirstPin;
            Mat4f.Translate(modelMatrix, modelMatrix, -firstPin.X, -firstPin.Y, -firstPin.Z);

            Vec4f light = capi.World.BlockAccessor.GetLightRGBs(lightPosition);
            foreach (LinkageMeshPiece piece in pool.Pieces)
            {
                materialUvOffset.X = 0;
                materialUvOffset.Y = 0;
                if (piece.MaterialTextureCode != null) linkage.Material.MaterialProperties.GetMaterialUvOffset(capi, stack, piece.MaterialTextureCode, materialUvOffset);
                foreach (MeshData mesh in piece.Meshes)
                {
                    CustomMeshDataPartFloat instanceData = mesh.CustomFloats;
                    int index = pool.Count * MechBlockRenderer.MaterialInstanceDataSize;
                    if (index + MechBlockRenderer.MaterialInstanceDataSize > instanceData.BufferSize) instanceData.GrowBuffer(instanceData.BufferSize);
                    instanceData.Values[index] = light.R;
                    instanceData.Values[++index] = light.G;
                    instanceData.Values[++index] = light.B;
                    instanceData.Values[++index] = light.A;
                    for (int i = 0; i < 16; i++) instanceData.Values[++index] = modelMatrix[i];
                    instanceData.Values[++index] = materialUvOffset.X;
                    instanceData.Values[++index] = materialUvOffset.Y;
                }
            }
            pool.Count++;
        }

        LinkageMeshPool? getPool(ItemLinkage linkage, float length)
        {
            (int, float) key = (linkage.Id, length);
            if (pools.TryGetValue(key, out LinkageMeshPool? pool)) return pool;

            if (!sourceMeshes.TryGetValue(linkage.Id, out var meshes))
            {
                CompositeShape? compositeShape = linkage.Shape;
                if (compositeShape?.Base == null) return null;

                AssetLocation shapeLocation = compositeShape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
                Shape? shape = Shape.TryGet(capi, shapeLocation);
                if (shape == null) return null;

                Vec3f rotation = new(compositeShape.rotateX, compositeShape.rotateY, compositeShape.rotateZ);
                LinkageMeshProperties properties = linkage.MeshProperties;
                MechanicalMaterialProperties material = linkage.Material.MaterialProperties;
                ITexPositionSource baseSource = linkage.Material.GetMaterialTextureSource(shape, null, capi.BlockTextureAtlas);
                List<string?> textureCodes = [null, .. material.MaterialTextureCodes];
                meshes = [];
                foreach (string? textureCode in textureCodes)
                {
                    Shape pieceShape = material.GetMaterialRenderShape(shape, textureCode);
                    ITexPositionSource textureSource = textureCode == null ? baseSource : material.GetMaterialRenderTextureSource(capi, baseSource, textureCode);
                    capi.Tesselator.TesselateShape(linkage.Code.ToString(), pieceShape, out var firstEndMesh, textureSource, rotation, selectiveElements: properties.FirstEndElements);
                    capi.Tesselator.TesselateShape(linkage.Code.ToString(), pieceShape, out var secondEndMesh, textureSource, rotation, selectiveElements: properties.SecondEndElements);
                    capi.Tesselator.TesselateShape(linkage.Code.ToString(), pieceShape, out var shaftMesh, textureSource, rotation, selectiveElements: properties.ShaftElements);
                    meshes.Add((textureCode, firstEndMesh, secondEndMesh, shaftMesh));
                }
                sourceMeshes[linkage.Id] = meshes;
            }

            pool = new LinkageMeshPool();
            try
            {
                foreach (var source in meshes)
                {
                    MeshData mesh = buildMesh(source.FirstEnd.Clone(), source.SecondEnd.Clone(), source.Shaft, linkage.MeshProperties, length);
                    if (mesh.VerticesCount == 0) continue;

                    // Each material region can use a different atlas page.
                    MeshData[] parts = mesh.SplitByTextureId();
                    if (parts.Length == 0) continue;

                    LinkageMeshPiece piece = new()
                    {
                        MeshRefs = new MeshRef[parts.Length],
                        Meshes = parts,
                        AtlasTextureIds = mesh.TextureIds,
                        MaterialTextureCode = source.MaterialTextureCode
                    };
                    pool.Pieces.Add(piece);
                    for (int i = 0; i < parts.Length; i++)
                    {
                        CustomMeshDataPartFloat instanceData = new(MechBlockRenderer.MaterialInstanceDataSize)
                        {
                            Instanced = true,
                            InterleaveOffsets = [0, 16, 32, 48, 64, 80],
                            InterleaveSizes = [4, 4, 4, 4, 4, 2],
                            InterleaveStride = MechBlockRenderer.MaterialInstanceDataSize * sizeof(float),
                            StaticDraw = false
                        };
                        instanceData.SetAllocationSize(instanceData.BufferSize);
                        parts[i].CustomFloats = instanceData;
                        piece.MeshRefs[i] = capi.Render.UploadMesh(parts[i]);
                    }
                }
            }
            catch
            {
                foreach (LinkageMeshPiece piece in pool.Pieces)
                {
                    foreach (MeshRef meshRef in piece.MeshRefs) meshRef?.Dispose();
                }
                throw;
            }

            return pools[key] = pool;
        }

        static MeshData buildMesh(MeshData firstEndMesh, MeshData secondEndMesh, MeshData shaftMesh, LinkageMeshProperties properties, float length)
        {
            float shaftLength = length - (properties.PinSpan - properties.ShaftLength);
            secondEndMesh.Translate(length - properties.PinSpan, 0, 0);
            MeshData mesh = firstEndMesh.VerticesCount > 0 ? firstEndMesh : secondEndMesh;
            if (firstEndMesh.VerticesCount > 0) mesh.AddMeshData(secondEndMesh);

            for (float offset = 0; shaftMesh.VerticesCount > 0 && offset < shaftLength; offset += properties.ShaftLength)
            {
                float sectionScale = Math.Min(1, (shaftLength - offset) / properties.ShaftLength);
                MeshData section = shaftMesh.Clone();
                if (sectionScale < 1)
                {
                    cropShaftUvs(section, sectionScale);
                    section.Scale(properties.ShaftOrigin, sectionScale, 1, 1);
                }
                section.Translate(offset, 0, 0);
                if (mesh.VerticesCount == 0) mesh = section;
                else mesh.AddMeshData(section);
            }

            return mesh;
        }

        // Crop the final repeat to its physical length so the texture keeps the same scale.
        static void cropShaftUvs(MeshData mesh, float length)
        {
            int verticesPerFace = mesh.VerticesPerFace;
            for (int firstVertex = 0; firstVertex < mesh.VerticesCount; firstVertex += verticesPerFace)
            {
                float startX = mesh.xyz[firstVertex * 3];
                float endX = startX;
                for (int i = 1; i < verticesPerFace; i++)
                {
                    float x = mesh.xyz[(firstVertex + i) * 3];
                    startX = Math.Min(startX, x);
                    endX = Math.Max(endX, x);
                }
                if (startX == endX) continue;

                float startU = 0;
                float startV = 0;
                float endU = 0;
                float endV = 0;
                int startVertices = 0;
                int endVertices = 0;
                for (int i = 0; i < verticesPerFace; i++)
                {
                    int vertex = firstVertex + i;
                    float x = mesh.xyz[vertex * 3];
                    if (x == startX)
                    {
                        startU += mesh.Uv[vertex * 2];
                        startV += mesh.Uv[vertex * 2 + 1];
                        startVertices++;
                    }
                    else if (x == endX)
                    {
                        endU += mesh.Uv[vertex * 2];
                        endV += mesh.Uv[vertex * 2 + 1];
                        endVertices++;
                    }
                }

                startU /= startVertices;
                startV /= startVertices;
                endU /= endVertices;
                endV /= endVertices;

                bool lengthUsesU = Math.Abs(endU - startU) >= Math.Abs(endV - startV);
                float croppedEnd = lengthUsesU
                    ? startU + (endU - startU) * length
                    : startV + (endV - startV) * length;
                for (int i = 0; i < verticesPerFace; i++)
                {
                    int vertex = firstVertex + i;
                    if (mesh.xyz[vertex * 3] != endX) continue;

                    mesh.Uv[vertex * 2 + (lengthUsesU ? 0 : 1)] = croppedEnd;
                }
            }
        }

        void renderPools()
        {
            bool hasInstances = false;
            foreach (LinkageMeshPool pool in pools.Values)
            {
                if (pool.Count > 0)
                {
                    hasInstances = true;
                    break;
                }
            }
            if (!hasInstances) return;

            shader ??= capi.Shader.GetProgramByName("instanced");
            if (shader.Disposed) shader = capi.Shader.GetProgramByName("instanced");

            var render = capi.Render;
            render.GlDisableCullFace();
            render.GlToggleBlend(false);
            shader.Use();
            shader.Uniform("useUvOffset", 1);
            shader.Uniform("rgbaFogIn", render.FogColor);
            shader.Uniform("rgbaAmbientIn", render.AmbientColor);
            shader.Uniform("fogMinIn", render.FogMin);
            shader.Uniform("fogDensityIn", render.FogDensity);
            shader.UniformMatrix("projectionMatrix", render.CurrentProjectionMatrix);
            shader.UniformMatrix("modelViewMatrix", render.CameraMatrixOriginf);

            foreach (LinkageMeshPool pool in pools.Values)
            {
                if (pool.Count == 0) continue;

                foreach (LinkageMeshPiece piece in pool.Pieces)
                {
                    for (int i = 0; i < piece.MeshRefs.Length; i++)
                    {
                        CustomMeshDataPartFloat instanceData = piece.Meshes[i].CustomFloats;
                        instanceData.Count = pool.Count * MechBlockRenderer.MaterialInstanceDataSize;
                        pool.UpdateMesh.CustomFloats = instanceData;
                        shader.BindTexture2D("tex", piece.AtlasTextureIds[i], 0);
                        if (instanceData.BufferSize > instanceData.AllocationSize)
                        {
                            instanceData.SetAllocationSize(instanceData.BufferSize);
                            piece.MeshRefs[i].Dispose();
                            piece.MeshRefs[i] = render.UploadMesh(piece.Meshes[i]);
                        }
                        else
                        {
                            render.UpdateMesh(piece.MeshRefs[i], pool.UpdateMesh);
                        }
                        render.RenderMeshInstanced(piece.MeshRefs[i], pool.Count);
                    }
                }
            }

            shader.Uniform("useUvOffset", 0);
            shader.Stop();
            render.GlEnableCullFace();
        }

        sealed class LinkageMeshPool
        {
            public readonly List<LinkageMeshPiece> Pieces = [];
            public readonly MeshData UpdateMesh = new();
            public int Count;
        }

        sealed class LinkageMeshPiece
        {
            public required MeshRef[] MeshRefs;
            public required MeshData[] Meshes;
            public required int[] AtlasTextureIds;
            public string? MaterialTextureCode;
        }
    }
}
