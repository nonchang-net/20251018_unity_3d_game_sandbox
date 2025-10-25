using UnityEngine;

/// <summary>
/// キャラクターを追跡するサードパーソンカメラ
/// Main Cameraオブジェクトにアタッチして使用
/// </summary>
public class CharacterTracker : MonoBehaviour
{
    [Header("ターゲット設定")]
    [SerializeField] private Transform targetTransform; // 追跡対象（プレイヤー）

    [Header("カメラ設定")]
    [SerializeField] private float cameraDistance = 6f;
    [SerializeField] private float cameraHeight = 2f;
    [SerializeField] private bool invertVerticalAxis = false;
    [SerializeField] private float mouseSensitivityMultiplier = 1f; // マウス感度倍率

    [Header("カメラ制限")]
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 70f;
    [SerializeField] private float initialPitch = -40f; // 初期カメラ角度

    [Header("障害物回避")]
    [SerializeField] private bool enableCollisionAvoidance = true;
    [SerializeField] private float cameraRadius = 0.3f;
    [SerializeField] private LayerMask collisionLayers = -1;
    [SerializeField] private float collisionSmoothSpeed = 10f;

    [Header("カメラスムージング")]
    [SerializeField] private float positionSmoothSpeed = 10f;
    [SerializeField] private float minDistanceThreshold = 0.5f;

    [Header("カメラリセット設定")]
    [Tooltip("カメラリセット時にピッチ（上下角度）もリセットするか")]
    [SerializeField] private bool resetPitchOnReset = true;
    [Tooltip("カメラリセット時の目標ピッチ角度（resetPitchOnResetがtrueの場合のみ使用）")]
    [SerializeField] private float resetPitchAngle = -40f;

    // カメラ回転角度
    private float cameraYaw = 0f;
    private float cameraPitch = 0f;

    // カメラ位置のスムージング用
    private Vector3 currentCameraPosition;
    private float currentCameraDistance;

    // 外部から入力を受け取る
    private Vector2 lookInput = Vector2.zero;

    void Start()
    {
        // 初期角度設定
        cameraPitch = initialPitch;

        if (targetTransform != null)
        {
            cameraYaw = targetTransform.eulerAngles.y;
        }

        // カメラ位置の初期化
        currentCameraPosition = transform.position;
        currentCameraDistance = cameraDistance;

        // カーソルをロック
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        if (targetTransform == null)
        {
            Debug.LogWarning("CharacterTracker: Target Transform が設定されていません。");
            return;
        }

        HandleCameraRotation();
        UpdateCameraPosition();
    }

    /// <summary>
    /// カメラの回転を処理
    /// </summary>
    void HandleCameraRotation()
    {
        float lookX = lookInput.x * mouseSensitivityMultiplier;
        float lookY = lookInput.y * mouseSensitivityMultiplier;

        // 上下反転の適用
        // マウスとgamepadで感覚が逆な気がする？
        float verticalMultiplier = invertVerticalAxis ? 1f : -1f;

        // 入力を合成
        cameraYaw += lookX;
        cameraPitch -= lookY * verticalMultiplier;

        // ピッチ角を制限
        cameraPitch = Mathf.Clamp(cameraPitch, minPitch, maxPitch);
    }

    /// <summary>
    /// カメラの位置を更新
    /// </summary>
    void UpdateCameraPosition()
    {
        // プレイヤーの位置からオフセット
        Vector3 targetPosition = targetTransform.position + Vector3.up * cameraHeight;

        // カメラの理想的な位置を計算
        float yawRad = cameraYaw * Mathf.Deg2Rad;
        float pitchRad = cameraPitch * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(
            Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
            Mathf.Sin(pitchRad),
            Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)
        ) * cameraDistance;

        Vector3 desiredPosition = targetPosition - offset;
        float desiredDistance = cameraDistance;

