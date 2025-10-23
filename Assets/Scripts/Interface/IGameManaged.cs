/// <summary>
/// 動的生成時にGameManagerの参照セットアップが必要なコンポーネントであることを示すインターフェース
/// </summary>
public interface IGameManaged
{
    void SetGameManager(GameManager gameManager);
}