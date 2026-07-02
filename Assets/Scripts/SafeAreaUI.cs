using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class SafeAreaUI : MonoBehaviour
{
    private RectTransform rectTransform;
    private Rect lastSafeArea = Rect.zero;
    private Vector2 lastScreenSize = Vector2.zero;

    [SerializeField]
    private float topPaddingReduction = 100f;

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

        Rect adjustedSafeArea = lastSafeArea;

        // Phần bị mất ở trên do tai thỏ / dynamic island
        float topInset = Screen.height - (lastSafeArea.y + lastSafeArea.height);

        // Nếu có tai thỏ thì giảm bớt phần Safe Area phía trên
        if (topInset > 0f)
        {
            float reduce = Mathf.Min(topPaddingReduction, topInset);
            adjustedSafeArea.height += reduce;
        }

        Vector2 anchorMin = adjustedSafeArea.position;
        Vector2 anchorMax = adjustedSafeArea.position + adjustedSafeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
    }
}