using System;
using System.Collections;
using Cysharp.Text;
using TMPro;
using UnityEngine;
using R3;
using UnityDebugSheet.Runtime.Core.Scripts;
using NaughtyAttributes;
using Cysharp.Threading.Tasks;

/// <summary>
/// ゲーム全体を管理するマネージャークラス
/// - TODO: アクティブキャラクターの管理責務はrepository側に集約すべきか検討中
/// </summary>
public class GameManager : MonoBehaviour
{
    const string version = "0.0.2";
    const long versionCode = 20251019;


    [Header("ログ制御")]
    [Tooltip("詳細ログを表示する（マスタースイッチ）")]
    [SerializeField] private bool enableVerboseLog = false;

    [Tooltip("カメラ系の詳細ログを表示する")]
    [SerializeField] private bool enableCameraVerboseLog = false;

    [Header("参照")]
    [Required, SerializeField] private GameStateManager stateManager;
    public GameStateManager StateManager => stateManager;
    [Required, SerializeField] private GameInputManager inputManager;
    public GameInputManager InputManager => inputManager;
    [Required, SerializeField] private GameUIManager gameUIManager;
    public GameUIManager UIManager => gameUIManager;
    [Required, SerializeField] private GameCharacterManager characterManager;
    public GameCharacterManager CharacterManager => characterManager;
    [Required, SerializeField] private GamePostProcessManager postProcessingManager;
    public GamePostProcessManager PostProcessingManager => postProcessingManager;
    [Required, SerializeField] private GameTimeManager timeManager;
    public GameTimeManager TimeManager => timeManager;
    [Required, SerializeField] private GameSoundManager soundManager;
    public GameSoundManager SoundManager => soundManager;

    [Header("キャラクター管理")]
    [Tooltip("プレイ可能なキャラクターを管理するリポジトリ")]
    [Required, SerializeField] private PlayableCharacterRepository characterRepository;

    [Header("カメラ設定")]
    [Tooltip("メインカメラのCharacterTracker")]
    [Required, SerializeField] private CharacterTracker characterTracker;
    public CharacterTracker CharacterTracker => characterTracker;
    [Tooltip("カメラ管理マネージャー")]
    [Required, SerializeField] private GameCameraManager cameraManager;
    public GameCameraManager CameraManager => cameraManager;

    [Header("水面設定")]
    [Tooltip("水面の高さを示すTransform（未設定の場合はY=0以下を水面とする）")]
    [SerializeField] private Transform waterSurfaceTransform;

    /// <summary>水面の高さを取得（未設定の場合は0を返す）</summary>
    public float WaterSurfaceHeight => waterSurfaceTransform != null ? waterSurfaceTransform.position.y : 0f;

    [Header("リスポーン設定")]
    [Tooltip("レベルのチェックポイント管理（設定されている場合はこちらを優先使用）")]
    [SerializeField] private LevelCheckPointManager levelCheckPointManager;

    [Tooltip("リスポーン地点（LevelCheckPointManagerが未設定の場合のみ使用）")]
    [SerializeField] private Transform defaultSpawnPoint;

    [Tooltip("死亡後リスポーンまでの待機時間（秒）\n※暗転開始ディレイ + 暗転時間 を考慮して設定してください")]
    [SerializeField] private float respawnDelay = 3f;

    /// <summary>現在操作中のキャラクター</summary>
    private GameObject activeCharacter;

    /// <summary>R3購読管理</summary>
    private IDisposable deadSubscription;
    private IDisposable checkPointActivatedSubscription;

    /// <summary>フォールバック用のデフォルトスポーンポイント（Vector3.zero）</summary>
    private GameObject fallbackSpawnPoint;

    /// <summary>現在のチェックポイント（最後にアクティブ化されたCheckPoint）</summary>
    private CheckPoint currentCheckPoint;

    /// <summary>DefaultSpawnPointへのアクセスプロパティ（他のコンポーネントから参照可能）</summary>
    public Transform DefaultSpawnPoint => defaultSpawnPoint;

    /// <summary>現在のチェックポイントへのアクセスプロパティ（他のコンポーネントから参照可能）</summary>
    public CheckPoint CurrentCheckPoint => currentCheckPoint;

