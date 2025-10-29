using UnityEngine;

/// <summary>
/// キャラクターを追跡するサードパーソンカメラ
/// Main Cameraオブジェクトにアタッチして使用
/// </summary>
public class CharacterTracker : MonoBehaviour
{
    [Header("ターゲット設定")]
    [SerializeField] private Transform targetTransform; // 追跡対象（プレイヤー）

    [Header("トラッキング設定")]
    [Tooltip("カメラトラッキングの設定（ScriptableObject）")]
    [SerializeField] private TrackingSetting trackingSetting;

    [Header("プレイヤー設定（ポーズメニューから変更可能）")]
    [Tooltip("上下カメラ操作方向反転")]
    [SerializeField] private bool invertVerticalAxis = false;
    [Tooltip("マウス感度倍率")]
    [SerializeField] private float mouseSensitivityMultiplier = 1f;

    // トランジション用のオーバーライド値
    private bool useOverrideValues = false;
    private float overrideCameraDistance;
    private float overrideCameraHeight;
    private float overrideMinPitch;
    private float overrideMaxPitch;
    private float overrideInitialPitch;
    private bool overrideEnableCollisionAvoidance;
    private float overrideCameraRadius;
    private LayerMask overrideCollisionLayers;
    private float overrideCollisionSmoothSpeed;
    private float overridePositionSmoothSpeed;
    private float overrideMinDistanceThreshold;
    private bool overrideResetPitchOnReset;
    private float overrideResetPitchAngle;

    // TrackingSettingから取得するプロパティ（オーバーライド対応）
    private float cameraDistance => useOverrideValues ? overrideCameraDistance : (trackingSetting != null ? trackingSetting.CameraDistance : 6f);
    private float cameraHeight => useOverrideValues ? overrideCameraHeight : (trackingSetting != null ? trackingSetting.CameraHeight : 2f);
    private float minPitch => useOverrideValues ? overrideMinPitch : (trackingSetting != null ? trackingSetting.MinPitch : -30f);
    private float maxPitch => useOverrideValues ? overrideMaxPitch : (trackingSetting != null ? trackingSetting.MaxPitch : 70f);
    private float initialPitch => useOverrideValues ? overrideInitialPitch : (trackingSetting != null ? trackingSetting.InitialPitch : -40f);
    private bool enableCollisionAvoidance => useOverrideValues ? overrideEnableCollisionAvoidance : (trackingSetting != null ? trackingSetting.EnableCollisionAvoidance : true);
    private float cameraRadius => useOverrideValues ? overrideCameraRadius : (trackingSetting != null ? trackingSetting.CameraRadius : 0.3f);
    private LayerMask collisionLayers => useOverrideValues ? overrideCollisionLayers : (trackingSetting != null ? trackingSetting.CollisionLayers : -1);
    private float collisionSmoothSpeed => useOverrideValues ? overrideCollisionSmoothSpeed : (trackingSetting != null ? trackingSetting.CollisionSmoothSpeed : 10f);
    private float positionSmoothSpeed => useOverrideValues ? overridePositionSmoothSpeed : (trackingSetting != null ? trackingSetting.PositionSmoothSpeed : 15f);
    private float minDistanceThreshold => useOverrideValues ? overrideMinDistanceThreshold : (trackingSetting != null ? trackingSetting.MinDistanceThreshold : 0.5f);
    private bool resetPitchOnReset => useOverrideValues ? overrideResetPitchOnReset : (trackingSetting != null ? trackingSetting.ResetPitchOnReset : true);
    private float resetPitchAngle => useOverrideValues ? overrideResetPitchAngle : (trackingSetting != null ? trackingSetting.ResetPitchAngle : -40f);
    private bool lockCameraRotation => trackingSetting != null ? trackingSetting.LockCameraRotation : false;
    private Vector3 lockedCameraRotation => trackingSetting != null ? trackingSetting.LockedCameraRotation : Vector3.zero;
    private bool disableVerticalInput => trackingSetting != null ? trackingSetting.DisableVerticalInput : false;

