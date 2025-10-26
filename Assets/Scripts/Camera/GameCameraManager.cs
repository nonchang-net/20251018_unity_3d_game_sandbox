using UnityEngine;

/// <summary>
/// ゲーム中のカメラを管理するクラス
/// - カメラの状態管理（水中判定など）
/// - CharacterTrackerとは異なり、ゲーム中のカメラの状態を集約的に管理
/// </summary>
public class GameCameraManager : MonoBehaviour
{
    [Header("GameManager")]
    [SerializeField] private GameManager gameManager;

    [Header("Camera Management")]
    [SerializeField] private Camera currentCamera;

    /// <summary>前フレームのカメラ状態</summary>
    private CameraState previousCameraState = CameraState.Normal;

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
}
