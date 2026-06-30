using UnityEngine;

[ExecuteAlways] 
public class PuzzleGroupController : MonoBehaviour
{
    [Header("Cấu hình Material cho nhóm này")]
    [Tooltip("Kéo file Material riêng của đồ vật này vào đây")]
    public Material groupMaterial; 

    void Start()
    {
        UpdateShaderPosition();
    }

    void Update()
    {
        UpdateShaderPosition();
    }

    private void UpdateShaderPosition()
    {
        if (groupMaterial != null)
        {
            groupMaterial.SetVector("_ParentPos", transform.position);
        }
    }
}