    // カメラ回転角度
    private float cameraYaw = 0f;
    private float cameraPitch = 0f;

    // カメラ位置のスムージング用
    private Vector3 currentCameraPosition;
    private float currentCameraDistance;

    // 外部から入力を受け取る
    private Vector2 lookInput = Vector2.zero;

    /// <summary>
    /// 垂直方向の入力を無効化するかどうかを取得
    /// </summary>
    public bool DisableVerticalInput => disableVerticalInput;

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
        // カメラ回転がロックされている場合は入力を無視
        if (lockCameraRotation)
        {
            return;
        }

        float lookX = lookInput.x * mouseSensitivityMultiplier;
        float lookY = lookInput.y * mouseSensitivityMultiplier;

        // 上下反転の適用
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

        // カメラの角度を決定（ロック時は固定角度、通常時は入力ベース）
        float yaw, pitch;
        if (lockCameraRotation)
        {
            // 固定カメラの場合、lockedCameraRotationから角度を取得
            yaw = lockedCameraRotation.y;
            pitch = lockedCameraRotation.x;
        }
        else
        {
            // 通常の場合、入力ベースの角度を使用
            yaw = cameraYaw;
            pitch = cameraPitch;
        }

        // カメラの理想的な位置を計算
        float yawRad = yaw * Mathf.Deg2Rad;
        float pitchRad = pitch * Mathf.Deg2Rad;

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

        // カメラの回転を設定（lockCameraRotation有効時でもキャラクターを見る）
        // カメラの回転をスムージング（旧実装: transform.LookAt(targetPosition);）
        Quaternion targetRotation = Quaternion.LookRotation(targetPosition - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation,
            Time.deltaTime * positionSmoothSpeed);
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
    /// トラッキング設定を変更
    /// </summary>
    /// <param name="newSetting">新しいトラッキング設定</param>
    public void SetTrackingSetting(TrackingSetting newSetting)
    {
        trackingSetting = newSetting;

        // オーバーライドを解除
        useOverrideValues = false;

        // 設定変更時にカメラ距離を再初期化
        if (trackingSetting != null)
        {
            currentCameraDistance = trackingSetting.CameraDistance;
        }
    }

    /// <summary>
    /// 現在のトラッキング設定を取得
    /// </summary>
    public TrackingSetting GetTrackingSetting()
    {
        return trackingSetting;
    }

    /// <summary>
    /// トランジション用の一時的な設定値を適用
    /// </summary>
    public void SetTransitionValues(
        float distance,
        float height,
        float minPitchValue,
        float maxPitchValue,
        float initialPitchValue,
        bool collisionAvoidance,
        float radius,
        LayerMask layers,
        float collisionSmooth,
        float positionSmooth,
        float minDistance,
        bool resetPitch,
        float resetPitchValue)
    {
        useOverrideValues = true;
        overrideCameraDistance = distance;
        overrideCameraHeight = height;
        overrideMinPitch = minPitchValue;
        overrideMaxPitch = maxPitchValue;
        overrideInitialPitch = initialPitchValue;
        overrideEnableCollisionAvoidance = collisionAvoidance;
        overrideCameraRadius = radius;
        overrideCollisionLayers = layers;
        overrideCollisionSmoothSpeed = collisionSmooth;
        overridePositionSmoothSpeed = positionSmooth;
        overrideMinDistanceThreshold = minDistance;
        overrideResetPitchOnReset = resetPitch;
        overrideResetPitchAngle = resetPitchValue;

        // カメラ距離の更新
        currentCameraDistance = distance;
    }

    /// <summary>
    /// トランジション用のオーバーライドを解除
    /// </summary>
    public void ClearTransitionOverride()
    {
        useOverrideValues = false;
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
