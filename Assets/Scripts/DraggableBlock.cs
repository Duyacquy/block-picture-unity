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
    // Mặc định là khối 1x1 chiếm ô gốc (0,0)
    public List<Vector2Int> shape = new List<Vector2Int> { new Vector2Int(0, 0) };

    public void SetGridPosition(int newCol, int newRow)
    {
        col = newCol;
        row = newRow;
    }

    /// <summary>
    /// Kiểm tra xem khối này đã nằm đúng vị trí mục tiêu để lắp tranh chưa
    /// </summary>
    public bool IsAtTarget()
    {
        return col == targetCol && row == targetRow; //
    }

    // ==========================================
    // ĐOẠN CODE BỔ SUNG: VẼ LƯỚI XEM TRƯỚC CHO KHỐI
    // ==========================================
    private void OnDrawGizmosSelected()
    {
        // SỬA TẠI ĐÂY: Thay FindFirstObjectByType bằng FindAnyObjectByType để hết warning
        BoardGridManager grid = FindAnyObjectByType<BoardGridManager>();
        float step = grid != null ? grid.cellStep : 0.75f;

        foreach (Vector2Int cell in shape)
        {
            // Tính toán vị trí ô tương đối dựa trên trục X (ngang) và Z (dọc)
            Vector3 cellLocalPos = new Vector3(cell.x * step, 0f, -cell.y * step);
            Vector3 cellWorldPos = transform.TransformPoint(cellLocalPos);
            
            // Vẽ các ô vuông màu vàng bao quanh chân khối để định hình footprint
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(cellWorldPos, new Vector3(step * 0.95f, 0.1f, step * 0.95f));
            
            // Nếu là ô gốc (0, 0), vẽ thêm một khối lập phương nhỏ màu đỏ để đánh dấu
            if (cell.x == 0 && cell.y == 0)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawCube(cellWorldPos, new Vector3(step * 0.25f, 0.15f, step * 0.25f));
            }
        }
    }
}