        // 障害物回避
        if (enableCollisionAvoidance)
        {
            Vector3 direction = desiredPosition - targetPosition;
            float distance = direction.magnitude;

            // 最小距離チェック
            if (distance > minDistanceThreshold)
            {
                // プレイヤーを除外してRaycastを実行
                RaycastHit[] hits = Physics.RaycastAll(targetPosition, direction.normalized, distance, collisionLayers);

                // プレイヤー以外の最も近い障害物を見つける
                float closestDistance = distance;
                bool foundObstacle = false;

                foreach (RaycastHit hit in hits)
                {
                    // プレイヤー自身やその子オブジェクトは無視
                    if (hit.collider.transform == targetTransform ||
                        hit.collider.transform.IsChildOf(targetTransform))
                    {
                        continue;
                    }

                    // コインなどの収集アイテムは無視
                    if (hit.collider.CompareTag("Coin"))
                    {
                        continue;
                    }

                    // より近い障害物が見つかった
                    if (hit.distance < closestDistance)
                    {
                        closestDistance = hit.distance;
                        foundObstacle = true;
                    }
                }

                if (foundObstacle)
                {
                    // 障害物が見つかった場合、安全な距離を計算
                    float safeDistance = Mathf.Max(minDistanceThreshold, closestDistance - cameraRadius);
                    desiredDistance = safeDistance;
                    desiredPosition = targetPosition + direction.normalized * safeDistance;
                }
            }
        }

        // カメラ距離のスムージング
        currentCameraDistance = Mathf.Lerp(currentCameraDistance, desiredDistance,
            Time.deltaTime * collisionSmoothSpeed);

        // カメラ位置のスムージング
        currentCameraPosition = Vector3.Lerp(currentCameraPosition, desiredPosition,
            Time.deltaTime * positionSmoothSpeed);

        // カメラの位置と向きを設定
        transform.position = currentCameraPosition;
        transform.LookAt(targetPosition);
    }

    /// <summary>
    /// カメラの前方向ベクトルを取得（Y成分を除く）
    /// </summary>
    public Vector3 GetCameraForward()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        return forward.normalized;
    }

    /// <summary>
    /// カメラの右方向ベクトルを取得（Y成分を除く）
    /// </summary>
    public Vector3 GetCameraRight()
    {
        Vector3 right = transform.right;
        right.y = 0f;
        return right.normalized;
    }

    /// <summary>
    /// 外部から視点入力を設定
    /// </summary>
    public void SetLookInput(Vector2 input)
    {
        lookInput = input;
    }

    /// <summary>
    /// カメラの距離を設定
    /// </summary>
    public void SetCameraDistance(float distance)
    {
        cameraDistance = Mathf.Max(1f, distance);
    }

    /// <summary>
    /// ターゲットを設定
    /// </summary>
    public void SetTarget(Transform target)
    {
        targetTransform = target;
    }

    /// <summary>
    /// カメラをキャラクターの向きにリセット
    /// </summary>
    public void ResetCamera()
    {
        if (targetTransform == null)
        {
            Debug.LogWarning("CharacterTracker: ターゲットが設定されていません。カメラリセットできません。");
            return;
        }

        // ヨー角をキャラクターの向きに合わせる
        cameraYaw = targetTransform.eulerAngles.y;

        // ピッチ角をリセット（設定に応じて）
        if (resetPitchOnReset)
        {
            cameraPitch = resetPitchAngle;
        }
    }

    /// <summary>
    /// カメラの上下反転設定を変更
    /// </summary>
    /// <param name="invert">true: 反転、false: 通常</param>
    public void SetCameraInvertY(bool invert)
    {
        invertVerticalAxis = invert;
    }

    /// <summary>
    /// マウス感度倍率を設定
    /// </summary>
    /// <param name="multiplier">感度倍率（0.1 ～ 3.0）</param>
    public void SetMouseSensitivity(float multiplier)
    {
        mouseSensitivityMultiplier = Mathf.Clamp(multiplier, 0.1f, 3.0f);
    }
}
