using UnityEngine;
using UnityEditor;

/// <summary>
/// LookAtCheckerのカスタムInspector
/// 計算された角度をコピーできるボタンを追加
/// </summary>
[CustomEditor(typeof(LookAtChecker))]
public class LookAtCheckerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // デフォルトのInspectorを描画
        DrawDefaultInspector();

        LookAtChecker checker = (LookAtChecker)target;

        // 区切り線
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.Space();

        // 計算結果の表示エリア
        EditorGUILayout.LabelField("計算結果", EditorStyles.boldLabel);

        // 角度の表示（読み取り専用）
        EditorGUI.BeginDisabledGroup(true);
        Vector3 rotation = checker.CalculatedRotation;
        EditorGUILayout.Vector3Field("Calculated Rotation", rotation);
        EditorGUI.EndDisabledGroup();

        // コピーボタン
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Copy Rotation to Clipboard", GUILayout.Width(200), GUILayout.Height(30)))
        {
            // クリップボードにコピー
            string rotationString = $"({rotation.x:F2}, {rotation.y:F2}, {rotation.z:F2})";
            EditorGUIUtility.systemCopyBuffer = rotationString;
            Debug.Log($"Rotation copied to clipboard: {rotationString}");
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // 各軸を個別にコピーできるボタンも追加
        EditorGUILayout.LabelField("個別にコピー:", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button($"X: {rotation.x:F2}", GUILayout.Height(25)))
        {
            EditorGUIUtility.systemCopyBuffer = rotation.x.ToString("F2");
            Debug.Log($"X rotation copied: {rotation.x:F2}");
        }

        if (GUILayout.Button($"Y: {rotation.y:F2}", GUILayout.Height(25)))
        {
            EditorGUIUtility.systemCopyBuffer = rotation.y.ToString("F2");
            Debug.Log($"Y rotation copied: {rotation.y:F2}");
        }

        if (GUILayout.Button($"Z: {rotation.z:F2}", GUILayout.Height(25)))
        {
            EditorGUIUtility.systemCopyBuffer = rotation.z.ToString("F2");
            Debug.Log($"Z rotation copied: {rotation.z:F2}");
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // ヘルプボックス
        EditorGUILayout.HelpBox(
            "1. このオブジェクトをカメラ位置に配置\n" +
            "2. Look At Targetにターゲットを設定\n" +
            "3. Calculated Rotationに計算された角度が表示されます\n" +
            "4. ボタンをクリックして角度をクリップボードにコピー\n" +
            "5. TrackingSettingのLocked Camera Rotationに貼り付け",
            MessageType.Info);
    }
}
