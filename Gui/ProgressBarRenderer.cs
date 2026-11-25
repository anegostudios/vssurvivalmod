using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent;

public class ProgressBarRenderer : IRenderer, IProgressBar
{
    MeshRef whiteRectangleRef;
    MeshRef progressQuadRef;
    ICoreClientAPI capi;
    Matrixf mvMatrix = new Matrixf();

    public float Progress { get; set; } = 0;


    public double RenderOrder { get { return 0; } }

    public int RenderRange { get { return 10; } }

    public ProgressBarRenderer(ICoreClientAPI api)
    {
        this.capi = api;

        // This will get a line loop with vertices inside [-1,-1] till [1,1]
        MeshData rectangle = LineMeshUtil.GetRectangle(ColorUtil.WhiteArgb);
        whiteRectangleRef = api.Render.UploadMesh(rectangle);

        // This will get a quad with vertices inside [-1,-1] till [1,1]
        progressQuadRef = api.Render.UploadMesh(QuadMeshUtil.GetQuad());
    }


    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        IShaderProgram curShader = capi.Render.CurrentActiveShader;

        Vec4f color = new Vec4f(1, 1, 1, 1);

        // Render rectangle
        curShader.Uniform("rgbaIn", color);
        curShader.Uniform("extraGlow", 0);
        curShader.Uniform("applyColor", 0);
        curShader.Uniform("tex2d", 0);
        curShader.Uniform("noTexture", 1f);

        var fwdt = capi.Render.FrameWidth;
        var fhgt = capi.Render.FrameHeight;

        mvMatrix
            .Set(capi.Render.CurrentModelviewMatrix)
            .Translate(fwdt / 2 - 50, fhgt / 2 + 15, 50)
            .Scale(100, 20, 0)
            .Translate(0.5f, 0.5f, 0)
            .Scale(0.5f, 0.5f, 0)
        ;

        curShader.UniformMatrix("projectionMatrix", capi.Render.CurrentProjectionMatrix);
        curShader.UniformMatrix("modelViewMatrix", mvMatrix.Values);

        capi.Render.RenderMesh(whiteRectangleRef);


        // Render progress bar
        float width = Progress * 100;

        mvMatrix
            .Set(capi.Render.CurrentModelviewMatrix)
            .Translate(fwdt / 2 - 50, fhgt / 2 + 15, 50)
            .Scale(width, 20, 0)
            .Translate(0.5f, 0.5f, 0)
            .Scale(0.5f, 0.5f, 0)
        ;

        curShader.UniformMatrix("projectionMatrix", capi.Render.CurrentProjectionMatrix);
        curShader.UniformMatrix("modelViewMatrix", mvMatrix.Values);

        capi.Render.RenderMesh(progressQuadRef);
    }

    public void Dispose()
    {
        capi.Render.DeleteMesh(whiteRectangleRef);
        capi.Render.DeleteMesh(progressQuadRef);
    }
}
