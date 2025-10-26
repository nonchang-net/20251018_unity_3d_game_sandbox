using UnityEngine;

/// <summary>
/// 複数のポイント間を直線的に移動するコンポーネント
/// - プラットフォームなどの移動オブジェクトに使用
/// - ポイント間を直線的に移動（線形補間）
/// - 繰り返しモード（Once, Loop, PingPong Loop）
/// - イージング機能（Linear, Ease In, Ease Out, Ease InOut）
/// </summary>
public class MovingCurve : MonoBehaviour
{
    /// <summary>
    /// 繰り返しモード
    /// </summary>
    public enum LoopMode
    {
        Once,           // 一度だけ移動して停止
        Loop,           // 最初に戻ってループ
        PingPongLoop    // 往復ループ
    }

    /// <summary>
    /// イージングタイプ
    /// </summary>
    public enum EasingType
    {
        Linear,     // 線形（イージングなし）
        EaseIn,     // 加速
        EaseOut,    // 減速
        EaseInOut   // 加速→減速
    }

    [Header("移動対象")]
    [Tooltip("移動させるTransform（未設定の場合は自分自身を移動）")]
    [SerializeField] private Transform targetTransform;

    [Header("パス設定")]
    [Tooltip("移動経路のポイント（最低2つ必要）")]
    [SerializeField] private Transform[] pathPoints;

    [Tooltip("繰り返しモード")]
    [SerializeField] private LoopMode loopMode = LoopMode.Once;

    [Header("移動速度設定")]
    [Tooltip("移動速度（単位/秒）")]
    [SerializeField] private float moveSpeed = 2f;

    [Header("待機設定")]
    [Tooltip("パスの端に到達した際の待機時間（秒）※PingPong Loopモード時のみ")]
    [SerializeField] private float waitTimeAtEnd = 1f;

    [Header("イージング設定")]
    [Tooltip("イージングタイプ")]
    [SerializeField] private EasingType easingType = EasingType.Linear;

    [Tooltip("PingPongLoop時、戻る際にイージングを反転するか")]
    [SerializeField] private bool invertEasingWhenPingPongBack = false;

    [Header("初期位置設定")]
    [Tooltip("ゲーム開始時のパス上の位置（0.0～1.0）")]
    [SerializeField][Range(0f, 1f)] private float initialPosition = 0f;

    [Header("Gizmo設定")]
    [Tooltip("Gizmoを表示するか")]
    [SerializeField] private bool showGizmos = true;

    [Tooltip("パスの色")]
    [SerializeField] private Color pathColor = Color.green;

    [Tooltip("初期位置マーカーの色")]
    [SerializeField] private Color initialPositionColor = Color.yellow;

    // 現在のパス上の位置（0.0～1.0）
    private float currentPathPosition = 0f;

    // パスの総距離
    private float totalPathLength = 0f;

    // パスポイントの位置情報のコピー（Start()時に保存）
    private Vector3[] pathPositions;

    // 移動方向（1: 順方向, -1: 逆方向）※PingPongLoopモード時に使用
    private int direction = 1;

    // 待機中フラグ※PingPongLoopモード時に使用
    private bool isWaiting = false;

    // 待機タイマー※PingPongLoopモード時に使用
    private float waitTimer = 0f;

    void Start()
    {
        // targetTransformが未設定の場合は自分自身を使用
        if (targetTransform == null)
        {
            targetTransform = transform;
        }

        // パスポイントのチェック
        if (pathPoints == null || pathPoints.Length < 2)
        {
            Debug.LogError("MovingCurve: パスポイントが2つ以上必要です。");
            enabled = false;
            return;
        }

        // パスポイントの位置情報をコピー（移動対象自身がパスに含まれていても問題ないように）
        pathPositions = new Vector3[pathPoints.Length];
        for (int i = 0; i < pathPoints.Length; i++)
        {
            if (pathPoints[i] != null)
            {
                pathPositions[i] = pathPoints[i].position;
            }
        }

        // パスの総距離を計算
        CalculateTotalPathLength();

        // 初期位置を設定
        currentPathPosition = initialPosition;
        UpdatePosition(currentPathPosition);
    }

