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
    }

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
        if (context.ReadValue<float>() > 0.5f)
        {
            gameManager.TimeManager.TogglePause();
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
