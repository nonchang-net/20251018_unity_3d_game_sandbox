using UnityEngine;
using R3;
using System;

/// <summary>
/// ゲーム中のカメラを管理するクラス
/// - カメラの状態管理（水中判定など）
/// - カメラビュー切り替え（ズームイン/アウト）
/// - CharacterTrackerとは異なり、ゲーム中のカメラの状態を集約的に管理
/// </summary>
public class GameCameraManager : MonoBehaviour
{
    /// <summary>
    /// カメラ系の詳細ログを有効にするかどうか（GameManagerから設定される）
    /// </summary>
    public static bool EnableCameraVerboseLog { get; set; } = false;

    [Header("GameManager")]
    [SerializeField] private GameManager gameManager;

    [Header("Camera Management")]
    [SerializeField] private Camera currentCamera;

    [Header("Tracking Settings")]
    [Tooltip("切り替え可能なトラッキング設定の配列")]
    [SerializeField] private TrackingSetting[] togglableTrackingSettings;

    [Header("Transition Settings")]
    [Tooltip("設定切り替え時にスムージングを有効にする")]
    [SerializeField] private bool enableTransitionAnimation = true;
    [Tooltip("設定切り替えのアニメーション時間（秒）")]
    [SerializeField] private float transitionDuration = 0.5f;

    [Header("CameraLockArea Transition Settings")]
    [Tooltip("CameraLockArea進入・退出時にスムージングを有効にする")]
    [SerializeField] private bool enableCameraLockAreaTransition = true;
    [Tooltip("CameraLockArea進入・退出時のアニメーション時間（秒）")]
    [SerializeField] private float cameraLockAreaTransitionDuration = 0.8f;

    /// <summary>前フレームのカメラ状態</summary>
    private CameraState previousCameraState = CameraState.Normal;

    /// <summary>現在のトラッキング設定インデックス</summary>
    private int currentTrackingSettingIndex = 0;

    /// <summary>CameraLockArea用の一時的なトラッキング設定</summary>
    private TrackingSetting[] temporaryTrackingSettings = null;
    /// <summary>一時設定を使用する前のオリジナル設定のインデックス</summary>
    private int originalTrackingSettingIndex = 0;

    /// <summary>R3購読管理</summary>
    private IDisposable cameraViewChangeSubscription;
    private IDisposable cameraLockAreaEnterSubscription;
    private IDisposable cameraLockAreaExitSubscription;

    /// <summary>トランジション制御</summary>
    private bool isTransitioning = false;
    private TrackingSetting transitionFromSetting;
    private TrackingSetting transitionToSetting;
    private float transitionProgress = 0f;
    private float currentTransitionDuration = 0f; // 現在のトランジション時間

    /// <summary>トランジション用のカメラ回転目標</summary>
    private Quaternion transitionToRotation;

    /// <summary>
    /// GameManagerから呼び出される初期化メソッド
    /// シリアライゼーション完了後に確実に呼び出されることを保証
    /// </summary>
    public void Initialize()
    {
        if (EnableCameraVerboseLog)
        {
            Debug.Log("[GameCameraManager] Initialize() が呼び出されました。");
        }

        // note: コンパイル直後などでなぜかnullじゃなくなって動作不良に陥る症状を確認したので、初期化時にnull代入しておく。正確な原因を追求できていないので要注意。ドメインリロードオフ周りなどの可能性？
        temporaryTrackingSettings = null;

        // 参照確認
        if (gameManager == null)
        {
            Debug.LogError("GameCameraManager: GameManager が設定されていません。");
            return;
        }

        if (currentCamera == null)
        {
            Debug.LogError("GameCameraManager: currentCamera が設定されていません。");
            return;
        }

        // トラッキング設定の検証
        if (togglableTrackingSettings == null || togglableTrackingSettings.Length == 0)
        {
            Debug.LogWarning("GameCameraManager: togglableTrackingSettings が設定されていません。カメラ切り替え機能は無効化されます。");
        }
        else
        {
            if (EnableCameraVerboseLog)
            {
                Debug.Log($"[GameCameraManager] トラッキング設定が {togglableTrackingSettings.Length} 個見つかりました。初期設定を適用します。");
            }

            // 初期設定を適用
            ApplyTrackingSetting(0);
        }

        // イベント購読（トラッキング設定の準備が完了した後に開始）
        if (EnableCameraVerboseLog)
        {
            Debug.Log("[GameCameraManager] カメライベントを購読します。");
        }

        SubscribeCameraViewChangeEvents();
        SubscribeCameraLockAreaEvents();

        if (EnableCameraVerboseLog)
        {
            Debug.Log("[GameCameraManager] Initialize() が完了しました。");
        }
    }