    void Awake()
    {
        // ログ設定を初期化
        CharacterTracker.EnableCameraVerboseLog = enableVerboseLog && enableCameraVerboseLog;
        GameCameraManager.EnableCameraVerboseLog = enableVerboseLog && enableCameraVerboseLog;

        // 参照確認
        if (inputManager == null)
        {
            Debug.LogError("GameManager: GameInputManager が設定されていません。");
            return;
        }
        if( gameUIManager == null)
        {
            Debug.LogError("GameManager: GameUIManager が設定されていません。");
            return;
        }
        if (characterManager == null)
        {
            Debug.LogError("GameManager: GameCharacterManager が設定されていません。");
            return;
        }
        if (characterTracker == null)
        {
            Debug.LogError("GameManager: CharacterTracker が設定されていません。メインカメラに付与して参照を設定してください。");
            return;
        }
        if (postProcessingManager == null)
        {
            Debug.LogError("GameManager: postProcessingManager が設定されていません。");
            return;
        }

        // PlayableCharacterRepositoryの自動検出
        if (characterRepository == null)
        {
            characterRepository = FindFirstObjectByType<PlayableCharacterRepository>();
            if (characterRepository == null)
            {
                Debug.LogWarning("GameManager: PlayableCharacterRepositoryが見つかりません。キャラクター切り替え機能が制限されます。");
            }
        }

        // note: LevelCheckPointManagerの自動検出は行わない方針で検討中
        // if (levelCheckPointManager == null)
        // {
        //     levelCheckPointManager = FindFirstObjectByType<LevelCheckPointManager>();
        // }

        // DefaultSpawnPointの設定
        // 優先順位: LevelCheckPointManager > defaultSpawnPoint > Vector3.zero
        if (levelCheckPointManager != null)
        {
            // LevelCheckPointManagerが設定されている場合
            Transform firstCheckPoint = levelCheckPointManager.GetFirstCheckPointPosition();
            if (firstCheckPoint != null)
            {
                // defaultSpawnPointも設定されている場合は警告
                if (defaultSpawnPoint != null)
                {
                    Debug.LogWarning("GameManager: Level CheckPoint Managerが有効です。default spawn pointの設定は無視されます。");
                }

                // LevelCheckPointManagerの最初のチェックポイントを使用
                defaultSpawnPoint = firstCheckPoint;

                if (enableVerboseLog)
                {
                    Debug.Log($"GameManager: LevelCheckPointManagerの最初のチェックポイント '{firstCheckPoint.name}' をリスポーン地点として使用します。");
                }
            }
            else
            {
                // LevelCheckPointManagerが空の場合
                Debug.LogError("GameManager: LevelCheckPointManagerが設定されていますが、チェックポイントが空です。Vector3.zeroをリスポーン地点として使用します。");
                CreateFallbackSpawnPoint();
            }
        }
        else if (defaultSpawnPoint != null)
        {
            // LevelCheckPointManagerがなく、defaultSpawnPointが設定されている場合
            if (enableVerboseLog)
            {
                Debug.Log($"GameManager: defaultSpawnPoint '{defaultSpawnPoint.name}' をリスポーン地点として使用します。");
            }
        }
        else
        {
            // どちらも設定されていない場合
            Debug.LogError("GameManager: DefaultSpawnPointとLevelCheckPointManagerが両方とも未設定です。Vector3.zeroをリスポーン地点として使用します。");
            CreateFallbackSpawnPoint();
        }
    }

    /// <summary>
    /// フォールバック用のスポーンポイントを作成（Vector3.zero）
    /// </summary>
    void CreateFallbackSpawnPoint()
    {
        fallbackSpawnPoint = new GameObject("FallbackSpawnPoint");
        fallbackSpawnPoint.transform.position = Vector3.zero;
        fallbackSpawnPoint.transform.rotation = Quaternion.identity;
        defaultSpawnPoint = fallbackSpawnPoint.transform;

        if (enableVerboseLog)
        {
            Debug.Log("GameManager: フォールバックスポーンポイント（Vector3.zero）を作成しました。");
        }
    }

    void Start()
    {
        //Screen.SetResolution(640, 480, false);

        // ゲーム初期化（UniTaskで非同期実行）
        Initialize().Forget();

        // 死亡イベントを購読
        SubscribeDeadEvents();

        // チェックポイントアクティブ化イベントを購読
        SubscribeCheckPointEvents();
    }

