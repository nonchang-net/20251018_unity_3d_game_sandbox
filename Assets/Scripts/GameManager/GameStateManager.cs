using UnityEngine;
using R3;

public class GameStateManager : MonoBehaviour
{

    private GameState state = new(3);
    public GameState State => state;
    public bool IsDead => State.IsDead.CurrentValue;

    /// <summary>
    /// ダメージを受ける処理
    /// </summary>
    /// <param name="damage">ダメージ量</param>
    /// <param name="damageSource">ダメージソース（GameObject）</param>
    public void TakeDamage(int damage, GameObject damageSource)
    {
        if (state.IsDead.CurrentValue)
        {
            // 既に死亡している場合は何もしない
            return;
        }

        // HPを減らす
        int newHp = Mathf.Max(0, state.CurrentHp.CurrentValue - damage);
        state.CurrentHp.Value = newHp;

        // ダメージイベントを発火
        state.OnDamageReceived.OnNext(new DamageInfo(damage, damageSource, newHp));

        if (state.IsDead.CurrentValue)
        {
            // Debug.Log("stateManager: キャラクターが死亡しました。");
        }
    }

    public void HealHp(int amount)
    {
        if (state.CurrentHp.Value >= state.MaxHp.Value) return;
        var newHp = state.CurrentHp.Value + amount;
        if (newHp > state.MaxHp.Value) newHp = state.MaxHp.Value;
        state.CurrentHp.Value = newHp;
        //Debug.Log($"HPが回復しました。{state.CurrentHp.Value}");
    }

    /// <summary>
    /// コインを追加する処理
    /// </summary>
    /// <param name="amount">追加するコイン数</param>
    /// <param name="coinSource">コインソース（GameObject）</param>
    public void AddCoin(int amount, GameObject coinSource)
    {
        // コイン数を増やす
        int newCoin = state.CurrentCoin.CurrentValue + amount;
        state.CurrentCoin.Value = newCoin;

        // コイン取得イベントを発火
        state.OnCoinGetReceived.OnNext(new CoinGetInfo(amount, coinSource, newCoin));
    }

    /// <summary>
    /// リスポーン処理（HPをMaxHpに回復、死亡状態を解除）
    /// </summary>
    public void Respawn()
    {
        // HPをMaxHpに回復
        state.CurrentHp.Value = state.MaxHp.CurrentValue;
        // Debug.Log("stateManager: リスポーンしました。HP回復。");
    }

    /// <summary>
    /// チェックポイントをアクティブ化
    /// </summary>
    /// <param name="checkPoint">アクティブ化するCheckPoint</param>
    public void ActivateCheckPoint(CheckPoint checkPoint)
    {
        if (checkPoint == null)
        {
            // Debug.LogWarning("stateManager: アクティブ化しようとしたCheckPointがnullです。");
            return;
        }

        // 既にアクティブ化済みかチェック
        if (state.ActivatedCheckPoints.Contains(checkPoint))
        {
            // Debug.Log($"stateManager: CheckPoint '{checkPoint.CheckPointName}' は既にアクティブ化済みです。");
            return;
        }

        // CheckPointをアクティブ化
        checkPoint.Activate();

        // アクティブ化済みリストに追加
        state.ActivatedCheckPoints.Add(checkPoint);

        // チェックポイントアクティブ化イベントを発火
        state.OnCheckPointActivated.OnNext(new CheckPointActivatedInfo(
            checkPoint,
            checkPoint.CheckPointName,
            checkPoint.SpawnPosition
        ));

        // Debug.Log($"stateManager: CheckPoint '{checkPoint.CheckPointName}' をアクティブ化しました。");
    }

    /// <summary>
    /// ハイジャンプを発動
    /// </summary>
    /// <param name="jumpHeight">ジャンプの高さ（メートル）</param>
    /// <param name="jumpSpeed">ジャンプの上昇速度</param>
    /// <param name="source">HighJumperソース（GameObject）</param>
    public void TriggerHighJump(float jumpHeight, float jumpSpeed, GameObject source)
    {
        // ハイジャンプイベントを発火
        state.OnHighJump.OnNext(new HighJumpInfo(jumpHeight, jumpSpeed, source));

        // Debug.Log($"stateManager: ハイジャンプを発動しました。高さ: {jumpHeight}m, 速度: {jumpSpeed}");
    }