    void OnDestroy()
    {
        // R3購読の解放
        cameraViewChangeSubscription?.Dispose();
        cameraLockAreaEnterSubscription?.Dispose();
        cameraLockAreaExitSubscription?.Dispose();
    }

    void Update()
    {
        // 水中判定を行い、カメラ状態を更新
        CheckWaterState();

        // トランジション処理
        if (isTransitioning)
        {
            UpdateTransition();
        }
    }

    /// <summary>
    /// カメラが水中にいるかどうかを判定し、状態を更新
    /// </summary>
    private void CheckWaterState()
    {
        if (gameManager == null || currentCamera == null) return;

        // カメラの現在位置を取得
        float cameraY = currentCamera.transform.position.y;

        // 水面の高さを取得
        float waterSurfaceHeight = gameManager.WaterSurfaceHeight;

        // カメラが水面以下にいるか判定
        bool isUnderWater = cameraY <= waterSurfaceHeight;

        // 現在のカメラ状態を判定
        CameraState currentState = isUnderWater ? CameraState.InWater : CameraState.Normal;

        // 状態が変わった場合のみGameStateManagerに通知
        if (currentState != previousCameraState)
        {
            gameManager.StateManager.SetCameraState(currentState);
            previousCameraState = currentState;
        }
    }

    /// <summary>
    /// カメラビュー切り替えイベントを購読
    /// </summary>
    private void SubscribeCameraViewChangeEvents()
    {
        cameraViewChangeSubscription = gameManager.StateManager.State.OnCameraViewChangeRequested.Subscribe(_ =>
        {
            if (EnableCameraVerboseLog)
            {
                Debug.Log("[GameCameraManager] カメラビュー切り替えイベントを受信しました。");
            }

            SwitchToNextTrackingSetting();
        });

        if (EnableCameraVerboseLog)
        {
            Debug.Log("[GameCameraManager] カメラビュー切り替えイベントの購読が完了しました。");
        }
    }

    /// <summary>
    /// CameraLockAreaイベントを購読
    /// </summary>
    private void SubscribeCameraLockAreaEvents()
    {
        // CameraLockArea進入イベント
        cameraLockAreaEnterSubscription = gameManager.StateManager.State.OnCameraLockAreaEnter.Subscribe(info =>
        {
            SetTemporaryTrackingSettings(info.TrackingSettings);
        });

        // CameraLockArea退出イベント
        cameraLockAreaExitSubscription = gameManager.StateManager.State.OnCameraLockAreaExit.Subscribe(info =>
        {
            ClearTemporaryTrackingSettings();
        });

        if (EnableCameraVerboseLog)
        {
            Debug.Log("[GameCameraManager] CameraLockAreaイベントの購読が完了しました。");
        }
    }

    /// <summary>
    /// 次のトラッキング設定に切り替える
    /// </summary>
    private void SwitchToNextTrackingSetting()
    {
        if (EnableCameraVerboseLog)
        {
            Debug.Log("[GameCameraManager] SwitchToNextTrackingSetting() が呼び出されました。");
        }

        // 一時設定があればそれを優先使用
        TrackingSetting[] activeSettings = temporaryTrackingSettings ?? togglableTrackingSettings;

        if (activeSettings == null || activeSettings.Length == 0)
        {
            Debug.LogWarning("GameCameraManager: トラッキング設定が設定されていないため、カメラ切り替えできません。");
            return;
        }

        if (EnableCameraVerboseLog)
        {
            Debug.Log($"[GameCameraManager] アクティブな設定数: {activeSettings.Length}, 現在のインデックス: {currentTrackingSettingIndex}");
        }

        // トランジション中の場合は、現在のトランジションをキャンセルして新しいトランジションを開始
        if (isTransitioning && enableTransitionAnimation)
        {
            // 現在のトランジションを完了させる
            CompleteTransition();
        }

        // 次のインデックスに進む（ループ）
        int nextIndex = (currentTrackingSettingIndex + 1) % activeSettings.Length;

        if (EnableCameraVerboseLog)
        {
            Debug.Log($"[GameCameraManager] 次のインデックス {nextIndex} の設定に切り替えます。");
        }

        // 新しい設定を適用
        ApplyTrackingSetting(nextIndex);
    }

