using UnityEngine;
using System.Collections.Generic;

public class ImageSliceManager : MonoBehaviour
{
    [System.Serializable]
    public struct ColorGroupTexture
    {
        public string colorGroup; // "green", "blue", "red", "purple"
        public Texture2D fullTexture; // Bức ảnh đầy đủ (Ví dụ: ảnh cây đàn Guitar)
    }

    [Header("Texture Mapping")]
    public List<ColorGroupTexture> textureConfig = new List<ColorGroupTexture>();

    [Header("References")]
    public Transform blocksRoot; // Kéo GameObject "Blocks" chứa các block vào đây

    private class GroupBounds
    {
        public int minCol = int.MaxValue;
        public int minRow = int.MaxValue;
        public int maxCol = int.MinValue;
        public int maxRow = int.MinValue;
    }

    private void Start()
    {
        ApplyImageSlicing();
    }

    [ContextMenu("Execute Slice In Editor")] // Thêm nút bấm chạy thử ngay trong Editor
    public void ApplyImageSlicing()
    {
        if (blocksRoot == null) return;

        // 1. Thu thập tất cả các DraggableBlock đang có trên map
        DraggableBlock[] blocks = blocksRoot.GetComponentsInChildren<DraggableBlock>();
        
        // 2. Tính toán ranh giới (Bounds) của từng nhóm màu khi xếp hoàn chỉnh
        Dictionary<string, GroupBounds> groupBoundsMap = new Dictionary<string, GroupBounds>();

        foreach (var block in blocks)
        {
            if (block == null || string.IsNullOrEmpty(block.colorGroup)) continue;

            if (!groupBoundsMap.ContainsKey(block.colorGroup))
                groupBoundsMap[block.colorGroup] = new GroupBounds();

            GroupBounds bounds = groupBoundsMap[block.colorGroup];

            // Quét qua cấu trúc ô shape để tìm biên lớn nhất ở vị trí TARGET
            foreach (Vector2Int cell in block.shape)
            {
                int tCol = block.targetCol + cell.x; //
                int tRow = block.targetRow + cell.y; //

                bounds.minCol = Mathf.Min(bounds.minCol, tCol);
                bounds.minRow = Mathf.Min(bounds.minRow, tRow);
                bounds.maxCol = Mathf.Max(bounds.maxCol, tCol);
                bounds.maxRow = Mathf.Max(bounds.maxRow, tRow);
            }
        }

        // 3. Tiến hành tính toán Tiling & Offset và áp dụng vào Material mặt trên của từng Block
        foreach (var block in blocks)
        {
            if (block == null || string.IsNullOrEmpty(block.colorGroup)) continue;

            // Tìm texture tương ứng với nhóm màu này
            Texture2D groupTex = GetTextureForGroup(block.colorGroup);
            if (groupTex == null) continue;

            BlockVisuals visuals = block.GetComponent<BlockVisuals>();
            if (visuals == null || visuals.topRenderer == null) continue;

            GroupBounds bounds = groupBoundsMap[block.colorGroup];

            // Kích thước ma trận ảnh tổng thể (Tính bằng số ô grid)
            float totalCols = bounds.maxCol - bounds.minCol + 1;
            float totalRows = bounds.maxRow - bounds.minRow + 1;

            // Kích thước của riêng block này (Tính bằng số ô grid)
            int blockCols = 0;
            int blockRows = 0;
            foreach (Vector2Int cell in block.shape)
            {
                if (cell.x + 1 > blockCols) blockCols = cell.x + 1;
                if (cell.y + 1 > blockRows) blockRows = cell.y + 1;
            }

            // TÍNH TOÁN TILING (Tỷ lệ co giãn của mảnh ảnh)
            float tilingX = blockCols / totalCols;
            float tilingY = blockRows / totalRows;

            // TÍNH TOÁN OFFSET (Vị trí cắt dịch tâm của mảnh ảnh)
            float offsetX = (block.targetCol - bounds.minCol) / totalCols;
            
            // Do UV của Unity tính từ dưới lên, ta đảo ngược trục hàng (Row) để ảnh không bị lộn ngược
            float offsetY = (bounds.maxRow - (block.targetRow + blockRows - 1)) / totalRows;

            Material targetMat = visuals.topRenderer.sharedMaterial;

            if (!Application.isPlaying)
            {
                // Nếu ở Edit Mode và vật liệu chưa từng được nhân bản, ta nhân bản tường minh 1 lần duy nhất
                if (targetMat != null && !targetMat.name.Contains("(Instance)"))
                {
                    targetMat = new Material(targetMat);
                    targetMat.name += " (Instance)";
                    visuals.topRenderer.sharedMaterial = targetMat;
                }
            }
            else
            {
                // Nếu đang chơi game (Play Mode), sử dụng .material bình thường
                targetMat = visuals.topRenderer.material;
            }

            // Tiến hành gán hình ảnh, tỉ lệ co giãn (Tiling) và vị trí cắt (Offset)
            if (targetMat != null)
            {
                // Cấu hình chuẩn cho Shader Standard cũ
                targetMat.mainTexture = groupTex;
                targetMat.mainTextureScale = new Vector2(tilingX, tilingY);
                targetMat.mainTextureOffset = new Vector2(offsetX, offsetY);

                // Cấu hình chuẩn nâng cao cho Shader URP mới của Unity 6 (Tránh lỗi không nhận thuộc tính ảnh)
                if (targetMat.HasProperty("_BaseMap"))
                {
                    targetMat.SetTexture("_BaseMap", groupTex);
                    targetMat.SetTextureScale("_BaseMap", new Vector2(tilingX, tilingY));
                    targetMat.SetTextureOffset("_BaseMap", new Vector2(offsetX, offsetY));
                }
            }
        }
    }

    private Texture2D GetTextureForGroup(string colorGroup)
    {
        foreach (var config in textureConfig)
        {
            if (config.colorGroup == colorGroup) return config.fullTexture;
        }
        return null;
    }
}