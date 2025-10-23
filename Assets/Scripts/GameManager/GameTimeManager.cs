using UnityEngine;
using R3;
using System;

/// <summary>
/// ゲームのタイムスケール（Time.timeScale）を管理するマネージャー
/// ポーズ、スローモーション、早送りなどの時間操作を一元管理
///
/// 使い方:
/// 1. GameManagerの参照に設定
/// 2. gameManager.StateManager経由でポーズ状態やタイムスケールを制御
/// </summary>
public class GameTimeManager : MonoBehaviour
{
    [Header("GameManager")]
    [SerializeField] private GameManager gameManager;

    [Header("ポーズ設定")]
    [Tooltip("ポーズ時のタイムスケール")]
    [SerializeField] private float pausedTimeScale = 0f;

    [Tooltip("通常時のタイムスケール")]
    [SerializeField] private float normalTimeScale = 1f;

    /// <summary>
    /// 詳細ログを有効にするかどうか（GameManagerから設定される）
    /// </summary>
    public static bool EnableVerboseLog { get; set; } = false;

    // R3購読用disposable
    private IDisposable disposable;

    // ポーズ前のタイムスケール（ポーズ解除時に復元するため）
    private float timeScaleBeforePause = 1f;

    void Start()
    {
        if (gameManager == null)
        {
            Debug.LogError($"GameTimeManager: GameManager参照が設定されていません。修正してください。");
            return;
        }

        // 初期タイムスケールを設定
        Time.timeScale = normalTimeScale;
        gameManager.StateManager.SetTimeScale(normalTimeScale);

        // ポーズ状態の購読
        var pauseSubscription = gameManager.StateManager.State.IsPaused.Subscribe(isPaused =>
        {
            OnPauseChanged(isPaused);
        });

        // タイムスケール変更の購読
        var timeScaleSubscription = gameManager.StateManager.State.CurrentTimeScale.Subscribe(timeScale =>
        {
            OnTimeScaleChanged(timeScale);
        });

        // disposable登録
        disposable = Disposable.Combine(
            pauseSubscription,
            timeScaleSubscription
        );
    }

    void OnDestroy()
    {
        disposable?.Dispose();

        // タイムスケールを通常に戻す
        Time.timeScale = normalTimeScale;
    }

    /// <summary>
    /// ポーズ状態が変化したときの処理
    /// </summary>
    void OnPauseChanged(bool isPaused)
    {
        if (isPaused)
        {
            // ポーズ前のタイムスケールを保存
            timeScaleBeforePause = Time.timeScale;

            // ポーズ時のタイムスケールを適用
            Time.timeScale = pausedTimeScale;
            gameManager.StateManager.SetTimeScale(pausedTimeScale);

            if (EnableVerboseLog)
            {
                Debug.Log($"GameTimeManager: ゲームをポーズしました。TimeScale: {Time.timeScale}");
            }
        }
        else
        {
            // ポーズ解除時は保存していたタイムスケールを復元
            Time.timeScale = timeScaleBeforePause;
            gameManager.StateManager.SetTimeScale(timeScaleBeforePause);

            if (EnableVerboseLog)
            {
                Debug.Log($"GameTimeManager: ポーズを解除しました。TimeScale: {Time.timeScale}");
            }
        }
    }

    /// <summary>
    /// タイムスケールが変化したときの処理
    /// </summary>
    void OnTimeScaleChanged(float timeScale)
    {
        // ポーズ中はタイムスケールを変更しない
        if (gameManager.StateManager.State.IsPaused.CurrentValue)
        {
            return;
        }

        // タイムスケールを適用
        Time.timeScale = timeScale;

        if (EnableVerboseLog)
        {
            Debug.Log($"GameTimeManager: タイムスケールを変更しました。TimeScale: {Time.timeScale}");
        }
    }

    /// <summary>
    /// ポーズをトグルする（外部から呼び出し可能）
    /// </summary>
    public void TogglePause()
    {
        gameManager.StateManager.TogglePause();
    }

    /// <summary>
    /// ゲームをポーズする（外部から呼び出し可能）
    /// </summary>
    public void Pause()
    {
        gameManager.StateManager.Pause();
    }

    /// <summary>
    /// ゲームのポーズを解除する（外部から呼び出し可能）
    /// </summary>
    public void Unpause()
    {
        gameManager.StateManager.Unpause();
    }

    /// <summary>
    /// タイムスケールを設定する（外部から呼び出し可能）
    /// </summary>
    public void SetTimeScale(float timeScale)
    {
        gameManager.StateManager.SetTimeScale(timeScale);
    }

    /// <summary>
    /// スローモーションを開始する
    /// </summary>
    /// <param name="slowMotionScale">スローモーションのタイムスケール（例: 0.5f = 半分の速度）</param>
    public void StartSlowMotion(float slowMotionScale)
    {
        if (gameManager.StateManager.State.IsPaused.CurrentValue)
        {
            Debug.LogWarning("GameTimeManager: ポーズ中はスローモーションを開始できません。");
            return;
        }

        gameManager.StateManager.SetTimeScale(Mathf.Clamp(slowMotionScale, 0f, 1f));

        if (EnableVerboseLog)
        {
            Debug.Log($"GameTimeManager: スローモーションを開始しました。TimeScale: {slowMotionScale}");
        }
    }

    /// <summary>
    /// 通常速度に戻す
    /// </summary>
    public void ResetToNormalSpeed()
    {
        gameManager.StateManager.SetTimeScale(normalTimeScale);

        if (EnableVerboseLog)
        {
            Debug.Log($"GameTimeManager: 通常速度に戻しました。TimeScale: {normalTimeScale}");
        }
    }

    public bool IsNormalSpeed => gameManager.StateManager.State.CurrentTimeScale. CurrentValue == normalTimeScale;
}
