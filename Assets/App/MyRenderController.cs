using System;
using UnityEngine;

[ExecuteAlways]
class MyRenderController : MonoBehaviour
{
    readonly string[] Selections = Enum.GetNames(typeof(TargetsAndOutputsCountMode));
    bool m_EnableRotation = false;

    protected virtual void OnGUI()
    {
        var matrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(Vector3.one * Screen.safeArea.height / 360f);
        using(new GUILayout.AreaScope(Screen.safeArea))
        using(new GUILayout.VerticalScope(GUI.skin.box, GUILayout.Width(175)))
        {
            if (QualitySettings.renderPipeline is MyRenderPipelineAsset asset)
            {
                GUILayout.Label(asset.TargetsAndOutputsCount.ToString());
                asset.TargetsAndOutputsCount = (TargetsAndOutputsCountMode)GUILayout.SelectionGrid((int)asset.TargetsAndOutputsCount, Selections, 1);
                m_EnableRotation = GUILayout.Toggle(m_EnableRotation, "Rotation");
            }
            else
            {
                GUILayout.Label("require MyRenderPipelineAsset");
            }
        }
        GUI.matrix = matrix;
    }
    
    protected virtual void Update()
    {
        if (m_EnableRotation && QualitySettings.renderPipeline is MyRenderPipelineAsset asset)
        {
            asset.TargetsAndOutputsCount = (TargetsAndOutputsCountMode)(((int)asset.TargetsAndOutputsCount + 1) % Selections.Length);
        }
    }

}