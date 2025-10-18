using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// VRM読み込み機能を提供するマネージャーコンポーネント
/// - ファイル選択ダイアログの管理
/// - StreamingAssetsからのVRM読み込み
/// - ランタイムでのVRMファイル選択・読み込み
/// - WebGL対応のVRMダウンロード・読み込み
/// </summary>
public class VRMLoadManager : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("GameManager")]
    [SerializeField] private GameManager gameManager;

    [Tooltip("PlayableCharacterRepository")]
    [SerializeField] private PlayableCharacterRepository characterRepository;

    [Header("ランタイムVRM読み込み設定")]
    [Tooltip("VRMロードキー")]
    [SerializeField] private KeyCode vrmLoadKey = KeyCode.L;

    [Header("VRM設定")]
    [Tooltip("VRMキャラクターに適用するアニメーションコントローラー")]
    [SerializeField] private RuntimeAnimatorController vrmAnimatorController;

    [Header("デバッグ設定")]
    [Tooltip("詳細ログを表示する")]
    [SerializeField] private bool enableVerboseLog = false;

    /// <summary>VRMロード中フラグ</summary>
    private bool isLoadingVrm = false;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void WebGL_GameManager_FileDialog(string target, string message);
#endif

    void Awake()
    {
        // GameManagerの自動検出
        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
            if (gameManager == null)
            {
                Debug.LogError("VRMLoadManager: GameManagerが見つかりません。シーン内に配置してください。");
            }
        }

        // PlayableCharacterRepositoryの自動検出
        if (characterRepository == null)
        {
            characterRepository = FindFirstObjectByType<PlayableCharacterRepository>();
            if (characterRepository == null)
            {
                Debug.LogWarning("VRMLoadManager: PlayableCharacterRepositoryが見つかりません。読み込んだキャラクターは登録されません。");
            }
        }
    }


    void Update()
    {
        // VRMファイル選択・ロード
        if (Input.GetKeyDown(vrmLoadKey) && !isLoadingVrm)
        {
            StartCoroutine(LoadVrmFromFileDialog());
        }
    }

    /// <summary>
    /// ファイル選択ダイアログからVRMをロードする
    /// </summary>
    public IEnumerator LoadVrmFromFileDialog()
    {
        if (isLoadingVrm)
        {
            Debug.LogWarning("VRMLoadManager: 既にVRMロード中です。");
            yield break;
        }

        isLoadingVrm = true;

        // ファイル選択ダイアログを開く
        string vrmPath = OpenFileDialog();

        // WebGLの場合はコールバックで処理されるのでここでは何もしない
        if (vrmPath == null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGLの場合、OnFileSelectedコールバックで処理される
            yield break;
#else
            // その他のプラットフォームでnullの場合はキャンセルまたはエラー
            if (enableVerboseLog)
            {
                Debug.Log("VRMLoadManager: ファイル選択がキャンセルされました、またはサポートされていないプラットフォームです。");
            }
            isLoadingVrm = false;
            yield break;
#endif
        }

        // Editorの場合、ここで同期的にファイルパスが返される
        if (string.IsNullOrEmpty(vrmPath))
        {
            if (enableVerboseLog)
            {
                Debug.Log("VRMLoadManager: ファイル選択がキャンセルされました。");
            }
            isLoadingVrm = false;
            yield break;
        }

        if (!File.Exists(vrmPath))
        {
            Debug.LogError($"VRMLoadManager: 選択されたファイルが見つかりません: {vrmPath}");
            isLoadingVrm = false;
            yield break;
        }

        if (!vrmPath.EndsWith(".vrm", System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError($"VRMLoadManager: VRMファイルではありません: {vrmPath}");
            isLoadingVrm = false;
            yield break;
        }

        // VRMUtilityの詳細ログを設定
        VRMUtility.EnableVerboseLog = enableVerboseLog;

        // スポーン位置を決定（GameManagerから取得）
        Vector3 spawnPosition = Vector3.zero;
        if (gameManager != null && gameManager.DefaultSpawnPoint != null)
        {
            spawnPosition = gameManager.DefaultSpawnPoint.position;
        }
        else
        {
            Debug.LogWarning("VRMLoadManager: GameManagerまたはDefaultSpawnPointが設定されていません。");
        }

        // VRMを読み込んでセットアップ
        yield return VRMUtility.LoadAndSetupVrmFromPath(
            vrmPath,
            spawnPosition,
            vrmAnimatorController,
            onComplete: (vrmCharacter) =>
            {
                OnVrmLoaded(vrmCharacter);
                isLoadingVrm = false;
            },
            onError: (errorMessage) =>
            {
                Debug.LogError($"VRMLoadManager: {errorMessage}");
                isLoadingVrm = false;
            }
        );
    }

    /// <summary>
    /// ファイル選択ダイアログを開く
    /// </summary>
    private string OpenFileDialog()
    {
#if UNITY_EDITOR
        return UnityEditor.EditorUtility.OpenFilePanel("Open VRM", "", "vrm");
#elif UNITY_WEBGL
        // WebGLの場合、jslib経由でファイルダイアログを開く
        // コールバックは OnFileSelected(string url) で受け取る
        WebGL_GameManager_FileDialog(gameObject.name, "OnFileSelected");
        return null;
#elif UNITY_STANDALONE_WIN
        Debug.LogError("VRMLoadManager: Windows向けのファイル選択ダイアログは未実装です。");
        return null;
#else
        Debug.LogError("VRMLoadManager: このプラットフォームではファイル選択ダイアログがサポートされていません。");
        return null;
#endif
    }

    /// <summary>
    /// WebGLのファイル選択コールバック（jslibから呼ばれる）
    /// </summary>
    /// <param name="url">選択されたファイルのblob URL</param>
    public void OnFileSelected(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            if (enableVerboseLog)
            {
                Debug.Log("VRMLoadManager: ファイル選択がキャンセルされました。");
            }
            isLoadingVrm = false;
            return;
        }

        if (enableVerboseLog)
        {
            Debug.Log($"VRMLoadManager: WebGLファイル選択: {url}");
        }

        // コルーチンでファイルをダウンロードして読み込む
        StartCoroutine(LoadVrmFromUrl(url));
    }

    /// <summary>
    /// WebGL用：URLからVRMをダウンロードして読み込む
    /// </summary>
    /// <param name="url">VRMファイルのURL</param>
    public IEnumerator LoadVrmFromUrl(string url)
    {
        UnityWebRequest www = UnityWebRequest.Get(url);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"VRMLoadManager: ファイルのダウンロードに失敗しました: {www.error}");
            isLoadingVrm = false;
            yield break;
        }

        byte[] vrmData = www.downloadHandler.data;

        if (vrmData == null || vrmData.Length == 0)
        {
            Debug.LogError("VRMLoadManager: ダウンロードしたデータが空です。");
            isLoadingVrm = false;
            yield break;
        }

        if (enableVerboseLog)
        {
            Debug.Log($"VRMLoadManager: VRMデータのダウンロードが完了しました。サイズ: {vrmData.Length} bytes");
        }

        // VRMUtilityの詳細ログを設定
        VRMUtility.EnableVerboseLog = enableVerboseLog;

        // スポーン位置を決定（GameManagerから取得）
        Vector3 spawnPosition = Vector3.zero;
        if (gameManager != null && gameManager.DefaultSpawnPoint != null)
        {
            spawnPosition = gameManager.DefaultSpawnPoint.position;
        }
        else
        {
            Debug.LogWarning("VRMLoadManager: GameManagerまたはDefaultSpawnPointが設定されていません。");
        }

        // バイトデータからVRMを読み込んでセットアップ
        yield return VRMUtility.LoadAndSetupVrmFromBytes(
            vrmData,
            "WebGL.vrm",
            spawnPosition,
            vrmAnimatorController,
            onComplete: (vrmCharacter) =>
            {
                OnVrmLoaded(vrmCharacter);
                isLoadingVrm = false;
            },
            onError: (errorMessage) =>
            {
                Debug.LogError($"VRMLoadManager: {errorMessage}");
                isLoadingVrm = false;
            }
        );
    }

    /// <summary>
    /// VRM読み込み完了時の処理
    /// </summary>
    /// <param name="vrmCharacter">読み込まれたVRMキャラクター</param>
    private void OnVrmLoaded(GameObject vrmCharacter)
    {
        // リポジトリに登録
        if (characterRepository != null)
        {
            characterRepository.RegisterCharacter(vrmCharacter);
        }

        // GameManagerのアクティブキャラクターに設定
        if (gameManager != null)
        {
            gameManager.SetActiveCharacter(vrmCharacter);
        }

        if (enableVerboseLog)
        {
            Debug.Log($"VRMLoadManager: VRMキャラクター '{vrmCharacter.name}' の読み込みが完了しました。");
        }
    }

    /// <summary>
    /// 指定されたパスからVRMを読み込む（外部から呼び出し可能）
    /// </summary>
    /// <param name="vrmPath">VRMファイルのパス</param>
    public void LoadVrmFromPath(string vrmPath)
    {
        StartCoroutine(LoadVrmFromPathCoroutine(vrmPath));
    }

    /// <summary>
    /// 指定されたパスからVRMを読み込むコルーチン
    /// </summary>
    private IEnumerator LoadVrmFromPathCoroutine(string vrmPath)
    {
        if (string.IsNullOrEmpty(vrmPath))
        {
            Debug.LogError("VRMLoadManager: VRMファイルパスが空です。");
            yield break;
        }

        if (!File.Exists(vrmPath))
        {
            Debug.LogError($"VRMLoadManager: VRMファイルが見つかりません: {vrmPath}");
            yield break;
        }

        // VRMUtilityの詳細ログを設定
        VRMUtility.EnableVerboseLog = enableVerboseLog;

        // スポーン位置を決定（GameManagerから取得）
        Vector3 spawnPosition = Vector3.zero;
        if (gameManager != null && gameManager.DefaultSpawnPoint != null)
        {
            spawnPosition = gameManager.DefaultSpawnPoint.position;
        }
        else
        {
            Debug.LogWarning("VRMLoadManager: GameManagerまたはDefaultSpawnPointが設定されていません。");
        }

        // VRMを読み込んでセットアップ
        yield return VRMUtility.LoadAndSetupVrmFromPath(
            vrmPath,
            spawnPosition,
            vrmAnimatorController,
            onComplete: (vrmCharacter) =>
            {
                OnVrmLoaded(vrmCharacter);
            },
            onError: (errorMessage) =>
            {
                Debug.LogError($"VRMLoadManager: {errorMessage}");
            }
        );
    }
}
