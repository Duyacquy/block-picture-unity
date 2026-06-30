using UnityEngine;
using System.Collections.Generic;

public class PuzzleDragController : MonoBehaviour
{
    [Header("References")]
    public Camera mainCamera;
    public BoardGridManager gridManager;
    public Transform blocksRoot;

    [Header("Drag Settings")]
    public float dragLiftY = 0.25f;          // Độ cao nhấc khối lên khi kéo
    public float pickupDuration = 0.1f;      // Thời gian phóng to nhẹ khi nhấc lên
    public float snapDuration = 0.12f;       // Thời gian hút vào ô lưới khi thả tay
    public float dragFollowSharpness = 20f;  // Độ mượt/độ bám của khối theo chuột
    public float holdScaleMultiplier = 1.05f;// Tỷ lệ phóng to khi giữ khối

    // Lưu trữ trạng thái chiếm chỗ của các ô trên Grid
    // Key: "row_col", Value: Block đang chiếm ô đó
    private Dictionary<string, DraggableBlock> occupiedCells = new Dictionary<string, DraggableBlock>();

    private DraggableBlock draggingBlock = null;
    private Vector3 desiredDragPosition;
    private Vector3 dragPointerOffset;
    private Vector3 originalScale;
    
    private int draggingStartCol;
    private int draggingStartRow;
    private bool isTweening = false;
    private float tweenTimer = 0f;
    private float currentTweenDuration = 0f;
    private Vector3 tweenStartPos;
    private Vector3 tweenEndPos;
    private Vector3 tweenStartScale;
    private Vector3 tweenEndScale;

    private void Start()
    {
        if (gridManager == null) gridManager = GetComponent<BoardGridManager>();
        RebuildOccupied();
    }

    private void Update()
    {
        HandleInput();
        UpdateDragFollow();
        HandleTweens();
    }

    private void HandleInput()
    {
        if (isTweening) return; // Khối đang tự hút vào lưới thì khóa điều khiển tạm thời

        // 1. Nhấn chuột / Chạm màn hình (Touch Start)
        if (Input.GetMouseButtonDown(0))
        {
            BeginDrag(Input.mousePosition);
        }
        // 2. Đang giữ và kéo (Touch Move)
        else if (Input.GetMouseButton(0) && draggingBlock != null)
        {
            MoveDrag(Input.mousePosition);
        }
        // 3. Thả tay (Touch End)
        else if (Input.GetMouseButtonUp(0) && draggingBlock != null)
        {
            EndDrag();
        }
    }

    private void BeginDrag(Vector2 screenPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        RaycastHit hit;

        // Bắn tia ray kiểm tra xem có trúng Collider của Block nào không
        if (Physics.Raycast(ray, out hit))
        {
            DraggableBlock block = hit.collider.GetComponentInParent<DraggableBlock>();
            if (block != null)
            {
                draggingBlock = block;
                draggingStartCol = block.col;
                draggingStartRow = block.row;
                originalScale = block.transform.localScale;

                // Tạm thời giải phóng các ô mà khối này đang chiếm giữ để tính toán di chuyển
                RemoveBlockFromOccupied(block);

                // Tính toán điểm lệch giữa vị trí chuột và tâm của khối
                Vector3 hitWorldPos = GetMouseWorldPosOnGridPlane(screenPos);
                dragPointerOffset = hit.collider.transform.position - hitWorldPos;
                dragPointerOffset.y = 0; // Không tính toán lệch độ cao

                // Thiết lập vị trí đích mong muốn (nhấc cao lên theo trục Y)
                Vector3 targetPos = hit.collider.transform.position;
                targetPos.y = gridManager.gridY + dragLiftY;
                desiredDragPosition = targetPos;

                // Chạy hiệu ứng nhấc khối lên (Phóng to nhẹ)
                StartTween(block.transform.position, targetPos, originalScale, originalScale * holdScaleMultiplier, pickupDuration);
            }
        }
    }

    private void MoveDrag(Vector2 screenPos)
    {
        Vector3 mouseWorldPos = GetMouseWorldPosOnGridPlane(screenPos);
        Vector3 nextPos = mouseWorldPos + dragPointerOffset;
        nextPos.y = gridManager.gridY + dragLiftY; // Luôn giữ độ cao khi kéo

        desiredDragPosition = nextPos;
    }

