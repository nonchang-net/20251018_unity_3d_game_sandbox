using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// プレイ可能なキャラクターを管理するリポジトリ
/// - ヒエラルキー上の初期化済みキャラクターを登録
/// - 起動時にVRMを読み込んで登録
/// - VRMロード時にも動的に登録
/// - キャラクター切り替え機能のデータソースとして機能
/// </summary>
public class PlayableCharacterRepository : MonoBehaviour
{
    [Header("初期キャラクター設定")]
    [Tooltip("シーン起動時に登録する初期キャラクター")]
    [SerializeField] private GameObject[] initialCharacters;

    [Header("起動時VRM読み込み設定")]
    [Tooltip("起動時にデフォルトのVRMキャラクターを読み込む")]
    [SerializeField] private bool loadStartupVrmCharacter = false;

    [Tooltip("StreamingAssets内のVRMファイル名")]
    [SerializeField] private string startupVrmFileName = "AliciaSolid.vrm";

    [Header("VRM設定")]
    [Tooltip("VRMキャラクターに適用するアニメーションコントローラー")]
    [SerializeField] private RuntimeAnimatorController vrmAnimatorController;

    [Header("GameManager参照")]
    [Tooltip("GameManager（DefaultSpawnPoint参照用）")]
    [SerializeField] private GameManager gameManager;

    [Header("デバッグ設定")]
    [Tooltip("詳細ログを表示する")]
    [SerializeField] private bool enableVerboseLog = false;

    /// <summary>
    /// 登録されているプレイ可能なキャラクターのリスト
    /// </summary>
    private List<GameObject> registeredCharacters = new List<GameObject>();

    /// <summary>
    /// 初期化完了フラグ
    /// </summary>
    private bool isInitialized = false;

    void Awake()
    {
        // GameManagerの自動検出
        gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager == null && loadStartupVrmCharacter)
        {
            Debug.LogWarning("PlayableCharacterRepository: GameManagerが見つかりません。起動時VRM読み込み完了時のアクティブキャラクター設定ができません。");
        }

