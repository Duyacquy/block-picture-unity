using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class SafeAreaUI : MonoBehaviour
{
    private RectTransform rectTransform;
    private Rect lastSafeArea = Rect.zero;
    private Vector2 lastScreenSize = Vector2.zero;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        RefreshSafeArea();
    }

    private void Update()
    {
        // Nếu người chơi đổi thiết bị trên Simulator hoặc xoay màn hình, tự động tính lại vùng an toàn
        if (lastSafeArea != Screen.safeArea || lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height)
        {
            RefreshSafeArea();
        }
    }

    private void RefreshSafeArea()
    {
        lastSafeArea = Screen.safeArea;
        lastScreenSize = new Vector2(Screen.width, Screen.height);

        // Quy đổi tọa độ pixel của Safe Area sang tỉ lệ phần trăm từ 0 -> 1 của Canvas
        Vector2 anchorMin = lastSafeArea.position;
        Vector2 anchorMax = lastSafeArea.position + lastSafeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
    }
}