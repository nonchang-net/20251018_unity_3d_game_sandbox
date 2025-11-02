using UnityEngine;
using UnityEngine.InputSystem;
using System;
using R3;
using NaughtyAttributes;

/// <summary>
/// ゲーム全体の入力管理
/// Input Systemからの入力を受け取り、各種コンポーネントに配信する
/// シーン内に1つだけ配置して使用
/// </summary>
public class GameInputManager : MonoBehaviour, InputSystem_Actions.IPlayerActions
{
    [Header("GameManager")]
    [Required("GameManagerの参照が必要です")]
    [SerializeField] private GameManager gameManager;

    [Header("参照")]
    [SerializeField] private RuntimeCharacterSwitcher runtimeCharacterSwitcher;
    [SerializeField] private VRMLoadManager vrmLoadManager;

    /// <summary>
    /// 詳細ログを有効にするかどうか（GameManagerから設定される）
    /// </summary>
    // public static bool EnableVerboseLog { get; set; } = false;

    // Input System
    private InputSystem_Actions inputSystemActions;
    private InputSystem_Actions.PlayerActions inputSystemActionMap;

    /// <summary>前フレームのカーソルロック状態</summary>
    private CursorLockMode previousCursorLockMode;

    /// <summary>ゲーム初期化が完了したかどうか</summary>
    private bool isInitialized = false;

    /// <summary>R3イベント購読管理</summary>
    private IDisposable initializeFinishedSubscription;

    void Awake()
    {
        // Input SystemはIME有効状態でプレイ開始するとshiftキーが反応しない
        // TODO: 以下では改善しなかった。制御方法など解決策を調査したい
        // Input.imeCompositionMode = IMECompositionMode.Off;
        // Keyboard.current?.SetIMEEnabled(false);

        inputSystemActions = new InputSystem_Actions();
        inputSystemActionMap = inputSystemActions.Player;
        inputSystemActionMap.AddCallbacks(this);

        // 初期化完了まで入力を無効化
        inputSystemActions?.Disable();
    }

    void OnEnable()
    {
        // 初期化完了後のみ入力を有効化
        if (isInitialized)
        {
            inputSystemActions?.Enable();
        }
    }

    void OnDisable()
    {
        inputSystemActions?.Disable();
    }

    void OnDestroy()
    {
        inputSystemActions?.Dispose();
        initializeFinishedSubscription?.Dispose();
    }

    void Start()
    {
        if (gameManager == null)
        {
            Debug.LogError($"GameInputManager: GameManager参照が設定されていません。修正してください。");
            return;
        }

        if (gameManager.CharacterTracker == null)
        {
            Debug.LogWarning($"GameInputManager: CharacterTracker参照が設定されていません。v0.0.2現在ではCharacterTracker未指定時の動作は検証対象外です。");
            return;
        }

        if (gameManager.CharacterManager == null)
        {
            Debug.LogError($"GameInputManager: GameCharacterManager参照が設定されていません。修正してください。");
            return;
        }

        // 初期カーソル状態を記録
        previousCursorLockMode = Cursor.lockState;

        // 初期化完了イベントを購読
        if (gameManager.StateManager != null)
        {
            initializeFinishedSubscription = gameManager.StateManager.State.OnInitializeFinished.Subscribe(_ =>
            {
                isInitialized = true;
                inputSystemActions?.Enable();
                Debug.Log("GameInputManager: ゲーム初期化完了。入力を有効化しました。");
            });
        }
        else
        {
            Debug.LogWarning("GameInputManager: GameStateManagerが設定されていません。入力制御が正常に動作しない可能性があります。");
        }
    }

    void Update()
    {
        // カーソルロック状態の変化を監視
        // TODO: 正常動作を確認できなかったので一旦コメントアウト中。後日再調査
        // MonitorCursorLockState();
    }

    /// <summary>
    /// カーソルロック状態の変化を監視し、ポーズ状態と連動させる
    /// TODO: 正常動作を確認できなかったので一旦コメントアウト中。後日再調査
    /// </summary>
    // private void MonitorCursorLockState()
    // {
    //     CursorLockMode currentLockMode = Cursor.lockState;