    /// <summary>
    /// ゲーム初期化処理
    /// ローディング画面の表示・非表示、各種マネージャーの初期化を行う
    /// </summary>
    private async UniTask Initialize()
    {
        if (enableVerboseLog)
        {
            Debug.Log("GameManager: Initialize() を開始しました。");
        }

        // ローディング画面を表示
        if (gameUIManager != null && gameUIManager.LoadingView != null && gameUIManager.LoadingViewCanvasGroup != null)
        {
            gameUIManager.LoadingView.SetActive(true);
            gameUIManager.LoadingViewCanvasGroup.alpha = 1.0f;

            if (enableVerboseLog)
            {
                Debug.Log("GameManager: ローディング画面を表示しました。");
            }
        }
        else
        {
            Debug.LogWarning("GameManager: LoadingViewまたはLoadingViewCanvasGroupが設定されていません。ローディング画面をスキップします。");
        }

        // キャラクター初期化
        await InitializeCharacterAsync();

        // マネージャー初期化
        await InitializeManagersAsync();

        // 1秒待機
        await UniTask.Delay(TimeSpan.FromSeconds(1));

        // ローディング画面をフェードアウト
        await FadeOutLoadingView();

        // 初期化完了を通知
        if (stateManager != null)
        {
            stateManager.NotifyInitializeFinished();

            if (enableVerboseLog)
            {
                Debug.Log("GameManager: 初期化完了イベントを通知しました。");
            }
        }

        if (enableVerboseLog)
        {
            Debug.Log("GameManager: Initialize() が完了しました。");
        }
    }

