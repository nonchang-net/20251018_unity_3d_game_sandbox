using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using R3;
using System;
using System.Collections;

/// <summary>
/// ゲーム全体のポストプロセッシング効果を管理
/// - 移動速度に応じたポストプロセス効果（レンズディストーション、ビネット、モーションブラー）
/// - ダメージ時の画面赤色フラッシュ効果
/// - HP警告時の画面赤色点滅効果
/// </summary>
public class GamePostProcessManager : MonoBehaviour
{
    [Header("GameManager")]
    [SerializeField] private GameManager gameManager;

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

    [Header("ダメージ時フラッシュ設定")]
    [SerializeField] private bool enableDamageFlash = true;
    [SerializeField] private Color damageFlashColor = new Color(1f, 0f, 0f, 1f);  // 赤色
    [SerializeField] private float damageFlashDuration = 0.3f;  // フラッシュ持続時間
    [SerializeField] private float damageFlashIntensity = 0.5f; // フラッシュの明るさ強度

    [Header("HP警告時点滅設定")]
    [SerializeField] private bool enableCautionBlink = true;
    [SerializeField] private Color cautionBlinkColor = new Color(1f, 0f, 0f, 1f);  // 赤色
    [SerializeField] private float cautionBlinkCycle = 1.5f;  // 点滅周期（秒）
    [SerializeField] private float cautionBlinkIntensity = 0.3f; // 点滅の明るさ強度

    [Header("死亡時暗転設定")]
    [SerializeField] private bool enableDeadFade = true;
    [Tooltip("死亡アニメーション表示後、暗転を開始するまでの待機時間（秒）")]
    [SerializeField] private float deadFadeDelay = 1.5f;
    [Tooltip("暗転アニメーションの長さ（秒）")]
    [SerializeField] private float deadFadeDuration = 1.0f;

    [Header("水中効果設定")]
    [SerializeField] private bool enableWaterEffect = true;
    [Tooltip("水中時のカラーフィルター色（青緑色）")]
    [SerializeField] private Color waterColor = new Color(0.01f, 0.9f, 0.28f, 1f);
    [Tooltip("水中時のカラーフィルター強度")]
    [SerializeField] private float waterColorIntensity = 0.6f;
    [Tooltip("水中時のビネット強度")]
    [SerializeField] private float waterVignetteIntensity = 0.8f;
    [Tooltip("水中効果の揺らぎ周期（秒）")]
    [SerializeField] private float waterWaveCycle = 5.0f;
    [Tooltip("水中効果の揺らぎ強度")]
    [SerializeField] private float waterWaveIntensity = 0.3f;

    // ポストプロセッシングコンポーネント
    private LensDistortion lensDistortion;
    private Vignette vignette;
    private MotionBlur motionBlur;
    private ChromaticAberration chromaticAberration;
    private ColorAdjustments colorAdjustments;

    // 現在の速度（外部から設定）
    private float currentSpeed = 0f;
    private float currentEffectIntensity = 0f; // 0 = 通常, 1 = 最大ダッシュ効果

    // ダメージフラッシュ制御
    private float damageFlashProgress = 0f;
    private bool isDamageFlashing = false;

    // HP警告時点滅制御
    private bool isCautionMode = false;
    private float cautionBlinkTime = 0f;

    // 死亡時暗転制御
    private bool isDeadFading = false;
    private bool isWaitingForDeadFade = false;  // 暗転開始待機中フラグ
    private float deadFadeWaitTimer = 0f;       // 暗転開始までの待機タイマー
    private float deadFadeProgress = 0f;

    // 水中効果制御
    private bool isInWater = false;
    private float waterEffectTime = 0f;

    // R3購読管理
    private IDisposable damageSubscription;
    private IDisposable cautionSubscription;
    private IDisposable deadSubscription;
    private IDisposable cameraStateSubscription;

    void Start()
    {
        InitializePostProcessing();
        SubscribeDamageEvents();
        SubscribeCautionEvents();
        SubscribeDeadEvents();
        SubscribeCameraStateEvents();
    }

    /// <summary>
    /// ダメージイベントを購読
    /// </summary>
    void SubscribeDamageEvents()
    {
        damageSubscription = gameManager.StateManager.State.OnDamageReceived.Subscribe(damageInfo =>
        {
            if (enableDamageFlash)
            {
                StartDamageFlash();
            }
        });
    }