    /// <summary>
    /// 指定されたインデックスのトラッキング設定を適用
    /// </summary>
    /// <param name="index">適用する設定のインデックス</param>
    /// <param name="useCustomTransition">カスタムトランジション設定を使用するか</param>
    /// <param name="customTransitionEnabled">カスタムトランジション有効フラグ</param>
    /// <param name="customTransitionDuration">カスタムトランジション時間</param>
    private void ApplyTrackingSetting(int index, bool useCustomTransition = false, bool customTransitionEnabled = false, float customTransitionDuration = 0f)
    {
        // 一時設定があればそれを優先使用
        TrackingSetting[] activeSettings = temporaryTrackingSettings ?? togglableTrackingSettings;

        if (activeSettings == null || activeSettings.Length == 0)
        {
            Debug.LogWarning($"GameCameraManager: トラッキング設定が設定されていません。初期化をスキップします。");
            if (EnableCameraVerboseLog)
            {
                // バグ調査時の深追いメモ
                Debug.Log($"temporaryTrackingSettings is null? {temporaryTrackingSettings == null}");
                Debug.Log($"activeSettings is null? {activeSettings == null}"); // コンパイル直後時はなぜかこれがnullじゃなくなって評価がおかしくなっていた → Initialize時にnull代入することで改善
                if(activeSettings != null)
                {
                    Debug.Log($"activeSettings length: {activeSettings.Length}");                
                }
            }
            return;
        }

        if (index < 0 || index >= activeSettings.Length)
        {
            Debug.LogError($"GameCameraManager: 無効なトラッキング設定インデックス {index} (設定数: {activeSettings.Length})");
            return;
        }

        TrackingSetting newSetting = activeSettings[index];
        if (newSetting == null)
        {
            Debug.LogError($"GameCameraManager: トラッキング設定 [{index}] が null です。");
            return;
        }

        if (gameManager.CharacterTracker == null)
        {
            Debug.LogError("GameCameraManager: CharacterTracker が見つかりません。");
            return;
        }

        // トランジション設定を決定
        bool shouldTransition;
        float duration;
        if (useCustomTransition)
        {
            shouldTransition = customTransitionEnabled;
            duration = customTransitionDuration;
        }
        else
        {
            shouldTransition = enableTransitionAnimation;
            duration = transitionDuration;
        }

        // 現在CharacterTrackerに適用されている設定を取得
        TrackingSetting currentSetting = gameManager.CharacterTracker.GetTrackingSetting();

        // トランジションアニメーションが有効な場合
        if (shouldTransition && duration > 0f)
        {
            if (currentSetting == null)
            {
                // 現在の設定がnullの場合は即座に切り替え
                gameManager.CharacterTracker.SetTrackingSetting(newSetting);
                currentTrackingSettingIndex = index;
                return;
            }

            // トランジション開始（カスタム時間を使用）
            StartTransition(currentSetting, newSetting, duration);
            currentTrackingSettingIndex = index;
        }
        else
        {
            // トランジションなしで即座に切り替え
            gameManager.CharacterTracker.SetTrackingSettingWithOldSetting(currentSetting, newSetting);
            currentTrackingSettingIndex = index;
        }
    }

