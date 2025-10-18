using UnityEngine;

/// <summary>
/// コインの回転とコレクションアニメーションを管理
/// </summary>
public class Coin : MonoBehaviour
{
    [Header("回転設定")]
    [SerializeField] private float rotationSpeed = 100f;

    [Header("取得アニメーション設定")]
    [Tooltip("ジャンプの高さ")]
    [SerializeField] private float jumpHeight = 2f;

    [Tooltip("ジャンプアニメーションの長さ（秒）")]
    [SerializeField] private float jumpDuration = 0.5f;

    [Tooltip("縮小アニメーションの開始時間（秒）")]
    [SerializeField] private float shrinkStart = 0.3f;

    [Tooltip("縮小アニメーションの長さ（秒）")]
    [SerializeField] private float shrinkDuration = 0.3f;

    [Tooltip("プレイヤーに向かって移動を開始する時間（秒）")]
    [SerializeField] private float moveToPlayerStart = 0.3f;

    [Tooltip("プレイヤーに向かって移動するアニメーションの長さ（秒）")]
    [SerializeField] private float moveToPlayerDuration = 0.3f;

    private bool isCollected = false;
    private float animationTimer = 0f;
    private Vector3 startPosition;
    private Vector3 startScale;
    private Collider coinCollider;
    private Transform playerTransform; // プレイヤーの位置

    void Awake()
    {
        // コイン生成をUserDataManagerに通知
        UserDataManager.AddGeneratedCoins(1, gameObject);
    }

    void Start()
    {
        startPosition = transform.position;
        startScale = transform.localScale;
        coinCollider = GetComponent<Collider>();
    }

    void Update()
    {
        if (!isCollected)
        {
            // 通常の回転
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        }
        else
        {
            // 取得アニメーション
            PlayCollectAnimation();
        }
    }

    /// <summary>
    /// コイン取得時に呼び出す
    /// </summary>
    /// <param name="player">プレイヤーのTransform</param>
    public void Collect(Transform player = null)
    {
        if (isCollected) return;

        isCollected = true;
        animationTimer = 0f;
        startPosition = transform.position;
        playerTransform = player;

        // コライダーを無効化（二重取得防止）
        if (coinCollider != null)
        {
            coinCollider.enabled = false;
        }

        // ユーザデータ更新（コイン取得イベントを発火）
        UserDataManager.AddCoin(1, gameObject);
    }

    /// <summary>
    /// 取得アニメーションを再生
    /// </summary>
    void PlayCollectAnimation()
    {
        animationTimer += Time.deltaTime;

        // アニメーション全体の終了時間を計算
        float totalDuration = Mathf.Max(
            jumpDuration,
            shrinkStart + shrinkDuration,
            moveToPlayerStart + moveToPlayerDuration
        );

        // 位置の初期値（各アニメーションで上書き）
        Vector3 newPosition = transform.position;
        float currentScale = transform.localScale.x / startScale.x; // 正規化されたスケール

        // === ジャンプアニメーション ===
        if (animationTimer <= jumpDuration)
        {
            float jumpProgress = animationTimer / jumpDuration;
            float jumpCurve = Mathf.Sin(jumpProgress * Mathf.PI); // 放物線
            newPosition = startPosition + Vector3.up * jumpCurve * jumpHeight;
        }

        // === 縮小アニメーション ===
        if (animationTimer >= shrinkStart && animationTimer <= shrinkStart + shrinkDuration)
        {
            float shrinkProgress = (animationTimer - shrinkStart) / shrinkDuration;
            currentScale = Mathf.Lerp(1f, 0f, shrinkProgress);
        }
        else if (animationTimer > shrinkStart + shrinkDuration)
        {
            currentScale = 0f; // 縮小完了
        }

        // === プレイヤーに向かって移動 ===
        if (playerTransform != null &&
            animationTimer >= moveToPlayerStart &&
            animationTimer <= moveToPlayerStart + moveToPlayerDuration)
        {
            float moveProgress = (animationTimer - moveToPlayerStart) / moveToPlayerDuration;

            // 移動開始時の位置を計算（ジャンプアニメーションの影響を考慮）
            Vector3 moveStartPosition;
            if (moveToPlayerStart <= jumpDuration)
            {
                // ジャンプ中に移動開始
                float jumpProgressAtMoveStart = moveToPlayerStart / jumpDuration;
                float jumpCurveAtMoveStart = Mathf.Sin(jumpProgressAtMoveStart * Mathf.PI);
                moveStartPosition = startPosition + Vector3.up * jumpCurveAtMoveStart * jumpHeight;
            }
            else
            {
                // ジャンプ終了後に移動開始
                moveStartPosition = startPosition;
            }

            // プレイヤーの中心位置（やや上）
            Vector3 playerCenter = playerTransform.position + Vector3.up * 1f;

            // 移動アニメーション
            newPosition = Vector3.Lerp(moveStartPosition, playerCenter, moveProgress);
        }

        // 位置とスケールを適用
        transform.position = newPosition;
        transform.localScale = startScale * currentScale;

        // 回転（アニメーション全体を通して継続）
        float rotationMultiplier = 1f;
        if (animationTimer <= jumpDuration)
        {
            rotationMultiplier = 2f; // ジャンプ中
        }
        else if (animationTimer >= shrinkStart)
        {
            rotationMultiplier = 3f; // 縮小中
        }
        transform.Rotate(Vector3.up, rotationSpeed * rotationMultiplier * Time.deltaTime, Space.World);

        // アニメーション完了チェック
        if (animationTimer >= totalDuration)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 既に取得済みかどうか
    /// </summary>
    public bool IsCollected()
    {
        return isCollected;
    }
}
