using UnityEngine;

public class BlockVisuals : MonoBehaviour
{
    [Header("Mesh Renderers")]
    public MeshRenderer baseRenderer;   // Kéo Mesh phần đế vào đây
    public MeshRenderer topRenderer;    // Kéo Mesh phần mặt phẳng dán ảnh vào đây

    [Header("Visual Settings")]
    public Color blockColor = Color.white; 

    private void OnValidate()
    {
        if (baseRenderer != null)
        {
            baseRenderer.sharedMaterial.color = blockColor;
        }
    }
}