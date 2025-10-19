using UnityEngine;

/// <summary>
/// チェックポイントコンポーネント
/// - Sphere Collider（isTrigger=true）とタグ「CheckPoint」を必要とします
/// - プレイヤーが踏むとactivatedフラグが有効になります
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class CheckPoint : MonoBehaviour
{
    [Header("チェックポイント設定")]
    [Tooltip("チェックポイントの表示名称")]
    [SerializeField] private string checkPointName = "CheckPoint";

    [Tooltip("リスポーン位置（通常はこのオブジェクトのTransformを指定）")]
    [SerializeField] private Transform spawnPosition;

    [Tooltip("チェックポイントのサムネイル画像")]
    [SerializeField] private Sprite thumbnail;

    [Header("パーティクル設定")]
    [Tooltip("未アクティブ時に表示するパーティクル（アクティブ化時にSetActive(false)される）")]
    [SerializeField] private GameObject nonActivatedParticles;

    /// <summary>アクティブ化済みかどうか</summary>
    [SerializeField] private bool activated = false;

    /// <summary>チェックポイント名 (TODO: 未使用 ファストトラベル時に利用を想定)</summary>
    public string CheckPointName => checkPointName;

    /// <summary>リスポーン位置</summary>
    public Transform SpawnPosition => spawnPosition;

    /// <summary>サムネイル画像 (TODO: 未実装 ファストトラベル時に利用を想定)</summary>
    public Sprite Thumbnail => thumbnail;

    /// <summary>アクティブ化済みかどうか</summary>
    public bool IsActivated => activated;

    void Awake()
    {
        // spawnPositionが未設定の場合は自分自身のTransformを使用
        if (spawnPosition == null)
        {
            spawnPosition = transform;
        }

        // SphereColliderのisTriggerを確認
        SphereCollider sphereCollider = GetComponent<SphereCollider>();
        if (sphereCollider != null && !sphereCollider.isTrigger)
        {
            Debug.LogWarning($"CheckPoint '{checkPointName}': SphereCollider の isTrigger が false です。true に設定してください。");
        }

        // タグの確認
        if (!CompareTag("CheckPoint"))
        {
            Debug.LogWarning($"CheckPoint '{checkPointName}': タグが 'CheckPoint' ではありません。タグを設定してください。");
        }
    }

    /// <summary>
    /// チェックポイントをアクティブ化
    /// </summary>
    public void Activate()
    {
        if (activated)
        {
            // 既にアクティブ化済み
            return;
        }

        activated = true;

        // 非アクティブ時のパーティクルを無効化
        if (nonActivatedParticles != null)
        {
            nonActivatedParticles.SetActive(false);
        }

        // Debug.Log($"CheckPoint '{checkPointName}' がアクティブ化されました。");
    }

    /// <summary>
    /// チェックポイントをリセット（デバッグ用）
    /// </summary>
    public void ResetCheckPoint()
    {
        activated = false;

        // 非アクティブ時のパーティクルを再度有効化
        if (nonActivatedParticles != null)
        {
            nonActivatedParticles.SetActive(true);
        }

        // Debug.Log($"CheckPoint '{checkPointName}' がリセットされました。");
    }

    void OnValidate()
    {
        // Inspectorでの検証
        if (spawnPosition == null)
        {
            spawnPosition = transform;
        }
    }
}
