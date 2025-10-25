using UnityEngine;

/// <summary>
/// ゲームの設定値を管理するクラス
/// PlayerPrefsを使用して設定値を永続化する
/// </summary>
public class GameSettings
{
    // PlayerPrefsのキー定義
    private const string KEY_CAMERA_INVERT_Y = "Settings_CameraInvertY";
    private const string KEY_MOUSE_SENSITIVITY = "Settings_MouseSensitivity";
    private const string KEY_BGM_VOLUME = "Settings_BGMVolume";
    private const string KEY_SE_VOLUME = "Settings_SEVolume";

    // デフォルト値
    public const bool DEFAULT_CAMERA_INVERT_Y = false;
    public const float DEFAULT_MOUSE_SENSITIVITY = 100f;  // 100%
    public const float DEFAULT_BGM_VOLUME = 0.8f;         // 80%
    public const float DEFAULT_SE_VOLUME = 0.8f;          // 80%

    /// <summary>上下カメラ操作方向反転</summary>
    public bool CameraInvertY { get; private set; }

    /// <summary>マウス感度（10% ～ 300%）</summary>
    public float MouseSensitivity { get; private set; }

    /// <summary>BGMボリューム（0.0 ～ 1.0）</summary>
    public float BGMVolume { get; private set; }

    /// <summary>サウンドエフェクトボリューム（0.0 ～ 1.0）</summary>
    public float SEVolume { get; private set; }

    /// <summary>
    /// コンストラクタ：PlayerPrefsから設定値を読み込む
    /// </summary>
    public GameSettings()
    {
        LoadSettings();
    }

    /// <summary>
    /// PlayerPrefsから設定値を読み込む
    /// </summary>
    public void LoadSettings()
    {
        CameraInvertY = PlayerPrefs.GetInt(KEY_CAMERA_INVERT_Y, DEFAULT_CAMERA_INVERT_Y ? 1 : 0) == 1;
        MouseSensitivity = PlayerPrefs.GetFloat(KEY_MOUSE_SENSITIVITY, DEFAULT_MOUSE_SENSITIVITY);
        BGMVolume = PlayerPrefs.GetFloat(KEY_BGM_VOLUME, DEFAULT_BGM_VOLUME);
        SEVolume = PlayerPrefs.GetFloat(KEY_SE_VOLUME, DEFAULT_SE_VOLUME);
    }

    /// <summary>
    /// 現在の設定値をPlayerPrefsに保存する
    /// </summary>
    public void SaveSettings()
    {
        PlayerPrefs.SetInt(KEY_CAMERA_INVERT_Y, CameraInvertY ? 1 : 0);
        PlayerPrefs.SetFloat(KEY_MOUSE_SENSITIVITY, MouseSensitivity);
        PlayerPrefs.SetFloat(KEY_BGM_VOLUME, BGMVolume);
        PlayerPrefs.SetFloat(KEY_SE_VOLUME, SEVolume);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 上下カメラ操作方向反転を設定
    /// </summary>
    public void SetCameraInvertY(bool value)
    {
        CameraInvertY = value;
        SaveSettings();
    }

    /// <summary>
    /// マウス感度を設定（10% ～ 300%）
    /// </summary>
    public void SetMouseSensitivity(float value)
    {
        MouseSensitivity = Mathf.Clamp(value, 10f, 300f);
        SaveSettings();
    }

    /// <summary>
    /// BGMボリュームを設定（0.0 ～ 1.0）
    /// </summary>
    public void SetBGMVolume(float value)
    {
        BGMVolume = Mathf.Clamp01(value);
        SaveSettings();
    }

    /// <summary>
    /// サウンドエフェクトボリュームを設定（0.0 ～ 1.0）
    /// </summary>
    public void SetSEVolume(float value)
    {
        SEVolume = Mathf.Clamp01(value);
        SaveSettings();
    }

    /// <summary>
    /// すべての設定値をデフォルトに戻す
    /// </summary>
    public void ResetToDefault()
    {
        CameraInvertY = DEFAULT_CAMERA_INVERT_Y;
        MouseSensitivity = DEFAULT_MOUSE_SENSITIVITY;
        BGMVolume = DEFAULT_BGM_VOLUME;
        SEVolume = DEFAULT_SE_VOLUME;
        SaveSettings();
    }
}