    /// <summary>
    /// コインが生成されたことを通知
    /// </summary>
    /// <param name="count">生成されたコイン数</param>
    /// <param name="source">生成ソース（GameObject）</param>
    public void AddGeneratedCoins(int count, GameObject source)
    {
        // 総コイン数を増やす
        int newTotal = state.TotalCoinCount.CurrentValue + count;
        state.TotalCoinCount.Value = newTotal;

        // コイン生成イベントを発火
        state.OnCoinGenerated.OnNext(new CoinGeneratedInfo(count, source));

        // Debug.Log($"stateManager: コインが{count}枚生成されました。総数: {newTotal}");
    }

    /// <summary>
    /// ゲームをポーズする
    /// </summary>
    public void Pause()
    {
        state.IsPaused.Value = true;
    }

    /// <summary>
    /// ゲームのポーズを解除する
    /// </summary>
    public void Unpause()
    {
        state.IsPaused.Value = false;
    }

    /// <summary>
    /// ポーズ状態をトグルする
    /// </summary>
    public void TogglePause()
    {
        state.IsPaused.Value = !state.IsPaused.CurrentValue;
    }

    /// <summary>
    /// タイムスケールを設定する
    /// </summary>
    /// <param name="timeScale">設定するタイムスケール</param>
    public void SetTimeScale(float timeScale)
    {
        state.CurrentTimeScale.Value = Mathf.Max(0f, timeScale);
    }
}


/// <summary>
/// ダメージ情報
/// </summary>
public readonly struct DamageInfo
{
    /// <summary>ダメージ量</summary>
    public readonly int Damage;

    /// <summary>ダメージソース（どのオブジェクトから受けたか）</summary>
    public readonly GameObject Source;

    /// <summary>ダメージ後の現在HP</summary>
    public readonly int CurrentHp;

    public DamageInfo(int damage, GameObject source, int currentHp)
    {
        Damage = damage;
        Source = source;
        CurrentHp = currentHp;
    }
}

/// <summary>
/// コイン取得情報
/// </summary>
public readonly struct CoinGetInfo
{
    /// <summary>取得したコイン数</summary>
    public readonly int Amount;

    /// <summary>コインソース（どのオブジェクトから取得したか）</summary>
    public readonly GameObject Source;

    /// <summary>取得後の現在コイン数</summary>
    public readonly int CurrentCoin;

    public CoinGetInfo(int amount, GameObject source, int currentCoin)
    {
        Amount = amount;
        Source = source;
        CurrentCoin = currentCoin;
    }
}

/// <summary>
/// チェックポイントアクティブ化情報
/// </summary>
public readonly struct CheckPointActivatedInfo
{
    /// <summary>アクティブ化されたCheckPoint</summary>
    public readonly CheckPoint CheckPoint;

    /// <summary>チェックポイント名</summary>
    public readonly string CheckPointName;

    /// <summary>リスポーン位置</summary>
    public readonly Transform SpawnPosition;

    public CheckPointActivatedInfo(CheckPoint checkPoint, string checkPointName, Transform spawnPosition)
    {
        CheckPoint = checkPoint;
        CheckPointName = checkPointName;
        SpawnPosition = spawnPosition;
    }
}

/// <summary>
/// ハイジャンプ情報
/// </summary>
public readonly struct HighJumpInfo
{
    /// <summary>ジャンプの高さ（メートル）</summary>
    public readonly float JumpHeight;

    /// <summary>ジャンプの上昇速度</summary>
    public readonly float JumpSpeed;

    /// <summary>HighJumperソース（どのオブジェクトから発動したか）</summary>
    public readonly GameObject Source;

    public HighJumpInfo(float jumpHeight, float jumpSpeed, GameObject source)
    {
        JumpHeight = jumpHeight;
        JumpSpeed = jumpSpeed;
        Source = source;
    }
}

