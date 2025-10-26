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
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using R3;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameUIManager : MonoBehaviour
{
    IDisposable disposable;

    [Header("GameManager")]
    [SerializeField] private GameManager gameManager;

    [Header("UI")]

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


    [Header("ポーズメニュー関連")]
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private CanvasGroup pauseMenuCanvasGroup;

    [Tooltip("ポーズメニューのフェードイン時間（秒）")]
    [SerializeField] private float pauseMenuFadeInDuration = 0.2f;

    [Tooltip("ポーズメニューのフェードアウト時間（秒）")]
    [SerializeField] private float pauseMenuFadeOutDuration = 0.2f;

    [Header("ポーズメニューUI要素")]
    [Tooltip("上下カメラ操作方向反転Toggle")]
    [SerializeField] private Toggle cameraInvertYToggle;

    [Tooltip("マウス感度調整スライダー")]
    [SerializeField] private Slider mouseSensitivitySlider;

    [Tooltip("マウス感度表示テキスト")]
    [SerializeField] private TextMeshProUGUI mouseSensitivityText;

    [Tooltip("BGMボリューム調整スライダー")]
    [SerializeField] private Slider bgmVolumeSlider;

    [Tooltip("BGMボリューム表示テキスト")]
    [SerializeField] private TextMeshProUGUI bgmVolumeText;

    [Tooltip("サウンドエフェクトボリューム調整スライダー")]
    [SerializeField] private Slider seVolumeSlider;

    [Tooltip("サウンドエフェクトボリューム表示テキスト")]
    [SerializeField] private TextMeshProUGUI seVolumeText;

    [Tooltip("デフォルトに戻すボタン")]
    [SerializeField] private Button resetToDefaultButton;

    [Tooltip("再開ボタン")]
    [SerializeField] private Button resumeButton;

    private FPSCounter fpsCounter = new();

    /// <summary>現在実行中のメッセージフェード処理のキャンセレーショントークン</summary>
    private CancellationTokenSource messageFadeCts = null;

    /// <summary>現在実行中のポーズメニューフェード処理のキャンセレーショントークン</summary>
    private CancellationTokenSource pauseMenuFadeCts = null;

    /// <summary>ポーズメニューが表示されているかどうかを外部から参照可能にする（GameStateManager.IsPausedを参照）</summary>
    public bool IsPauseMenuVisible => gameManager?.StateManager?.State?.IsPaused?.CurrentValue ?? false;

    /// <summary>ゲーム設定</summary>
    private GameSettings gameSettings;


    void Start()
    {
        fpsCounterFrame?.SetActive(enableFpsCounter);

        // メッセージ表示枠を初期状態で非表示
        if (staticMessageCanvasGroup != null)
        {
            staticMessageCanvasGroup.alpha = 0f;
        }

        // ポーズメニューを初期状態で非表示
        if (pauseMenuCanvasGroup != null)
        {
            pauseMenuCanvasGroup.alpha = 0f;
        }
        if (pauseMenu != null)
        {
            pauseMenu.SetActive(false);
        }

        // ゲーム設定の初期化とロード
        gameSettings = new GameSettings();
        InitializePauseMenuUI();

        // gameManager.StateManagerのreactive property購読・更新

        // Coin変動購読
        var coinSubscriber = gameManager.StateManager.State.CurrentCoin.Subscribe(x =>
        {
            // Debug.Log($"Coin変動検知: {x}");
            int totalCoin = gameManager.StateManager.State.TotalCoinCount.CurrentValue;
            currentCoinText.SetText($"{x}/{totalCoin}");
        });

        // TotalCoinCount変動購読（コイン生成時に総数を更新）
        var totalCoinSubscriber = gameManager.StateManager.State.TotalCoinCount.Subscribe(totalCoin =>
        {
            // Debug.Log($"TotalCoin変動検知: {totalCoin}");
            int currentCoin = gameManager.StateManager.State.CurrentCoin.CurrentValue;
            currentCoinText.SetText($"{currentCoin}/{totalCoin}");
        });

        // HP変動購読
        var hitpointSubscriber = gameManager.StateManager.State.CurrentHp.Subscribe(x =>
        {
            // Debug.Log($"HP変動検知: {x}");
            UpdateHitpointGuage(x);
        });

        // ダメージイベント購読
        var damageSubscriber = gameManager.StateManager.State.OnDamageReceived.Subscribe(damageInfo =>
        {
            // Debug.Log($"GameUIManager: ダメージを受けました！ " +
            //           $"ダメージ量: {damageInfo.Damage}, " +
            //           $"ソース: {damageInfo.Source.name}, " +
            //           $"残りHP: {damageInfo.CurrentHp}");
            UpdateHitpointGuage(damageInfo.CurrentHp);
        });

        // ポーズ状態変動購読（ポーズメニュー表示制御）
        var pauseSubscriber = gameManager.StateManager.State.IsPaused.Subscribe(isPaused =>
        {
            if (isPaused)
            {
                ShowPauseMenuInternal();
            }
            else
            {
                HidePauseMenuInternal();
            }
        });

        // disposable登録
        disposable = Disposable.Combine(
            coinSubscriber,
            totalCoinSubscriber,
            hitpointSubscriber,
            damageSubscriber,
            pauseSubscriber
        );
    }

    void Update()
    {
        // FPS Counter更新
        if (fpsCounterFrame != null && fpsCounterFrame.activeSelf)
        {
            var result = fpsCounter.UpdateMeasure();
            fpsText.SetTextFormat("FPS:{0} (ave:{1}) ", result.fps, result.average);
        }
    }

    public void ToggleFpsCounterFrameActive()
    {
        fpsCounterFrame.SetActive(!fpsCounterFrame.activeSelf);
    }

    void OnDestroy()
    {
        disposable?.Dispose();
        messageFadeCts?.Cancel();
        messageFadeCts?.Dispose();
        pauseMenuFadeCts?.Cancel();
        pauseMenuFadeCts?.Dispose();
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
        messageFadeCts?.Cancel();
        messageFadeCts?.Dispose();
        messageFadeCts = new CancellationTokenSource();

        // フェードインを開始
        FadeInStaticMessageAsync(messageFadeCts.Token).Forget();
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
        messageFadeCts?.Cancel();
        messageFadeCts?.Dispose();
        messageFadeCts = new CancellationTokenSource();

        // フェードアウトを開始
        FadeOutStaticMessageAsync(messageFadeCts.Token).Forget();
    }

    /// <summary>
    /// 静的メッセージをフェードインさせる
    /// </summary>
    /// <param name="ct">キャンセルトークン</param>
    private async UniTaskVoid FadeInStaticMessageAsync(CancellationToken ct)
    {
        try
        {
            float elapsedTime = 0f;
            float startAlpha = staticMessageCanvasGroup.alpha;

            while (elapsedTime < staticMessageFadeInDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / staticMessageFadeInDuration);
                staticMessageCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, t);
                await UniTask.Yield(ct);
            }

            staticMessageCanvasGroup.alpha = 1f;
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            Debug.LogError($"GameUIManager: メッセージフェードイン中にエラーが発生しました: {ex}");
        }
    }

    /// <summary>
    /// 静的メッセージをフェードアウトさせる
    /// </summary>
    /// <param name="ct">キャンセルトークン</param>
    private async UniTaskVoid FadeOutStaticMessageAsync(CancellationToken ct)
    {
        try
        {
            float elapsedTime = 0f;
            float startAlpha = staticMessageCanvasGroup.alpha;

            while (elapsedTime < staticMessageFadeOutDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / staticMessageFadeOutDuration);
                staticMessageCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                await UniTask.Yield(ct);
            }

            staticMessageCanvasGroup.alpha = 0f;
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            Debug.LogError($"GameUIManager: メッセージフェードアウト中にエラーが発生しました: {ex}");
        }
    }

    /// <summary>
    /// ポーズメニューを表示する（内部用：IsPaused購読から呼ばれる）
    /// </summary>
    private void ShowPauseMenuInternal()
    {
        if (pauseMenu == null || pauseMenuCanvasGroup == null)
        {
            Debug.LogWarning("GameUIManager: pauseMenuまたはpauseMenuCanvasGroupが設定されていません。");
            return;
        }

        // ポーズメニューをアクティブ化
        pauseMenu.SetActive(true);

        // 既存のフェードを停止
        pauseMenuFadeCts?.Cancel();
        pauseMenuFadeCts?.Dispose();
        pauseMenuFadeCts = new CancellationTokenSource();

        // フェードインを開始
        FadeInPauseMenuAsync(pauseMenuFadeCts.Token).Forget();
    }

    /// <summary>
    /// ポーズメニューを非表示にする（内部用：IsPaused購読から呼ばれる）
    /// </summary>
    private void HidePauseMenuInternal()
    {
        if (pauseMenu == null || pauseMenuCanvasGroup == null)
        {
            return;
        }

        // 既存のフェードを停止
        pauseMenuFadeCts?.Cancel();
        pauseMenuFadeCts?.Dispose();
        pauseMenuFadeCts = new CancellationTokenSource();

        // フェードアウトを開始
        FadeOutPauseMenuAsync(pauseMenuFadeCts.Token).Forget();
    }

    /// <summary>
    /// ポーズメニューの表示状態をトグルする（GameStateManager.TogglePause()を呼ぶ）
    /// </summary>
    public void TogglePauseMenu()
    {
        gameManager.StateManager.TogglePause();
    }

    /// <summary>
    /// ポーズメニューをフェードインさせる
    /// </summary>
    /// <param name="ct">キャンセルトークン</param>
    private async UniTaskVoid FadeInPauseMenuAsync(CancellationToken ct)
    {
        try
        {
            float elapsedTime = 0f;
            float startAlpha = pauseMenuCanvasGroup.alpha;

            while (elapsedTime < pauseMenuFadeInDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / pauseMenuFadeInDuration);
                pauseMenuCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, t);
                await UniTask.Yield(ct);
            }

            pauseMenuCanvasGroup.alpha = 1f;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Debug.LogError($"GameUIManager: ポーズメニューフェードイン中にエラーが発生しました: {ex}");
        }
    }

    /// <summary>
    /// ポーズメニューをフェードアウトさせる
    /// </summary>
    /// <param name="ct">キャンセルトークン</param>
    private async UniTaskVoid FadeOutPauseMenuAsync(CancellationToken ct)
    {
        try
        {
            float elapsedTime = 0f;
            float startAlpha = pauseMenuCanvasGroup.alpha;

            while (elapsedTime < pauseMenuFadeOutDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / pauseMenuFadeOutDuration);
                pauseMenuCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                await UniTask.Yield(ct);
            }

            pauseMenuCanvasGroup.alpha = 0f;

            // フェードアウト完了後、GameObjectを非アクティブ化
            pauseMenu.SetActive(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Debug.LogError($"GameUIManager: ポーズメニューフェードアウト中にエラーが発生しました: {ex}");
        }
    }

    #region ポーズメニュー設定UI

    /// <summary>
    /// ポーズメニューUIの初期化
    /// </summary>
    private void InitializePauseMenuUI()
    {
        // スライダーの範囲設定
        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.minValue = 10f;   // 10%
            mouseSensitivitySlider.maxValue = 300f;  // 300%
            mouseSensitivitySlider.wholeNumbers = true;  // 整数値のみ
            mouseSensitivitySlider.value = gameSettings.MouseSensitivity;
            mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
        }

        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.minValue = 0f;
            bgmVolumeSlider.maxValue = 1f;
            bgmVolumeSlider.value = gameSettings.BGMVolume;
            bgmVolumeSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
        }

        if (seVolumeSlider != null)
        {
            seVolumeSlider.minValue = 0f;
            seVolumeSlider.maxValue = 1f;
            seVolumeSlider.value = gameSettings.SEVolume;
            seVolumeSlider.onValueChanged.AddListener(OnSEVolumeChanged);
        }

        // Toggleの初期化
        if (cameraInvertYToggle != null)
        {
            cameraInvertYToggle.isOn = gameSettings.CameraInvertY;
            cameraInvertYToggle.onValueChanged.AddListener(OnCameraInvertYChanged);
        }

        // ボタンのイベント設定
        if (resetToDefaultButton != null)
        {
            resetToDefaultButton.onClick.AddListener(OnResetToDefaultClicked);
        }

        if (resumeButton != null)
        {
            resumeButton.onClick.AddListener(OnResumeClicked);
        }

        // 初期値の表示更新
        UpdateSettingsUI();
    }

    /// <summary>
    /// 設定UIの表示を更新
    /// </summary>
    private void UpdateSettingsUI()
    {
        // マウス感度の表示更新
        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.value = gameSettings.MouseSensitivity;
        }
        if (mouseSensitivityText != null)
        {
            mouseSensitivityText.text = $"{gameSettings.MouseSensitivity:F0}%";
        }

        // BGMボリュームの表示更新
        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.value = gameSettings.BGMVolume;
        }
        if (bgmVolumeText != null)
        {
            bgmVolumeText.text = $"{gameSettings.BGMVolume * 100f:F0}%";
        }

        // サウンドエフェクトボリュームの表示更新
        if (seVolumeSlider != null)
        {
            seVolumeSlider.value = gameSettings.SEVolume;
        }
        if (seVolumeText != null)
        {
            seVolumeText.text = $"{gameSettings.SEVolume * 100f:F0}%";
        }

        // カメラ反転の表示更新
        if (cameraInvertYToggle != null)
        {
            cameraInvertYToggle.isOn = gameSettings.CameraInvertY;
        }

        // カメラ設定をCharacterTrackerに反映
        ApplyCameraSettings();

        // サウンド設定をSoundManagerに反映
        ApplySoundSettings();
    }

    /// <summary>
    /// カメラ設定をCharacterTrackerに反映
    /// </summary>
    private void ApplyCameraSettings()
    {
        if (gameManager?.CharacterTracker != null)
        {
            gameManager.CharacterTracker.SetCameraInvertY(gameSettings.CameraInvertY);
            gameManager.CharacterTracker.SetMouseSensitivity(gameSettings.MouseSensitivity / 100f);
        }
    }

    /// <summary>
    /// サウンド設定をSoundManagerに反映
    /// </summary>
    private void ApplySoundSettings()
    {
        if (gameManager?.SoundManager != null)
        {
            gameManager.SoundManager.SetBGMVolume(gameSettings.BGMVolume);
            gameManager.SoundManager.SetSEVolume(gameSettings.SEVolume);
        }
    }

    /// <summary>
    /// マウス感度スライダーが変更されたときの処理
    /// </summary>
    private void OnMouseSensitivityChanged(float value)
    {
        // 10%刻みに丸める
        float roundedValue = Mathf.Round(value / 10f) * 10f;
        gameSettings.SetMouseSensitivity(roundedValue);

        // 表示を更新
        if (mouseSensitivityText != null)
        {
            mouseSensitivityText.text = $"{roundedValue:F0}%";
        }

        // カメラに反映
        ApplyCameraSettings();
    }

    /// <summary>
    /// BGMボリュームスライダーが変更されたときの処理
    /// </summary>
    private void OnBGMVolumeChanged(float value)
    {
        gameSettings.SetBGMVolume(value);

        // 表示を更新
        if (bgmVolumeText != null)
        {
            bgmVolumeText.text = $"{value * 100f:F0}%";
        }

        // SoundManagerに反映
        if (gameManager?.SoundManager != null)
        {
            gameManager.SoundManager.SetBGMVolume(value);
        }
    }

    /// <summary>
    /// サウンドエフェクトボリュームスライダーが変更されたときの処理
    /// </summary>
    private void OnSEVolumeChanged(float value)
    {
        gameSettings.SetSEVolume(value);

        // 表示を更新
        if (seVolumeText != null)
        {
            seVolumeText.text = $"{value * 100f:F0}%";
        }

        // SoundManagerに反映
        if (gameManager?.SoundManager != null)
        {
            gameManager.SoundManager.SetSEVolume(value);
        }
    }

    /// <summary>
    /// カメラ反転Toggleが変更されたときの処理
    /// </summary>
    private void OnCameraInvertYChanged(bool value)
    {
        gameSettings.SetCameraInvertY(value);

        // カメラに反映
        ApplyCameraSettings();
    }

    /// <summary>
    /// デフォルトに戻すボタンがクリックされたときの処理
    /// </summary>
    private void OnResetToDefaultClicked()
    {
        gameSettings.ResetToDefault();
        UpdateSettingsUI();
    }

    /// <summary>
    /// 再開ボタンがクリックされたときの処理
    /// </summary>
    private void OnResumeClicked()
    {
        // ポーズ解除（UIは自動的に非表示になる）
        gameManager.TimeManager.Unpause();

        // マウスカーソルをロック
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>
    /// ゲーム設定を取得
    /// </summary>
    public GameSettings GetGameSettings()
    {
        return gameSettings;
    }

    #endregion

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