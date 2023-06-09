
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/PixelRPAsset")]
public class PixelRenderPipelineAsset : RenderPipelineAsset
{
    public ResolutionSettings resolutionSettings = new(new(320, 180), .5f);

    [System.Serializable]
    public struct ResolutionSettings : IReadOnlyResolutionSettings
    {
        public Vector2Int TargetResolution;
        Vector2Int IReadOnlyResolutionSettings.TargetResolution => this.TargetResolution;
        [Range(0, 1)] public float MatchWidth;
        float IReadOnlyResolutionSettings.MatchWidth => this.MatchWidth;

        public ResolutionSettings(Vector2Int TargetResolution, float MatchWidth = 0.5f)
        {
            this.TargetResolution = TargetResolution;
            this.MatchWidth = MatchWidth;
        }
    }

    public interface IReadOnlyResolutionSettings
    {
        Vector2Int TargetResolution { get; }
        float MatchWidth { get; }
        public Vector2Int CalculateResolution(Vector2 ScreenBufferSize)
        {
            var widthDriven = new Vector2(TargetResolution.x, TargetResolution.x * ScreenBufferSize.y / (float)ScreenBufferSize.x);
            var heightDriven = new Vector2(TargetResolution.y * ScreenBufferSize.x / (float)ScreenBufferSize.y, TargetResolution.y);
            return Vector2Int.RoundToInt(Vector2.Lerp(widthDriven, heightDriven, 1 - MatchWidth));
        }
    }

    protected override RenderPipeline CreatePipeline()
        => new PixelRenderPipeline(resolutionSettings);

}
