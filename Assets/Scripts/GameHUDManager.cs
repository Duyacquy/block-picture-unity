using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameHUDManager : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI timerText;
    public Button replayButton; 

    [Header("Timer Settings")]
    public float timeRemaining = 120f; // Tổng số giây màn chơi
    public Color normalColor = Color.white;
    public Color warningColor = Color.red;

    [Header("Timer Pop Animation (On Tick)")]
    public float pulseAmount = 0.2f;       // Độ phóng to thêm khi nhảy số (0.2 = bự hơn 20%)
    public float pulseDuration = 0.25f;    // Thời gian nảy to rồi thu nhỏ lại của chữ (giây)

    [Header("Button Juice Settings")]
    public float buttonJuiceDuration = 0.15f; 
    [Range(0.05f, 0.3f)]
    public float pressShrinkAmount = 0.15f;   

    private bool isTimerStarted = false; 
    private bool isTimeOut = false;
    private bool isReplaying = false;    
    
    private Vector3 originalTextScale;
    private Vector3 originalButtonScale;
    
    // Biến lưu mốc giây nguyên vẹn cuối cùng để phát hiện sự kiện đổi số
    private int lastWholeSeconds = -1; 
    private Coroutine pulseCoroutine;

    private void Start()
    {
        // 1. Cấu hình nút Replay
        if (replayButton != null)
        {
            originalButtonScale = replayButton.transform.localScale;
            replayButton.onClick.AddListener(OnReplayClicked);
        }
        
        // 2. Cấu hình chữ đếm ngược
        if (timerText != null)
        {
            originalTextScale = timerText.transform.localScale;
            timerText.color = normalColor;
        }

        // Lấy mốc giây làm tròn lên ban đầu (Ví dụ: 120.0f -> 120)
        lastWholeSeconds = Mathf.CeilToInt(timeRemaining);
        UpdateTimerDisplay(lastWholeSeconds);
    }

    private void Update()
    {
        if (!isTimerStarted || isTimeOut) return;

        if (timeRemaining > 0)
        {
            timeRemaining -= Time.deltaTime;
            
            int currentWholeSeconds = Mathf.CeilToInt(timeRemaining);
            
            // PHÁT HIỆN SỰ KIỆN: Số giây chính thức giảm xuống (1 khung hình duy nhất mỗi giây)
            if (currentWholeSeconds != lastWholeSeconds)
            {
                lastWholeSeconds = currentWholeSeconds;
                UpdateTimerDisplay(currentWholeSeconds);

                // KIỂM TRA MỐC ĐỎ ĐÚNG TỪ GIÂY THỨ 15
                if (currentWholeSeconds <= 15 && currentWholeSeconds > 0)
                {
                    timerText.color = warningColor;

                    if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(AudioManager.Instance.tickTock);

                    if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
                    pulseCoroutine = StartCoroutine(PulseTextOnce());
                }
                else if (currentWholeSeconds > 15)
                {
                    timerText.color = normalColor;
                }
            }
        }
        else
        {
            timeRemaining = 0;
            isTimeOut = true;
            UpdateTimerDisplay(0);
            
            if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
            timerText.transform.localScale = originalTextScale;
            
            OnTimeOut();
        }
    }

    public void StartTimer()
    {
        if (!isTimerStarted)
        {
            isTimerStarted = true;
            Debug.Log("Bắt đầu tính giờ màn chơi!");
        }
    }

    private void UpdateTimerDisplay(int totalSeconds)
    {
        if (timerText == null) return;

        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    // THÊM MỚI: Coroutine tự động chạy nảy chữ một vòng duy nhất rồi dừng lại
    private IEnumerator PulseTextOnce()
    {
        float elapsed = 0f;
        
        while (elapsed < pulseDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / pulseDuration;

            // Hàm Sin chạy từ 0 đến PI tạo ra một chu kỳ hình nấm mượt mà (0 -> 1 -> 0)
            float wave = Mathf.Sin(progress * Mathf.PI);
            float currentScaleMultiplier = 1f + (wave * pulseAmount);

            if (timerText != null)
            {
                timerText.transform.localScale = originalTextScale * currentScaleMultiplier;
            }

            yield return null; // Chờ khung hình tiếp theo
        }

        // Ép trả lại kích thước gốc sau khi kết thúc 1 nhịp nảy
        if (timerText != null) timerText.transform.localScale = originalTextScale;
    }

    private void OnTimeOut()
    {
        EndgameManager endgameUI = FindAnyObjectByType<EndgameManager>();
        if (endgameUI != null)
        {
            endgameUI.ShowLose();
        }
    }

    private void OnReplayClicked()
    {
        if (isReplaying) return; 

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(AudioManager.Instance.clickBtn);

        StartCoroutine(PlayButtonJuiceAndReload());
    }

    private IEnumerator PlayButtonJuiceAndReload()
    {
        isReplaying = true;
        float elapsed = 0f;

        while (elapsed < buttonJuiceDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / buttonJuiceDuration;
            float wave = Mathf.Sin(progress * Mathf.PI);
            float currentScaleMultiplier = 1f - (wave * pressShrinkAmount);

            if (replayButton != null)
            {
                replayButton.transform.localScale = originalButtonScale * currentScaleMultiplier;
            }

            yield return null;
        }

        if (replayButton != null) replayButton.transform.localScale = originalButtonScale;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void StopTimer()
    {
        this.enabled = false;
    }
}