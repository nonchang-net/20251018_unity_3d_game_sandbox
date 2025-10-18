using UnityEngine;

/// <summary>
/// 任意個数のGameObjectの位置を通るパスに沿ってコインを配置するコンポーネント
/// Catmull-Romスプラインを使用して滑らかな曲線を生成します
/// </summary>
public class CoinCurveScatter : MonoBehaviour
{
    [Header("コインプレハブ設定")]
    [Tooltip("配置するコインのプレハブ")]
    [SerializeField] private GameObject coinPrefab;

    [Header("パスポイント設定")]
    [Tooltip("パスを構成するポイント（Transform）の配列")]
    [SerializeField] private Transform[] pathPoints;

    [Header("配置設定")]
    [Tooltip("配置するコインの数")]
    [SerializeField] private int coinCount = 20;

    [Tooltip("配置する列数（1列、2列、3列など）")]
    [SerializeField] private int rowCount = 1;

    [Tooltip("列間の間隔")]
    [SerializeField] private float rowSpacing = 1f;

    [Header("スプライン設定")]
    [Tooltip("スプラインの張り（0.5が標準のCatmull-Rom）")]
    [SerializeField] [Range(0f, 1f)] private float tension = 0.5f;

    [Tooltip("始点と終点をループさせる")]
    [SerializeField] private bool closedLoop = false;

    [Header("デバッグ設定")]
    [Tooltip("シーンビューでギズモを表示")]
    [SerializeField] private bool showGizmos = true;

    [Tooltip("ギズモの線分数（滑らかさ）")]
    [SerializeField] private int gizmoSegments = 50;

    // 生成したコインを格納
    private GameObject coinsParent;

    void Start()
    {
        SpawnCoins();
    }

    /// <summary>
    /// コインを配置
    /// </summary>
    void SpawnCoins()
    {
        if (coinPrefab == null)
        {
            Debug.LogError($"CoinCurveScatter ({gameObject.name}): Coin Prefabが設定されていません。");
            return;
        }

        if (pathPoints == null || pathPoints.Length < 2)
        {
            Debug.LogError($"CoinCurveScatter ({gameObject.name}): Path Pointsが不足しています（最低2つ必要）。");
            return;
        }

        if (coinCount <= 0)
        {
            Debug.LogWarning($"CoinCurveScatter ({gameObject.name}): Coin Countが0以下です。");
            return;
        }

        // nullチェック
        bool hasNullPoint = false;
        for (int i = 0; i < pathPoints.Length; i++)
        {
            if (pathPoints[i] == null)
            {
                Debug.LogError($"CoinCurveScatter ({gameObject.name}): Path Points[{i}]がnullです。");
                hasNullPoint = true;
            }
        }
        if (hasNullPoint) return;

        // コインをまとめる親オブジェクトを作成
        coinsParent = new GameObject($"{gameObject.name}_Coins");
        coinsParent.transform.SetParent(transform);
        coinsParent.transform.localPosition = Vector3.zero;
        coinsParent.transform.localRotation = Quaternion.identity;

        // 列ごとにコインを配置
        for (int row = 0; row < rowCount; row++)
        {
            SpawnCoinRow(row);
        }

        //Debug.Log($"CoinCurveScatter ({gameObject.name}): {coinCount * rowCount}個のコインを配置しました（{rowCount}列）。");
    }

