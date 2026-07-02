using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.UI;

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
    private int draggingCurrentCol; // Vị trí cột thực tế an toàn hiện tại lúc kéo
    private int draggingCurrentRow; // Vị trí hàng thực tế an toàn hiện tại lúc kéo

    private bool isTweening = false;
    private float tweenTimer = 0f;
    private float currentTweenDuration = 0f;
    private Vector3 tweenStartPos;
    private Vector3 tweenEndPos;
    private Vector3 tweenStartScale;
    private Vector3 tweenEndScale;

    private Vector3 lastValidDragPosition;

    [Header("VFX Completed Settings")]
    public GameObject flashParticlePrefab;
    public GameObject shatterFragmentPrefab;
    public GameObject starTrailParticlePrefab;
    public Transform uiCanvasRoot;
    public float imageFlyDuration = 0.6f;
    public Transform targetBarsRoot;

    private void Start()
    {
        if (gridManager == null) gridManager = FindAnyObjectByType<BoardGridManager>();

        SnapAllBlocksToGridInstant();
        RebuildOccupied(); //
    }

    private void SnapAllBlocksToGridInstant()
    {
        if (blocksRoot == null || gridManager == null) return;

        DraggableBlock[] blocks = blocksRoot.GetComponentsInChildren<DraggableBlock>();
        foreach (DraggableBlock block in blocks)
        {
            if (block != null && block.enabled)
            {
                Vector3 correctWorldPos = gridManager.GridToWorld(block.row, block.col); //
                block.transform.position = correctWorldPos;
            }
        }
    }

    private void Update()
    {
        HandleInput(); //
        UpdateDragFollow(); //
        HandleTweens(); //
    }

    private void HandleInput()
    {
        Vector2 pointerPos = Vector2.zero; //
        bool pressed = false; //
        bool held = false; //
        bool released = false; //

        if (Touchscreen.current != null) //
        {
            var touch = Touchscreen.current.primaryTouch; //
            pressed = touch.press.wasPressedThisFrame; //
            held = touch.press.isPressed; //
            released = touch.press.wasReleasedThisFrame; //

            if (pressed || held || released) pointerPos = touch.position.ReadValue(); //
        }

        if (!pressed && !held && !released && Mouse.current != null) //
        {
            pointerPos = Mouse.current.position.ReadValue(); //
            pressed = Mouse.current.leftButton.wasPressedThisFrame; //
            held = Mouse.current.leftButton.isPressed; //
            released = Mouse.current.leftButton.wasReleasedThisFrame; //
        }

        if (released && draggingBlock != null) EndDrag(); //
        else if (pressed) //
        {
            if (isTweening) return; //
            BeginDrag(pointerPos); //
        }
        else if (held && draggingBlock != null) MoveDrag(pointerPos); //
    }

    private void BeginDrag(Vector2 screenPos)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(AudioManager.Instance.blockUp);

        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit)) //
        {
            DraggableBlock block = hit.collider.GetComponentInParent<DraggableBlock>(); //
            if (block != null) //
            {
                GameHUDManager hudManager = FindAnyObjectByType<GameHUDManager>();
                if (hudManager != null) hudManager.StartTimer();

                draggingBlock = block; //
                draggingStartCol = block.col; //
                draggingStartRow = block.row; //
                draggingCurrentCol = block.col;
                draggingCurrentRow = block.row;

                if (!isTweening) originalScale = block.transform.localScale; //

                RemoveBlockFromOccupied(block); //

                Vector3 hitWorldPos = GetMouseWorldPosOnGridPlane(screenPos); //
                dragPointerOffset = block.transform.position - hitWorldPos; //
                dragPointerOffset.y = 0; //

                Vector3 targetPos = block.transform.position; //
                targetPos.y = gridManager.gridY + dragLiftY; //
                desiredDragPosition = targetPos; //

                lastValidDragPosition = block.transform.position;

                StartTween(block.transform.position, targetPos, block.transform.localScale, originalScale * holdScaleMultiplier, pickupDuration); //

                block.ToggleOutline(true);
            }
        }
    }

    private void MoveDrag(Vector2 screenPos)
    {
        Vector3 mouseWorldPos = GetMouseWorldPosOnGridPlane(screenPos);
        Vector3 rawTargetPos = mouseWorldPos + dragPointerOffset;
        rawTargetPos.y = gridManager.gridY;

        // 1. TÍNH TOÁN GIỚI HẠN BIÊN ĐỘNG THEO SHAPE (CHỐNG LÒI RÌA TUYỆT ĐỐI)
        int minShapeX = 0;
        int maxShapeX = 0;
        int minShapeY = 0;
        int maxShapeY = 0;

        // Quét toàn bộ danh sách ô của khối để tìm kích thước biên thực tế (Hỗ trợ cả khối dị hình)
        foreach (Vector2Int cell in draggingBlock.shape)
        {
            if (cell.x < minShapeX) minShapeX = cell.x;
            if (cell.x > maxShapeX) maxShapeX = cell.x;
            if (cell.y < minShapeY) minShapeY = cell.y;
            if (cell.y > maxShapeY) maxShapeY = cell.y;
        }

        // Xác định các ô lưới giới hạn cực đại/cực tiểu mà ô gốc (0,0) được phép đứng
        int minCol = 0 - minShapeX;
        int maxCol = Mathf.Max(0, gridManager.cols - 1 - maxShapeX);
        int minRow = 0 - minShapeY;
        int maxRow = Mathf.Max(0, gridManager.rows - 1 - maxShapeY);

        // Đổi giới hạn ô lưới sang tọa độ không gian 3D chuẩn (Lấy mốc là tâm các ô cờ biên)
        Vector3 topCenterLimit = gridManager.GridToWorld(minRow, minCol);
        Vector3 bottomCenterLimit = gridManager.GridToWorld(maxRow, maxCol);

        float minX = Mathf.Min(topCenterLimit.x, bottomCenterLimit.x);
        float maxX = Mathf.Max(topCenterLimit.x, bottomCenterLimit.x);
        float minZ = Mathf.Min(topCenterLimit.z, bottomCenterLimit.z);
        float maxZ = Mathf.Max(topCenterLimit.z, bottomCenterLimit.z);

        // [LỚP KHÓA 1]: Giới hạn ngay từ vị trí thô của pointer chuột, không cho phép vượt qua tâm ô biên
        rawTargetPos.x = Mathf.Clamp(rawTargetPos.x, minX, maxX);
        rawTargetPos.z = Mathf.Clamp(rawTargetPos.z, minZ, maxZ);

        // 2. PHÁT HIỆN VA CHẠM VÀ TRƯỢT SÁT MÉP KHỐI KHÁC
        Vector3 collisionResolvedPos = ClampDragWorldAgainstOccupied(draggingBlock, rawTargetPos);

        // [LỚP KHÓA 2]: Ép cứng tọa độ sau va chạm. Triệt tiêu hoàn toàn việc thuật toán trượt mép 
        // vô tình đẩy khối lọt ra ngoài rìa hộp mềm của bàn cờ.
        collisionResolvedPos.x = Mathf.Clamp(collisionResolvedPos.x, minX, maxX);
        collisionResolvedPos.z = Mathf.Clamp(collisionResolvedPos.z, minZ, maxZ);

        // 3. THUẬT TOÁN TÌM ĐƯỜNG LƯỚI (BFS)
        Vector2Int targetGrid = gridManager.WorldToGrid(collisionResolvedPos);
        int testRow = targetGrid.x;
        int testCol = targetGrid.y;

        if (CanPlaceBlock(draggingBlock, testCol, testRow) && CanReachGrid(draggingCurrentCol, draggingCurrentRow, testCol, testRow, draggingBlock))
        {
            draggingCurrentCol = testCol;
            draggingCurrentRow = testRow;
            lastValidDragPosition = collisionResolvedPos;
        }

        // Cập nhật vị trí hiển thị bám theo mốc an toàn đã qua kiểm duyệt 2 lớp khóa biên
        Vector3 finalDisplayPos = lastValidDragPosition;
        finalDisplayPos.y = gridManager.gridY + dragLiftY;
        desiredDragPosition = finalDisplayPos;
    }

    private void EndDrag()
    {
        if (draggingBlock != null)
        {
            draggingBlock.ToggleOutline(false);
        }

        // Khóa mục tiêu hạ cánh chuẩn xác tại ô Grid an toàn cuối cùng
        int finalCol = draggingCurrentCol;
        int finalRow = draggingCurrentRow;

        if (CanPlaceBlock(draggingBlock, finalCol, finalRow))
        {
            draggingBlock.SetGridPosition(finalCol, finalRow);
        }
        else
        {
            finalCol = draggingStartCol;
            finalRow = draggingStartRow;
            draggingBlock.SetGridPosition(finalCol, finalRow);
        }

        AddBlockToOccupied(draggingBlock);
        Vector3 snapWorldPos = gridManager.GridToWorld(finalRow, finalCol);

        // ====================================================================
        // SỬA LỖI CHÍ MẠNG: KIỂM TRA KHỚP NHÓM TRƯỚC KHI CHẠY TWEEN HÚT LƯỚI
        // ====================================================================
        if (CheckGroupAssembled(draggingBlock.colorGroup))
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(AudioManager.Instance.blockMatch);

            // Ép khối về đúng vị trí đích ngay lập tức, không chạy Tween của chuột nữa
            draggingBlock.transform.position = snapWorldPos;
            draggingBlock.transform.localScale = originalScale;
            
            // GIẢI PHÓNG LOCK: Trả trạng thái Tween về false ngay để click được khối khác!
            isTweening = false; 

            DraggableBlock[] allBlocks = blocksRoot.GetComponentsInChildren<DraggableBlock>();
            List<DraggableBlock> groupBlocks = new List<DraggableBlock>();
            foreach (var b in allBlocks)
            {
                if (b.enabled && b.colorGroup == draggingBlock.colorGroup)
                {
                    groupBlocks.Add(b);

                    // Tắt toàn bộ Collider của nhóm này ngay lập tức để tránh người chơi click nhầm lúc đang nổ
                    b.enabled = false;
                    foreach (var col in b.GetComponentsInChildren<Collider>()) col.enabled = false;
                }
            }

            // Kích hoạt chuỗi hoạt ảnh nổ khối - bay tranh
            StartCoroutine(PlayGroupShatterSequence(groupBlocks, draggingBlock.colorGroup));
            draggingBlock = null; 
        }
        else
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(AudioManager.Instance.blockDown);

            StartTween(
                draggingBlock.transform.position,
                snapWorldPos,
                draggingBlock.transform.localScale,
                originalScale,
                snapDuration,
                () => { draggingBlock = null; }
            );
        }
    }

    private void UpdateDragFollow()
    {
        if (draggingBlock == null || isTweening) return; //
        float t = 1f - Mathf.Exp(-dragFollowSharpness * Time.deltaTime); //
        draggingBlock.transform.position = Vector3.Lerp(draggingBlock.transform.position, desiredDragPosition, t); //
    }

    private Vector3 GetMouseWorldPosOnGridPlane(Vector2 screenPos)
    {
        Plane plane = new Plane(Vector3.up, new Vector3(0, gridManager.gridY, 0)); //
        Ray ray = mainCamera.ScreenPointToRay(screenPos); //
        if (plane.Raycast(ray, out float distance)) return ray.GetPoint(distance); //
        return Vector3.zero; //
    }

    private Vector3 ClampDragWorldAgainstOccupied(DraggableBlock block, Vector3 desiredWorld)
    {
        Vector3 previousWorld = lastValidDragPosition;
        previousWorld.y = desiredWorld.y;

        // SỬA TẠI ĐÂY: Loại bỏ hoàn toàn shortcut if(!Overlaps) cũ, bắt buộc luôn chạy Sweep liên tục từ điểm cũ ra điểm mới
        Vector3 directSweep = SweepToLastClearWorld(block, previousWorld, desiredWorld);
        if (Vector3.Distance(directSweep, desiredWorld) < 0.001f)
        {
            return desiredWorld;
        }

        // Cơ chế tách trục giúp trượt mượt quanh góc khối chặn
        Vector3 xOnly = new Vector3(desiredWorld.x, desiredWorld.y, previousWorld.z);
        Vector3 zOnly = new Vector3(previousWorld.x, desiredWorld.y, desiredWorld.z);

        Vector3 xSweep = SweepToLastClearWorld(block, previousWorld, xOnly);
        Vector3 zSweep = SweepToLastClearWorld(block, previousWorld, zOnly);

        Vector3 bestCandidate = directSweep;
        float bestDistance = Vector3.Distance(directSweep, desiredWorld);

        if (!OverlapsOccupiedOrBounds(block, xSweep))
        {
            float d = Vector3.Distance(xSweep, desiredWorld);
            if (d < bestDistance)
            {
                bestDistance = d;
                bestCandidate = xSweep;
            }
        }

        if (!OverlapsOccupiedOrBounds(block, zSweep))
        {
            float d = Vector3.Distance(zSweep, desiredWorld);
            if (d < bestDistance)
            {
                bestCandidate = zSweep;
            }
        }

        return bestCandidate;
    }

    private Vector3 SweepToLastClearWorld(DraggableBlock block, Vector3 fromWorld, Vector3 toWorld)
    {
        if (OverlapsOccupiedOrBounds(block, fromWorld)) return fromWorld;

        float distance = Vector3.Distance(fromWorld, toWorld);
        if (distance <= 0.001f) return toWorld;

        int steps = Mathf.Max(4, Mathf.CeilToInt(distance / (gridManager.cellStep * 0.1f)));
        Vector3 lastClear = fromWorld;

        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector3 testPos = Vector3.Lerp(fromWorld, toWorld, t);

            if (OverlapsOccupiedOrBounds(block, testPos))
            {
                return lastClear; // Bị chặn, giữ nguyên tại điểm sạch kề cận trước đó
            }
            lastClear = testPos;
        }

        return toWorld;
    }

    private bool OverlapsOccupiedOrBounds(DraggableBlock block, Vector3 worldPos)
    {
        float step = gridManager.cellStep;
        float boardWidth = gridManager.cols * step;
        float boardHeight = gridManager.rows * step;
        float firstCellX = gridManager.centerX - (boardWidth * 0.5f) + (step * 0.5f);
        float firstCellZ = gridManager.centerZ + (boardHeight * 0.5f) - (step * 0.5f);

        float halfCell = step * 0.5f;

        foreach (Vector2Int cell in block.shape)
        {
            float cellWorldX = worldPos.x + cell.x * step;
            float cellWorldZ = worldPos.z - cell.y * step;

            // Kiểm tra va chạm biên lưới
            float minXBound = firstCellX - halfCell;
            float maxXBound = firstCellX + (gridManager.cols - 1) * step + halfCell;
            float minZBound = firstCellZ - (gridManager.rows - 1) * step - halfCell;
            float maxZBound = firstCellZ + halfCell;

            if (cellWorldX < minXBound || cellWorldX > maxXBound || cellWorldZ < minZBound || cellWorldZ > maxZBound)
            {
                return true;
            }

            // Kiểm tra đè ô của khối khác (Giữ hộp va chạm mềm 88% để lách ngách mượt)
            float collisionBoxSize = step * 0.88f;

            foreach (var pair in occupiedCells)
            {
                if (pair.Value == block) continue;

                string[] parts = pair.Key.Split('_');
                int occRow = int.Parse(parts[0]);
                int occCol = int.Parse(parts[1]);

                Vector3 occWorld = gridManager.GridToWorld(occRow, occCol); //

                float dx = Mathf.Abs(cellWorldX - occWorld.x);
                float dz = Mathf.Abs(cellWorldZ - occWorld.z);

                if (dx < collisionBoxSize && dz < collisionBoxSize)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // THÊM MỚI: Thuật toán tìm đường BFS kiểm tra kết nối ô lưới từ ô cũ (start) đến ô mới (target)
    private bool CanReachGrid(int startCol, int startRow, int targetCol, int targetRow, DraggableBlock block)
    {
        if (startCol == targetCol && startRow == targetRow) return true;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<string> visited = new HashSet<string>();

        // Đẩy ô xuất phát hiện tại vào hàng đợi (X lưu Row, Y lưu Col)
        queue.Enqueue(new Vector2Int(startRow, startCol));
        visited.Add($"{startRow}_{startCol}");

        Vector2Int[] dirs = new Vector2Int[]
        {
            new Vector2Int(0, 1),   // Phải
            new Vector2Int(0, -1),  // Trái
            new Vector2Int(1, 0),   // Xuống
            new Vector2Int(-1, 0)   // Lên
        };

        while (queue.Count > 0)
        {
            Vector2Int curr = queue.Dequeue();

            foreach (Vector2Int dir in dirs)
            {
                int nextRow = curr.x + dir.x;
                int nextCol = curr.y + dir.y;

                // Nếu ô lân cận này khối hoàn toàn có thể lọt qua được (không bị đè vật cản hay ra rìa)
                if (!CanPlaceBlock(block, nextCol, nextRow)) continue;

                if (nextCol == targetCol && nextRow == targetRow) return true; // Tìm thấy lối đi thông suốt!

                string key = $"{nextRow}_{nextCol}";
                if (!visited.Contains(key))
                {
                    visited.Add(key);
                    queue.Enqueue(new Vector2Int(nextRow, nextCol));
                }
            }
        }

        return false; // Bị bao vây cô lập, không có đường đi đến ô đích
    }

    public void RebuildOccupied()
    {
        occupiedCells.Clear(); //
        if (blocksRoot == null) return; //

        DraggableBlock[] blocks = blocksRoot.GetComponentsInChildren<DraggableBlock>(); //
        foreach (DraggableBlock block in blocks) //
        {
            if (block != null && block.enabled) AddBlockToOccupied(block); //
        }
    }

    private void AddBlockToOccupied(DraggableBlock block)
    {
        foreach (Vector2Int cell in block.shape) //
        {
            string key = $"{block.row + cell.y}_{block.col + cell.x}"; //
            occupiedCells[key] = block; //
        }
    }

    private void RemoveBlockFromOccupied(DraggableBlock block)
    {
        foreach (Vector2Int cell in block.shape) //
        {
            string key = $"{block.row + cell.y}_{block.col + cell.x}"; //
            occupiedCells.Remove(key); //
        }
    }

    private bool CanPlaceBlock(DraggableBlock block, int col, int row)
    {
        foreach (Vector2Int cell in block.shape) //
        {
            int targetC = col + cell.x; //
            int targetR = row + cell.y; //

            if (targetC < 0 || targetC >= gridManager.cols || targetR < 0 || targetR >= gridManager.rows) return false; //

            string key = $"{targetR}_{targetC}"; //
            if (occupiedCells.ContainsKey(key) && occupiedCells[key] != block) return false; //
        }
        return true; //
    }

    private System.Action onTweenCompleteCallback; //
    private void StartTween(Vector3 startP, Vector3 endP, Vector3 startS, Vector3 endS, float duration, System.Action onComplete = null)
    {
        tweenStartPos = startP; tweenEndPos = endP; tweenStartScale = startS; tweenEndScale = endS; //
        currentTweenDuration = duration; tweenTimer = 0f; onTweenCompleteCallback = onComplete; isTweening = true; //
    }

    private void HandleTweens()
    {
        if (!isTweening || draggingBlock == null) return; //

        tweenTimer += Time.deltaTime; //
        float percent = Mathf.Clamp01(tweenTimer / currentTweenDuration); //
        float t = percent * percent * (3f - 2f * percent); //

        draggingBlock.transform.position = Vector3.Lerp(tweenStartPos, tweenEndPos, t); //
        draggingBlock.transform.localScale = Vector3.Lerp(tweenStartScale, tweenEndScale, t); //

        if (percent >= 1f) //
        {
            isTweening = false; //
            onTweenCompleteCallback?.Invoke(); //
        }
    }

    private bool CheckGroupAssembled(string colorGroup)
    {
        if (string.IsNullOrEmpty(colorGroup) || blocksRoot == null) return false; //

        DraggableBlock[] allBlocks = blocksRoot.GetComponentsInChildren<DraggableBlock>(); //
        List<DraggableBlock> groupBlocks = new List<DraggableBlock>(); //

        foreach (var b in allBlocks) //
        {
            if (b.enabled && b.colorGroup == colorGroup) groupBlocks.Add(b); //
        }

        if (groupBlocks.Count == 0) return false; //

        DraggableBlock first = groupBlocks[0]; //
        int offsetX = first.col - first.targetCol; //
        int offsetY = first.row - first.targetRow; //

        for (int i = 1; i < groupBlocks.Count; i++) //
        {
            DraggableBlock block = groupBlocks[i]; //
            if ((block.col - block.targetCol) != offsetX || (block.row - block.targetRow) != offsetY) return false; //
        }
        return true; //
    }

    private IEnumerator PlayGroupShatterSequence(List<DraggableBlock> blocks, string colorGroup)
    {
        // --- GIAI ĐOẠN 1: BÙNG SÁNG TẠI CÁC Ô LƯỚI ---
        Vector3 sumWorldPos = Vector3.zero;
        int totalCells = 0;

        foreach (var block in blocks)
        {
            foreach (Vector2Int cell in block.shape)
            {
                // Tính toán vị trí world chính xác của từng ô nhỏ để sinh tia sáng
                Vector3 cellWorldPos = block.transform.TransformPoint(new Vector3(cell.x * gridManager.cellStep, 0f, -cell.y * gridManager.cellStep));
                sumWorldPos += cellWorldPos;
                totalCells++;

                if (flashParticlePrefab != null)
                {
                    Instantiate(flashParticlePrefab, cellWorldPos + Vector3.up * 0.1f, Quaternion.identity);
                }
            }
        }
        // Tính trọng tâm của cả nhóm màu trong không gian 3D
        Vector3 groupCenterWorld = sumWorldPos / totalCells;

        // Chờ 0.06 giây cho hiệu ứng chớp sáng đạt đỉnh chói (Khớp Ảnh 1)
        yield return new WaitForSeconds(0.06f);

        // --- GIAI ĐOẠN 2: BẮN MẢNH VỠ 3D & TẠO ẢNH UI BAY ---
        // Trích xuất Texture mục tiêu từ ImageSliceManager của bạn
        ImageSliceManager sliceManager = FindAnyObjectByType<ImageSliceManager>();
        Texture2D groupTexture = sliceManager != null ? sliceManager.textureConfig.Find(t => t.colorGroup == colorGroup).fullTexture : null;

        Transform targetUiSlot = null;
        if (targetBarsRoot != null)
        {
            // Duyệt qua tất cả các con và cháu bên trong TargetBars
            foreach (Transform child in targetBarsRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == colorGroup)
                {
                    targetUiSlot = child;
                    break; // Đã tìm thấy node vật phẩm (ví dụ: Pan), thoát vòng lặp
                }
            }
        }

        GameObject flyingImageObj = null;
        if (groupTexture != null && targetUiSlot != null && uiCanvasRoot != null)
        {
            // Khởi tạo một UI Image động để làm ảnh bay
            flyingImageObj = new GameObject($"FlyingImage_{colorGroup}");
            flyingImageObj.transform.SetParent(uiCanvasRoot, false);

            var rect = flyingImageObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(120, 120); // Kích thước hiển thị ban đầu của ảnh bay

            var img = flyingImageObj.AddComponent<Image>();
            img.sprite = Sprite.Create(groupTexture, new Rect(0, 0, groupTexture.width, groupTexture.height), new Vector2(0.5f, 0.5f));

            Vector3 screenPos = mainCamera.WorldToScreenPoint(groupCenterWorld);
            rect.position = screenPos;
        }

        Color groupColor = Color.white;
        if (blocks.Count > 0)
        {
            BlockVisuals visuals = blocks[0].GetComponent<BlockVisuals>();
            if (visuals != null) groupColor = visuals.blockColor;
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(AudioManager.Instance.blockBreak);

        // Sinh lượng lớn mảnh vỡ tập trung tại tâm (Số mảnh = 15 mảnh * số lượng block)
        int totalFragments = 15 * blocks.Count;
        SpawnShatterFragmentsAtCenter(groupCenterWorld, groupColor, totalFragments);

        // Xóa toàn bộ các khối 3D cũ trên bàn cờ
        foreach (var block in blocks)
        {
            block.transform.localScale = Vector3.zero; 
            Destroy(block.gameObject);
        }

        // Cập nhật lại từ điển ô trống của bàn cờ
        RebuildOccupied();

        CheckWin();

        // --- GIAI ĐOẠN 3: ĐUÔI SAO & TWEEN ẢNH UI VỀ ĐÍCH ---
        if (flyingImageObj != null && targetUiSlot != null)
        {
            GameObject trailFx = null;
            if (starTrailParticlePrefab != null)
            {
                trailFx = Instantiate(starTrailParticlePrefab, flyingImageObj.transform);
                trailFx.transform.localPosition = Vector3.zero;
            }

            RectTransform imgRect = flyingImageObj.GetComponent<RectTransform>();
            Vector3 startUiPos = imgRect.position;
            Vector3 startScale = imgRect.localScale;

            float elapsed = 0f;
            while (elapsed < imageFlyDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / imageFlyDuration);

                // Sử dụng mượt mà SmoothStep hoặc SineInOut để đường bay tự nhiên
                float tEased = Mathf.SmoothStep(0f, 1f, t);

                // Cập nhật tọa độ bay bám theo vị trí thời gian thực của Target UI Slot (đề phòng UI động đậy)
                imgRect.position = Vector3.Lerp(startUiPos, targetUiSlot.position, tEased);

                // Thu nhỏ dần bức ảnh lại để vừa khít với kích thước ô chứa trên Target Bar
                imgRect.localScale = Vector3.Lerp(startScale, startScale * 0.4f, tEased);

                yield return null;
            }

            // Đã chạm đích: Xóa ảnh bay và kích hoạt hiệu ứng nảy nhẹ ăn mừng tại thẻ bài UI
            if (trailFx != null) Destroy(trailFx);
            Destroy(flyingImageObj);

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(AudioManager.Instance.pictureCollect);

            if (targetUiSlot.parent != null && targetUiSlot.parent != targetBarsRoot)
            {
                StartCoroutine(PunchScaleUI(targetUiSlot.parent));
            }
        }
    }

    private void SpawnShatterFragmentsAtCenter(Vector3 centerWorldPos, Color blockColor, int fragmentCount)
    {
        for (int i = 0; i < fragmentCount; i++)
        {
            if (shatterFragmentPrefab == null) break;

            // Sinh ngẫu nhiên rải rác trong phạm vi hẹp của khối (Rộng khoảng tầm ô lưới)
            float spawnRangeX = Random.Range(-0.35f, 0.35f);
            float spawnRangeZ = Random.Range(-0.35f, 0.35f);
            Vector3 spawnPos = centerWorldPos + new Vector3(spawnRangeX, 0.05f, spawnRangeZ);

            Quaternion randomRot = Quaternion.Euler(Random.Range(0, 360), Random.Range(0, 360), Random.Range(0, 360));
            GameObject frag = Instantiate(shatterFragmentPrefab, spawnPos, randomRot);

            // Giữ nguyên kích cỡ to ghồ ghề bạn đã ưng ý
            float scaleX = Random.Range(0.18f, 0.32f);
            float scaleY = Random.Range(0.18f, 0.32f);
            float scaleZ = Random.Range(0.18f, 0.32f);
            frag.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);

            // Nhuộm màu khối gốc
            MeshRenderer fragRenderer = frag.GetComponent<MeshRenderer>();
            if (fragRenderer != null) fragRenderer.material.color = blockColor;

            // ====================================================================
            // KHẮC PHỤC CHÍ MẠNG: TẮT KHÓA COLLIDER ĐỂ CHỐNG "NỔ VẬT LÝ" BAY TỨ PHÍA
            // ====================================================================
            Collider fragCollider = frag.GetComponent<Collider>();
            if (fragCollider != null)
            {
                fragCollider.enabled = false; // Tắt đi để các mảnh xuyên qua nhau, không đẩy nhau bay loạn nữa
            }

            Rigidbody rb = frag.GetComponent<Rigidbody>();
            if (rb == null) rb = frag.AddComponent<Rigidbody>();

            rb.useGravity = true; // Bật trọng lực để khối tự rơi xuống dưới bàn cờ

            rb.linearVelocity = new Vector3(
                Random.Range(-2.5f, 2.5f),
                Random.Range(1.2f, 2.2f),
                Random.Range(-2.5f, 2.5f)
            );

            // Giảm tốc độ tự xoay góc xuống mức vừa phải (Chuẩn Unity 6 dùng angularVelocity)
            rb.angularVelocity = new Vector3(Random.Range(-3f, 3f), Random.Range(-3f, 3f), Random.Range(-3f, 3f));

            // Tự động xóa sau khi rơi khuất xuống dưới
            Destroy(frag, 1.2f);
        }
    }

    private IEnumerator PunchScaleUI(Transform uiTarget)
    {
        Vector3 originScale = Vector3.one;
        float duration = 0.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            float wave = Mathf.Sin(progress * Mathf.PI);

            // Thẻ UI nảy to lên 15% rồi thu hồi về kích thước chuẩn ban đầu
            uiTarget.localScale = originScale * (1f + (wave * 0.15f));
            yield return null;
        }
        uiTarget.localScale = originScale;
    }

    private void CheckWin()
    {
        if (blocksRoot == null) return;

        bool hasActiveBlocks = false;
        
        DraggableBlock[] allBlocks = blocksRoot.GetComponentsInChildren<DraggableBlock>(true);

        foreach (var block in allBlocks)
        {
            if (block != null && block.enabled && block.gameObject.activeSelf)
            {
                hasActiveBlocks = true;
                break; 
            }
        }

        if (!hasActiveBlocks)
        {
            GameHUDManager hudManager = FindAnyObjectByType<GameHUDManager>();
            if (hudManager != null)
            {
                hudManager.StopTimer();
            }

            EndgameManager endgameUI = FindAnyObjectByType<EndgameManager>();
            if (endgameUI != null)
            {
                endgameUI.ShowEndgame();
            }
        }
    }
}