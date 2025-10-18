using UnityEngine;

/// <summary>
/// 2点間にコインを直線またはアーチ状に配置するコンポーネント
/// マリオゲームのようなコイン配置を簡単に作成できます
/// </summary>
public class CoinLineScatter : MonoBehaviour
{
    [Header("コインプレハブ設定")]
    [Tooltip("配置するコインのプレハブ")]
    [SerializeField] private GameObject coinPrefab;

    [Header("配置ポイント設定")]
    [Tooltip("開始位置のGameObject")]
    [SerializeField] private Transform startPoint;

    [Tooltip("終了位置のGameObject")]
    [SerializeField] private Transform endPoint;

    [Header("配置設定")]
    [Tooltip("配置するコインの数")]
    [SerializeField] private int coinCount = 8;

    [Tooltip("曲率（0=直線、正の値=上向きアーチ、負の値=下向きアーチ）")]
    [SerializeField] private float curvature = 0f;

    [Tooltip("配置する列数（1列、2列、3列など）")]
    [SerializeField] private int rowCount = 1;

    [Tooltip("列間の間隔")]
    [SerializeField] private float rowSpacing = 1f;

    [Header("デバッグ設定")]
    [Tooltip("シーンビューでギズモを表示")]
    [SerializeField] private bool showGizmos = true;

    [Tooltip("ギズモの線分数（滑らかさ）")]
    [SerializeField] private int gizmoSegments = 20;

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
            Debug.LogError($"CoinLineScatter ({gameObject.name}): Coin Prefabが設定されていません。");
            return;
        }

        if (startPoint == null || endPoint == null)
        {
            Debug.LogError($"CoinLineScatter ({gameObject.name}): Start PointまたはEnd Pointが設定されていません。");
            return;
        }

        if (coinCount <= 0)
        {
            Debug.LogWarning($"CoinLineScatter ({gameObject.name}): Coin Countが0以下です。");
            return;
        }

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

        //Debug.Log($"CoinLineScatter ({gameObject.name}): {coinCount * rowCount}個のコインを配置しました（{rowCount}列）。");
    }

    /// <summary>
    /// 1列分のコインを配置
    /// </summary>
    void SpawnCoinRow(int rowIndex)
    {
        // 列のオフセットを計算（中心を基準に左右に配置）
        Vector3 direction = (endPoint.position - startPoint.position).normalized;
        Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized;

        // 列数が偶数か奇数かで中心の計算方法を変える
        float rowOffset;
        if (rowCount == 1)
        {
            rowOffset = 0f;
        }
        else if (rowCount % 2 == 1)
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

        Vector3 rowOffsetVector = perpendicular * rowOffset;

        // 各コインを配置
        for (int i = 0; i < coinCount; i++)
        {
            float t = coinCount > 1 ? (float)i / (coinCount - 1) : 0.5f;

            // 基本位置（開始点と終了点の間の線形補間）
            Vector3 basePosition = Vector3.Lerp(startPoint.position, endPoint.position, t);

            // アーチの高さを計算（放物線）
            float archHeight = CalculateArchHeight(t, curvature);
            Vector3 archOffset = Vector3.up * archHeight;

            // 最終位置 = 基本位置 + アーチオフセット + 列オフセット
            Vector3 finalPosition = basePosition + archOffset + rowOffsetVector;

            // コインを生成
            GameObject coin = Instantiate(coinPrefab, coinsParent.transform);
            coin.transform.position = finalPosition;
            coin.name = $"Coin_Row{rowIndex + 1}_Col{i + 1}";
        }
    }

    /// <summary>
    /// アーチの高さを計算（放物線）
    /// </summary>
    float CalculateArchHeight(float t, float curve)
    {
        // 放物線: y = -4 * curve * (t - 0.5)^2 + curve
        // t=0とt=1で高さ0、t=0.5で最大高さcurve
        return -4f * curve * Mathf.Pow(t - 0.5f, 2f) + curve;
    }

    /// <summary>
    /// エディタでコインの配置をプレビュー
    /// </summary>
    void OnDrawGizmos()
    {
        if (!showGizmos) return;
        if (startPoint == null || endPoint == null) return;

        // 各列のアーチを描画
        for (int row = 0; row < rowCount; row++)
        {
            DrawArchGizmo(row);
        }

        // 開始点と終了点のマーカー
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(startPoint.position, 0.3f);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(endPoint.position, 0.3f);
    }

    /// <summary>
    /// 1列分のアーチをギズモで描画
    /// </summary>
    void DrawArchGizmo(int rowIndex)
    {
        // 列のオフセットを計算
        Vector3 direction = (endPoint.position - startPoint.position).normalized;
        Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized;

        float rowOffset;
        if (rowCount == 1)
        {
            rowOffset = 0f;
        }
        else if (rowCount % 2 == 1)
        {
            int centerIndex = rowCount / 2;
            rowOffset = (rowIndex - centerIndex) * rowSpacing;
        }
        else
        {
            float centerOffset = (rowCount - 1) / 2f;
            rowOffset = (rowIndex - centerOffset) * rowSpacing;
        }

        Vector3 rowOffsetVector = perpendicular * rowOffset;

        // アーチの曲線を描画
        Gizmos.color = Color.yellow;
        Vector3 prevPoint = Vector3.zero;

        for (int i = 0; i <= gizmoSegments; i++)
        {
            float t = (float)i / gizmoSegments;

            Vector3 basePosition = Vector3.Lerp(startPoint.position, endPoint.position, t);
            float archHeight = CalculateArchHeight(t, curvature);
            Vector3 point = basePosition + Vector3.up * archHeight + rowOffsetVector;

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

            Vector3 basePosition = Vector3.Lerp(startPoint.position, endPoint.position, t);
            float archHeight = CalculateArchHeight(t, curvature);
            Vector3 coinPosition = basePosition + Vector3.up * archHeight + rowOffsetVector;

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
