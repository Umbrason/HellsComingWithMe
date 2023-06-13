using System.Collections.Generic;
using System.Linq;
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
        buffer.SetRenderTarget(color: environmentColorAttachmentID, depth: environmentDepthAttachmentID);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    private void ReleaseTempRTs()
    {
        buffer.ReleaseTemporaryRT(environmentColorAttachmentID);
        buffer.ReleaseTemporaryRT(environmentDepthAttachmentID);
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
        if (!camera.TryGetCullingParameters(out var cullingParameters)) return;
        var cullingResults = context.Cull(ref cullingParameters);
        ClearRenderTarget();

        BeginSample(DefaultSampleName);

        SetupLights(cullingResults);

        BeginSample("Draw Environment");
        SetupEnvironmentTargets(environmentResolution);
        ClearRenderTarget();
        DrawEnvironment(cullingResults);
        EndSample();


        BeginSample("Draw Characters");
        SetupCharacterTargets();
        ClearRenderTarget();
        UpscaleEnvironment(environmentResolution, camera.pixelRect.size);
        DrawCharacters(cullingResults);
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

    #region Light

    private static int directionalLightCountId = Shader.PropertyToID("_DirectionalLightCount");
    private static int directionalLightColorId = Shader.PropertyToID("_DirectionalLightColors");
    private static int directionalLightDirectionId = Shader.PropertyToID("_DirectionalLightDirections");

    private const int MAX_DIR_LIGHTS_COUNT = 4;
    private Vector4[] directionalLightColors = new Vector4[4];
    private Vector4[] directionalLightDirections = new Vector4[4];
    private void SetupLights(CullingResults cullingResults)
    {
        var lights = cullingResults.visibleLights;
        dirLightIndex = 0;
        for (int i = 0; i < lights.Length; i++)
        {
            var light = lights[i];
            if (light.lightType != UnityEngine.LightType.Directional) continue;
            SetupDirLight(light);
        }
        buffer.SetGlobalInt(directionalLightCountId, dirLightIndex);
        buffer.SetGlobalVectorArray(directionalLightColorId, directionalLightColors);
        buffer.SetGlobalVectorArray(directionalLightDirectionId, directionalLightDirections);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    private int dirLightIndex = 0;
    private void SetupDirLight(VisibleLight light)
    {
        if (dirLightIndex >= MAX_DIR_LIGHTS_COUNT) return;
        directionalLightColors[dirLightIndex] = light.finalColor;
        directionalLightDirections[dirLightIndex] = -light.localToWorldMatrix.GetColumn(2).normalized;
        dirLightIndex++;
    }
    #endregion

    private static ShaderTagId UnlitLightMode = new ShaderTagId("SRPDefaultUnlit");
    private static FilteringSettings EnvironmentOpaqueFilterSettings = new()
    {
        layerMask = -1,
        renderQueueRange = RenderQueueRange.opaque,
        sortingLayerRange = SortingLayerRange.all,
        renderingLayerMask = 1,
    };
    private static FilteringSettings EnvironmentTransparentFilterSettings = new()
    {
        layerMask = -1,
        renderQueueRange = RenderQueueRange.transparent,
        sortingLayerRange = SortingLayerRange.all,
        renderingLayerMask = 1,
    };

    private static FilteringSettings CharacterOpaqueFilterSettings = new()
    {
        layerMask = -1,
        renderQueueRange = RenderQueueRange.opaque,
        sortingLayerRange = SortingLayerRange.all,
        renderingLayerMask = 1 << 1,
    };
    private static FilteringSettings CharacterTransparentFilterSettings = new()
    {
        layerMask = -1,
        renderQueueRange = RenderQueueRange.transparent,
        sortingLayerRange = SortingLayerRange.all,
        renderingLayerMask = 1 << 1,
    };

    void DrawEnvironment(CullingResults cullingResults)
    {
        DrawingSettings OpaqueDrawSettings = new(
            UnlitLightMode,
            new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque }
        );
        DrawingSettings TransparentDrawSettings = new(
            UnlitLightMode,
            new SortingSettings(camera) { criteria = SortingCriteria.CommonTransparent }
        );
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
        context.DrawRenderers(cullingResults, ref OpaqueDrawSettings, ref EnvironmentOpaqueFilterSettings);
        context.DrawSkybox(camera);
        context.DrawRenderers(cullingResults, ref TransparentDrawSettings, ref EnvironmentTransparentFilterSettings);
    }

    void DrawCharacters(CullingResults cullingResults)
    {
        DrawingSettings OpaqueDrawSettings = new(
            UnlitLightMode,
            new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque }
        )
        {
            enableDynamicBatching = false
        };
        DrawingSettings TransparentDrawSettings = new(
            UnlitLightMode,
            new SortingSettings(camera) { criteria = SortingCriteria.CommonTransparent }
        )
        {
            enableDynamicBatching = false
        };
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();

        context.DrawRenderers(cullingResults, ref OpaqueDrawSettings, ref CharacterOpaqueFilterSettings);
        context.DrawRenderers(cullingResults, ref TransparentDrawSettings, ref CharacterTransparentFilterSettings);
    }

    partial void DrawSceneGizmos();
    partial void EmitSceneViewGeometry();
}
