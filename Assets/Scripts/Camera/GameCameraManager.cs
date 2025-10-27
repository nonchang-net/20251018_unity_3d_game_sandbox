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

    /// <summary>前フレームのカメラ状態</summary>
    private CameraState previousCameraState = CameraState.Normal;

    /// <summary>現在のトラッキング設定インデックス</summary>
    private int currentTrackingSettingIndex = 0;

    /// <summary>R3購読管理</summary>
    private IDisposable cameraViewChangeSubscription;

    /// <summary>トランジション制御</summary>
    private bool isTransitioning = false;
    private TrackingSetting transitionFromSetting;
    private TrackingSetting transitionToSetting;
    private float transitionProgress = 0f;

    void Start()
    {
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
            // 初期設定を適用
            ApplyTrackingSetting(0);
        }

        // カメラビュー切り替えイベントを購読
        SubscribeCameraViewChangeEvents();
    }

    void OnDestroy()
    {
        // R3購読の解放
        cameraViewChangeSubscription?.Dispose();
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
            SwitchToNextTrackingSetting();
        });
    }

    /// <summary>
    /// 次のトラッキング設定に切り替える
    /// </summary>
    private void SwitchToNextTrackingSetting()
    {
        if (togglableTrackingSettings == null || togglableTrackingSettings.Length == 0)
        {
            Debug.LogWarning("GameCameraManager: togglableTrackingSettings が設定されていないため、カメラ切り替えできません。");
            return;
        }

        // トランジション中の場合は、現在のトランジションをキャンセルして新しいトランジションを開始
        if (isTransitioning && enableTransitionAnimation)
        {
            // 現在のトランジションを完了させる
            CompleteTransition();
        }

        // 次のインデックスに進む（ループ）
        int nextIndex = (currentTrackingSettingIndex + 1) % togglableTrackingSettings.Length;

        // 新しい設定を適用
        ApplyTrackingSetting(nextIndex);
    }

    /// <summary>
    /// 指定されたインデックスのトラッキング設定を適用
    /// </summary>
    /// <param name="index">適用する設定のインデックス</param>
    private void ApplyTrackingSetting(int index)
    {
        if (togglableTrackingSettings == null || index < 0 || index >= togglableTrackingSettings.Length)
        {
            Debug.LogError($"GameCameraManager: 無効なトラッキング設定インデックス {index}");
            return;
        }

        TrackingSetting newSetting = togglableTrackingSettings[index];
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

        // トランジションアニメーションが有効な場合
        if (enableTransitionAnimation && transitionDuration > 0f)
        {
            // 現在の設定を取得
            TrackingSetting currentSetting = togglableTrackingSettings[currentTrackingSettingIndex];
            if (currentSetting == null)
            {
                // 現在の設定がnullの場合は即座に切り替え
                gameManager.CharacterTracker.SetTrackingSetting(newSetting);
                currentTrackingSettingIndex = index;
                return;
            }

            // トランジション開始
            StartTransition(currentSetting, newSetting);
            currentTrackingSettingIndex = index;
        }
        else
        {
            // トランジションなしで即座に切り替え
            gameManager.CharacterTracker.SetTrackingSetting(newSetting);
            currentTrackingSettingIndex = index;
        }
    }

    /// <summary>
    /// トランジションを開始
    /// </summary>
    private void StartTransition(TrackingSetting fromSetting, TrackingSetting toSetting)
    {
        isTransitioning = true;
        transitionFromSetting = fromSetting;
        transitionToSetting = toSetting;
        transitionProgress = 0f;
    }

    /// <summary>
    /// トランジションを更新
    /// </summary>
    private void UpdateTransition()
    {
        if (!isTransitioning) return;

        // 進行度を更新
        transitionProgress += Time.deltaTime / transitionDuration;

        if (transitionProgress >= 1f)
        {
            // トランジション完了
            CompleteTransition();
            return;
        }

        // 設定値を補間
        float lerpedDistance = Mathf.Lerp(transitionFromSetting.CameraDistance, transitionToSetting.CameraDistance, transitionProgress);
        float lerpedHeight = Mathf.Lerp(transitionFromSetting.CameraHeight, transitionToSetting.CameraHeight, transitionProgress);
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
            lerpedHeight,
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
    }

    /// <summary>
    /// トランジションを完了
    /// </summary>
    private void CompleteTransition()
    {
        isTransitioning = false;
        transitionProgress = 1f;

        // 最終的な設定を適用
        if (gameManager.CharacterTracker != null && transitionToSetting != null)
        {
            gameManager.CharacterTracker.SetTrackingSetting(transitionToSetting);
        }
    }
}
