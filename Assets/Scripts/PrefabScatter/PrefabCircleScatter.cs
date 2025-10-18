using UnityEngine;

/// <summary>
/// コインを様々なパターンで配置するコンポーネント
/// マリオゲームのようにコインを自在に配置できます
/// </summary>
public class CoinCircleScatter : MonoBehaviour
{
    [Header("コインプレハブ設定")]
    [Tooltip("配置するコインのプレハブ")]
    [SerializeField] private GameObject coinPrefab;

    [Header("円形配置設定")]
    [Tooltip("配置するコインの数")]
    [SerializeField] private int coinCount = 8;

    [Tooltip("円の半径")]
    [SerializeField] private float radius = 3f;

    [Tooltip("配置する高さのオフセット")]
    [SerializeField] private float heightOffset = 0f;

    [Tooltip("開始角度（度）")]
    [SerializeField] private float startAngle = 0f;

    [Header("回転設定")]
    [Tooltip("回転を有効化")]
    [SerializeField] private bool enableRotation = true;

    [Tooltip("回転速度（度/秒）")]
    [SerializeField] private float rotationSpeed = 30f;

    [Tooltip("回転軸（Y軸が標準）")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up;

    [Header("デバッグ設定")]
    [Tooltip("シーンビューでギズモを表示")]
    [SerializeField] private bool showGizmos = true;

    // 生成したコインを格納
    private GameObject coinsParent;

    void Start()
    {
        SpawnCoins();
    }

    void Update()
    {
        if (enableRotation && coinsParent != null)
        {
            // コイン全体を回転
            coinsParent.transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime, Space.World);
        }
    }

    /// <summary>
    /// コインを円形に配置
    /// </summary>
    void SpawnCoins()
    {
        if (coinPrefab == null)
        {
            Debug.LogError($"CoinScatter ({gameObject.name}): Coin Prefabが設定されていません。");
            return;
        }

        if (coinCount <= 0)
        {
            Debug.LogWarning($"CoinScatter ({gameObject.name}): Coin Countが0以下です。");
            return;
        }

        // コインをまとめる親オブジェクトを作成
        coinsParent = new GameObject($"{gameObject.name}_Coins");
        coinsParent.transform.SetParent(transform);
        coinsParent.transform.localPosition = Vector3.zero;
        coinsParent.transform.localRotation = Quaternion.identity;

        // 円形にコインを配置
        float angleStep = 360f / coinCount;

        for (int i = 0; i < coinCount; i++)
        {
            float angle = startAngle + (angleStep * i);
            float angleRad = angle * Mathf.Deg2Rad;

            // 円周上の位置を計算
            Vector3 localPosition = new Vector3(
                Mathf.Cos(angleRad) * radius,
                heightOffset,
                Mathf.Sin(angleRad) * radius
            );

            // コインを生成
            GameObject coin = Instantiate(coinPrefab, coinsParent.transform);
            coin.transform.localPosition = localPosition;
            coin.name = $"Coin_{i + 1}";
        }

        //Debug.Log($"CoinCircleScatter ({gameObject.name}): {coinCount}個のコインを円形に配置しました。");
    }

    /// <summary>
    /// エディタでコインの配置をプレビュー
    /// </summary>
    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // 円の描画
        Gizmos.color = Color.yellow;
        int segments = Mathf.Max(coinCount, 16);
        float angleStep = 360f / segments;

        Vector3 prevPoint = Vector3.zero;
        for (int i = 0; i <= segments; i++)
        {
            float angle = startAngle + (angleStep * i);
            float angleRad = angle * Mathf.Deg2Rad;

            Vector3 point = transform.position + new Vector3(
                Mathf.Cos(angleRad) * radius,
                heightOffset,
                Mathf.Sin(angleRad) * radius
            );

            if (i > 0)
            {
                Gizmos.DrawLine(prevPoint, point);
            }

            prevPoint = point;
        }

        // コイン位置のマーカー
        Gizmos.color = Color.cyan;
        angleStep = 360f / coinCount;

        for (int i = 0; i < coinCount; i++)
        {
            float angle = startAngle + (angleStep * i);
            float angleRad = angle * Mathf.Deg2Rad;

            Vector3 coinPosition = transform.position + new Vector3(
                Mathf.Cos(angleRad) * radius,
                heightOffset,
                Mathf.Sin(angleRad) * radius
            );

            Gizmos.DrawWireSphere(coinPosition, 0.2f);
        }

        // 中心マーカー
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * heightOffset, 0.3f);
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
