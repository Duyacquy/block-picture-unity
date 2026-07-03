using UnityEngine;
using System.Collections.Generic;

public class DraggableBlock : MonoBehaviour
{
    [Header("Block Identity")]
    public string blockId;
    public string colorGroup; // "green", "blue", "red", "purple"

    [Header("Current Grid Coordinates")]
    public int col;
    public int row;

    [Header("Target Coordinates (For Puzzle Assembly)")]
    public int targetCol;
    public int targetRow;

    [Header("Block Shape / Footprint")]
    [Tooltip("Danh sách các ô chiếm chỗ tương đối, ô gốc luôn là (0,0)")]
    public List<Vector2Int> shape = new List<Vector2Int> { new Vector2Int(0, 0) };

    private Renderer[] childRenderers;
    private MaterialPropertyBlock propBlock;

    [Header("Runtime Outline Reference (Pure Code)")]
    private GameObject outlineContainer = null;
    public Material outlineMaterial;

    [Tooltip("Chỉnh về 1.0f để khối viền bằng khít khối gốc, không phình sang ngang gây lấn block khác")]
    public float outlineScaleMultiplier = 1.0f; 
    
    [Tooltip("Độ cao nhấc hẳn viền lên trên đầu khối (Ví dụ: 0.1 đến 0.3 tùy độ cao khối của bạn)")]
    private float outlineYOffset = 0.3f;

    private void Start()
    {
        // TỰ ĐỘNG BỔ SUNG: Sinh Collider cho toàn bộ các ô dựa theo shape khi vào game
        RebuildCellColliders();

        InitOutlineSystem();
    }

    private void InitOutlineSystem()
    {
        // Lấy toàn bộ các Renderer con (bao gồm cả mặt dán ảnh lẫn đế nhựa)
        childRenderers = GetComponentsInChildren<Renderer>();
        propBlock = new MaterialPropertyBlock();

        // Mặc định lúc vào game, ẩn hoàn toàn đường viền đi
        ToggleOutline(false);
    }

    /// <summary>
    /// Hàm Public để điều khiển bật/tắt đường viền trắng
    /// </summary>
    // public void ToggleOutline(bool enable)
    // {
    //     if (childRenderers == null) return;

    //     foreach (Renderer ren in childRenderers)
    //     {
    //         if (ren == null) continue;

    //         // Đọc PropertyBlock hiện tại của Mesh, chỉnh sửa thông số width rồi nạp ngược lại
    //         ren.GetPropertyBlock(propBlock);

    //         // SỬA TẠI ĐÂY: Thay vì dùng số cố định, ta dùng biến chosenOutlineWidth
    //         propBlock.SetFloat("_OutlineWidth", enable ? chosenOutlineWidth : 0f);

    //         ren.SetPropertyBlock(propBlock);
    //     }
    // }