    /// <summary>
    /// トランジションを開始
    /// </summary>
    private void StartTransition(TrackingSetting fromSetting, TrackingSetting toSetting, float duration)
    {
        isTransitioning = true;
        transitionFromSetting = fromSetting;
        transitionToSetting = toSetting;
        transitionProgress = 0f;
        currentTransitionDuration = duration;

        // 目標回転を計算（LockCameraRotationの場合のみ固定角度）
        if (toSetting != null && toSetting.LockCameraRotation)
        {
            transitionToRotation = Quaternion.Euler(toSetting.LockedCameraRotation);
        }
    }

    /// <summary>
    /// トランジションを更新
    /// </summary>
    private void UpdateTransition()
    {
        if (!isTransitioning) return;

        // 進行度を更新
        transitionProgress += Time.deltaTime / currentTransitionDuration;

        if (transitionProgress >= 1f)
        {
            // トランジション完了
            CompleteTransition();
            return;
        }

        // 設定値を補間
        float lerpedDistance = Mathf.Lerp(transitionFromSetting.CameraDistance, transitionToSetting.CameraDistance, transitionProgress);
        float lerpedTargetHeight = Mathf.Lerp(transitionFromSetting.TargetHeightOffset, transitionToSetting.TargetHeightOffset, transitionProgress);
        float lerpedMinPitch = Mathf.Lerp(transitionFromSetting.MinPitch, transitionToSetting.MinPitch, transitionProgress);
        float lerpedMaxPitch = Mathf.Lerp(transitionFromSetting.MaxPitch, transitionToSetting.MaxPitch, transitionProgress);
        float lerpedInitialPitch = Mathf.Lerp(transitionFromSetting.InitialPitch, transitionToSetting.InitialPitch, transitionProgress);
        float lerpedCameraRadius = Mathf.Lerp(transitionFromSetting.CameraRadius, transitionToSetting.CameraRadius, transitionProgress);
        float lerpedCollisionSmoothSpeed = Mathf.Lerp(transitionFromSetting.CollisionSmoothSpeed, transitionToSetting.CollisionSmoothSpeed, transitionProgress);
        float lerpedPositionSmoothSpeed = Mathf.Lerp(transitionFromSetting.PositionSmoothSpeed, transitionToSetting.PositionSmoothSpeed, transitionProgress);
        float lerpedMinDistanceThreshold = Mathf.Lerp(transitionFromSetting.MinDistanceThreshold, transitionToSetting.MinDistanceThreshold, transitionProgress);
        float lerpedResetPitchAngle = Mathf.Lerp(transitionFromSetting.ResetPitchAngle, transitionToSetting.ResetPitchAngle, transitionProgress);

        // bool値とLayerMaskは補間せず、中間点で切り替え
        bool useToSettingBools = transitionProgress >= 0.5f;
        bool lerpedEnableCollisionAvoidance = useToSettingBools ? transitionToSetting.EnableCollisionAvoidance : transitionFromSetting.EnableCollisionAvoidance;
        LayerMask lerpedCollisionLayers = useToSettingBools ? transitionToSetting.CollisionLayers : transitionFromSetting.CollisionLayers;
        bool lerpedResetPitchOnReset = useToSettingBools ? transitionToSetting.ResetPitchOnReset : transitionFromSetting.ResetPitchOnReset;

        // CharacterTrackerに補間された値を適用
        gameManager.CharacterTracker.SetTransitionValues(
            lerpedDistance,
            lerpedTargetHeight,
            lerpedMinPitch,
            lerpedMaxPitch,
            lerpedInitialPitch,
            lerpedEnableCollisionAvoidance,
            lerpedCameraRadius,
            lerpedCollisionLayers,
            lerpedCollisionSmoothSpeed,
            lerpedPositionSmoothSpeed,
            lerpedMinDistanceThreshold,
            lerpedResetPitchOnReset,
            lerpedResetPitchAngle
        );

        // カメラ回転と位置計算用の角度を補間してCharacterTrackerに適用
        // 毎フレーム、現在のキャラクター注視点を基準に回転を計算
        Vector3 targetPosition = gameManager.CharacterTracker.GetTargetPosition();
        Vector3 cameraPosition = gameManager.CharacterTracker.transform.position;
        Quaternion currentLookRotation = Quaternion.LookRotation(targetPosition - cameraPosition);

        // 現在の注視角度（yaw/pitch）を計算
        CameraUtility.ExtractYawPitchFromRotation(currentLookRotation, out float currentYaw, out float currentPitch);

        // 目標回転と目標角度を決定
        Quaternion targetRotation;
        float targetYaw, targetPitch;
        if (transitionToSetting != null && transitionToSetting.LockCameraRotation)
        {
            // ロック有効の場合は固定角度
            targetRotation = transitionToRotation;
            CameraUtility.ExtractYawPitchFromEuler(transitionToSetting.LockedCameraRotation, out targetYaw, out targetPitch);
        }
        else
        {
            // ロック無効の場合はキャラクター注視
            targetRotation = currentLookRotation;
            targetYaw = currentYaw;
            targetPitch = currentPitch;
        }

        // 回転と角度を補間
        Quaternion lerpedRotation = CameraUtility.LerpRotation(currentLookRotation, targetRotation, transitionProgress);
        CameraUtility.LerpYawPitch(currentYaw, targetYaw, currentPitch, targetPitch, transitionProgress, out float lerpedYaw, out float lerpedPitch);

        // CharacterTrackerに適用
        gameManager.CharacterTracker.SetTransitionRotation(lerpedRotation, lerpedYaw, lerpedPitch);
    }

