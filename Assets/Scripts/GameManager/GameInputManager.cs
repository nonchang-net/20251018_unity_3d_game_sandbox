using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ゲーム全体の入力管理
/// Input Systemからの入力を受け取り、各種コンポーネントに配信する
/// シーン内に1つだけ配置して使用
///
/// 使い方:
/// 1. 空のGameObjectにこのコンポーネントをアタッチ
/// 2. 必要な参照を設定
/// 3. GameManagerから初期化される
/// </summary>
public class GameInputManager : MonoBehaviour, InputSystem_Actions.IPlayerActions
{
    [Header("GameManager")]
    [SerializeField] private GameManager gameManager;

    [Header("参照")]
    [SerializeField] private CharacterTracker cameraTracker;
    [SerializeField] private GameUIManager gameUIManager;
    [SerializeField] private RuntimeCharacterSwitcher runtimeCharacterSwitcher;
    [SerializeField] private VRMLoadManager vrmLoadManager;
    [SerializeField] private GameCharacterManager gameCharacterManager;

    /// <summary>
    /// 詳細ログを有効にするかどうか（GameManagerから設定される）
    /// </summary>
    public static bool EnableVerboseLog { get; set; } = false;

    // Input System
    private InputSystem_Actions inputSystemActions;
    private InputSystem_Actions.PlayerActions inputSystemActionMap;

    // 入力値
    private Vector2 movement;
    private bool requestJump = false;
    private bool requestSprint = false;
    private bool requestResetCamera = false;

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

        if (cameraTracker == null)
        {
            Debug.LogWarning($"GameInputManager: CharacterTracker参照が設定されていません。v0.0.2現在ではcameraTracker未指定時の動作は検証対象外です。");
            return;
        }

        if (gameCharacterManager == null)
        {
            Debug.LogError($"GameInputManager: GameCharacterManager参照が設定されていません。修正してください。");
            return;
        }
    }

    void Update()
    {
        // カメラリセット処理
        if (requestResetCamera)
        {
            cameraTracker.ResetCamera();
            requestResetCamera = false;
        }

        // 入力値をGameCharacterManagerに送信
        if (gameCharacterManager != null)
        {
            gameCharacterManager.SetMovementInput(movement);
            gameCharacterManager.SetJumpRequest(requestJump);
            gameCharacterManager.SetSprintRequest(requestSprint);
        }

        // ジャンプリクエストはperformedの次のフレームでリセット
        if (requestJump)
        {
            requestJump = false;
        }
    }

    #region Interface implementation of InputSystem_Actions.IPlayerActions

    public void OnMove(InputAction.CallbackContext context)
    {
        movement = context.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        cameraTracker?.SetLookInput(context.ReadValue<Vector2>());
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        requestJump = context.performed;
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        requestSprint = context.ReadValue<float>() > 0.5f;
    }

    public void OnResetCamera(InputAction.CallbackContext context)
    {
        requestResetCamera = context.ReadValue<float>() > 0.5f;
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
            gameUIManager.ToggleFpsCounterFrameActive();
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

    /// <summary>
    /// カメラトラッカーを設定
    /// </summary>
    public void SetCameraTracker(CharacterTracker tracker)
    {
        cameraTracker = tracker;
    }

    /// <summary>
    /// GameCharacterManagerを設定
    /// </summary>
    public void SetGameCharacterManager(GameCharacterManager characterManager)
    {
        gameCharacterManager = characterManager;
    }
}