    /// <summary>
    /// Bật/Tắt đường viền trắng bao quanh khối khi được kéo nhấc lên giống mẫu Cocos
    /// </summary>
    public void ToggleOutline(bool active)
    {
        // 1. Nếu tắt viền hoặc đã có viền cũ từ lượt kéo trước, tiến hành xóa bỏ để tránh trùng lặp
        if (outlineContainer != null)
        {
            Destroy(outlineContainer);
            outlineContainer = null;
        }

        if (!active) return;

        // 2. KHỞI TẠO CONTAINER CHA CHO HỆ THỐNG VIỀN
        outlineContainer = new GameObject("__DragOutlineContainer");
        outlineContainer.transform.SetParent(this.transform, false);
        outlineContainer.transform.localPosition = Vector3.zero;
        outlineContainer.transform.localRotation = Quaternion.identity;
        outlineContainer.transform.localScale = Vector3.one;

        // Thu thập tất cả MeshFilter của khối (Đế nhựa + Mặt tranh) để nhân bản chính xác phom dáng khối
        MeshFilter[] childMeshFilters = GetComponentsInChildren<MeshFilter>();

        if (outlineMaterial == null) {
            Debug.LogError("Chưa kéo file Mat_Outline vào DraggableBlock của ô này!");
            return;
        }

        foreach (MeshFilter filter in childMeshFilters)
        {
            if (filter.gameObject == outlineContainer || filter.transform.IsChildOf(outlineContainer.transform)) 
                continue;

            GameObject outlineMeshObj = new GameObject("__DragOutlineMesh");
            outlineMeshObj.transform.SetParent(outlineContainer.transform, false);
            
            outlineMeshObj.transform.localPosition = filter.transform.localPosition;
            outlineMeshObj.transform.localRotation = filter.transform.localRotation;
            
            // 1. ÁP DỤNG SCALE MỚI: Bằng khít mốc gốc (1.0f) không phình ngang
            outlineMeshObj.transform.localScale = Vector3.Scale(
                filter.transform.localScale, 
                new Vector3(outlineScaleMultiplier, outlineScaleMultiplier, outlineScaleMultiplier)
            );

            outlineMeshObj.transform.localPosition += new Vector3(0f, outlineYOffset, 0f);

            MeshFilter mf = outlineMeshObj.AddComponent<MeshFilter>();
            mf.sharedMesh = filter.sharedMesh;

            MeshRenderer mr = outlineMeshObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = outlineMaterial;
            
            mr.receiveShadows = false;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    public void RebuildCellColliders()
    {
        // 1. Dọn dẹp các Collider tự động cũ nếu có để tránh bị trùng lặp
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("__CellCollider_"))
            {
                Destroy(child.gameObject);
            }
        }

        // Lấy thông số khoảng cách ô từ GridManager
        BoardGridManager grid = FindAnyObjectByType<BoardGridManager>();
        float step = grid != null ? grid.cellStep : 0.75f;

        // 2. Vô hiệu hóa BoxCollider ở ngay node cha (nếu có) để không xung đột vị trí
        BoxCollider rootCollider = GetComponent<BoxCollider>();
        if (rootCollider != null) rootCollider.enabled = false;

        // 3. Quét qua mảng shape để sinh tự động BoxCollider cho từng ô con
        for (int i = 0; i < shape.Count; i++)
        {
            Vector2Int cell = shape[i];

            GameObject colliderObj = new GameObject($"__CellCollider_{i}");
            colliderObj.transform.SetParent(transform);

            // Tính toán vị trí tương đối chuẩn dựa theo trục X (ngang) và Z (dọc ngược từ trên xuống)
            colliderObj.transform.localPosition = new Vector3(cell.x * step, 0f, -cell.y * step);
            colliderObj.transform.localRotation = Quaternion.identity;
            colliderObj.transform.localScale = Vector3.one;

            // Thêm thành phần BoxCollider vào đối tượng ô con này
            BoxCollider box = colliderObj.AddComponent<BoxCollider>();

            // Cấu hình kích thước Collider cho khít với ô lưới cờ
            box.center = new Vector3(0f, 0.1f, 0f); // Độ cao vùng chạm (bạn có thể tăng giảm tùy độ dày model)
            box.size = new Vector3(step * 0.95f, 0.2f, step * 0.95f);
        }
    }

    public void SetGridPosition(int newCol, int newRow)
    {
        col = newCol;
        row = newRow;
    }

    public bool IsAtTarget()
    {
        return col == targetCol && row == targetRow;
    }

    private void OnDrawGizmosSelected()
    {
        BoardGridManager grid = FindAnyObjectByType<BoardGridManager>();
        float step = grid != null ? grid.cellStep : 0.75f;

        foreach (Vector2Int cell in shape)
        {
            Vector3 cellLocalPos = new Vector3(cell.x * step, 0f, -cell.y * step);
            Vector3 cellWorldPos = transform.TransformPoint(cellLocalPos);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(cellWorldPos, new Vector3(step * 0.95f, 0.1f, step * 0.95f));

            if (cell.x == 0 && cell.y == 0)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawCube(cellWorldPos, new Vector3(step * 0.25f, 0.15f, step * 0.25f));
            }
        }
    }
    
    private void OnDestroy()
    {
        // Giải phóng bộ nhớ vật liệu runtime khi khối bị phá hủy
        if (outlineContainer != null)
        {
            Destroy(outlineContainer);
        }
    }
}