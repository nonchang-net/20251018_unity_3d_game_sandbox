using UniVRM10;
using UnityEngine;

/// <summary>
/// VRM1.0モデル専用の自動瞬きコンポーネント
/// Vrm10Instanceの"blink"エクスプレッションを使用してランダムな間隔で自動的に瞬きを行います
/// </summary>
public class AutoEyeBlinkForVRM : MonoBehaviour
{
    [Header("VRM設定")]
    [Tooltip("VRM1.0のインスタンス（未設定の場合は自動検索）")]
    [SerializeField] private Vrm10Instance vrmInstance;

    [Header("瞬き設定")]
    [Tooltip("瞬きの最小間隔（秒）")]
    [SerializeField] private float minBlinkInterval = 2f;

    [Tooltip("瞬きの最大間隔（秒）")]
    [SerializeField] private float maxBlinkInterval = 6f;

    [Tooltip("目を閉じる速度")]
    [SerializeField] private float closeSpeed = 10f;

    [Tooltip("目を開ける速度")]
    [SerializeField] private float openSpeed = 8f;

    [Tooltip("目を完全に閉じている時間（秒）")]
    [SerializeField] private float closedDuration = 0.1f;

    [Header("デバッグ")]
    [Tooltip("デバッグログを表示する")]
    [SerializeField] private bool showDebugLog;

    [Tooltip("詳細なデバッグ情報を表示する")]
    [SerializeField] private bool showVerboseLog;

    [Header("ランタイム情報（読み取り専用）")]
    [SerializeField] [Tooltip("現在のブレンドシェイプ値")] private float debugCurrentValue = 0f;
    [SerializeField] [Tooltip("現在瞬き中か")] private bool debugIsBlinking = false;

    private ExpressionKey blinkKey;
    private float nextBlinkTime;
    private float currentBlinkValue = 0f;
    private bool isBlinking = false;
    private bool isClosing = false;
    private float closedTimer = 0f;

    void Start()
    {
        // Vrm10Instanceが未設定の場合、自動で取得を試みる
        vrmInstance ??= GetComponent<Vrm10Instance>();
        vrmInstance ??= GetComponentInChildren<Vrm10Instance>();
        vrmInstance ??= FindFirstObjectByType<Vrm10Instance>();

        if (vrmInstance == null)
        {
            Debug.LogError("AutoEyeBlinkForVRM: Vrm10Instanceが見つかりませんでした。", this);
            enabled = false;
            return;
        }

        if (vrmInstance.Runtime?.Expression == null)
        {
            Debug.LogError("AutoEyeBlinkForVRM: VRM Runtime Expressionが初期化されていません。", this);
            enabled = false;
            return;
        }

        // blinkエクスプレッションのキーを作成
        blinkKey = ExpressionKey.CreateFromPreset(ExpressionPreset.blink);

        if (showDebugLog)
        {
            Debug.Log($"AutoEyeBlinkForVRM初期化完了: VRM={vrmInstance.name}", this);

            if (showVerboseLog)
            {
                // すべてのエクスプレッションをログ出力
                var expressions = vrmInstance.Runtime.Expression;
                Debug.Log($"利用可能なエクスプレッション:", this);
                foreach (var key in expressions.ExpressionKeys)
                {
                    var weight = expressions.GetWeight(key);
                    Debug.Log($"  {key.Name} = {weight}", this);
                }
            }
        }

        // 初回の瞬き時間を設定
        ScheduleNextBlink();
    }

    void Update()
    {
        if (vrmInstance == null || vrmInstance.Runtime?.Expression == null) return;

        // 瞬きのタイミングチェック
        if (!isBlinking && Time.time >= nextBlinkTime)
        {
            StartBlink();
        }

        // 瞬き処理
        if (isBlinking)
        {
            ProcessBlink();
        }

        // デバッグ情報を更新
        debugCurrentValue = currentBlinkValue;
        debugIsBlinking = isBlinking;
    }

    /// <summary>
    /// LateUpdate で最終的なエクスプレッション値を適用
    /// 他のコンポーネントによる上書きを防ぐため
    /// </summary>
    void LateUpdate()
    {
        if (vrmInstance == null || vrmInstance.Runtime?.Expression == null) return;

        // エクスプレッション値を常に設定（他のコンポーネントによる上書きを防ぐ）
        vrmInstance.Runtime.Expression.SetWeight(blinkKey, currentBlinkValue);

        if (showVerboseLog && isBlinking)
        {
            float actualValue = vrmInstance.Runtime.Expression.GetWeight(blinkKey);
            Debug.Log($"LateUpdate: 設定値={currentBlinkValue:F2}, 実際の値={actualValue:F2}, 状態={GetBlinkState()}", this);
        }
    }

