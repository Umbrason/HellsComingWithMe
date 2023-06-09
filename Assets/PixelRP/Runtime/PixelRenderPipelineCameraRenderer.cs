using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class PixelRenderPipelineCameraRenderer
{
    private Camera camera;
    private ScriptableRenderContext context;
    private PixelRenderPipelineAsset.IReadOnlyResolutionSettings resolutionSettings;
    private string DefaultSampleName;
    private static CommandBuffer buffer = new();

    public PixelRenderPipelineCameraRenderer(ScriptableRenderContext context, Camera camera, PixelRenderPipelineAsset.IReadOnlyResolutionSettings resolutionSettings)
    {
        this.context = context;
        this.camera = camera;
        this.resolutionSettings = resolutionSettings;
        this.DefaultSampleName = $"Render {camera.name}";
    }

    private static int environmentColorAttachmentID = Shader.PropertyToID("_EnvColor0");
    private static int environmentDepthAttachmentID = Shader.PropertyToID("_EnvDepth0");

    private static int environmentColor2xAttachmentID = Shader.PropertyToID("_EnvColor2x");
    private static int environmentDepth2xAttachmentID = Shader.PropertyToID("_EnvDepth2x");

    private static int characterColorAttachmentID = Shader.PropertyToID("_CharacterColor0");
    private static int characterDepthAttachmentID = Shader.PropertyToID("_CharacterDepth0");

    private void SetupCharacterTargets()
    {
        buffer.GetTemporaryRT(characterColorAttachmentID, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Trilinear);
        buffer.GetTemporaryRT(characterDepthAttachmentID, camera.pixelWidth, camera.pixelHeight, 24, FilterMode.Trilinear, UnityEngine.Experimental.Rendering.GraphicsFormat.None);
        buffer.SetRenderTarget(color: characterColorAttachmentID, depth: characterDepthAttachmentID);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }


    private void SetupEnvironmentTargets(Vector2Int environmentResolution)
    {
        buffer.GetTemporaryRT(environmentColorAttachmentID, environmentResolution.x, environmentResolution.y, 0, FilterMode.Trilinear);
        buffer.GetTemporaryRT(environmentDepthAttachmentID, environmentResolution.x, environmentResolution.y, 24, FilterMode.Trilinear, UnityEngine.Experimental.Rendering.GraphicsFormat.None);
        buffer.GetTemporaryRT(environmentColor2xAttachmentID, environmentResolution.x * 2, environmentResolution.y * 2, 0, FilterMode.Trilinear);
        buffer.GetTemporaryRT(environmentDepth2xAttachmentID, environmentResolution.x * 2, environmentResolution.y * 2, 24, FilterMode.Trilinear, UnityEngine.Experimental.Rendering.GraphicsFormat.None);
        buffer.SetRenderTarget(color: environmentColor2xAttachmentID, depth: environmentDepth2xAttachmentID);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    private void ReleaseTempRTs()
    {
        buffer.ReleaseTemporaryRT(environmentColorAttachmentID);
        buffer.ReleaseTemporaryRT(environmentDepthAttachmentID);
        buffer.ReleaseTemporaryRT(environmentColor2xAttachmentID);
        buffer.ReleaseTemporaryRT(environmentDepth2xAttachmentID);
        buffer.ReleaseTemporaryRT(characterColorAttachmentID);
        buffer.ReleaseTemporaryRT(characterDepthAttachmentID);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    private void PresentImage()
    {
        buffer.Blit(characterColorAttachmentID, BuiltinRenderTextureType.CameraTarget);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public void Render()
    {
        var environmentResolution = resolutionSettings.CalculateResolution(camera.pixelRect.size);
        context.SetupCameraProperties(camera);
        ClearRenderTarget();

        BeginSample(DefaultSampleName);

        BeginSample("Draw Environment");
        SetupEnvironmentTargets(environmentResolution);
        ClearRenderTarget();
        DrawEnvironment();
        MedianFilterEnvironment(environmentResolution);
        EndSample();


        BeginSample("Draw Characters");
        SetupCharacterTargets();
        ClearRenderTarget();
        UpscaleEnvironment(environmentResolution, camera.pixelRect.size);

        EndSample();



        BeginSample("Present");
        PresentImage();
        EndSample();

        ReleaseTempRTs();
        EndSample();


#if UNITY_EDITOR
        BeginSample(DefaultSampleName);
        EmitSceneViewGeometry();
        DrawSceneGizmos();
        EndSample();
#endif        

        context.Submit();
    }

    #region Util
    private Stack<string> Samples = new();
    private void BeginSample(string SampleName)
    {
        Samples.Push(SampleName);
        buffer.name = "";
        buffer.BeginSample(SampleName);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    private void EndSample()
    {
        buffer.EndSample(Samples.Pop());
        buffer.name = Samples.Count > 0 ? Samples.Peek() : this.DefaultSampleName;
    }
    #endregion

    #region Median Filter

    private Material MedianBlitMaterial => cached_MedianBlitMaterial ??= MedianBlitShader ? new Material(MedianBlitShader) : null;
    private Material cached_MedianBlitMaterial;

    private Shader MedianBlitShader => cached_MedianBlitShader ??= Shader.Find("Hidden/MedianDownscale");
    private Shader cached_MedianBlitShader;

    private void MedianFilterEnvironment(Vector2 srcRes)
    {
        if (MedianBlitMaterial == null) return;
        BeginSample("Median Filter Environment");

        buffer.SetRenderTarget(environmentColorAttachmentID, environmentDepthAttachmentID);

        var srcColorIdentifier = new RenderTargetIdentifier(nameID: environmentColor2xAttachmentID);
        var srcDepthIdentifier = new RenderTargetIdentifier(nameID: environmentDepth2xAttachmentID);
        buffer.SetGlobalTexture("_BlitSrcColor", srcColorIdentifier, RenderTextureSubElement.Color);
        buffer.SetGlobalTexture("_BlitSrcDepth", srcDepthIdentifier, RenderTextureSubElement.Depth);

        var texelSize = new Vector4(1f / srcRes.x, 1f / srcRes.y, srcRes.x, srcRes.y);
        buffer.SetGlobalVector("_BlitSrcColor_TexelSize", texelSize);
        buffer.SetGlobalVector("_BlitSrcDepth_TexelSize", texelSize);

        buffer.DrawProcedural(Matrix4x4.identity, MedianBlitMaterial, 0, MeshTopology.Triangles, 3, 1);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
        EndSample();
    }
    #endregion

    #region Upscale
    private Material UpscaleBlitMaterial => cached_UpscaleBlitMaterial ??= UpscaleBlitShader ? new Material(UpscaleBlitShader) : null;
    private Material cached_UpscaleBlitMaterial;

    private Shader UpscaleBlitShader => cached_UpscaleBlitShader ??= Shader.Find("Hidden/PixelUpscaleColorAndDepth");
    private Shader cached_UpscaleBlitShader;

    private void UpscaleEnvironment(Vector2 from, Vector2 to)
    {
        if (UpscaleBlitMaterial == null) return;
        BeginSample("Upscale Environment");
        var depthIdentifier = new RenderTargetIdentifier(nameID: environmentDepthAttachmentID);
        var colorIdentifier = new RenderTargetIdentifier(nameID: environmentColorAttachmentID);
        buffer.SetGlobalTexture("_EnvironmentDepth", depthIdentifier, RenderTextureSubElement.Depth);
        buffer.SetGlobalTexture("_EnvironmentColor", colorIdentifier, RenderTextureSubElement.Color);

        var texelSize = new Vector4(1f / from.x, 1f / from.y, from.x, from.y);
        buffer.SetGlobalVector("_EnvironmentColor_TexelSize", texelSize);
        buffer.SetGlobalVector("_EnvironmentDepth_TexelSize", texelSize);
        buffer.SetGlobalVector("_pxPerTex", to / from);
        buffer.DrawProcedural(Matrix4x4.identity, UpscaleBlitMaterial, 0, MeshTopology.Triangles, 3, 1);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
        EndSample();
    }
    #endregion

    #region Clear
    private void ClearRenderTarget()
    {
        var flags = (camera.clearFlags) switch
        {
            CameraClearFlags.Skybox => RTClearFlags.All,
            CameraClearFlags.Color => RTClearFlags.All,
            CameraClearFlags.Depth => RTClearFlags.DepthStencil,
            CameraClearFlags.Nothing => RTClearFlags.None,
            _ => RTClearFlags.All
        };

        buffer.ClearRenderTarget(flags, camera.backgroundColor, 1f, 0);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
    #endregion

    private static ShaderTagId UnlitLightMode = new ShaderTagId("SRPDefaultUnlit");
    private static FilteringSettings OpaqueFilterSettings = new()
    {
        layerMask = -1,
        renderQueueRange = RenderQueueRange.opaque,
        sortingLayerRange = SortingLayerRange.all,
        renderingLayerMask = uint.MaxValue,
    };
    private static FilteringSettings TransparentFilterSettings = new()
    {
        layerMask = -1,
        renderQueueRange = RenderQueueRange.transparent,
        sortingLayerRange = SortingLayerRange.all,
        renderingLayerMask = uint.MaxValue,
    };




    void DrawEnvironment()
    {
        DrawingSettings OpaqueDrawSettings = new(
            UnlitLightMode,
            new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque }
        );
        DrawingSettings TransparentDrawSettings = new(
            UnlitLightMode,
            new SortingSettings(camera) { criteria = SortingCriteria.CommonTransparent }
        );
        if (!camera.TryGetCullingParameters(out var cullingParameters)) return;
        var cullingResults = context.Cull(ref cullingParameters);

        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
        context.DrawRenderers(cullingResults, ref OpaqueDrawSettings, ref OpaqueFilterSettings);
        //context.DrawSkybox(camera);
        context.DrawRenderers(cullingResults, ref TransparentDrawSettings, ref TransparentFilterSettings);
    }

    partial void DrawSceneGizmos();
    partial void EmitSceneViewGeometry();
}
