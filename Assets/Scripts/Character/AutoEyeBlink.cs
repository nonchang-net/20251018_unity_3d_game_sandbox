using UnityEngine;

/// <summary>
/// SkinnedMeshRendererの"Fcl_EYE_Close"ブレンドシェイプを使用して
/// ランダムな間隔で自動的に瞬きを行うコンポーネント
/// </summary>
public class AutoEyeBlink : MonoBehaviour
{
    [Header("SkinnedMeshRenderer設定")]
    [Tooltip("目のブレンドシェイプを持つSkinnedMeshRenderer")]
    [SerializeField] private SkinnedMeshRenderer skinnedMeshRenderer;

    [Tooltip("対象Blend Shape名")]
    [SerializeField] private string blendShapeName = "Fcl_EYE_Close";

    [Header("瞬き設定")]
    [Tooltip("瞬きの最小間隔（秒）")]
    [SerializeField] private float minBlinkInterval = 2f;
    
    [Tooltip("瞬きの最大間隔（秒）")]
    [SerializeField] private float maxBlinkInterval = 6f;
    
    [Tooltip("目を閉じる速度")]
    [SerializeField] private float closeSpeed = 10f;
    
    [Tooltip("目を開ける速度")]
    [SerializeField] private float openSpeed = 8f;

    [Header("デバッグ")]
    [Tooltip("デバッグログを表示する")]
    [SerializeField] private bool showDebugLog = false;

    [Tooltip("詳細なデバッグ情報を表示する")]
    [SerializeField] private bool showVerboseLog = false;

    [Header("ランタイム情報（読み取り専用）")]
    [SerializeField] [Tooltip("現在のブレンドシェイプ値")] private float debugCurrentValue = 0f;
    [SerializeField] [Tooltip("ブレンドシェイプインデックス")] private int debugBlendShapeIndex = -1;
    [SerializeField] [Tooltip("現在瞬き中か")] private bool debugIsBlinking = false;

    private int blendShapeIndex = -1;
    private float nextBlinkTime;
    private float currentBlinkValue = 0f;
    private bool isBlinking = false;
    private bool isClosing = false;


    void Start()
    {
        // SkinnedMeshRendererが未設定の場合、自動で取得を試みる
        if (skinnedMeshRenderer == null)
        {
            skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
            
            if (skinnedMeshRenderer == null)
            {
                skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            }
        }

        // ブレンドシェイプのインデックスを取得
        if (skinnedMeshRenderer != null)
        {
            blendShapeIndex = skinnedMeshRenderer.sharedMesh.GetBlendShapeIndex(blendShapeName);
            
            if (blendShapeIndex == -1)
            {
                Debug.LogError($"ブレンドシェイプ '{blendShapeName}' が見つかりませんでした。", this);
                enabled = false;
                return;
            }

            debugBlendShapeIndex = blendShapeIndex;

            if (showDebugLog)
            {
                Debug.Log($"AutoEyeBlink初期化完了: ブレンドシェイプインデックス = {blendShapeIndex}", this);

                // すべてのブレンドシェイプ名をログ出力（デバッグ用）
                if (showVerboseLog)
                {
                    Mesh mesh = skinnedMeshRenderer.sharedMesh;
                    Debug.Log($"ブレンドシェイプ総数: {mesh.blendShapeCount}", this);
                    for (int i = 0; i < mesh.blendShapeCount; i++)
                    {
                        string name = mesh.GetBlendShapeName(i);
                        float value = skinnedMeshRenderer.GetBlendShapeWeight(i);
                        Debug.Log($"  [{i}] {name} = {value}", this);
                    }
                }
            }
        }
        else
        {
            Debug.LogError("SkinnedMeshRendererが見つかりませんでした。", this);
            enabled = false;
            return;
        }

        // 初回の瞬き時間を設定
        ScheduleNextBlink();
    }

    void Update()
    {
        if (blendShapeIndex == -1) return;

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
    }

    /// <summary>
    /// LateUpdate で最終的なブレンドシェイプ値を適用
    /// 他のコンポーネントによる上書きを防ぐため
    /// </summary>
    void LateUpdate()
    {
        if (blendShapeIndex == -1) return;

        // デバッグ情報を更新
        debugCurrentValue = currentBlinkValue;
        debugIsBlinking = isBlinking;

        // ブレンドシェイプの値を設定（他のコンポーネントより後に実行）
        if (isBlinking || currentBlinkValue > 0f)
        {
            skinnedMeshRenderer.SetBlendShapeWeight(blendShapeIndex, currentBlinkValue);

            if (showVerboseLog && isBlinking)
            {
                float actualValue = skinnedMeshRenderer.GetBlendShapeWeight(blendShapeIndex);
                Debug.Log($"LateUpdate: 設定値={currentBlinkValue:F2}, 実際の値={actualValue:F2}", this);
            }
        }
    }

    /// <summary>
    /// 瞬きを開始
    /// </summary>
    private void StartBlink()
    {
        isBlinking = true;
        isClosing = true;

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
            currentBlinkValue += closeSpeed * Time.deltaTime * 100f;
            
            if (currentBlinkValue >= 100f)
            {
                currentBlinkValue = 100f;
                isClosing = false;
            }
        }
        else
        {
            // 目を開ける
            currentBlinkValue -= openSpeed * Time.deltaTime * 100f;
            
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

        // ブレンドシェイプの値を計算（LateUpdateで実際に適用）
        // skinnedMeshRenderer.SetBlendShapeWeight(blendShapeIndex, currentBlinkValue);
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
    }
}