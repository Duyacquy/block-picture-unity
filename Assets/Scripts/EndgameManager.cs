using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;
using Spine.Unity;

public class EndgameManager : MonoBehaviour
{
    [Header("Win Settings")]
    public GameObject winPanel;
    public SkeletonGraphic wellDoneSpine; 
    public Button replayButton; 

    [Header("Lose Settings (Chỉ giữ lại Out Of Time)")]
    public GameObject losePanel;            // Node cha tổng của màn hình thua
    public RectTransform loseContainer;     // Khung vuông LoseContainer chứa nội dung để chạy BackOut
    public CanvasGroup backgroundMaskGroup; // Gắn CanvasGroup vào Background đen để fade-in
    public CanvasGroup loseContainerGroup;  // Gắn CanvasGroup vào LoseContainer để fade-in
    public Button retryButton;  

    [Header("Button Juice Anim Settings")]
    public float buttonJuiceDuration = 0.15f; 
    [Range(0.05f, 0.3f)]
    public float pressShrinkAmount = 0.15f;   

    private bool isReloading = false; 

    private void Start()
    {
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
        
        if (replayButton != null) 
        {
            replayButton.gameObject.SetActive(false);
            replayButton.onClick.RemoveAllListeners();
            replayButton.onClick.AddListener(() => OnButtonClicked(replayButton));
        }
        
        if (retryButton != null) 
        {
            retryButton.gameObject.SetActive(false);
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(() => OnButtonClicked(retryButton));
        }
    }

    public void ShowEndgame()
    {
        StartCoroutine(EndgameRoutine());
    }

    private IEnumerator EndgameRoutine()
    {
        yield return new WaitForSeconds(3f);

        if (winPanel != null) winPanel.SetActive(true);
        if (AudioManager.Instance != null) AudioManager.Instance.StopBGM();
        if (ConfettiManager.Instance != null) ConfettiManager.Instance.PlayWinConfetti();
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(AudioManager.Instance.winLevel);

        if (wellDoneSpine != null)
        {
            wellDoneSpine.Initialize(true); 
            int randomIdx = Random.Range(1, 6); 
            string randomSkinName = randomIdx.ToString(); 

            if (wellDoneSpine.Skeleton != null && wellDoneSpine.AnimationState != null)
            {
                wellDoneSpine.Skeleton.SetSkin(randomSkinName);
                wellDoneSpine.Skeleton.SetSlotsToSetupPose();
                wellDoneSpine.AnimationState.ClearTracks();
                wellDoneSpine.AnimationState.SetAnimation(0, "appear", false);
                wellDoneSpine.AnimationState.AddAnimation(0, "loop", true, 0f);
                wellDoneSpine.UpdateMesh();
            }
        }

        if (replayButton != null) replayButton.gameObject.SetActive(true);
    }

    // ====================================================================
    // HÀM GỌI THUA: Khóa tương tác kéo khối và kích hoạt chuỗi hoãn hiện panel
    // ====================================================================
    public void ShowLose()
    {
        // Khóa lập tức script kéo thả khối để người chơi không ăn gian được khi đã hết giờ
        PuzzleDragController dragCtrl = FindAnyObjectByType<PuzzleDragController>();
        if (dragCtrl != null) dragCtrl.enabled = false;

        if (AudioManager.Instance != null) {
            AudioManager.Instance.StopBGM();

            AudioManager.Instance.PlaySFX(AudioManager.Instance.outOfTime);
        }

        // Chạy Coroutine xử lý trì hoãn và hoạt ảnh
        StartCoroutine(LoseRoutine());
    }

    private IEnumerator LoseRoutine()
    {
        yield return new WaitForSeconds(0.8f);

        // 2. Bật các Node giao diện thua lên
        if (losePanel != null) losePanel.SetActive(true);
        if (retryButton != null) retryButton.gameObject.SetActive(true);

        // 3. Đưa các thông số mờ rõ và kích thước về mốc ban đầu để chạy hoạt ảnh
        if (backgroundMaskGroup != null) backgroundMaskGroup.alpha = 0f;
        if (loseContainerGroup != null) loseContainerGroup.alpha = 0f;
        if (loseContainer != null) loseContainer.localScale = new Vector3(0.1f, 0.1f, 1f);

        float fadeDuration = 0.3f;   // Làm mờ rõ hoàn thành trong 0.3s (Giống Cocos)
        float scaleDuration = 1.0f;  // Phóng to giật nảy hoàn thành trong 1.0s (Giống Cocos)
        float elapsed = 0f;

        // 4. Vòng lặp tính toán nội suy chuyển động (Tweening) mỗi khung hình
        while (elapsed < scaleDuration)
        {
            elapsed += Time.deltaTime;

            // A. HIỆU ỨNG FADE IN (LÀM MỜ RÕ TRONG 0.2S ĐẦU)
            float fadeProgress = Mathf.Clamp01(elapsed / fadeDuration);
            if (backgroundMaskGroup != null) backgroundMaskGroup.alpha = fadeProgress;
            if (loseContainerGroup != null) loseContainerGroup.alpha = fadeProgress;

            // B. HIỆU ỨNG GIẬT NẢY ĐÀN HỒI (BACK OUT EASING TRONG 1.0S)
            float scaleProgress = Mathf.Clamp01(elapsed / scaleDuration);
            
            // Công thức toán học giả lập đồ thị BackOut nguyên bản
            float t = scaleProgress - 1f;
            float s = 1.70158f; // Chỉ số biên độ hất ngược tiêu chuẩn hình sin
            float backOutValue = 1f + (s + 1f) * t * t * t + s * t * t;

            // Nội suy kích thước từ mốc tí hon 0.1 phóng vượt ngưỡng 1.0 rồi tự hồi về
            float currentScale = Mathf.Lerp(0.1f, 1f, backOutValue);

            if (loseContainer != null)
            {
                loseContainer.localScale = new Vector3(currentScale, currentScale, 1f);
            }

            yield return null;
        }

        // Ép cố định trạng thái hoàn chỉnh 100% khi kết thúc vòng lặp để tránh sai lệch số
        if (backgroundMaskGroup != null) backgroundMaskGroup.alpha = 1f;
        if (loseContainerGroup != null) loseContainerGroup.alpha = 1f;
        if (loseContainer != null) loseContainer.localScale = Vector3.one;
    }

    private void OnButtonClicked(Button targetButton)
    {
        if (isReloading) return;
        
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(AudioManager.Instance.clickBtn);

        StartCoroutine(PlayButtonJuiceAndReload(targetButton));
    }

    private IEnumerator PlayButtonJuiceAndReload(Button targetButton)
    {
        isReloading = true; 
        float elapsed = 0f;
        Vector3 originalButtonScale = targetButton.transform.localScale;

        while (elapsed < buttonJuiceDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / buttonJuiceDuration;
            float wave = Mathf.Sin(progress * Mathf.PI); 
            float currentScaleMultiplier = 1f - (wave * pressShrinkAmount);

            if (targetButton != null) targetButton.transform.localScale = originalButtonScale * currentScaleMultiplier;
            yield return null;
        }

        if (targetButton != null) targetButton.transform.localScale = originalButtonScale;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}