    /// <summary>
    /// HP警告状態を購読
    /// </summary>
    void SubscribeCautionEvents()
    {
        cautionSubscription = gameManager.StateManager.State.IsCaution.Subscribe(isCaution =>
        {
            isCautionMode = isCaution;
            cautionBlinkTime = 0f;

            // 警告モードが終了したらColorFilterをリセット
            if (!isCaution && colorAdjustments != null && !isDamageFlashing)
            {
                colorAdjustments.colorFilter.value = Color.white;
            }
        });
    }

    /// <summary>
    /// 死亡イベントを購読
    /// </summary>
    void SubscribeDeadEvents()
    {
        deadSubscription = gameManager.StateManager.State.IsDead.Subscribe(isDead =>
        {
            if (isDead && enableDeadFade)
            {
                StartDeadFade();
            }
        });
    }

    /// <summary>
    /// カメラ状態変更イベントを購読
    /// </summary>
    void SubscribeCameraStateEvents()
    {
        cameraStateSubscription = gameManager.StateManager.State.CurrentCameraState.Subscribe(cameraState =>
        {
            // 水中状態の更新
            isInWater = (cameraState == CameraState.InWater);

            // 水中状態から抜けた場合は、効果をリセット
            if (!isInWater && enableWaterEffect && colorAdjustments != null && vignette != null)
            {
                // ダメージフラッシュや警告点滅中でない場合のみリセット
                if (!isDamageFlashing && !isCautionMode && !isDeadFading)
                {
                    colorAdjustments.colorFilter.value = Color.white;
                    vignette.intensity.value = normalVignetteIntensity;
                }
                waterEffectTime = 0f;
            }
        });
    }

