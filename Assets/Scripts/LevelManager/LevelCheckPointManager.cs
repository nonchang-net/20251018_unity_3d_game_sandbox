using UnityEngine;

/// <summary>
/// レベル（ステージ）ごとのチェックポイント管理
/// シーン上の複数のレベルに対応し、各レベルのチェックポイント一覧を保持する
/// </summary>
public class LevelCheckPointManager : MonoBehaviour
{
    [Header("チェックポイント設定")]
    [Tooltip("このレベルのチェックポイント一覧")]
    [SerializeField] private CheckPoint[] checkPoints;

    /// <summary>
    /// チェックポイント一覧を取得
    /// </summary>
    public CheckPoint[] CheckPoints => checkPoints;

    /// <summary>
    /// 最初のチェックポイントの位置を取得
    /// チェックポイントが存在しない場合はnullを返す
    /// </summary>
    /// <returns>最初のチェックポイントのTransform、存在しない場合はnull</returns>
    public Transform GetFirstCheckPointPosition()
    {
        if (checkPoints == null || checkPoints.Length == 0)
        {
            return null;
        }

        // 最初のチェックポイントを取得
        CheckPoint firstCheckPoint = checkPoints[0];
        if (firstCheckPoint == null || firstCheckPoint.SpawnPosition == null)
        {
            return null;
        }

        return firstCheckPoint.SpawnPosition;
    }

    /// <summary>
    /// 指定インデックスのチェックポイントを取得
    /// </summary>
    /// <param name="index">チェックポイントのインデックス</param>
    /// <returns>チェックポイント、範囲外の場合はnull</returns>
    public CheckPoint GetCheckPoint(int index)
    {
        if (checkPoints == null || index < 0 || index >= checkPoints.Length)
        {
            return null;
        }

        return checkPoints[index];
    }

    /// <summary>
    /// チェックポイントの総数を取得
    /// </summary>
    /// <returns>チェックポイントの総数</returns>
    public int GetCheckPointCount()
    {
        if (checkPoints == null)
        {
            return 0;
        }

        return checkPoints.Length;
    }

    void OnValidate()
    {
        // Inspectorでの検証
        if (checkPoints != null)
        {
            for (int i = 0; i < checkPoints.Length; i++)
            {
                if (checkPoints[i] != null && checkPoints[i].SpawnPosition == null)
                {
                    Debug.LogWarning($"LevelCheckPointManager: CheckPoint[{i}] の SpawnPosition が設定されていません。");
                }
            }
        }
    }
}