    /// <summary>
    /// ローディング画面をフェードアウト
    /// </summary>
    private async UniTask FadeOutLoadingView()
    {
        if (gameUIManager == null || gameUIManager.LoadingView == null || gameUIManager.LoadingViewCanvasGroup == null)
        {
            return;
        }

        float fadeDuration = 0.5f; // フェード時間
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1.0f, 0f, elapsed / fadeDuration);
            gameUIManager.LoadingViewCanvasGroup.alpha = alpha;
            await UniTask.Yield();
        }

        // 完全に透明にしてから非表示
        gameUIManager.LoadingViewCanvasGroup.alpha = 0f;
        gameUIManager.LoadingView.SetActive(false);

        if (enableVerboseLog)
        {
            Debug.Log("GameManager: ローディング画面をフェードアウトしました。");
        }
    }

    void OnDestroy()
    {
        // R3購読の解放
        deadSubscription?.Dispose();
        checkPointActivatedSubscription?.Dispose();

        // フォールバックスポーンポイントの破棄
        if (fallbackSpawnPoint != null)
        {
            Destroy(fallbackSpawnPoint);
        }
    }

    /// <summary>
    /// キャラクター初期化処理
    /// </summary>
    private async UniTask InitializeCharacterAsync()
    {
        // 1フレーム待機してキャラクターの初期化を確実に完了させる
        await UniTask.Yield();

        // PlayableCharacterRepositoryの初期化完了を待つ
        if (characterRepository != null)
        {
            while (!characterRepository.IsInitialized())
            {
                await UniTask.Yield();
            }

            // リポジトリから最初のキャラクターを取得してアクティブに設定
            // （起動時VRM読み込みがある場合は既にSetActiveCharacterされているが、ない場合はここで設定）
            if (characterRepository.GetCharacterCount() > 0)
            {
                GameObject firstCharacter = characterRepository.GetCharacterAt(0);
                if (firstCharacter != null && GetActiveCharacter() == null)
                {
                    SetActiveCharacter(firstCharacter);
                }
            }
        }
    }

    /// <summary>
    /// ゲーム開始時に初期化順序に依存する物たちの初期化処理
    /// </summary>
    private async UniTask InitializeManagersAsync()
    {
        if (enableVerboseLog)
        {
            Debug.Log("GameManager: InitializeManagersAsync() を開始しました。");
        }

        // DebugSheet初期化
        var rootPage = DebugSheet.Instance.GetOrCreateInitialPage();
        // Example Page追加
        rootPage.AddPageLinkButton<ExampleDebugPage>(nameof(ExampleDebugPage));

        // カメラマネージャー初期化
        if (cameraManager == null)
        {
            Debug.LogError("GameManager: GameCameraManager が設定されていません。カメラ機能が正常に動作しません。");
        }
        else
        {
            cameraManager.Initialize();
        }

        await UniTask.Yield();
    }

    /// <summary>
    /// 操作対象キャラクターを設定
    /// この関数を呼ぶだけで、カメラの追跡対象と入力の接続が自動的に設定されます
    /// </summary>
    /// <param name="character">操作対象にするキャラクター（GameObject）</param>
    public void SetActiveCharacter(GameObject character)
    {
        if (character == null)
        {
            Debug.LogError("GameManager: 設定しようとしたキャラクターがnullです。");
            return;
        }

        if (inputManager == null)
        {
            Debug.LogError("GameManager: GameInputManagerが設定されていません。");
            return;
        }

        // 新しいキャラクターをアクティブに
        activeCharacter = character;
        character.SetActive(true);

        // キャラクターを設定（カメラ追跡対象もGameCharacterManager内で自動設定される）
        characterManager.SetTargetCharacter(character);

        if (enableVerboseLog)
        {
            Debug.Log($"GameManager: 操作キャラクターを {character.name} に切り替えました。");
        }
    }

    /// <summary>
    /// 現在の操作キャラクターを取得
    /// </summary>
    public GameObject GetActiveCharacter()
    {
        return activeCharacter;
    }

    /// <summary>
    /// PlayableCharacterRepositoryを取得
    /// </summary>
    public PlayableCharacterRepository GetCharacterRepository()
    {
        return characterRepository;
    }

    /// <summary>
    /// GameInputManagerを取得
    /// </summary>
    public GameInputManager GetInputManager()
    {
        return inputManager;
    }

    /// <summary>
    /// 死亡イベントを購読
    /// </summary>
    void SubscribeDeadEvents()
    {
        deadSubscription = StateManager.State.IsDead.Subscribe(isDead =>
        {
            if (isDead)
            {
                // リスポーン処理を開始
                StartCoroutine(RespawnSequence());
            }
        });
    }

    /// <summary>
    /// チェックポイントアクティブ化イベントを購読
    /// </summary>
    void SubscribeCheckPointEvents()
    {
        checkPointActivatedSubscription = StateManager.State.OnCheckPointActivated.Subscribe(checkPointInfo =>
        {
            // 現在のチェックポイントを更新
            currentCheckPoint = checkPointInfo.CheckPoint;

            // リスポーン位置を更新
            if (checkPointInfo.SpawnPosition != null)
            {
                defaultSpawnPoint = checkPointInfo.SpawnPosition;

                if (enableVerboseLog)
                {
                    Debug.Log($"GameManager: リスポーン位置を更新しました。CheckPoint: '{checkPointInfo.CheckPointName}', 位置: {checkPointInfo.SpawnPosition.position}");
                }
            }
        });
    }

    /// <summary>
    /// リスポーン処理のシーケンス
    /// </summary>
    IEnumerator RespawnSequence()
    {
        if (enableVerboseLog)
        {
            Debug.Log("GameManager: リスポーン処理を開始します。");
        }

        // 暗転完了まで待機
        yield return new WaitForSeconds(respawnDelay);

        // HPをリセット
        StateManager.Respawn();

        // キャラクターをリスポーン地点に移動
        if (activeCharacter != null && defaultSpawnPoint != null)
        {
            // GameCharacterManager.TeleportTo()を使用して物理状態も適切にリセット
            characterManager.TeleportTo(defaultSpawnPoint.position, defaultSpawnPoint.rotation);

            if (enableVerboseLog)
            {
                Debug.Log($"GameManager: キャラクターをリスポーン地点に移動しました。位置: {defaultSpawnPoint.position}");
            }
        }

        // 暗転を解除
        postProcessingManager?.ClearDeadFade();
        if (enableVerboseLog)
        {
            Debug.Log("GameManager: 暗転を解除しました。");
        }

        if (enableVerboseLog)
        {
            Debug.Log("GameManager: リスポーン処理が完了しました。");
        }
    }
}
