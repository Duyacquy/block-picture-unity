using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource; // Chuyên dùng để chạy nhạc nền (BGM)
    [SerializeField] private AudioSource sfxSource; // Chuyên dùng để chạy tiếng động hiệu ứng (SFX)

    [Header("Gameplay Clips (5 Âm thanh đầu tiên)")]
    public AudioClip bgPlay;
    public AudioClip blockUp;
    public AudioClip blockDown;
    public AudioClip blockMatch;
    public AudioClip blockBreak;
    public AudioClip clickBtn;
    public AudioClip outOfTime;
    public AudioClip pictureCollect;
    public AudioClip tickTock;
    public AudioClip winLevel;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Giữ âm thanh chạy xuyên suốt khi đổi màn
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        // Đăng ký hàm OnSceneLoaded vào hệ thống quản lý chuyển cảnh của Unity
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        // Hủy đăng ký khi đối tượng bị xóa để tránh rò rỉ bộ nhớ (Memory Leak)
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"Scene '{scene.name}' đã nạp xong! Tự động bật lại nhạc nền.");
        PlayBGM(); // Ép nhạc nền phát lại từ đầu mỗi khi reload màn chơi!
    }

    // ====================================================================
    // CÁC HÀM ĐIỀU KHIỂN NHẠC NỀN (BGM)
    // ====================================================================
    public void PlayBGM()
    {
        if (bgPlay != null && bgmSource != null)
        {
            bgmSource.clip = bgPlay;
            bgmSource.loop = true; // Bật lặp vô hạn cho nhạc nền chơi game
            bgmSource.Play();
        }
    }

    public void StopBGM()
    {
        if (bgmSource != null && bgmSource.isPlaying)
        {
            bgmSource.Stop(); 
        }
    }

    // ====================================================================
    // CÁC HÀM ĐIỀU KHIỂN HIỆU ỨNG (SFX)
    // ====================================================================
    public void PlaySFX(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }
}