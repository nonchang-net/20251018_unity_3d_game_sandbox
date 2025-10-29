using UnityEngine;

/// <summary>
/// カメラトラッキングの設定を保持するScriptableObject
/// CharacterTrackerで使用する各種パラメータをプリセットとして保存可能
/// </summary>
[CreateAssetMenu(fileName = "TrackingSetting", menuName = "Game/Camera/Tracking Setting", order = 1)]
public class TrackingSetting : ScriptableObject
{
    [Header("カメラ設定")]
    [Tooltip("カメラとターゲットの距離")]
    [SerializeField] private float cameraDistance = 6f;
    public float CameraDistance => cameraDistance;

    [Tooltip("カメラの高さオフセット")]
    [SerializeField] private float cameraHeight = 2f;
    public float CameraHeight => cameraHeight;

    [Header("カメラ制限")]
    [Tooltip("最小ピッチ角度（下向き制限）")]
    [SerializeField] private float minPitch = -30f;
    public float MinPitch => minPitch;

    [Tooltip("最大ピッチ角度（上向き制限）")]
    [SerializeField] private float maxPitch = 70f;
    public float MaxPitch => maxPitch;

    [Tooltip("初期ピッチ角度")]
    [SerializeField] private float initialPitch = -40f;
    public float InitialPitch => initialPitch;

    [Header("障害物回避")]
    [Tooltip("障害物回避を有効にするか")]
    [SerializeField] private bool enableCollisionAvoidance = true;
    public bool EnableCollisionAvoidance => enableCollisionAvoidance;

    [Tooltip("カメラの半径（障害物判定用）")]
    [SerializeField] private float cameraRadius = 0.3f;
    public float CameraRadius => cameraRadius;

    [Tooltip("障害物判定に使用するレイヤーマスク")]
    [SerializeField] private LayerMask collisionLayers = -1;
    public LayerMask CollisionLayers => collisionLayers;

    [Tooltip("障害物回避時のスムージング速度")]
    [SerializeField] private float collisionSmoothSpeed = 10f;
    public float CollisionSmoothSpeed => collisionSmoothSpeed;

    [Header("カメラスムージング")]
    [Tooltip("カメラ位置のスムージング速度")]
    [SerializeField] private float positionSmoothSpeed = 15f;
    public float PositionSmoothSpeed => positionSmoothSpeed;

    [Tooltip("最小距離閾値（これ以下の距離では障害物判定を行わない）")]
    [SerializeField] private float minDistanceThreshold = 0.5f;
    public float MinDistanceThreshold => minDistanceThreshold;

    [Header("カメラリセット設定")]
    [Tooltip("カメラリセット時にピッチ角度もリセットするか")]
    [SerializeField] private bool resetPitchOnReset = true;
    public bool ResetPitchOnReset => resetPitchOnReset;

    [Tooltip("カメラリセット時の目標ピッチ角度")]
    [SerializeField] private float resetPitchAngle = -40f;
    public float ResetPitchAngle => resetPitchAngle;

    [Header("カメラロック設定")]
    [Tooltip("カメラの向きを固定する（2Dゲーム風の操作）")]
    [SerializeField] private bool lockCameraRotation = false;
    public bool LockCameraRotation => lockCameraRotation;

    [Tooltip("カメラ固定時の角度（Euler Angles）\nLockCameraRotationがtrueの場合に適用されます")]
    [SerializeField] private Vector3 lockedCameraRotation = new Vector3(0f, 0f, 0f);
    public Vector3 LockedCameraRotation => lockedCameraRotation;

    [Tooltip("カメラ固定時に上下方向の入力を無効化する（2Dゲーム風の操作）")]
    [SerializeField] private bool disableVerticalInput = false;
    public bool DisableVerticalInput => disableVerticalInput;
}