        // 初期キャラクターを登録
        if (initialCharacters != null)
        {
            foreach (var character in initialCharacters)
            {
                if (character != null)
                {
                    RegisterCharacter(character);
                }
            }
        }
    }

    void Start()
    {
        // 起動時VRM読み込みが有効な場合
        if (loadStartupVrmCharacter)
        {
            StartCoroutine(LoadStartupVrmCharacter());
        }
        else
        {
            // 起動時VRM読み込みがない場合は即座に初期化完了
            isInitialized = true;
        }
    }

    /// <summary>
    /// 起動時にVRMキャラクターを読み込む
    /// </summary>
    private IEnumerator LoadStartupVrmCharacter()
    {
        // VRMUtilityの詳細ログを設定
        VRMUtility.EnableVerboseLog = enableVerboseLog;

        // StreamingAssetsからVRMファイルのパスを構築
        string vrmPath = Path.Combine(Application.streamingAssetsPath, startupVrmFileName);

        if (!File.Exists(vrmPath))
        {
            Debug.LogError($"PlayableCharacterRepository: VRMファイルが見つかりません: {vrmPath}");
            isInitialized = true;
            yield break;
        }

        // スポーン位置を決定（GameManagerから取得）
        Vector3 spawnPosition = Vector3.zero;
        if (gameManager != null && gameManager.DefaultSpawnPoint != null)
        {
            spawnPosition = gameManager.DefaultSpawnPoint.position;
        }
        else
        {
            Debug.LogWarning("PlayableCharacterRepository: GameManagerまたはDefaultSpawnPointが設定されていません。");
        }

        if (enableVerboseLog)
        {
            Debug.Log($"PlayableCharacterRepository: 起動時VRM読み込み開始: {startupVrmFileName}");
        }

        // VRMを読み込んでセットアップ
        yield return VRMUtility.LoadAndSetupVrmFromPath(
            vrmPath,
            spawnPosition,
            vrmAnimatorController,
            onComplete: (vrmCharacter) =>
            {
                // リストの先頭に登録
                RegisterCharacterAtFront(vrmCharacter);

                // GameManagerのアクティブキャラクターに設定
                if (gameManager != null)
                {
                    gameManager.SetActiveCharacter(vrmCharacter);
                }

                if (enableVerboseLog)
                {
                    Debug.Log($"PlayableCharacterRepository: 起動時VRM読み込み完了: {vrmCharacter.name}");
                }

                isInitialized = true;
            },
            onError: (errorMessage) =>
            {
                Debug.LogError($"PlayableCharacterRepository: 起動時VRM読み込みエラー: {errorMessage}");
                isInitialized = true;
            }
        );
    }

    /// <summary>
    /// 初期化が完了しているかを取得
    /// </summary>
    public bool IsInitialized()
    {
        return isInitialized;
    }

    /// <summary>
    /// キャラクターを登録する
    /// </summary>
    /// <param name="character">登録するキャラクター</param>
    /// <returns>登録に成功した場合true</returns>
    public bool RegisterCharacter(GameObject character)
    {
        if (character == null)
        {
            Debug.LogWarning("PlayableCharacterRepository: 登録しようとしたキャラクターがnullです。");
            return false;
        }

        if (registeredCharacters.Contains(character))
        {
            Debug.LogWarning($"PlayableCharacterRepository: キャラクター '{character.name}' は既に登録されています。");
            return false;
        }

        registeredCharacters.Add(character);
        // Debug.Log($"PlayableCharacterRepository: キャラクター '{character.name}' を登録しました。（現在の登録数: {registeredCharacters.Count}）");
        return true;
    }

    /// <summary>
    /// キャラクターをリストの先頭に登録する
    /// </summary>
    /// <param name="character">登録するキャラクター</param>
    /// <returns>登録に成功した場合true</returns>
    public bool RegisterCharacterAtFront(GameObject character)
    {
        if (character == null)
        {
            Debug.LogWarning("PlayableCharacterRepository: 登録しようとしたキャラクターがnullです。");
            return false;
        }

        if (registeredCharacters.Contains(character))
        {
            Debug.LogWarning($"PlayableCharacterRepository: キャラクター '{character.name}' は既に登録されています。");
            return false;
        }

        registeredCharacters.Insert(0, character);
        // Debug.Log($"PlayableCharacterRepository: キャラクター '{character.name}' をリストの先頭に登録しました。（現在の登録数: {registeredCharacters.Count}）");
        return true;
    }

    /// <summary>
    /// キャラクターの登録を解除する
    /// </summary>
    /// <param name="character">登録解除するキャラクター</param>
    /// <returns>登録解除に成功した場合true</returns>
    public bool UnregisterCharacter(GameObject character)
    {
        if (character == null)
        {
            Debug.LogWarning("PlayableCharacterRepository: 登録解除しようとしたキャラクターがnullです。");
            return false;
        }

        if (!registeredCharacters.Contains(character))
        {
            Debug.LogWarning($"PlayableCharacterRepository: キャラクター '{character.name}' は登録されていません。");
            return false;
        }

        registeredCharacters.Remove(character);
        // Debug.Log($"PlayableCharacterRepository: キャラクター '{character.name}' の登録を解除しました。（現在の登録数: {registeredCharacters.Count}）");
        return true;
    }

    /// <summary>
    /// 登録されているすべてのキャラクターを取得
    /// </summary>
    /// <returns>登録されているキャラクターのリスト（読み取り専用）</returns>
    public IReadOnlyList<GameObject> GetAllCharacters()
    {
        return registeredCharacters.AsReadOnly();
    }

    /// <summary>
    /// インデックスを指定してキャラクターを取得
    /// </summary>
    /// <param name="index">取得するキャラクターのインデックス</param>
    /// <returns>指定されたインデックスのキャラクター、無効な場合はnull</returns>
    public GameObject GetCharacterAt(int index)
    {
        if (index < 0 || index >= registeredCharacters.Count)
        {
            Debug.LogWarning($"PlayableCharacterRepository: インデックス {index} は無効です。（登録数: {registeredCharacters.Count}）");
            return null;
        }

        return registeredCharacters[index];
    }

    /// <summary>
    /// 登録されているキャラクターの数を取得
    /// </summary>
    /// <returns>登録されているキャラクターの数</returns>
    public int GetCharacterCount()
    {
        return registeredCharacters.Count;
    }

    /// <summary>
    /// キャラクターが登録されているかを確認
    /// </summary>
    /// <param name="character">確認するキャラクター</param>
    /// <returns>登録されている場合true</returns>
    public bool IsRegistered(GameObject character)
    {
        return character != null && registeredCharacters.Contains(character);
    }

    /// <summary>
    /// キャラクターのインデックスを取得
    /// </summary>
    /// <param name="character">インデックスを取得するキャラクター</param>
    /// <returns>キャラクターのインデックス、登録されていない場合は-1</returns>
    public int GetCharacterIndex(GameObject character)
    {
        if (character == null)
        {
            return -1;
        }

        return registeredCharacters.IndexOf(character);
    }

    /// <summary>
    /// 次のキャラクターのインデックスを取得（循環）
    /// </summary>
    /// <param name="currentIndex">現在のインデックス</param>
    /// <returns>次のインデックス</returns>
    public int GetNextCharacterIndex(int currentIndex)
    {
        if (registeredCharacters.Count == 0)
        {
            return -1;
        }

        return (currentIndex + 1) % registeredCharacters.Count;
    }

    /// <summary>
    /// すべてのキャラクター登録を解除
    /// </summary>
    public void ClearAllCharacters()
    {
        int count = registeredCharacters.Count;
        registeredCharacters.Clear();
        // Debug.Log($"PlayableCharacterRepository: すべてのキャラクター登録を解除しました。（解除数: {count}）");
    }
}