    /// <summary>
    /// 1列分のコインを配置
    /// </summary>
    void SpawnCoinRow(int rowIndex)
    {
        for (int i = 0; i < coinCount; i++)
        {
            float t = coinCount > 1 ? (float)i / (coinCount - 1) : 0.5f;

            // スプライン上の位置を取得
            Vector3 position = GetPointOnSpline(t);

            // 列オフセットを計算
            if (rowCount > 1)
            {
                Vector3 tangent = GetTangentOnSpline(t);
                Vector3 perpendicular = Vector3.Cross(tangent.normalized, Vector3.up).normalized;

                // 列数が偶数か奇数かで中心の計算方法を変える
                float rowOffset;
                if (rowCount % 2 == 1)
                {
                    // 奇数列: 中心を0として左右に配置
                    int centerIndex = rowCount / 2;
                    rowOffset = (rowIndex - centerIndex) * rowSpacing;
                }
                else
                {
                    // 偶数列: 中心をずらして配置
                    float centerOffset = (rowCount - 1) / 2f;
                    rowOffset = (rowIndex - centerOffset) * rowSpacing;
                }

                position += perpendicular * rowOffset;
            }

            // コインを生成
            GameObject coin = Instantiate(coinPrefab, coinsParent.transform);
            coin.transform.position = position;
            coin.name = $"Coin_Row{rowIndex + 1}_Col{i + 1}";
        }
    }

    /// <summary>
    /// Catmull-Romスプライン上の点を取得
    /// </summary>
    /// <param name="t">0〜1の範囲のパラメータ</param>
    /// <returns>スプライン上の位置</returns>
    Vector3 GetPointOnSpline(float t)
    {
        if (pathPoints.Length == 2)
        {
            // ポイントが2つの場合は線形補間
            return Vector3.Lerp(pathPoints[0].position, pathPoints[1].position, t);
        }

        // 全体のパスの長さに対するtの位置を計算
        int segmentCount = closedLoop ? pathPoints.Length : pathPoints.Length - 1;
        float scaledT = t * segmentCount;
        int segmentIndex = Mathf.FloorToInt(scaledT);
        float localT = scaledT - segmentIndex;

        // ループの場合は最後のセグメントを処理
        if (closedLoop)
        {
            segmentIndex = segmentIndex % pathPoints.Length;
        }
        else
        {
            // 最後のセグメントを超えないようにクランプ
            if (segmentIndex >= pathPoints.Length - 1)
            {
                segmentIndex = pathPoints.Length - 2;
                localT = 1f;
            }
        }

        // Catmull-Romスプラインの制御点を取得
        Vector3 p0 = GetPathPoint(segmentIndex - 1);
        Vector3 p1 = GetPathPoint(segmentIndex);
        Vector3 p2 = GetPathPoint(segmentIndex + 1);
        Vector3 p3 = GetPathPoint(segmentIndex + 2);

        // Catmull-Romスプライン計算
        return CatmullRom(p0, p1, p2, p3, localT, tension);
    }

    /// <summary>
    /// スプライン上の接線ベクトルを取得（列オフセット計算用）
    /// </summary>
    Vector3 GetTangentOnSpline(float t)
    {
        float delta = 0.01f;
        Vector3 p1 = GetPointOnSpline(Mathf.Clamp01(t - delta));
        Vector3 p2 = GetPointOnSpline(Mathf.Clamp01(t + delta));
        return (p2 - p1).normalized;
    }

    /// <summary>
    /// パスポイントを取得（ループ処理とクランプ処理を含む）
    /// </summary>
    Vector3 GetPathPoint(int index)
    {
        if (closedLoop)
        {
            // ループの場合は配列をループ
            index = ((index % pathPoints.Length) + pathPoints.Length) % pathPoints.Length;
            return pathPoints[index].position;
        }
        else
        {
            // ループしない場合は両端をクランプ
            if (index < 0) return pathPoints[0].position;
            if (index >= pathPoints.Length) return pathPoints[pathPoints.Length - 1].position;
            return pathPoints[index].position;
        }
    }

