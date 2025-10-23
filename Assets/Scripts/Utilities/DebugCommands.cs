using UnityEngine;
using System;

/// <summary>
/// デバッグコマンドの種類
/// </summary>
public enum DebugCommandType
{
    Warp,   // 指定位置にワープ
    Death,  // 死亡状態にする
    Heal    // HPを1回復
}

/// <summary>
/// デバッグコマンドのエントリ
/// </summary>
[Serializable]
public class DebugCommandEntry
{
    [Tooltip("入力キー")]
    public KeyCode key;

    [Tooltip("コマンドの種類")]
    public DebugCommandType commandType;

    [Tooltip("ワープ先Transform（Warpコマンド用）")]
    public Transform warpTarget;
}

/// <summary>
/// デバッグコマンド管理
/// キーボード入力に応じてデバッグ機能を実行
/// </summary>
public class DebugCommands : MonoBehaviour
{
    [Header("デバッグコマンド設定")]
    [Tooltip("デバッグコマンドのリスト")]
    [SerializeField] private DebugCommandEntry[] debugCommands;

    [Header("参照")]
    [Tooltip("GameManager")]
    [SerializeField] private GameManager gameManager;

    [Header("デバッグ設定")]
    [Tooltip("デバッグログを表示する")]
    [SerializeField] private bool enableDebugLog = true;

    void Awake()
    {
        // GameManagerの自動検出
        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
            if (gameManager == null)
            {
                Debug.LogWarning("DebugCommands: GameManagerが見つかりません。一部のデバッグコマンドが動作しない可能性があります。");
            }
        }
    }

    void Update()
    {
        // デバッグコマンドの入力チェック
        if (debugCommands == null || debugCommands.Length == 0)
        {
            return;
        }

        foreach (var command in debugCommands)
        {
            if (command == null)
            {
                continue;
            }

            // キー入力をチェック
            if (Input.GetKeyDown(command.key))
            {
                ExecuteCommand(command);
            }
        }
    }

    /// <summary>
    /// デバッグコマンドを実行
    /// </summary>
    /// <param name="command">実行するコマンド</param>
    void ExecuteCommand(DebugCommandEntry command)
    {
        switch (command.commandType)
        {
            case DebugCommandType.Warp:
                ExecuteWarp(command);
                break;
            case DebugCommandType.Death:
                ExecuteDeath();
                break;
            case DebugCommandType.Heal:
                ExecuteHeal();
                break;
            default:
                Debug.LogWarning($"DebugCommands: 未知のコマンドタイプ: {command.commandType}");
                break;
        }
    }

    /// <summary>
    /// ワープコマンドを実行
    /// </summary>
    /// <param name="command">コマンドエントリ</param>
    void ExecuteWarp(DebugCommandEntry command)
    {
        if (command.warpTarget == null)
        {
            Debug.LogWarning($"DebugCommands: ワープ先が設定されていません（キー: {command.key}）");
            return;
        }

        if (gameManager == null)
        {
            Debug.LogWarning("DebugCommands: GameManagerが設定されていません。ワープできません。");
            return;
        }

        GameObject activeCharacter = gameManager.GetActiveCharacter();
        if (activeCharacter == null)
        {
            Debug.LogWarning("DebugCommands: アクティブキャラクターが見つかりません。ワープできません。");
            return;
        }

        // CharacterControllerを取得
        CharacterController characterController = activeCharacter.GetComponent<CharacterController>();
        if (characterController != null)
        {
            // CharacterControllerの場合は無効化してから移動
            characterController.enabled = false;
            activeCharacter.transform.position = command.warpTarget.position;
            activeCharacter.transform.rotation = command.warpTarget.rotation;
            characterController.enabled = true;
        }
        else
        {
            // CharacterController以外の場合は直接移動
            activeCharacter.transform.position = command.warpTarget.position;
            activeCharacter.transform.rotation = command.warpTarget.rotation;
        }

        if (enableDebugLog)
        {
            Debug.Log($"DebugCommands: キャラクターを '{command.warpTarget.name}' にワープしました（キー: {command.key}）");
        }
    }

    /// <summary>
    /// 死亡コマンドを実行
    /// </summary>
    void ExecuteDeath()
    {
        // 現在のHPと同じダメージを与えて死亡させる
        int currentHp = gameManager.StateManager.State.CurrentHp.CurrentValue;
        gameManager.StateManager.TakeDamage(currentHp, gameObject);

        if (enableDebugLog)
        {
            Debug.Log($"DebugCommands: 死亡コマンドを実行しました");
        }
    }

    /// <summary>
    /// 回復コマンドを実行
    /// </summary>
    void ExecuteHeal()
    {
        gameManager.StateManager.HealHp(1);

        if (enableDebugLog)
        {
            Debug.Log($"DebugCommands: HPを1回復しました");
        }
    }
}
