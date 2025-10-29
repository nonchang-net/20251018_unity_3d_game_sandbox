using UnityEngine;

/// <summary>
/// カメラ角度計算補助ツール
/// このオブジェクトの位置をカメラ位置とし、ターゲットを見たときのEuler Anglesを計算してInspectorに表示します
/// </summary>
public class LookAtChecker : MonoBehaviour
{
    [Header("ターゲット設定")]
    [Tooltip("カメラが見るターゲットオブジェクト")]
    [SerializeField] private Transform lookAtTarget;

    [Header("計算結果")]
    [Tooltip("計算されたカメラ角度（Euler Angles）\n※ReadOnlyです")]
    [SerializeField] private Vector3 calculatedRotation;

    /// <summary>
    /// 計算されたカメラ角度を取得（Inspector表示用）
    /// </summary>
    public Vector3 CalculatedRotation => calculatedRotation;

    void Update()
    {
        // ターゲットが設定されている場合のみ計算
        if (lookAtTarget != null)
        {
            CalculateLookAtRotation();
        }
    }

    /// <summary>
    /// ターゲットを見るためのカメラ角度を計算
    /// </summary>
    void CalculateLookAtRotation()
    {
        // カメラ位置からターゲット位置への方向ベクトル
        Vector3 direction = lookAtTarget.position - transform.position;

        // 方向ベクトルが0の場合は計算しない
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        // LookRotationでQuaternionを計算
        Quaternion rotation = Quaternion.LookRotation(direction);

        // Euler Anglesに変換
        calculatedRotation = rotation.eulerAngles;
    }

    void OnDrawGizmos()
    {
        // ターゲットが設定されている場合のみギズモを描画
        if (lookAtTarget == null)
        {
            return;
        }

        // カメラ位置（このオブジェクトの位置）
        Vector3 cameraPosition = transform.position;
        Vector3 targetPosition = lookAtTarget.position;

        // カメラ位置を青い球で表示
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(cameraPosition, 0.2f);

        // ターゲット位置を赤い球で表示
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(targetPosition, 0.2f);

        // カメラからターゲットへの線を緑で表示
        Gizmos.color = Color.green;
        Gizmos.DrawLine(cameraPosition, targetPosition);

        // カメラの視線方向を黄色の矢印で表示
        Vector3 direction = (targetPosition - cameraPosition).normalized;
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(cameraPosition, direction * 2f);
    }
}
