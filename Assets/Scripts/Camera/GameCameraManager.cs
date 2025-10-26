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

    /// <summary>前フレームのカメラ状態</summary>
    private CameraState previousCameraState = CameraState.Normal;

    /// <summary>現在のトラッキング設定インデックス</summary>
    private int currentTrackingSettingIndex = 0;

    /// <summary>R3購読管理</summary>
    private IDisposable cameraViewChangeSubscription;

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

        // 次のインデックスに進む（ループ）
        currentTrackingSettingIndex = (currentTrackingSettingIndex + 1) % togglableTrackingSettings.Length;

        // 新しい設定を適用
        ApplyTrackingSetting(currentTrackingSettingIndex);
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

        // CharacterTrackerに新しい設定を適用
        if (gameManager.CharacterTracker != null)
        {
            gameManager.CharacterTracker.SetTrackingSetting(newSetting);
            Debug.Log($"GameCameraManager: トラッキング設定を '{newSetting.name}' に切り替えました。");
        }
        else
        {
            Debug.LogError("GameCameraManager: CharacterTracker が見つかりません。");
        }
    }
}
