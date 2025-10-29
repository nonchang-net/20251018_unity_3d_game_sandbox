using UnityEngine;

/// <summary>
/// カメラロックエリアのカメラ設定を管理するコンポーネント
/// CameraLockAreaタグを持つコリジョンと一緒に使用
/// </summary>
public class CameraLocker : MonoBehaviour
{
    [Header("カメラロック設定")]
    [Tooltip("このエリアで使用するカメラ設定のリスト（複数設定可能、Zキーで切り替え）")]
    [SerializeField] private TrackingSetting[] trackingSettings;

    /// <summary>
    /// カメラ設定のリストを取得
    /// </summary>
    /// <returns>TrackingSettingの配列（null または空の可能性あり）</returns>
    public TrackingSetting[] GetTrackingSettings()
    {
        return trackingSettings;
    }

    void OnValidate()
    {
        // Inspectorで変更時の検証
        if (trackingSettings != null && trackingSettings.Length > 0)
        {
            int nullCount = 0;
            foreach (var setting in trackingSettings)
            {
                if (setting == null)
                {
                    nullCount++;
                }
            }

            if (nullCount > 0)
            {
                Debug.LogWarning($"CameraLocker: {nullCount}個のnull TrackingSettingが含まれています。設定を確認してください。", this);
            }
        }
        else
        {
            Debug.LogWarning("CameraLocker: TrackingSettings が設定されていません。", this);
        }
    }
}
