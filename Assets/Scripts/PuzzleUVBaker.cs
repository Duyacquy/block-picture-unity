using UnityEngine;
using System.Collections.Generic;

public class PuzzleUVBaker : MonoBehaviour
{
    [Tooltip("Cụm từ nằm trong tên của Mesh mặt trên để nhận diện (Ví dụ: 'top', 'mat_tren')")]
    public string topFaceMeshNameContains = "top";

    void Awake()
    {
        BakeGroupUVs();
    }

    private void BakeGroupUVs()
    {
        // 1. Tìm tất cả các MeshFilter của các mặt trên nằm trong nhóm này
        MeshFilter[] allMeshFilters = GetComponentsInChildren<MeshFilter>();
        List<MeshFilter> topMeshes = new List<MeshFilter>();

        foreach (var mf in allMeshFilters)
        {
            // Kiểm tra xem tên vật thể con có chứa ký tự nhận diện mặt trên không
            if (mf.gameObject.name.ToLower().Contains(topFaceMeshNameContains.ToLower()))
            {
                topMeshes.Add(mf);
            }
        }

        if (topMeshes.Count == 0)
        {
            Debug.LogWarning($"Không tìm thấy mặt trên nào có tên chứa '{topFaceMeshNameContains}' trong nhóm {gameObject.name}");
            return;
        }

        // 2. Tính toán khung giới hạn (Bounding Box) của toàn bộ nhóm ở trạng thái hoàn chỉnh hiện tại
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        foreach (var mf in topMeshes)
        {
            Vector3[] vertices = mf.sharedMesh.vertices;
            foreach (var v in vertices)
            {
                // Chuyển tọa độ đỉnh từ Local sang World Space
                Vector3 worldPos = mf.transform.TransformPoint(v);
                if (worldPos.x < minX) minX = worldPos.x;
                if (worldPos.x > maxX) maxX = worldPos.x;
                if (worldPos.y < minY) minY = worldPos.y;
                if (worldPos.y > maxY) maxY = worldPos.y;
            }
        }

        float width = maxX - minX;
        float height = maxY - minY;

        if (width <= 0 || height <= 0) return;

        // 3. Tiến hành gán lại tọa độ UV mới dựa trên vị trí tương đối trong khung hình
        foreach (var mf in topMeshes)
        {
            // Tạo bản sao mesh độc lập khi chạy game để tránh ghi đè làm hỏng file FBX gốc
            Mesh uniqueMesh = Instantiate(mf.sharedMesh);
            Vector3[] vertices = uniqueMesh.vertices;
            Vector2[] uvs = new Vector2[vertices.Length];

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 worldPos = mf.transform.TransformPoint(vertices[i]);

                // Quy đổi vị trí World sang tỉ lệ từ 0 đến 1 tương ứng với Texture
                // Đoạn này đang giả định game của bạn hiển thị trên mặt phẳng đứng XY (2D/Canvas)
                // Nếu game nằm trên mặt phẳng ngang XZ (nhìn từ trên xuống), hãy đổi worldPos.y thành worldPos.z
                float u = (worldPos.x - minX) / width;
                float v = (worldPos.y - minY) / height;

                uvs[i] = new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));
            }

            uniqueMesh.uv = uvs;
            mf.mesh = uniqueMesh; // Cập nhật mesh mới đã được tính lại UV cho khối
        }

        Debug.Log($"Đã tự động dán ảnh chuẩn cho nhóm: {gameObject.name}");
    }
}