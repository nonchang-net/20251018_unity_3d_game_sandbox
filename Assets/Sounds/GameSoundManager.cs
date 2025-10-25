/*
シンプルなサウンド管理

- やること
  - BGM/SFXの再生管理
  - sound listenerもカメラから剥がしてここに配置

- やらないこと
- - 3Dサウンド系の処理は一旦保留。あとで別枠で考えたい
  - マテリアル別のfoot stepサウンド切り替え処理的なこと
    - 3D GameKitがやってたけど今はいいや

*/

using Unity.Collections;
using UnityEngine;
using R3;
using System;
using System.Collections.Generic;

/// <summary>
/// サウンドのカテゴリー分類
/// </summary>
public enum SoundCategory
{
    /// <summary>背景音楽</summary>
    BGM,
    /// <summary>サウンドエフェクト</summary>
    SE
}

public class GameSoundManager : MonoBehaviour
{
    [Header("GameManager")]
    [SerializeField] private GameManager gameManager;

    [Header("ボリューム設定")]
    private float masterVolume = 1.0f;
    private float bgmVolume = 0.8f;
    private float seVolume = 0.8f;

    [SerializeField] AudioClip[] bgms;

    [SerializeField] AudioClip coinGetSound;
    [SerializeField] AudioClip checkPointActivatedSound;
    [SerializeField] AudioClip highJumpSound;

    [SerializeField] AudioClip damagedSound;
    [SerializeField] AudioClip cautionSound;

    // カテゴリー別AudioSource管理
    private Dictionary<SoundCategory, List<AudioSource>> audioSourcesByCategory = new Dictionary<SoundCategory, List<AudioSource>>();

    private AudioSource bgmAudioSource;
    private AudioSource sfxAudioSource;
    private AudioSource cautionAudioSource;

    // R3購読管理
    private IDisposable damageSubscription;
    private IDisposable cautionSubscription;
    private IDisposable coinGetSubscription;
    private IDisposable checkPointActivatedSubscription;
    private IDisposable highJumpSubscription;

    private void Awake()
    {
        // カテゴリー別辞書の初期化
        audioSourcesByCategory[SoundCategory.BGM] = new List<AudioSource>();
        audioSourcesByCategory[SoundCategory.SE] = new List<AudioSource>();

        // BGM用AudioSource
        bgmAudioSource = gameObject.AddComponent<AudioSource>();
        bgmAudioSource.loop = true;
        bgmAudioSource.volume = bgmVolume * masterVolume;
        RegisterAudioSource(bgmAudioSource, SoundCategory.BGM);

        if (bgms != null && bgms.Length > 0)
        {
            bgmAudioSource.clip = bgms[0];
            bgmAudioSource.Play();
        }
        else
        {
            Debug.LogWarning("BGMが登録されていません。");
        }

        // SFX用AudioSource
        sfxAudioSource = gameObject.AddComponent<AudioSource>();
        sfxAudioSource.loop = false;
        sfxAudioSource.volume = seVolume * masterVolume;
        RegisterAudioSource(sfxAudioSource, SoundCategory.SE);

        // 警告サウンド用AudioSource
        cautionAudioSource = gameObject.AddComponent<AudioSource>();
        cautionAudioSource.loop = true;
        cautionAudioSource.volume = seVolume * masterVolume;
        RegisterAudioSource(cautionAudioSource, SoundCategory.SE);
    }

    private void Start()
    {
        // ダメージイベントを購読
        damageSubscription = gameManager.StateManager.State.OnDamageReceived.Subscribe(damageInfo =>
        {
            PlayDamagedSound();
        });

        // HP警告状態を購読
        cautionSubscription = gameManager.StateManager.State.IsCaution.Subscribe(isCaution =>
        {
            if (isCaution)
            {
                PlayCautionSound();
            }
            else
            {
                StopCautionSound();
            }
        });

        // コイン取得イベントを購読
        coinGetSubscription = gameManager.StateManager.State.OnCoinGetReceived.Subscribe(coinGetInfo =>
        {
            PlayCoinGetSound();
        });

        // チェックポイントアクティブ化イベントを購読
        checkPointActivatedSubscription = gameManager.StateManager.State.OnCheckPointActivated.Subscribe(checkPointInfo =>
        {
            PlayCheckPointActivatedSound();
        });

        // ハイジャンプイベントを購読
        highJumpSubscription = gameManager.StateManager.State.OnHighJump.Subscribe(highJumpInfo =>
        {
            PlayHighJumpSound();
        });
    }