    /// <summary>
    /// トランジションを完了
    /// </summary>
    private void CompleteTransition()
    {
        isTransitioning = false;
        transitionProgress = 1f;

        // カメラ回転のオーバーライドをクリア
        gameManager.CharacterTracker?.ClearTransitionRotationOverride();

        // 最終的な設定を適用
        if (gameManager.CharacterTracker != null && transitionToSetting != null)
        {
            // トランジション開始設定を明示的に渡す
            gameManager.CharacterTracker.SetTrackingSettingWithOldSetting(transitionFromSetting, transitionToSetting);
        }
    }

    /// <summary>
    /// CameraLockArea用の一時的なトラッキング設定を設定
    /// Zキーによる切り替え対象がこの設定配列に差し替わる
    /// </summary>
    /// <param name="settings">一時的に使用するTrackingSetting配列</param>
    public void SetTemporaryTrackingSettings(TrackingSetting[] settings)
    {
        if (settings == null || settings.Length == 0)
        {
            Debug.LogWarning("GameCameraManager: SetTemporaryTrackingSettings() に null または空の配列が渡されました。");
            return;
        }

        // 現在のインデックスを保存
        originalTrackingSettingIndex = currentTrackingSettingIndex;

        // 一時設定を設定
        temporaryTrackingSettings = settings;

        // インデックスをリセットして最初の設定を適用（CameraLockArea用のカスタムトランジションを使用）
        currentTrackingSettingIndex = 0;
        ApplyTrackingSetting(0, useCustomTransition: true, customTransitionEnabled: enableCameraLockAreaTransition, customTransitionDuration: cameraLockAreaTransitionDuration);

        if (EnableCameraVerboseLog)
        {
            Debug.Log($"[GameCameraManager] CameraLockArea用の一時トラッキング設定を適用しました（{settings.Length}個）。");
        }
    }

    /// <summary>
    /// CameraLockArea用の一時的なトラッキング設定をクリア
    /// 元のトラッキング設定に戻る
    /// </summary>
    public void ClearTemporaryTrackingSettings()
    {
        if (temporaryTrackingSettings == null)
        {
            // 一時設定が設定されていない場合は何もしない
            return;
        }

        // 一時設定をクリア
        temporaryTrackingSettings = null;

        // 元のインデックスに戻す
        currentTrackingSettingIndex = originalTrackingSettingIndex;

        // 元の設定を適用（CameraLockArea用のカスタムトランジションを使用）
        if (togglableTrackingSettings != null && togglableTrackingSettings.Length > 0)
        {
            ApplyTrackingSetting(currentTrackingSettingIndex, useCustomTransition: true, customTransitionEnabled: enableCameraLockAreaTransition, customTransitionDuration: cameraLockAreaTransitionDuration);

            if (EnableCameraVerboseLog)
            {
                Debug.Log("[GameCameraManager] 一時トラッキング設定をクリアし、元の設定に戻しました。");
            }
        }
    }
}
