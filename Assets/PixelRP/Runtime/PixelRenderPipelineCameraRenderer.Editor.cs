using UnityEngine;
using UnityEngine.Rendering;

public partial class PixelRenderPipelineCameraRenderer
{
#if UNITY_EDITOR
    partial void EmitSceneViewGeometry()
    {
        if (camera.cameraType != CameraType.SceneView) return;
        ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
    }

    partial void DrawSceneGizmos()
    {
        if (!UnityEditor.Handles.ShouldRenderGizmos()) return;
        context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
        context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
    }
#endif
}