    /// <summary>
    /// AudioSourceをカテゴリーに登録
    /// </summary>
    /// <param name="audioSource">登録するAudioSource</param>
    /// <param name="category">サウンドカテゴリー</param>
    public void RegisterAudioSource(AudioSource audioSource, SoundCategory category)
    {
        if (audioSource == null) return;

        if (!audioSourcesByCategory.ContainsKey(category))
        {
            audioSourcesByCategory[category] = new List<AudioSource>();
        }

        if (!audioSourcesByCategory[category].Contains(audioSource))
        {
            audioSourcesByCategory[category].Add(audioSource);
        }
    }

    /// <summary>
    /// AudioSourceをカテゴリーから登録解除
    /// </summary>
    /// <param name="audioSource">登録解除するAudioSource</param>
    /// <param name="category">サウンドカテゴリー</param>
    public void UnregisterAudioSource(AudioSource audioSource, SoundCategory category)
    {
        if (audioSource == null) return;

        if (audioSourcesByCategory.ContainsKey(category))
        {
            audioSourcesByCategory[category].Remove(audioSource);
        }
    }

    /// <summary>
    /// 指定カテゴリーの全AudioSourceのボリュームを設定
    /// </summary>
    /// <param name="category">サウンドカテゴリー</param>
    /// <param name="volume">ボリューム（0.0 ～ 1.0）</param>
    public void SetCategoryVolume(SoundCategory category, float volume)
    {
        volume = Mathf.Clamp01(volume);

        // カテゴリー別のボリューム変数を更新
        switch (category)
        {
            case SoundCategory.BGM:
                bgmVolume = volume;
                break;
            case SoundCategory.SE:
                seVolume = volume;
                break;
        }

        // 該当カテゴリーの全AudioSourceのボリュームを更新
        if (audioSourcesByCategory.ContainsKey(category))
        {
            foreach (var audioSource in audioSourcesByCategory[category])
            {
                if (audioSource != null)
                {
                    audioSource.volume = volume * masterVolume;
                }
            }
        }
    }

    /// <summary>
    /// BGMボリュームを設定
    /// </summary>
    /// <param name="volume">ボリューム（0.0 ～ 1.0）</param>
    public void SetBGMVolume(float volume)
    {
        SetCategoryVolume(SoundCategory.BGM, volume);
    }

    /// <summary>
    /// SEボリュームを設定
    /// </summary>
    /// <param name="volume">ボリューム（0.0 ～ 1.0）</param>
    public void SetSEVolume(float volume)
    {
        SetCategoryVolume(SoundCategory.SE, volume);
    }

    /// <summary>
    /// マスターボリュームを設定
    /// </summary>
    /// <param name="volume">ボリューム（0.0 ～ 1.0）</param>
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);

        // 全カテゴリーのボリュームを再適用
        SetCategoryVolume(SoundCategory.BGM, bgmVolume);
        SetCategoryVolume(SoundCategory.SE, seVolume);
    }

    /// <summary>
    /// ダメージサウンドを再生
    /// </summary>
    private void PlayDamagedSound()
    {
        if (damagedSound != null && sfxAudioSource != null)
        {
            sfxAudioSource.PlayOneShot(damagedSound, seVolume * masterVolume);
        }
    }

    /// <summary>
    /// コイン取得サウンドを再生
    /// </summary>
    private void PlayCoinGetSound()
    {
        if (coinGetSound != null && sfxAudioSource != null)
        {
            sfxAudioSource.PlayOneShot(coinGetSound, seVolume * masterVolume);
        }
    }

    /// <summary>
    /// チェックポイントアクティブ化サウンドを再生
    /// </summary>
    private void PlayCheckPointActivatedSound()
    {
        if (checkPointActivatedSound != null && sfxAudioSource != null)
        {
            sfxAudioSource.PlayOneShot(checkPointActivatedSound, seVolume * masterVolume);
        }
    }

    /// <summary>
    /// ハイジャンプサウンドを再生
    /// </summary>
    private void PlayHighJumpSound()
    {
        if (highJumpSound != null && sfxAudioSource != null)
        {
            sfxAudioSource.PlayOneShot(highJumpSound, seVolume * masterVolume);
        }
    }

    /// <summary>
    /// 警告サウンドをループ再生
    /// </summary>
    private void PlayCautionSound()
    {
        if (cautionSound != null && cautionAudioSource != null)
        {
            cautionAudioSource.clip = cautionSound;
            cautionAudioSource.Play();
        }
    }

    /// <summary>
    /// 警告サウンドを停止
    /// </summary>
    private void StopCautionSound()
    {
        if (cautionAudioSource != null && cautionAudioSource.isPlaying)
        {
            cautionAudioSource.Stop();
        }
    }

    private void OnDestroy()
    {
        // R3購読の解放
        damageSubscription?.Dispose();
        cautionSubscription?.Dispose();
        coinGetSubscription?.Dispose();
        checkPointActivatedSubscription?.Dispose();
        highJumpSubscription?.Dispose();
    }
}