    //     // カーソルロック状態が変化した場合
    //     if (currentLockMode != previousCursorLockMode)
    //     {
    //         // ESCキーなどでカーソルがロック解除された場合、ポーズメニューを表示
    //         if (previousCursorLockMode == CursorLockMode.Locked && currentLockMode == CursorLockMode.None)
    //         {
    //             // ポーズメニューが非表示の場合のみ表示する
    //             if (!gameManager.UIManager.IsPauseMenuVisible)
    //             {
    //                 gameManager.TimeManager.Pause();
    //                 gameManager.UIManager.ShowPauseMenu();
    //                 Cursor.visible = true;
    //             }
    //         }
    //         // カーソルがロックされた場合、ポーズメニューを非表示
    //         else if (previousCursorLockMode == CursorLockMode.None && currentLockMode == CursorLockMode.Locked)
    //         {
    //             // ポーズメニューが表示中の場合のみ非表示にする
    //             if (gameManager.UIManager.IsPauseMenuVisible)
    //             {
    //                 gameManager.TimeManager.Unpause();
    //                 gameManager.UIManager.HidePauseMenu();
    //                 Cursor.visible = false;
    //             }
    //         }

    //         previousCursorLockMode = currentLockMode;
    //     }
    // }

    #region IPlayerActions implementation

    public void OnMove(InputAction.CallbackContext context)
    {
        Vector2 input = context.ReadValue<Vector2>();

        // CharacterTrackerのDisableVerticalInputフラグを確認
        if (gameManager.CharacterTracker != null && gameManager.CharacterTracker.DisableVerticalInput)
        {
            // 上下方向の入力を無効化（2Dゲーム風の操作）
            input.y = 0f;
        }

        gameManager.CharacterManager.SetMovementInput(input);
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        gameManager?.CharacterTracker?.SetLookInput(context.ReadValue<Vector2>());
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        gameManager.CharacterManager.SetJumpRequest(context.ReadValue<float>() > 0.5f);
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        gameManager.CharacterManager.SetSprintRequest(context.ReadValue<float>() > 0.5f);
    }

    public void OnToggleZoomMode(InputAction.CallbackContext context)
    {
        if (context.ReadValue<float>() > 0.5f)
        {
            // カメラビュー切り替えをリクエスト
            gameManager.StateManager.RequestNextCameraView();
        }

    }

    public void OnResetCamera(InputAction.CallbackContext context)
    {
        gameManager.CharacterTracker.ResetCamera();
    }

    // -----
    // ゲームシステム
    public void OnTogglePause(InputAction.CallbackContext context)
    {
        if (context.ReadValue<float>() < 0.5f) return;

        // ポーズ状態をトグル（UIは自動的に更新される）
        gameManager.TimeManager.TogglePause();

        // マウスカーソル制御（IsPausedの状態を直接参照）
        bool isPaused = gameManager.StateManager.State.IsPaused.CurrentValue;

        if (isPaused)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // -----
    // 以下はテスト機能類
    public void OnToggleSlowMotion(InputAction.CallbackContext context)
    {
        if (context.ReadValue<float>() > 0.5f)
        {
        if (gameManager.TimeManager.IsNormalSpeed)
        {
            gameManager.TimeManager.StartSlowMotion(0.2f);
            return;
        }
        
        gameManager.TimeManager.ResetToNormalSpeed();
        }
    }

    public void OnChangeCharacter(InputAction.CallbackContext context)
    {
        if (context.ReadValue<float>() > 0.5f)
        {
            runtimeCharacterSwitcher?.SwitchToNextCharacter();
        }
    }

    public void OnToggleFPSView(InputAction.CallbackContext context)
    {
        if (context.ReadValue<float>() > 0.5f)
        {
            gameManager.UIManager.ToggleFpsCounterFrameActive();
        }
    }

    public void OnLoadVRMFile(InputAction.CallbackContext context)
    {
        if (context.ReadValue<float>() > 0.5f)
        {
            vrmLoadManager.OpenLoadVrmFileDialog();
        }
    }

    #endregion

}