    /// <summary>
    /// ポストプロセッシングコンポーネントを初期化
    /// </summary>
    void InitializePostProcessing()
    {
        if (postProcessVolume == null)
        {
            Debug.LogWarning("GamePostProcessManager: Post Process Volume が未設定です。");
            return;
        }
        if (postProcessVolume.profile == null)
        {
            Debug.LogWarning("GamePostProcessManager: Post Process Volume profile が未設定です。");
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

        if (enableDamageFlash)
        {
            if (!volumeProfile.TryGet(out colorAdjustments))
            {
                colorAdjustments = volumeProfile.Add<ColorAdjustments>(false);
            }
            colorAdjustments.active = true;
        }
    }

    void Update()
    {
        UpdatePostProcessingEffects();
        UpdateDamageFlash();
        UpdateCautionBlink();
        UpdateDeadFade();
        UpdateWaterEffect();
    }

    void OnDestroy()
    {
        // R3購読の解放
        damageSubscription?.Dispose();
        cautionSubscription?.Dispose();
        deadSubscription?.Dispose();
        cameraStateSubscription?.Dispose();
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
    /// ダメージフラッシュを開始
    /// </summary>
    void StartDamageFlash()
    {
        isDamageFlashing = true;
        damageFlashProgress = 0f;
    }

    /// <summary>
    /// ダメージフラッシュエフェクトを更新
    /// </summary>
    void UpdateDamageFlash()
    {
        if (!isDamageFlashing || colorAdjustments == null) return;

        // フラッシュの進行度を更新
        damageFlashProgress += Time.deltaTime / damageFlashDuration;

        if (damageFlashProgress >= 1f)
        {
            // フラッシュ終了
            isDamageFlashing = false;
            damageFlashProgress = 1f;
        }

        // フラッシュ強度をイージング（最初は強く、徐々に減衰）
        float easedProgress = 1f - damageFlashProgress;
        float currentFlashIntensity = easedProgress * damageFlashIntensity;

        // ColorAdjustmentsのColorFilterを使用して画面を赤く染める
        Color flashColor = Color.Lerp(Color.white, damageFlashColor, currentFlashIntensity);
        colorAdjustments.colorFilter.value = flashColor;
    }

    /// <summary>
    /// HP警告時の点滅エフェクトを更新
    /// </summary>
    void UpdateCautionBlink()
    {
        if (!enableCautionBlink || !isCautionMode || colorAdjustments == null) return;

        // ダメージフラッシュ中は警告点滅を行わない
        if (isDamageFlashing) return;

        // 死亡フェード中は警告点滅を行わない
        if (isDeadFading) return;

        // 点滅時間を更新
        cautionBlinkTime += Time.deltaTime;

        // サイン波で滑らかな点滅を作成（0〜1の範囲）
        float blinkPhase = Mathf.Sin(cautionBlinkTime * Mathf.PI * 2f / cautionBlinkCycle);
        blinkPhase = (blinkPhase + 1f) * 0.5f; // -1〜1 を 0〜1 に変換

        // 点滅強度を計算
        float currentBlinkIntensity = blinkPhase * cautionBlinkIntensity;

        // ColorAdjustmentsのColorFilterを使用して画面を赤く染める
        Color blinkColor = Color.Lerp(Color.white, cautionBlinkColor, currentBlinkIntensity);
        colorAdjustments.colorFilter.value = blinkColor;
    }

    /// <summary>
    /// 死亡時の暗転を開始（待機タイマーをセット）
    /// </summary>
    void StartDeadFade()
    {
        isWaitingForDeadFade = true;
        deadFadeWaitTimer = 0f;
        deadFadeProgress = 0f;
    }

    /// <summary>
    /// 死亡時の暗転エフェクトを更新
    /// </summary>
    void UpdateDeadFade()
    {
        if (colorAdjustments == null) return;

        // 暗転開始待機中の処理
        if (isWaitingForDeadFade)
        {
            deadFadeWaitTimer += Time.deltaTime;

            if (deadFadeWaitTimer >= deadFadeDelay)
            {
                // 待機時間が経過したら暗転を開始
                isWaitingForDeadFade = false;
                isDeadFading = true;
            }

            return;
        }

        // 暗転エフェクトの実行
        if (!isDeadFading) return;

        // 暗転の進行度を更新
        deadFadeProgress += Time.deltaTime / deadFadeDuration;

        if (deadFadeProgress >= 1f)
        {
            deadFadeProgress = 1f;
        }

        // 暗転強度を計算（0 → -100で完全に黒くなる）
        float exposure = Mathf.Lerp(0f, -100f, deadFadeProgress);
        colorAdjustments.postExposure.value = exposure;
    }

    /// <summary>
    /// 暗転を解除してゲームを再開
    /// </summary>
    public void ClearDeadFade()
    {
        isWaitingForDeadFade = false;
        isDeadFading = false;
        deadFadeWaitTimer = 0f;
        deadFadeProgress = 0f;

        if (colorAdjustments != null)
        {
            colorAdjustments.postExposure.value = 0f;
            colorAdjustments.colorFilter.value = Color.white;
        }
    }

    /// <summary>
    /// エフェクトを即座にリセット
    /// </summary>
    public void ResetEffects()
    {
        currentSpeed = 0f;
        currentEffectIntensity = 0f;
        isDamageFlashing = false;
        damageFlashProgress = 0f;
        isCautionMode = false;
        cautionBlinkTime = 0f;

        UpdatePostProcessingEffects();

        if (colorAdjustments != null)
        {
            colorAdjustments.colorFilter.value = Color.white;
        }
    }

    /// <summary>
    /// 水中効果を更新
    /// </summary>
    void UpdateWaterEffect()
    {
        if (!enableWaterEffect || !isInWater || colorAdjustments == null || vignette == null) return;

        // ダメージフラッシュ中、警告点滅中、死亡フェード中は水中効果を適用しない
        if (isDamageFlashing || isCautionMode || isDeadFading) return;

        // 時間を更新
        waterEffectTime += Time.deltaTime;

        // サイン波で揺らぎを作成（0〜1の範囲）
        float wave = Mathf.Sin(waterEffectTime * Mathf.PI * 2f / waterWaveCycle);
        wave = (wave + 1f) * 0.5f; // -1〜1 を 0〜1 に変換

        // 揺らぎ強度を計算
        float waveEffect = Mathf.Lerp(1f - waterWaveIntensity, 1f + waterWaveIntensity, wave);

        // カラーフィルターを適用（水の色）
        Color currentWaterColor = Color.Lerp(Color.white, waterColor, waterColorIntensity * waveEffect);
        colorAdjustments.colorFilter.value = currentWaterColor;

        // ビネット強度を適用（水中の暗さ）
        vignette.intensity.value = Mathf.Lerp(normalVignetteIntensity, waterVignetteIntensity, waveEffect);
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
            case "damageflash":
                enableDamageFlash = enabled;
                break;
            case "cautionblink":
                enableCautionBlink = enabled;
                break;
            case "watereffect":
                enableWaterEffect = enabled;
                break;
        }
    }
}
