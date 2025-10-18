using UnityEngine;

/// <summary>
/// ハイジャンプトリガーコンポーネント
/// - Sphere Collider（isTrigger=true）とタグ「HighJumper」を必要とします
/// - プレイヤーが触れると頭上10メートルの高さまで舞い上がります
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class HighJumper : MonoBehaviour
{
    [Header("ハイジャンプ設定")]
    [Tooltip("ジャンプの高さ（メートル）")]
    [SerializeField] private float jumpHeight = 10f;

    [Tooltip("ジャンプの上昇速度")]
    [SerializeField] private float jumpSpeed = 15f;

    /// <summary>ジャンプの高さ</summary>
    public float JumpHeight => jumpHeight;

    /// <summary>ジャンプの上昇速度</summary>
    public float JumpSpeed => jumpSpeed;

    void Awake()
    {
        // SphereColliderのisTriggerを確認
        SphereCollider sphereCollider = GetComponent<SphereCollider>();
        if (sphereCollider != null && !sphereCollider.isTrigger)
        {
            Debug.LogWarning($"HighJumper '{name}': SphereCollider の isTrigger が false です。true に設定してください。");
        }

        // タグの確認
        if (!CompareTag("HighJumper"))
        {
            Debug.LogWarning($"HighJumper '{name}': タグが 'HighJumper' ではありません。タグを設定してください。");
        }
    }
}
