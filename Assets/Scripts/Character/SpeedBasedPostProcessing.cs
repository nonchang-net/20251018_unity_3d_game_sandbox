using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 移動速度に応じてポストプロセッシング効果を動的に制御
/// レンズディストーション、ビネット、モーションブラーなどをダッシュ時に強化
/// </summary>
public class SpeedBasedPostProcessing : MonoBehaviour
{
    [Header("ポストプロセッシング設定")]
    [SerializeField] private Volume postProcessVolume;
    [SerializeField] private VolumeProfile volumeProfile;

    [Header("速度設定")]
    [SerializeField] private float normalSpeed = 4f;     // 通常速度
    [SerializeField] private float dashSpeed = 8f;       // ダッシュ速度
    [SerializeField] private float transitionSpeed = 5f;  // エフェクト遷移速度

    [Header("レンズディストーション設定")]
    [SerializeField] private bool enableLensDistortion = true;
    [SerializeField] private float maxDistortionIntensity = -0.3f;  // ダッシュ時の最大歪み強度
    [SerializeField] private float distortionScale = 1f;             // 歪みのスケール

    [Header("ビネット設定")]
    [SerializeField] private bool enableVignette = true;
    [SerializeField] private float normalVignetteIntensity = 0.2f;  // 通常時のビネット強度
    [SerializeField] private float maxVignetteIntensity = 0.45f;     // ダッシュ時の最大ビネット強度

    [Header("モーションブラー設定")]
    [SerializeField] private bool enableMotionBlur = true;
    [SerializeField] private float maxMotionBlurIntensity = 0.6f;   // ダッシュ時の最大モーションブラー強度

    [Header("クロマティックアベレーション設定")]
    [SerializeField] private bool enableChromaticAberration = false;
    [SerializeField] private float maxChromaticIntensity = 0.5f;    // ダッシュ時の色収差強度

    // ポストプロセッシングコンポーネント
    private LensDistortion lensDistortion;
    private Vignette vignette;
    private MotionBlur motionBlur;
    private ChromaticAberration chromaticAberration;

    // 現在の速度（外部から設定）
    private float currentSpeed = 0f;
    private float currentEffectIntensity = 0f; // 0 = 通常, 1 = 最大ダッシュ効果

    void Start()
    {
        InitializePostProcessing();
    }

    /// <summary>
    /// ポストプロセッシングコンポーネントを初期化
    /// </summary>
    void InitializePostProcessing()
    {
        if (postProcessVolume == null)
        {
            Debug.LogWarning("SpeedBasedPostProcessing: Post Process Volume が未設定です。");
            return;
        }
        if (postProcessVolume.profile == null)
        {
            Debug.LogWarning("SpeedBasedPostProcessing: Post Process Volume profile が未設定です。");
            return;
        }

        volumeProfile = postProcessVolume.profile;

        // 各エフェクトコンポーネントを取得または追加
        if (enableLensDistortion)
        {
            if (!volumeProfile.TryGet(out lensDistortion))
            {
                lensDistortion = volumeProfile.Add<LensDistortion>(false);
            }
        }

        if (enableVignette)
        {
            if (!volumeProfile.TryGet(out vignette))
            {
                vignette = volumeProfile.Add<Vignette>(false);
            }
        }

        if (enableMotionBlur)
        {
            if (!volumeProfile.TryGet(out motionBlur))
            {
                motionBlur = volumeProfile.Add<MotionBlur>(false);
            }
        }

        if (enableChromaticAberration)
        {
            if (!volumeProfile.TryGet(out chromaticAberration))
            {
                chromaticAberration = volumeProfile.Add<ChromaticAberration>(false);
            }
        }
    }

    void Update()
    {
        UpdatePostProcessingEffects();
    }

    /// <summary>
    /// 現在の速度を設定（外部から呼び出し用）
    /// </summary>
    public void SetCurrentSpeed(float speed)
    {
        currentSpeed = speed;
    }

    /// <summary>
    /// ポストプロセッシング効果を更新
    /// </summary>
    void UpdatePostProcessingEffects()
    {
        // 速度に基づいてエフェクト強度を計算（0〜1の範囲）
        float targetIntensity = Mathf.InverseLerp(normalSpeed, dashSpeed, currentSpeed);

        // スムーズに遷移
        currentEffectIntensity = Mathf.Lerp(currentEffectIntensity, targetIntensity,
            Time.deltaTime * transitionSpeed);

        // 各エフェクトを更新
        UpdateLensDistortion();
        UpdateVignette();
        UpdateMotionBlur();
        UpdateChromaticAberration();
    }

    /// <summary>
    /// レンズディストーションを更新
    /// </summary>
    void UpdateLensDistortion()
    {
        if (lensDistortion != null && enableLensDistortion)
        {
            lensDistortion.active = currentEffectIntensity > 0.01f;

            // 強度を設定
            lensDistortion.intensity.value = Mathf.Lerp(0f, maxDistortionIntensity, currentEffectIntensity);
            lensDistortion.scale.value = distortionScale;
        }
    }

    /// <summary>
    /// ビネットを更新
    /// </summary>
    void UpdateVignette()
    {
        if (vignette != null && enableVignette)
        {
            vignette.active = true;

            // 通常時からダッシュ時まで滑らかに遷移
            vignette.intensity.value = Mathf.Lerp(normalVignetteIntensity,
                maxVignetteIntensity, currentEffectIntensity);
        }
    }

    /// <summary>
    /// モーションブラーを更新
    /// </summary>
    void UpdateMotionBlur()
    {
        if (motionBlur != null && enableMotionBlur)
        {
            motionBlur.active = currentEffectIntensity > 0.01f;

            // 強度を設定
            motionBlur.intensity.value = Mathf.Lerp(0f, maxMotionBlurIntensity, currentEffectIntensity);
        }
    }

    /// <summary>
    /// クロマティックアベレーション（色収差）を更新
    /// </summary>
    void UpdateChromaticAberration()
    {
        if (chromaticAberration != null && enableChromaticAberration)
        {
            chromaticAberration.active = currentEffectIntensity > 0.01f;

            // 強度を設定
            chromaticAberration.intensity.value = Mathf.Lerp(0f, maxChromaticIntensity,
                currentEffectIntensity);
        }
    }

    /// <summary>
    /// エフェクトを即座にリセット
    /// </summary>
    public void ResetEffects()
    {
        currentSpeed = 0f;
        currentEffectIntensity = 0f;
        UpdatePostProcessingEffects();
    }

    /// <summary>
    /// 特定のエフェクトのON/OFFを切り替え
    /// </summary>
    public void SetEffectEnabled(string effectName, bool enabled)
    {
        switch (effectName.ToLower())
        {
            case "lensdistortion":
                enableLensDistortion = enabled;
                if (lensDistortion != null) lensDistortion.active = enabled;
                break;
            case "vignette":
                enableVignette = enabled;
                if (vignette != null) vignette.active = enabled;
                break;
            case "motionblur":
                enableMotionBlur = enabled;
                if (motionBlur != null) motionBlur.active = enabled;
                break;
            case "chromaticaberration":
                enableChromaticAberration = enabled;
                if (chromaticAberration != null) chromaticAberration.active = enabled;
                break;
        }
    }
}
