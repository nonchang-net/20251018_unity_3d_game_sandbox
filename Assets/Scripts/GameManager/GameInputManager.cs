using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ゲーム全体の入力管理
/// Input Systemからの入力を受け取り、各種コンポーネントに配信する
/// シーン内に1つだけ配置して使用
/// </summary>
public class GameInputManager : MonoBehaviour, InputSystem_Actions.IPlayerActions
{
    [Header("GameManager")]
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

    void Awake()
    {
        // Input SystemはIME有効状態でプレイ開始するとshiftキーが反応しない
        // TODO: 以下では改善しなかった。制御方法など解決策を調査したい
        // Input.imeCompositionMode = IMECompositionMode.Off;
        // Keyboard.current?.SetIMEEnabled(false);

        inputSystemActions = new InputSystem_Actions();
        inputSystemActionMap = inputSystemActions.Player;
        inputSystemActionMap.AddCallbacks(this);
    }

    void OnEnable()
    {
        inputSystemActions?.Enable();
    }

    void OnDisable()
    {
        inputSystemActions?.Disable();
    }

    void OnDestroy()
    {
        inputSystemActions?.Dispose();
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
        gameManager.CharacterManager.SetMovementInput(context.ReadValue<Vector2>());
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

    public void OnResetCamera(InputAction.CallbackContext context)
    {
        gameManager.CharacterTracker.ResetCamera();
    }

    // -----
    // ゲームシステム
    public void OnTogglePause(InputAction.CallbackContext context)
    {
        if (context.ReadValue<float>() < 0.5f) return;

        gameManager.TimeManager.TogglePause();
        gameManager.UIManager.TogglePauseMenu();

        // マウスカーソル制御
        bool isMenuVisible = gameManager.UIManager.IsPauseMenuVisible;

        if (isMenuVisible)
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
