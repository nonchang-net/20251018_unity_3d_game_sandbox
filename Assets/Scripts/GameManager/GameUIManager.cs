/*
UI管理クラス

- とりあえずコイン枚数、残りライフ表示とFPSカウンター表示を管理
- UserDataに応じた表示制御は、すべてReactive Property購読のみで管理する

*/

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Linq;
using Cysharp.Text;
using R3;

public class GameUIManager : MonoBehaviour
{
    IDisposable disposable;

    [Tooltip("ハート表示配列")]
    [SerializeField] private Image[] hearts;


    [Tooltip("コイン数表示テキスト")]
    [SerializeField] private TextMeshProUGUI currentCoinText;


    [Tooltip("FPS表示")]
    [SerializeField] private bool enableFpsCounter;
    [SerializeField] private GameObject fpsCounterFrame;
    [SerializeField] private TextMeshProUGUI fpsText;

    [Tooltip("メッセージ表示枠")]
    [SerializeField] private CanvasGroup staticMessageCanvasGroup;
    [SerializeField] private TextMeshProUGUI staticMessageTMP;

    [Tooltip("メッセージのフェードイン時間（秒）")]
    [SerializeField] private float staticMessageFadeInDuration = 0.3f;

    [Tooltip("メッセージのフェードアウト時間（秒）")]
    [SerializeField] private float staticMessageFadeOutDuration = 0.3f;

    [Header("入力設定")]
    [Tooltip("FPS表示切り替えキー")]
    [SerializeField] private KeyCode switchFPSKey = KeyCode.F;

    private FPSCounter fpsCounter = new();

    /// <summary>現在実行中のメッセージフェードコルーチン</summary>
    private Coroutine currentMessageFadeCoroutine = null;


    void Start()
    {
        fpsCounterFrame?.SetActive(enableFpsCounter);

        // メッセージ表示枠を初期状態で非表示
        if (staticMessageCanvasGroup != null)
        {
            staticMessageCanvasGroup.alpha = 0f;
        }

        // UserDataManagerのreactive property購読・更新

        // Coin変動購読
        var coinSubscriber = UserDataManager.Data.CurrentCoin.Subscribe(x =>
        {
            // Debug.Log($"Coin変動検知: {x}");
            int totalCoin = UserDataManager.Data.TotalCoinCount.CurrentValue;
            currentCoinText.SetText($"{x}/{totalCoin}");
        });

        // TotalCoinCount変動購読（コイン生成時に総数を更新）
        var totalCoinSubscriber = UserDataManager.Data.TotalCoinCount.Subscribe(totalCoin =>
        {
            // Debug.Log($"TotalCoin変動検知: {totalCoin}");
            int currentCoin = UserDataManager.Data.CurrentCoin.CurrentValue;
            currentCoinText.SetText($"{currentCoin}/{totalCoin}");
        });

        // HP変動購読
        var hitpointSubscriber = UserDataManager.Data.CurrentHp.Subscribe(x =>
        {
            // Debug.Log($"HP変動検知: {x}");
            UpdateHitpointGuage(x);
        });

        // ダメージイベント購読
        var damageSubscriber = UserDataManager.Data.OnDamageReceived.Subscribe(damageInfo =>
        {
            // Debug.Log($"GameUIManager: ダメージを受けました！ " +
            //           $"ダメージ量: {damageInfo.Damage}, " +
            //           $"ソース: {damageInfo.Source.name}, " +
            //           $"残りHP: {damageInfo.CurrentHp}");
            UpdateHitpointGuage(damageInfo.CurrentHp);
        });

        // disposable登録
        disposable = Disposable.Combine(
            coinSubscriber,
            totalCoinSubscriber,
            hitpointSubscriber,
            damageSubscriber
        );
    }

