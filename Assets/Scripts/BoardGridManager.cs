using UnityEngine;

public class BoardGridManager : MonoBehaviour
{
    [Header("Grid Dimensions")]
    public int cols = 5; 
    public int rows = 7; 

    [Header("Cell Settings")]
    public float cellStep = 0.75f; // Khoảng cách giữa tâm ô này đến tâm ô kia
    public float gridY = 0.08f;    // Độ cao mặt phẳng đặt khối đố

    [Header("Grid Center")]
    public float centerX = 0f;
    public float centerZ = 0f;

    public Vector3 GridToWorld(int row, int col)
    {
        float boardWidth = cols * cellStep;
        float boardHeight = rows * cellStep;
        float firstCellX = centerX - (boardWidth * 0.5f) + (cellStep * 0.5f);
        float firstCellZ = centerZ + (boardHeight * 0.5f) - (cellStep * 0.5f);

        float x = firstCellX + col * cellStep;
        float z = firstCellZ - row * cellStep;

        return new Vector3(x, gridY, z);
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        float boardWidth = cols * cellStep;
        float boardHeight = rows * cellStep;
        float firstCellX = centerX - (boardWidth * 0.5f) + (cellStep * 0.5f);
        float firstCellZ = centerZ + (boardHeight * 0.5f) - (cellStep * 0.5f);

        int col = Mathf.RoundToInt((worldPos.x - firstCellX) / cellStep);
        int row = Mathf.RoundToInt((firstCellZ - worldPos.z) / cellStep);

        return new Vector2Int(Mathf.Clamp(row, 0, rows - 1), Mathf.Clamp(col, 0, cols - 1));
    }

    // Vẽ lưới trong Editor để bạn nhìn vào đó mà xếp các ô FBX cho thẳng hàng
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                Vector3 cellPos = GridToWorld(r, c);
                Gizmos.DrawWireCube(cellPos, new Vector3(cellStep * 0.95f, 0.02f, cellStep * 0.95f));
            }
        }
    }
}