    private void EndDrag()
    {
        // Xác định ô Grid gần nhất dựa trên vị trí hiện tại của khối
        Vector3 checkPos = draggingBlock.transform.position;
        Vector2Int targetGrid = gridManager.WorldToGrid(checkPos); //

        int finalCol = targetGrid.y;
        int finalRow = targetGrid.x;

        // Kiểm tra xem vị trí mới có hợp lệ (trong biên và không đè lên khối khác) không
        if (CanPlaceBlock(draggingBlock, finalCol, finalRow))
        {
            draggingBlock.SetGridPosition(finalCol, finalRow); //
        }
        else
        {
            // Nếu không hợp lệ, trả về vị trí ban đầu
            finalCol = draggingStartCol;
            finalRow = draggingStartRow;
            draggingBlock.SetGridPosition(finalCol, finalRow);
        }

        // Đăng ký lại các ô chiếm chỗ mới của khối vào dữ liệu hệ thống
        AddBlockToOccupied(draggingBlock);

        // Tính tọa độ World chuẩn của ô đó để hút khối vào chính tâm ô
        Vector3 snapWorldPos = gridManager.GridToWorld(finalRow, finalCol);
        
        // Chạy hiệu ứng hạ khối và co scale về bình thường
        StartTween(draggingBlock.transform.position, snapWorldPos, draggingBlock.transform.localScale, originalScale, snapDuration, () => {
            draggingBlock = null;
        });
    }

    private void UpdateDragFollow()
    {
        if (draggingBlock == null || isTweening) return;

        float t = 1f - Mathf.Exp(-dragFollowSharpness * Time.deltaTime);
        
        draggingBlock.transform.position = Vector3.Lerp(draggingBlock.transform.position, desiredDragPosition, t);
    }

    // Lấy tọa độ chuột trên một mặt phẳng ảo nằm ngang tại độ cao đặt khối
    private Vector3 GetMouseWorldPosOnGridPlane(Vector2 screenPos)
    {
        Plane plane = new Plane(Vector3.up, new Vector3(0, gridManager.gridY, 0));
        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        if (plane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }
        return Vector3.zero;
    }

    public void RebuildOccupied()
    {
        occupiedCells.Clear(); 
        if (blocksRoot == null) return;
        
        DraggableBlock[] blocks = blocksRoot.GetComponentsInChildren<DraggableBlock>();
        foreach (DraggableBlock block in blocks)
        {
            if (block != null && block.enabled)
            {
                AddBlockToOccupied(block);
            }
        }
    }

    private void AddBlockToOccupied(DraggableBlock block)
    {
        foreach (Vector2Int cell in block.shape) //
        {
            string key = $"{block.row + cell.y}_{block.col + cell.x}"; //
            occupiedCells[key] = block;
        }
    }

    private void RemoveBlockFromOccupied(DraggableBlock block)
    {
        foreach (Vector2Int cell in block.shape) //
        {
            string key = $"{block.row + cell.y}_{block.col + cell.x}"; //
            occupiedCells.Remove(key);
        }
    }

    // Thuật toán kiểm tra không cho các khối đè lên nhau
    private bool CanPlaceBlock(DraggableBlock block, int col, int row)
    {
        foreach (Vector2Int cell in block.shape) //
        {
            int targetC = col + cell.x;
            int targetR = row + cell.y;

            // Kiểm tra xem có vượt ra ngoài biên của mảng ẩn không
            if (targetC < 0 || targetC >= gridManager.cols || targetR < 0 || targetR >= gridManager.rows)
            {
                return false; //
            }

            // Kiểm tra xem ô đó đã có khối nào khác chiếm chưa
            string key = $"{targetR}_{targetC}";
            if (occupiedCells.ContainsKey(key) && occupiedCells[key] != block)
            {
                return false; //
            }
        }
        return true; // Vị trí hoàn toàn trống
    }

    // Tự viết bộ Tween cơ bản để không bắt buộc bạn phải cài DOTween ngay lập tức
    private System.Action onTweenCompleteCallback;
    private void StartTween(Vector3 startP, Vector3 endP, Vector3 startS, Vector3 endS, float duration, System.Action onComplete = null)
    {
        tweenStartPos = startP;
        tweenEndPos = endP;
        tweenStartScale = startS;
        tweenEndScale = endS;
        currentTweenDuration = duration;
        tweenTimer = 0f;
        onTweenCompleteCallback = onComplete;
        isTweening = true;
    }

    private void HandleTweens()
    {
        if (!isTweening || draggingBlock == null) return;

        tweenTimer += Time.deltaTime;
        float percent = Mathf.Clamp01(tweenTimer / currentTweenDuration);
        
        // Tạo hiệu ứng mượt mờ (Smooth Step)
        float t = percent * percent * (3f - 2f * percent);

        draggingBlock.transform.position = Vector3.Lerp(tweenStartPos, tweenEndPos, t);
        draggingBlock.transform.localScale = Vector3.Lerp(tweenStartScale, tweenEndScale, t);

        if (percent >= 1f)
        {
            isTweening = false;
            onTweenCompleteCallback?.Invoke();
        }
    }
}