    /// <summary>
    /// デバッグ用：現在の瞬き状態を取得
    /// </summary>
    private string GetBlinkState()
    {
        if (!isBlinking) return "待機中";
        if (isClosing && currentBlinkValue < 1f) return "閉じている";
        if (isClosing && currentBlinkValue >= 1f) return "閉じた状態維持";
        return "開けている";
    }

    /// <summary>
    /// 瞬きを開始
    /// </summary>
    private void StartBlink()
    {
        isBlinking = true;
        isClosing = true;
        closedTimer = 0f;

        if (showDebugLog)
        {
            Debug.Log("瞬き開始", this);
        }
    }

    /// <summary>
    /// 瞬き処理
    /// </summary>
    private void ProcessBlink()
    {
        if (isClosing)
        {
            // 目を閉じる
            if (currentBlinkValue < 1f)
            {
                currentBlinkValue += closeSpeed * Time.deltaTime;

                if (currentBlinkValue >= 1f)
                {
                    currentBlinkValue = 1f;
                    closedTimer = 0f; // タイマーをリセット

                    if (showVerboseLog)
                    {
                        Debug.Log($"完全に閉じた。{closedDuration:F2}秒待機開始", this);
                    }
                }
            }
            else
            {
                // 完全に閉じた状態で待機
                closedTimer += Time.deltaTime;

                if (showVerboseLog)
                {
                    Debug.Log($"閉じた状態維持中: {closedTimer:F2} / {closedDuration:F2}秒", this);
                }

                if (closedTimer >= closedDuration)
                {
                    isClosing = false;

                    if (showDebugLog)
                    {
                        Debug.Log($"目を閉じた状態を{closedDuration:F2}秒維持、開け始めます", this);
                    }
                }
            }
        }
        else
        {
            // 目を開ける
            currentBlinkValue -= openSpeed * Time.deltaTime;

            if (showVerboseLog)
            {
                Debug.Log($"目を開けている: currentBlinkValue={currentBlinkValue:F2}", this);
            }

            if (currentBlinkValue <= 0f)
            {
                currentBlinkValue = 0f;
                isBlinking = false;
                ScheduleNextBlink();

                if (showDebugLog)
                {
                    Debug.Log("瞬き終了", this);
                }
            }
        }
    }

    /// <summary>
    /// 次の瞬きをスケジュール
    /// </summary>
    private void ScheduleNextBlink()
    {
        float interval = Random.Range(minBlinkInterval, maxBlinkInterval);
        nextBlinkTime = Time.time + interval;

        if (showDebugLog)
        {
            Debug.Log($"次の瞬きまで: {interval:F2}秒", this);
        }
    }

    /// <summary>
    /// 手動で瞬きを実行
    /// </summary>
    public void TriggerBlink()
    {
        if (!isBlinking)
        {
            StartBlink();
        }
    }

    /// <summary>
    /// 瞬きを一時停止/再開
    /// </summary>
    public void SetBlinkEnabled(bool enabled)
    {
        this.enabled = enabled;

        if (enabled)
        {
            ScheduleNextBlink();
        }
        else
        {
            // 無効化時は目を開ける
            if (vrmInstance != null && vrmInstance.Runtime?.Expression != null)
            {
                vrmInstance.Runtime.Expression.SetWeight(blinkKey, 0f);
            }
            currentBlinkValue = 0f;
            isBlinking = false;
        }
    }

    /// <summary>
    /// 現在の瞬き値を取得
    /// </summary>
    public float GetBlinkValue()
    {
        return currentBlinkValue;
    }

    void OnDisable()
    {
        // コンポーネント無効化時に目を開ける
        if (vrmInstance != null && vrmInstance.Runtime?.Expression != null)
        {
            vrmInstance.Runtime.Expression.SetWeight(blinkKey, 0f);
        }
    }

    // インスペクタでリアルタイムに値を確認するため
    void OnValidate()
    {
        if (minBlinkInterval < 0.5f)
        {
            minBlinkInterval = 0.5f;
        }

        if (maxBlinkInterval < minBlinkInterval)
        {
            maxBlinkInterval = minBlinkInterval + 1f;
        }

        if (closeSpeed < 1f)
        {
            closeSpeed = 1f;
        }

        if (openSpeed < 1f)
        {
            openSpeed = 1f;
        }

        if (closedDuration < 0f)
        {
            closedDuration = 0f;
        }
    }
}
