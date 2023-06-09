using UnityEngine;
using UnityEngine.Rendering;

public class PixelRenderPipeline : RenderPipeline
{ 

    private PixelRenderPipelineAsset.IReadOnlyResolutionSettings resolutionSettings;

    public PixelRenderPipeline(PixelRenderPipelineAsset.IReadOnlyResolutionSettings resolutionSettings)
    {
        this.resolutionSettings = resolutionSettings;
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        GraphicsSettings.useScriptableRenderPipelineBatching = true;
        foreach (var camera in cameras)
            RenderCamera(context, camera);
    }

    private void RenderCamera(ScriptableRenderContext context, Camera camera)
    {
        var renderer = new PixelRenderPipelineCameraRenderer(context, camera, resolutionSettings);
        renderer.Render();        
    }    
}