    /// <summary>
    /// Catmull-Romスプライン補間
    /// </summary>
    Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t, float alpha)
    {
        // Catmull-Rom基底関数
        float t2 = t * t;
        float t3 = t2 * t;

        Vector3 result =
            p1 +
            (-p0 + p2) * t * alpha +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 * alpha +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3 * alpha;

        return result;
    }

    /// <summary>
    /// エディタでコインの配置をプレビュー
    /// </summary>
    void OnDrawGizmos()
    {
        if (!showGizmos) return;
        if (pathPoints == null || pathPoints.Length < 2) return;

        // nullチェック
        for (int i = 0; i < pathPoints.Length; i++)
        {
            if (pathPoints[i] == null) return;
        }

        // 各列のスプラインを描画
        for (int row = 0; row < rowCount; row++)
        {
            DrawSplineGizmo(row);
        }

        // パスポイントのマーカー
        Gizmos.color = Color.green;
        for (int i = 0; i < pathPoints.Length; i++)
        {
            Gizmos.DrawWireSphere(pathPoints[i].position, 0.3f);

            // ポイント番号を表示するための小さなオフセット
            if (i < pathPoints.Length - 1 || closedLoop)
            {
                Vector3 nextPoint = closedLoop && i == pathPoints.Length - 1
                    ? pathPoints[0].position
                    : pathPoints[i + 1].position;
                Gizmos.color = Color.gray;
                Gizmos.DrawLine(pathPoints[i].position, nextPoint);
            }
        }
    }

    /// <summary>
    /// 1列分のスプラインをギズモで描画
    /// </summary>
    void DrawSplineGizmo(int rowIndex)
    {
        // スプライン曲線を描画
        Gizmos.color = Color.yellow;
        Vector3 prevPoint = Vector3.zero;

        for (int i = 0; i <= gizmoSegments; i++)
        {
            float t = (float)i / gizmoSegments;
            Vector3 point = GetPointOnSpline(t);

            // 列オフセットを適用
            if (rowCount > 1)
            {
                Vector3 tangent = GetTangentOnSpline(t);
                Vector3 perpendicular = Vector3.Cross(tangent.normalized, Vector3.up).normalized;

                float rowOffset;
                if (rowCount % 2 == 1)
                {
                    int centerIndex = rowCount / 2;
                    rowOffset = (rowIndex - centerIndex) * rowSpacing;
                }
                else
                {
                    float centerOffset = (rowCount - 1) / 2f;
                    rowOffset = (rowIndex - centerOffset) * rowSpacing;
                }

                point += perpendicular * rowOffset;
            }

            if (i > 0)
            {
                Gizmos.DrawLine(prevPoint, point);
            }

            prevPoint = point;
        }

        // コイン位置のマーカー
        Gizmos.color = Color.cyan;

        for (int i = 0; i < coinCount; i++)
        {
            float t = coinCount > 1 ? (float)i / (coinCount - 1) : 0.5f;
            Vector3 coinPosition = GetPointOnSpline(t);

            // 列オフセットを適用
            if (rowCount > 1)
            {
                Vector3 tangent = GetTangentOnSpline(t);
                Vector3 perpendicular = Vector3.Cross(tangent.normalized, Vector3.up).normalized;

                float rowOffset;
                if (rowCount % 2 == 1)
                {
                    int centerIndex = rowCount / 2;
                    rowOffset = (rowIndex - centerIndex) * rowSpacing;
                }
                else
                {
                    float centerOffset = (rowCount - 1) / 2f;
                    rowOffset = (rowIndex - centerOffset) * rowSpacing;
                }

                coinPosition += perpendicular * rowOffset;
            }

            Gizmos.DrawWireSphere(coinPosition, 0.2f);
        }
    }

    /// <summary>
    /// 実行時にコインを再配置（エディタ用）
    /// </summary>
    [ContextMenu("Respawn Coins")]
    public void RespawnCoins()
    {
        // 既存のコインを削除
        if (coinsParent != null)
        {
            if (Application.isPlaying)
            {
                Destroy(coinsParent);
            }
            else
            {
                DestroyImmediate(coinsParent);
            }
        }

        // 再配置
        if (Application.isPlaying)
        {
            SpawnCoins();
        }
    }
}