    void Update()
    {
        // FPS切り替え入力を検出
        if (Input.GetKeyDown(switchFPSKey))
        {
            fpsCounterFrame.SetActive(!fpsCounterFrame.activeSelf);
        }

        // FPS Counter更新
        if (fpsCounterFrame != null && fpsCounterFrame.activeSelf)
        {
            var result = fpsCounter.UpdateMeasure();
            fpsText.SetTextFormat("FPS:{0} (ave:{1}) ", result.fps, result.average);
        }
    }

    void OnDestroy()
    {
        disposable.Dispose();
    }

    /// <summary>
    /// HP表示更新
    /// </summary>
    /// <param name="newHp"></param>
    void UpdateHitpointGuage(int newHp)
    {
        foreach (var heart in hearts) heart.gameObject.SetActive(false);
        if (newHp < 0) return;
        if (newHp >= 1 ) hearts[0].gameObject.SetActive(true);
        if (newHp >= 2 ) hearts[1].gameObject.SetActive(true);
        if (newHp >= 3 ) hearts[2].gameObject.SetActive(true);
    }

    /// <summary>
    /// 静的メッセージを表示する
    /// </summary>
    /// <param name="message">表示するメッセージ</param>
    public void ShowStaticMessage(string message)
    {
        if (staticMessageCanvasGroup == null || staticMessageTMP == null)
        {
            Debug.LogWarning("GameUIManager: staticMessageCanvasGroupまたはstaticMessageTMPが設定されていません。");
            return;
        }

        // テキスト内容を更新
        staticMessageTMP.text = message;

        // 既存のフェードを停止
        if (currentMessageFadeCoroutine != null)
        {
            StopCoroutine(currentMessageFadeCoroutine);
        }

        // フェードインを開始
        currentMessageFadeCoroutine = StartCoroutine(FadeInStaticMessage());
    }

    /// <summary>
    /// 静的メッセージを非表示にする
    /// </summary>
    public void HideStaticMessage()
    {
        if (staticMessageCanvasGroup == null)
        {
            return;
        }

        // 既存のフェードを停止
        if (currentMessageFadeCoroutine != null)
        {
            StopCoroutine(currentMessageFadeCoroutine);
        }

        // フェードアウトを開始
        currentMessageFadeCoroutine = StartCoroutine(FadeOutStaticMessage());
    }

    /// <summary>
    /// 静的メッセージをフェードインさせるコルーチン
    /// </summary>
    private System.Collections.IEnumerator FadeInStaticMessage()
    {
        float elapsedTime = 0f;
        float startAlpha = staticMessageCanvasGroup.alpha;

        while (elapsedTime < staticMessageFadeInDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / staticMessageFadeInDuration);
            staticMessageCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, t);
            yield return null;
        }

        staticMessageCanvasGroup.alpha = 1f;
        currentMessageFadeCoroutine = null;
    }

    /// <summary>
    /// 静的メッセージをフェードアウトさせるコルーチン
    /// </summary>
    private System.Collections.IEnumerator FadeOutStaticMessage()
    {
        float elapsedTime = 0f;
        float startAlpha = staticMessageCanvasGroup.alpha;

        while (elapsedTime < staticMessageFadeOutDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / staticMessageFadeOutDuration);
            staticMessageCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            yield return null;
        }

        staticMessageCanvasGroup.alpha = 0f;
        currentMessageFadeCoroutine = null;
    }

}


public class FPSCounter
{
    const float MeasureInterval = 1.0f;
    const int AveNum = 10;
    
    float nextTime;
    int frameCount, measureCount;
    
    float fps, aveFps;
    readonly float[] fpsHistory = new float[AveNum];

    public (float fps, float average) UpdateMeasure()
    {
        frameCount++;
        if (nextTime > Time.fixedTime) return (fps, aveFps);
        
        fps = frameCount / MeasureInterval;
        fpsHistory[measureCount] = fps;
        aveFps = fpsHistory.Average();

        nextTime = Time.fixedTime + MeasureInterval;
        frameCount = 0;

        measureCount++;
        if (measureCount >= AveNum) measureCount = 0;

        return (fps, aveFps);
    }
}