/// <summary>
/// コイン生成情報
/// </summary>
public readonly struct CoinGeneratedInfo
{
    /// <summary>生成されたコイン数</summary>
    public readonly int GeneratedCount;

    /// <summary>生成ソース（どのオブジェクトから生成されたか）</summary>
    public readonly GameObject Source;

    public CoinGeneratedInfo(int generatedCount, GameObject source)
    {
        GeneratedCount = generatedCount;
        Source = source;
    }
}

/// <summary>
/// ゲーム状態のReactivePropertyソース
/// </summary>
public class GameState
{
    public ReactiveProperty<int> CurrentCoin { get; set; }
    public ReactiveProperty<int> CurrentHp { get; private set; }
    public ReactiveProperty<int> MaxHp { get; private set; }
    public ReadOnlyReactiveProperty<bool> IsDead { get; private set; }

    /// <summary>
    /// HPが1以下になったときにtrueになる警告状態フラグ
    /// </summary>
    public ReadOnlyReactiveProperty<bool> IsCaution { get; private set; }

    /// <summary>
    /// ステージに生成されたコインの総数
    /// </summary>
    public ReactiveProperty<int> TotalCoinCount { get; private set; }

    /// <summary>
    /// ダメージを受けたときに発火するSubject
    /// ダメージ量、ダメージソース、ダメージ後のHPを通知
    /// </summary>
    public Subject<DamageInfo> OnDamageReceived { get; private set; }

    /// <summary>
    /// コインを取得したときに発火するSubject
    /// 取得したコイン数、コインソース、取得後のコイン数を通知
    /// </summary>
    public Subject<CoinGetInfo> OnCoinGetReceived { get; private set; }

    /// <summary>
    /// コインが生成されたときに発火するSubject
    /// 生成されたコイン数、生成ソースを通知
    /// </summary>
    public Subject<CoinGeneratedInfo> OnCoinGenerated { get; private set; }

    /// <summary>
    /// チェックポイントがアクティブ化されたときに発火するSubject
    /// アクティブ化されたCheckPoint、チェックポイント名、リスポーン位置を通知
    /// </summary>
    public Subject<CheckPointActivatedInfo> OnCheckPointActivated { get; private set; }

    /// <summary>
    /// ハイジャンプが発動したときに発火するSubject
    /// ジャンプの高さ、上昇速度、ソースを通知
    /// </summary>
    public Subject<HighJumpInfo> OnHighJump { get; private set; }

    /// <summary>
    /// アクティブ化済みのCheckPointリスト
    /// </summary>
    public System.Collections.Generic.List<CheckPoint> ActivatedCheckPoints { get; private set; }

    /// <summary>
    /// ゲームがポーズ状態かどうか
    /// </summary>
    public ReactiveProperty<bool> IsPaused { get; private set; }

    /// <summary>
    /// 現在のタイムスケール（Time.timeScale）
    /// </summary>
    public ReactiveProperty<float> CurrentTimeScale { get; private set; }

    public GameState(int initialHp)
    {
        CurrentCoin = new ReactiveProperty<int>(0);
        TotalCoinCount = new ReactiveProperty<int>(0);
        CurrentHp = new ReactiveProperty<int>(initialHp);
        MaxHp = new ReactiveProperty<int>(initialHp);
        IsDead = CurrentHp.Select(x => x <= 0).ToReadOnlyReactiveProperty();
        IsCaution = CurrentHp.Select(x => x <= 1 && x > 0).ToReadOnlyReactiveProperty();
        OnDamageReceived = new Subject<DamageInfo>();
        OnCoinGetReceived = new Subject<CoinGetInfo>();
        OnCoinGenerated = new Subject<CoinGeneratedInfo>();
        OnCheckPointActivated = new Subject<CheckPointActivatedInfo>();
        OnHighJump = new Subject<HighJumpInfo>();
        ActivatedCheckPoints = new System.Collections.Generic.List<CheckPoint>();
        IsPaused = new ReactiveProperty<bool>(false);
        CurrentTimeScale = new ReactiveProperty<float>(1f);
    }
}