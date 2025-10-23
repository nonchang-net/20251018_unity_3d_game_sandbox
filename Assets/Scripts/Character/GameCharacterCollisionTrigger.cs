using UnityEngine;

/// <summary>
/// プレイヤーのコリジョントリガー
/// プレイヤーオブジェクトにアタッチして使用
/// - コイン取得
/// - ダメージソースとの衝突検知
/// - DeadZone（落下死亡判定）
/// </summary>
public class GameCharacterCollisionTrigger : MonoBehaviour, IGameManaged
{
    [Header("GameManager")]
    [SerializeField] private GameManager gameManager;

    [Header("ダメージ設定")]
    [Tooltip("DamageSourceタグのオブジェクトから受けるダメージ量")]
    [SerializeField] private int damageAmount = 1;

    [Header("DeadZone設定")]
    [Tooltip("DeadZoneに触れた時に即座に死亡するか（true: 即死, false: MaxHPのダメージ）")]
    [SerializeField] private bool instantDeathOnDeadZone = true;

    [Header("UI管理")]
    [Tooltip("GameUIManager（StaticMessage表示用）")]
    [SerializeField] private GameUIManager uiManager;

    void Awake()
    {
        // GameUIManagerの自動検出
        if (uiManager == null)
        {
            uiManager = FindFirstObjectByType<GameUIManager>();
            if (uiManager == null)
            {
                Debug.LogWarning("GameCharacterCollisionTrigger: GameUIManagerが見つかりません。StaticMessage表示機能は動作しません。");
            }
        }
    }

    public void SetGameManager(GameManager gameManager)
    {
        this.gameManager = gameManager;

        // コイン生成をgameManager.StateManagerに通知
        gameManager.StateManager.AddGeneratedCoins(1, gameObject);
    }


    /// <summary>
    /// トリガーコライダーに入ったときに呼ばれる
    /// </summary>
    void OnTriggerEnter(Collider other)
    {
        //Debug.Log($"GameCharacterCollisionTrigger: Trigger entered with {other.name}, tag: {other.tag}");

        // コインタグのオブジェクトに触れたかチェック
        if (other.CompareTag("Coin"))
        {
            CollectCoin(other.gameObject);
        }
        // ダメージソースタグのオブジェクトに触れたかチェック
        else if (other.CompareTag("DamageSource"))
        {
            TakeDamageFromSource(other.gameObject);
        }
        // DeadZoneタグのオブジェクトに触れたかチェック
        else if (other.CompareTag("DeadZone"))
        {
            EnterDeadZone(other.gameObject);
        }
        // StaticMessageタグのオブジェクトに触れたかチェック
        else if (other.CompareTag("StaticMessage"))
        {
            EnterStaticMessage(other.gameObject);
        }
        // CheckPointタグのオブジェクトに触れたかチェック
        else if (other.CompareTag("CheckPoint"))
        {
            ActivateCheckPoint(other.gameObject);
        }
        // HighJumperタグのオブジェクトに触れたかチェック
        else if (other.CompareTag("HighJumper"))
        {
            TriggerHighJump(other.gameObject);
        }
    }

    /// <summary>
    /// トリガーコライダーから出たときに呼ばれる
    /// </summary>
    void OnTriggerExit(Collider other)
    {
        // StaticMessageタグのオブジェクトから離れたかチェック
        if (other.CompareTag("StaticMessage"))
        {
            ExitStaticMessage(other.gameObject);
        }
    }

    /// <summary>
    /// コインを取得する処理
    /// </summary>
    void CollectCoin(GameObject coin)
    {
        // Coinスクリプトを取得
        Coin coinScript = coin.GetComponent<Coin>();
        if (coinScript == null)
        {
            Debug.LogWarning($"GameCharacterCollisionTrigger: {coin.name} にCoinスクリプトがありません。");
            return;
        }

        // 既に取得済みの場合は処理しない
        if (coinScript.IsCollected())
        {
            return;
        }

        // コインの取得アニメーションを開始（プレイヤーのTransformを渡す）
        coinScript.Collect(transform);
        // HP回復
        gameManager.StateManager.HealHp(1);
    }

