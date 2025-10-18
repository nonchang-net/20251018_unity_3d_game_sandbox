using UnityEngine;

/// <summary>
/// ランタイムでのキャラクター切り替え機能を提供するコンポーネント
/// - PlayableCharacterRepositoryから利用可能なキャラクターを取得
/// - 入力に応じてGameManagerのアクティブキャラクターを切り替える
/// - GameManagerはキャラクター切り替えロジックについて一切関知しない
/// </summary>
public class RuntimeCharacterSwitcher : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("GameManager")]
    [SerializeField] private GameManager gameManager;

    [Tooltip("PlayableCharacterRepository")]
    [SerializeField] private PlayableCharacterRepository characterRepository;

    [Header("入力設定")]
    [Tooltip("キャラクター切り替えキー")]
    [SerializeField] private KeyCode switchCharacterKey = KeyCode.Tab;

    [Header("デバッグ設定")]
    [Tooltip("詳細ログを表示する")]
    [SerializeField] private bool enableVerboseLog = false;

    void Awake()
    {
        // GameManagerの自動検出
        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
            if (gameManager == null)
            {
                Debug.LogError("RuntimeCharacterSwitcher: GameManagerが見つかりません。シーン内に配置してください。");
            }
        }

        // PlayableCharacterRepositoryの自動検出
        if (characterRepository == null)
        {
            characterRepository = FindFirstObjectByType<PlayableCharacterRepository>();
            if (characterRepository == null)
            {
                Debug.LogWarning("RuntimeCharacterSwitcher: PlayableCharacterRepositoryが見つかりません。キャラクター切り替え機能が動作しません。");
            }
        }
    }

    void Update()
    {
        // キャラクター切り替え入力を検出
        if (Input.GetKeyDown(switchCharacterKey))
        {
            SwitchToNextCharacter();
        }
    }

    /// <summary>
    /// 次のキャラクターに切り替え
    /// </summary>
    public void SwitchToNextCharacter()
    {
        if (gameManager == null)
        {
            Debug.LogWarning("RuntimeCharacterSwitcher: GameManagerが設定されていません。");
            return;
        }

        if (characterRepository == null)
        {
            Debug.LogWarning("RuntimeCharacterSwitcher: PlayableCharacterRepositoryが設定されていません。");
            return;
        }

        if (characterRepository.GetCharacterCount() == 0)
        {
            Debug.LogWarning("RuntimeCharacterSwitcher: 切り替え可能なキャラクターが登録されていません。");
            return;
        }

        if (characterRepository.GetCharacterCount() == 1)
        {
            if (enableVerboseLog)
            {
                Debug.Log("RuntimeCharacterSwitcher: 登録されているキャラクターは1体のみです。切り替えは不要です。");
            }
            return;
        }

        // 現在のアクティブキャラクターを取得
        GameObject currentCharacter = gameManager.GetActiveCharacter();

        // 現在のキャラクターのインデックスを取得
        int currentIndex = characterRepository.GetCharacterIndex(currentCharacter);

        // 次のインデックスを計算
        int nextIndex = characterRepository.GetNextCharacterIndex(currentIndex);

        if (nextIndex >= 0)
        {
            GameObject nextCharacter = characterRepository.GetCharacterAt(nextIndex);
            if (nextCharacter != null)
            {
                // GameManagerのAPIを呼び出してアクティブキャラクターを切り替え
                gameManager.SetActiveCharacter(nextCharacter);

                if (enableVerboseLog)
                {
                    Debug.Log($"RuntimeCharacterSwitcher: キャラクターを切り替えました: {currentCharacter?.name} -> {nextCharacter.name}");
                }
            }
        }
    }

    /// <summary>
    /// インデックスを指定してキャラクターに切り替え
    /// </summary>
    /// <param name="index">キャラクターのインデックス</param>
    public void SwitchToCharacter(int index)
    {
        if (gameManager == null)
        {
            Debug.LogWarning("RuntimeCharacterSwitcher: GameManagerが設定されていません。");
            return;
        }

        if (characterRepository == null)
        {
            Debug.LogWarning("RuntimeCharacterSwitcher: PlayableCharacterRepositoryが設定されていません。");
            return;
        }

        GameObject character = characterRepository.GetCharacterAt(index);
        if (character != null)
        {
            gameManager.SetActiveCharacter(character);

            if (enableVerboseLog)
            {
                Debug.Log($"RuntimeCharacterSwitcher: キャラクターをインデックス {index} に切り替えました: {character.name}");
            }
        }
    }

    /// <summary>
    /// 名前を指定してキャラクターに切り替え
    /// </summary>
    /// <param name="characterName">キャラクター名</param>
    public void SwitchToCharacterByName(string characterName)
    {
        if (gameManager == null)
        {
            Debug.LogWarning("RuntimeCharacterSwitcher: GameManagerが設定されていません。");
            return;
        }

        if (characterRepository == null)
        {
            Debug.LogWarning("RuntimeCharacterSwitcher: PlayableCharacterRepositoryが設定されていません。");
            return;
        }

        // リポジトリから全キャラクターを取得して名前で検索
        var characters = characterRepository.GetAllCharacters();
        foreach (var character in characters)
        {
            if (character != null && character.name == characterName)
            {
                gameManager.SetActiveCharacter(character);

                if (enableVerboseLog)
                {
                    Debug.Log($"RuntimeCharacterSwitcher: キャラクターを名前で切り替えました: {characterName}");
                }
                return;
            }
        }

        Debug.LogWarning($"RuntimeCharacterSwitcher: 名前 '{characterName}' のキャラクターが見つかりません。");
    }

    /// <summary>
    /// 前のキャラクターに切り替え（逆順）
    /// </summary>
    public void SwitchToPreviousCharacter()
    {
        if (gameManager == null)
        {
            Debug.LogWarning("RuntimeCharacterSwitcher: GameManagerが設定されていません。");
            return;
        }

        if (characterRepository == null)
        {
            Debug.LogWarning("RuntimeCharacterSwitcher: PlayableCharacterRepositoryが設定されていません。");
            return;
        }

        if (characterRepository.GetCharacterCount() == 0)
        {
            Debug.LogWarning("RuntimeCharacterSwitcher: 切り替え可能なキャラクターが登録されていません。");
            return;
        }

        if (characterRepository.GetCharacterCount() == 1)
        {
            if (enableVerboseLog)
            {
                Debug.Log("RuntimeCharacterSwitcher: 登録されているキャラクターは1体のみです。切り替えは不要です。");
            }
            return;
        }

        // 現在のアクティブキャラクターを取得
        GameObject currentCharacter = gameManager.GetActiveCharacter();

        // 現在のキャラクターのインデックスを取得
        int currentIndex = characterRepository.GetCharacterIndex(currentCharacter);

        // 前のインデックスを計算（循環）
        int count = characterRepository.GetCharacterCount();
        int previousIndex = (currentIndex - 1 + count) % count;

        if (previousIndex >= 0)
        {
            GameObject previousCharacter = characterRepository.GetCharacterAt(previousIndex);
            if (previousCharacter != null)
            {
                gameManager.SetActiveCharacter(previousCharacter);

                if (enableVerboseLog)
                {
                    Debug.Log($"RuntimeCharacterSwitcher: 前のキャラクターに切り替えました: {currentCharacter?.name} -> {previousCharacter.name}");
                }
            }
        }
    }
}
