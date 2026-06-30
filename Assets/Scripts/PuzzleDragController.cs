using UnityEngine;
using UnityEngine.InputSystem;
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
        Vector2 pointerPos = Vector2.zero;
        bool pressed = false;
        bool held = false;
        bool released = false;

        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;

            pressed = touch.press.wasPressedThisFrame;
            held = touch.press.isPressed;
            released = touch.press.wasReleasedThisFrame;

            if (pressed || held || released)
            {
                pointerPos = touch.position.ReadValue();
            }
        }

        if (!pressed && !held && !released && Mouse.current != null)
        {
            pointerPos = Mouse.current.position.ReadValue();
            pressed = Mouse.current.leftButton.wasPressedThisFrame;
            held = Mouse.current.leftButton.isPressed;
            released = Mouse.current.leftButton.wasReleasedThisFrame;
        }

        if (released && draggingBlock != null)
        {
            EndDrag();
        }
        else if (pressed)
        {
            if (isTweening) return;
            BeginDrag(pointerPos);
        }
        else if (held && draggingBlock != null)
        {
            MoveDrag(pointerPos);
        }
    }

    private void BeginDrag(Vector2 screenPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            DraggableBlock block = hit.collider.GetComponentInParent<DraggableBlock>();
            if (block != null)
            {
                draggingBlock = block;
                draggingStartCol = block.col;
                draggingStartRow = block.row;
                
                // Lưu lại scale gốc chuẩn nếu khối không ở trong trạng thái tween dở dang
                if (!isTweening)
                {
                    originalScale = block.transform.localScale;
                }

                RemoveBlockFromOccupied(block);

                Vector3 hitWorldPos = GetMouseWorldPosOnGridPlane(screenPos);
                
                // SỬA TẠI ĐÂY: Dùng block.transform.position (Khối cha) thay vì hit.collider để tránh lệch trục
                dragPointerOffset = block.transform.position - hitWorldPos;
                dragPointerOffset.y = 0; 

                Vector3 targetPos = block.transform.position;
                targetPos.y = gridManager.gridY + dragLiftY;
                desiredDragPosition = targetPos;

                // Chạy hiệu ứng nhấc khối dựa trên scale hiện tại của khối cha
                StartTween(block.transform.position, targetPos, block.transform.localScale, originalScale * holdScaleMultiplier, pickupDuration);
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
        StartTween(
            draggingBlock.transform.position,
            snapWorldPos,
            draggingBlock.transform.localScale,
            originalScale,
            snapDuration,
            () => { draggingBlock = null; }
        );

        if (CheckGroupAssembled(draggingBlock.colorGroup))
        {
            Debug.Log($"Nhóm màu {draggingBlock.colorGroup} đã xếp khớp tương đối!");
            // Kích hoạt hiệu ứng nổ khối hoặc ẩn khối tại đây...
        }
    }

    private void UpdateDragFollow()
    {
        if (draggingBlock == null || isTweening) return;

        float t = 1f - Mathf.Exp(-dragFollowSharpness * Time.deltaTime);
        
        draggingBlock.transform.position = Vector3.Lerp(draggingBlock.transform.position, desiredDragPosition, t);
    }

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

    // Hàm kiểm tra xem một nhóm màu đã được xếp khớp tương đối với nhau chưa
    private bool CheckGroupAssembled(string colorGroup)
    {
        if (string.IsNullOrEmpty(colorGroup) || blocksRoot == null) return false;

        // 1. Lấy ra tất cả các block thuộc nhóm màu này
        DraggableBlock[] allBlocks = blocksRoot.GetComponentsInChildren<DraggableBlock>();
        List<DraggableBlock> groupBlocks = new List<DraggableBlock>();
        
        foreach (var b in allBlocks)
        {
            if (b.enabled && b.colorGroup == colorGroup)
            {
                groupBlocks.Add(b);
            }
        }

        if (groupBlocks.Count == 0) return false;

        // 2. Tính toán độ lệch tương đối dựa trên khối đầu tiên trong danh sách
        DraggableBlock first = groupBlocks[0];
        int offsetX = first.col - first.targetCol;
        int offsetY = first.row - first.targetRow;

        // 3. Kiểm tra xem các khối còn lại có cùng độ lệch không
        for (int i = 1; i < groupBlocks.Count; i++)
        {
            DraggableBlock block = groupBlocks[i];
            if ((block.col - block.targetCol) != offsetX || (block.row - block.targetRow) != offsetY)
            {
                return false; // Chỉ cần 1 khối lệch hàng/cột là chưa khớp
            }
        }

        return true; // Toàn bộ nhóm đã khớp hoàn hảo!
    }
}