    /// <summary>
    /// ダメージソースからダメージを受ける処理
    /// </summary>
    /// <param name="damageSource">ダメージソースのGameObject</param>
    void TakeDamageFromSource(GameObject damageSource)
    {
        // gameManager.StateManagerのダメージ処理を呼び出す
        gameManager.StateManager.TakeDamage(damageAmount, damageSource);
    }

    /// <summary>
    /// DeadZoneに入った時の処理
    /// </summary>
    /// <param name="deadZone">DeadZoneのGameObject</param>
    void EnterDeadZone(GameObject deadZone)
    {
        // 既に死亡している場合は処理しない
        if (gameManager.StateManager.State.IsDead.CurrentValue)
        {
            return;
        }

        // Debug.Log($"GameCharacterCollisionTrigger: DeadZone ({deadZone.name}) に入りました。");

        if (instantDeathOnDeadZone)
        {
            // 即座に死亡（現在HPと同じダメージを与える）
            int currentHp = gameManager.StateManager.State.CurrentHp.CurrentValue;
            gameManager.StateManager.TakeDamage(currentHp, deadZone);
        }
        else
        {
            // MaxHP分のダメージを与える（通常は即死）
            int maxHp = gameManager.StateManager.State.MaxHp.CurrentValue;
            gameManager.StateManager.TakeDamage(maxHp, deadZone);
        }
    }

    /// <summary>
    /// StaticMessageに入った時の処理
    /// </summary>
    /// <param name="staticMessage">StaticMessageのGameObject</param>
    void EnterStaticMessage(GameObject staticMessage)
    {
        // StaticMessengerスクリプトを取得
        StaticMessenger messenger = staticMessage.GetComponent<StaticMessenger>();
        if (messenger == null)
        {
            Debug.LogWarning($"GameCharacterCollisionTrigger: {staticMessage.name} にStaticMessengerスクリプトがありません。");
            return;
        }

        // UIManagerが設定されていない場合は処理しない
        if (uiManager == null)
        {
            Debug.LogWarning("GameCharacterCollisionTrigger: GameUIManagerが設定されていません。StaticMessageを表示できません。");
            return;
        }

        // メッセージを表示
        uiManager.ShowStaticMessage(messenger.Message);
    }

    /// <summary>
    /// StaticMessageから出た時の処理
    /// </summary>
    /// <param name="staticMessage">StaticMessageのGameObject</param>
    void ExitStaticMessage(GameObject staticMessage)
    {
        // UIManagerが設定されていない場合は処理しない
        if (uiManager == null)
        {
            return;
        }

        // メッセージを非表示
        uiManager.HideStaticMessage();
    }

    /// <summary>
    /// CheckPointをアクティブ化する処理
    /// </summary>
    /// <param name="checkPointObject">CheckPointのGameObject</param>
    void ActivateCheckPoint(GameObject checkPointObject)
    {
        // CheckPointコンポーネントを取得
        CheckPoint checkPoint = checkPointObject.GetComponent<CheckPoint>();
        if (checkPoint == null)
        {
            Debug.LogWarning($"GameCharacterCollisionTrigger: CheckPointタグのオブジェクト '{checkPointObject.name}' にCheckPointコンポーネントがありません。");
            return;
        }

        // 既にアクティブ化済みの場合は処理しない
        if (checkPoint.IsActivated)
        {
            return;
        }

        // gameManager.StateManagerを通じてCheckPointをアクティブ化
        gameManager.StateManager.ActivateCheckPoint(checkPoint);
    }

    /// <summary>
    /// HighJumperを発動する処理
    /// </summary>
    /// <param name="highJumperObject">HighJumperのGameObject</param>
    void TriggerHighJump(GameObject highJumperObject)
    {
        // HighJumperコンポーネントを取得
        HighJumper highJumper = highJumperObject.GetComponent<HighJumper>();
        if (highJumper == null)
        {
            Debug.LogWarning($"GameCharacterCollisionTrigger: HighJumperタグのオブジェクト '{highJumperObject.name}' にHighJumperコンポーネントがありません。");
            return;
        }

        // gameManager.StateManagerを通じてハイジャンプを発動
        gameManager.StateManager.TriggerHighJump(highJumper.JumpHeight, highJumper.JumpSpeed, highJumperObject);
    }
}
