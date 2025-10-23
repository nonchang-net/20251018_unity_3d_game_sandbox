using UnityEngine;

public static class PrefabUtility
{
    /// <summary>
    /// IGameManagedインターフェースを実装しているコンポーネントがあればGameManager参照を渡す
    /// </summary>
    public static void SetupGameManagedComponent(GameManager gameManager, GameObject gameObject)
    {
        IGameManaged[] managedComponents = gameObject.GetComponents<IGameManaged>();
        foreach (var managedComponent in managedComponents)
        {
            managedComponent.SetGameManager(gameManager);
        }
    }
}