    void FixedUpdate()
    {
        // 待機中の処理（PingPongLoopモード時）
        if (isWaiting)
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= waitTimeAtEnd)
            {
                isWaiting = false;
                waitTimer = 0f;
                direction *= -1; // 方向を反転
            }
            return;
        }

        // 移動処理
        float deltaPosition = (moveSpeed * Time.deltaTime) / totalPathLength * direction;
        currentPathPosition += deltaPosition;

        // パスの端到達チェック（ループモード別）
        switch (loopMode)
        {
            case LoopMode.Once:
                // 一度だけ移動: 端で停止
                if (currentPathPosition >= 1f)
                {
                    currentPathPosition = 1f;
                    UpdatePosition(currentPathPosition);
                    enabled = false; // 移動を停止
                    return;
                }
                else if (currentPathPosition <= 0f)
                {
                    currentPathPosition = 0f;
                    UpdatePosition(currentPathPosition);
                    enabled = false; // 移動を停止
                    return;
                }
                break;

            case LoopMode.Loop:
                // ループ: 0～1の範囲でループ
                if (currentPathPosition > 1f)
                {
                    currentPathPosition -= 1f;
                }
                else if (currentPathPosition < 0f)
                {
                    currentPathPosition += 1f;
                }
                break;

            case LoopMode.PingPongLoop:
                // 往復ループ: 端で反転
                if (currentPathPosition >= 1f)
                {
                    currentPathPosition = 1f;
                    isWaiting = true;
                    waitTimer = 0f;
                }
                else if (currentPathPosition <= 0f)
                {
                    currentPathPosition = 0f;
                    isWaiting = true;
                    waitTimer = 0f;
                }
                break;
        }

        // 位置を更新
        UpdatePosition(currentPathPosition);
    }

    /// <summary>
    /// パスの総距離を計算
    /// </summary>
    void CalculateTotalPathLength()
    {
        totalPathLength = 0f;

        if (loopMode == LoopMode.Loop)
        {
            // ループモード: すべてのポイント間の距離を加算（最後から最初へも含む）
            for (int i = 0; i < pathPositions.Length; i++)
            {
                int nextIndex = (i + 1) % pathPositions.Length;
                totalPathLength += Vector3.Distance(pathPositions[i], pathPositions[nextIndex]);
            }
        }
        else
        {
            // Once/PingPongループモード: 最初から最後までの距離を加算
            for (int i = 0; i < pathPositions.Length - 1; i++)
            {
                totalPathLength += Vector3.Distance(pathPositions[i], pathPositions[i + 1]);
            }
        }
    }

    /// <summary>
    /// パス上の位置（0.0～1.0）に基づいて、実際の位置を更新
    /// </summary>
    /// <param name="t">パス上の位置（0.0～1.0）</param>
    void UpdatePosition(float t)
    {
        // イージングを適用
        float easedT = ApplyEasingWithDirection(t, easingType);

        // 線形補間で位置を計算
        Vector3 newPosition = GetPositionOnPath(easedT);

        // ターゲットの位置を更新
        if (targetTransform != null)
        {
            targetTransform.position = newPosition;
        }
    }

    /// <summary>
    /// パス上の位置（0.0～1.0）から実際の座標を取得
    /// 線形補間を使用
    /// </summary>
    /// <param name="t">パス上の位置（0.0～1.0）</param>
    /// <returns>実際の座標</returns>
    Vector3 GetPositionOnPath(float t)
    {
        // tを0.0～1.0にクランプ
        t = Mathf.Clamp01(t);

        // パス全体の距離に対する現在の距離を計算
        float targetDistance = t * totalPathLength;
        float accumulatedDistance = 0f;

        // セグメント数を計算
        int segmentCount = (loopMode == LoopMode.Loop) ? pathPositions.Length : pathPositions.Length - 1;

        // どのセグメント上にいるかを特定（コピーした位置情報を使用）
        for (int i = 0; i < segmentCount; i++)
        {
            int nextIndex = (loopMode == LoopMode.Loop) ? (i + 1) % pathPositions.Length : i + 1;

            Vector3 segmentStart = pathPositions[i];
            Vector3 segmentEnd = pathPositions[nextIndex];
            float segmentLength = Vector3.Distance(segmentStart, segmentEnd);

            if (accumulatedDistance + segmentLength >= targetDistance)
            {
                // このセグメント上にいる
                float segmentT = (targetDistance - accumulatedDistance) / segmentLength;
                return Vector3.Lerp(segmentStart, segmentEnd, segmentT);
            }

            accumulatedDistance += segmentLength;
        }

        // 最後のポイントを返す（念のため）
        if (loopMode == LoopMode.Loop)
        {
            return pathPositions[0]; // ループモードは最初のポイント
        }
        else
        {
            return pathPositions[pathPositions.Length - 1]; // その他は最後のポイント
        }
    }

    /// <summary>
    /// イージングを適用（方向を考慮）
    /// </summary>
    /// <param name="t">パス上の位置（0.0～1.0）</param>
    /// <param name="type">イージングタイプ</param>
    /// <returns>イージング適用後の位置</returns>
    float ApplyEasingWithDirection(float t, EasingType type)
    {
        // PingPongLoopモードで逆方向の場合
        if (loopMode == LoopMode.PingPongLoop && direction == -1)
        {
            if (invertEasingWhenPingPongBack)
            {
                // 反転フラグがON: イージングをそのまま適用（現在の実装）
                return ApplyEasing(t, type);
            }
            else
            {
                // 反転フラグがOFF: イージングを反転
                // t を 1-t に変換してイージング適用後、再度 1-t に変換
                float invertedT = 1f - t;
                float easedInvertedT = ApplyEasing(invertedT, type);
                return 1f - easedInvertedT;
            }
        }
        else
        {
            // 順方向、または他のモード: イージングをそのまま適用
            return ApplyEasing(t, type);
        }
    }

    /// <summary>
    /// イージングを適用
    /// </summary>
    /// <param name="t">パス上の位置（0.0～1.0）</param>
    /// <param name="type">イージングタイプ</param>
    /// <returns>イージング適用後の位置</returns>
    float ApplyEasing(float t, EasingType type)
    {
        switch (type)
        {
            case EasingType.EaseIn:
                // 加速（2次関数）
                return t * t;

            case EasingType.EaseOut:
                // 減速（2次関数の逆）
                return 1f - (1f - t) * (1f - t);

            case EasingType.EaseInOut:
                // 加速→減速
                if (t < 0.5f)
                {
                    return 2f * t * t;
                }
                else
                {
                    return 1f - 2f * (1f - t) * (1f - t);
                }

            case EasingType.Linear:
            default:
                // 線形（イージングなし）
                return t;
        }
    }

    /// <summary>
    /// Gizmoを描画
    /// </summary>
    void OnDrawGizmos()
    {
        if (!showGizmos || pathPoints == null || pathPoints.Length < 2)
        {
            return;
        }

        // パスを描画（直線）
        Gizmos.color = pathColor;

        // 実行前はpathPointsを使用、実行後はpathPositionsを使用
        if (pathPositions != null && pathPositions.Length == pathPoints.Length)
        {
            // 実行後: コピーした位置情報を使用
            if (loopMode == LoopMode.Loop)
            {
                // ループモード: すべてのポイントを結ぶ（最後から最初へも）
                for (int i = 0; i < pathPositions.Length; i++)
                {
                    int nextIndex = (i + 1) % pathPositions.Length;
                    Gizmos.DrawLine(pathPositions[i], pathPositions[nextIndex]);
                }
            }
            else
            {
                // Once/PingPongループモード: 最初から最後まで結ぶ
                for (int i = 0; i < pathPositions.Length - 1; i++)
                {
                    Gizmos.DrawLine(pathPositions[i], pathPositions[i + 1]);
                }
            }

            // ポイントを描画
            foreach (Vector3 pos in pathPositions)
            {
                Gizmos.DrawSphere(pos, 0.2f);
            }
        }
        else
        {
            // 実行前: pathPointsのTransformを直接使用
            if (loopMode == LoopMode.Loop)
            {
                // ループモード: すべてのポイントを結ぶ（最後から最初へも）
                for (int i = 0; i < pathPoints.Length; i++)
                {
                    if (pathPoints[i] == null) continue;

                    int nextIndex = (i + 1) % pathPoints.Length;
                    if (pathPoints[nextIndex] == null) continue;

                    Gizmos.DrawLine(pathPoints[i].position, pathPoints[nextIndex].position);
                }
            }
            else
            {
                // Once/PingPongループモード: 最初から最後まで結ぶ
                for (int i = 0; i < pathPoints.Length - 1; i++)
                {
                    if (pathPoints[i] == null || pathPoints[i + 1] == null) continue;

                    Gizmos.DrawLine(pathPoints[i].position, pathPoints[i + 1].position);
                }
            }

            // ポイントを描画
            foreach (Transform point in pathPoints)
            {
                if (point == null) continue;
                Gizmos.DrawSphere(point.position, 0.2f);
            }
        }

        // 初期位置マーカーを描画
        if (totalPathLength > 0f && pathPositions != null)
        {
            Gizmos.color = initialPositionColor;
            Vector3 initialPos = GetPositionOnPath(initialPosition);
            Gizmos.DrawWireSphere(initialPos, 0.3f);
            Gizmos.DrawLine(initialPos + Vector3.up * 0.5f, initialPos - Vector3.up * 0.5f);
            Gizmos.DrawLine(initialPos + Vector3.right * 0.5f, initialPos - Vector3.right * 0.5f);
            Gizmos.DrawLine(initialPos + Vector3.forward * 0.5f, initialPos - Vector3.forward * 0.5f);
        }
    }

    /// <summary>
    /// パス上の位置を設定（外部から呼び出し可能）
    /// </summary>
    public void SetPathPosition(float t)
    {
        currentPathPosition = Mathf.Clamp01(t);
        UpdatePosition(currentPathPosition);
    }

    /// <summary>
    /// 現在のパス上の位置を取得
    /// </summary>
    public float GetCurrentPathPosition()
    {
        return currentPathPosition;
    }

    /// <summary>
    /// 移動を一時停止/再開
    /// </summary>
    public void SetPaused(bool paused)
    {
        enabled = !paused;
    }
}
