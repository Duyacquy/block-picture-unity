using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class ConfettiManager : MonoBehaviour
{
    public static ConfettiManager Instance;

    [Header("Cấu hình hạt")]
    public Sprite[] confettiSprites;
    public RectTransform spawnArea; // Kéo Canvas hoặc Panel vào đây
    public int piecesPerWave = 60;
    public int totalWaves = 3;
    public float waveInterval = 0.5f;

    [Header("Kích thước")]
    public float minScale = 1.2f;
    public float maxScale = 2.2f;

    private void Awake() { Instance = this; }

    public void PlayWinConfetti()
    {
        for (int i = 0; i < totalWaves; i++)
        {
            StartCoroutine(FireWave(i * waveInterval));
        }
    }

    private IEnumerator FireWave(float delay)
    {
        yield return new WaitForSeconds(delay);
        for (int i = 0; i < piecesPerWave; i++)
        {
            SpawnSinglePiece();
        }
    }

    private void SpawnSinglePiece()
    {
        if (confettiSprites.Length == 0) return;

        // Tạo Object
        GameObject piece = new GameObject("ConfettiPiece");
        piece.transform.SetParent(spawnArea, false);
        
        Image img = piece.AddComponent<Image>();
        img.sprite = confettiSprites[Random.Range(0, confettiSprites.Length)];
        img.SetNativeSize();

        RectTransform rect = piece.GetComponent<RectTransform>();
        
        // Vị trí bắt đầu (Dưới đáy màn hình)
        float viewWidth = spawnArea.rect.width;
        float viewHeight = spawnArea.rect.height;
        float startX = Random.Range(-viewWidth * 0.5f, viewWidth * 0.5f);
        float startY = -viewHeight * 0.5f - 100f;
        rect.anchoredPosition = new Vector2(startX, startY);

        // Cấu hình ngẫu nhiên
        float scale = Random.Range(minScale, maxScale);
        rect.localScale = Vector3.zero;

        Vector2 targetPos = new Vector2(
            startX + Random.Range(-viewWidth * 0.3f, viewWidth * 0.3f),
            Random.Range(-viewHeight * 0.1f, viewHeight * 0.4f)
        );

        float duration = Random.Range(1.0f, 1.8f);
        StartCoroutine(AnimatePiece(rect, img, targetPos, duration, scale));
    }

    private IEnumerator AnimatePiece(RectTransform rect, Image img, Vector2 targetPos, float duration, float finalScale)
    {
        float elapsed = 0;
        Vector2 startPos = rect.anchoredPosition;
        float randomRot = Random.Range(-540f, 540f);

        // Giai đoạn 1: Bắn lên (QuadOut)
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float tOut = 1 - (1 - t) * (1 - t); // QuadOut formula

            rect.anchoredPosition = Vector2.Lerp(startPos, targetPos, tOut);
            rect.localScale = Vector3.Lerp(Vector3.zero, new Vector3(finalScale, finalScale, 1), tOut * 2);
            rect.localRotation = Quaternion.Euler(0, 0, t * randomRot);
            yield return null;
        }

        // Giai đoạn 2: Rơi xuống (QuadIn) và mờ dần
        elapsed = 0;
        float fallDuration = duration * 1.5f;
        Vector2 posBeforeFall = rect.anchoredPosition;
        Vector2 finalFallPos = new Vector2(posBeforeFall.x + Random.Range(-100, 100), -spawnArea.rect.height * 0.6f);

        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fallDuration;
            float tIn = t * t; // QuadIn formula

            rect.anchoredPosition = Vector2.Lerp(posBeforeFall, finalFallPos, tIn);
            img.color = new Color(img.color.r, img.color.g, img.color.b, 1 - tIn);
            yield return null;
        }

        Destroy(rect.gameObject);
    }
}