/*
シンプルなサウンド管理

- やること
  - BGM/SFXの再生管理をsingletonでどこからでも呼べるようにする
  - DontDestroyOnLoadしておく
  - sound listenerもカメラから剥がしてこいつにおいておく

- やらないこと
- - 3Dサウンド系の処理は一旦保留。あとで別枠で考えたい
  - マテリアル別のfoot stepサウンド切り替え処理的なこと
    - 3D GameKitがやってたけど今はいいや

*/

using Unity.Collections;
using UnityEngine;
using R3;
using System;

public class GameSoundManager : MonoBehaviour
{
    private float masterVolume = 1.0f;
    private float sfxVolume = 0.5f;
    private float musicVolume = 0.5f;

    [SerializeField] AudioClip[] bgms;

    [SerializeField] AudioClip coinGetSound;
    [SerializeField] AudioClip checkPointActivatedSound;
    [SerializeField] AudioClip highJumpSound;

    [SerializeField] AudioClip damagedSound;
    [SerializeField] AudioClip cautionSound;
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
        // BGM用AudioSource
        bgmAudioSource = gameObject.AddComponent<AudioSource>();
        bgmAudioSource.loop = true;
        bgmAudioSource.volume = musicVolume * masterVolume;

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
        sfxAudioSource.volume = sfxVolume * masterVolume;

        // 警告サウンド用AudioSource
        cautionAudioSource = gameObject.AddComponent<AudioSource>();
        cautionAudioSource.loop = true;
        cautionAudioSource.volume = sfxVolume * masterVolume;
    }

    private void Start()
    {
        // ダメージイベントを購読
        damageSubscription = UserDataManager.Data.OnDamageReceived.Subscribe(damageInfo =>
        {
            PlayDamagedSound();
        });

        // HP警告状態を購読
        cautionSubscription = UserDataManager.Data.IsCaution.Subscribe(isCaution =>
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
        coinGetSubscription = UserDataManager.Data.OnCoinGetReceived.Subscribe(coinGetInfo =>
        {
            PlayCoinGetSound();
        });

        // チェックポイントアクティブ化イベントを購読
        checkPointActivatedSubscription = UserDataManager.Data.OnCheckPointActivated.Subscribe(checkPointInfo =>
        {
            PlayCheckPointActivatedSound();
        });

        // ハイジャンプイベントを購読
        highJumpSubscription = UserDataManager.Data.OnHighJump.Subscribe(highJumpInfo =>
        {
            PlayHighJumpSound();
        });
    }

    /// <summary>
    /// ダメージサウンドを再生
    /// </summary>
    private void PlayDamagedSound()
    {
        if (damagedSound != null && sfxAudioSource != null)
        {
            sfxAudioSource.PlayOneShot(damagedSound, sfxVolume * masterVolume);
        }
    }

    /// <summary>
    /// コイン取得サウンドを再生
    /// </summary>
    private void PlayCoinGetSound()
    {
        if (coinGetSound != null && sfxAudioSource != null)
        {
            sfxAudioSource.PlayOneShot(coinGetSound, sfxVolume * masterVolume);
        }
    }

    /// <summary>
    /// チェックポイントアクティブ化サウンドを再生
    /// </summary>
    private void PlayCheckPointActivatedSound()
    {
        if (checkPointActivatedSound != null && sfxAudioSource != null)
        {
            sfxAudioSource.PlayOneShot(checkPointActivatedSound, sfxVolume * masterVolume);
        }
    }

    /// <summary>
    /// ハイジャンプサウンドを再生
    /// </summary>
    private void PlayHighJumpSound()
    {
        if (highJumpSound != null && sfxAudioSource != null)
        {
            sfxAudioSource.PlayOneShot(highJumpSound, sfxVolume * masterVolume);
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
