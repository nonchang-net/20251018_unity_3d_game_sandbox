using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UniGLTF;
using UnityEngine;
using UniVRM10;

/// <summary>
/// VRM関連の処理を提供するユーティリティクラス
/// </summary>
public static class VRMUtility
{
    /// <summary>
    /// 詳細ログを有効にするかどうか
    /// </summary>
    public static bool EnableVerboseLog { get; set; } = false;

    /// <summary>
    /// VRMファイルパスからVRMを読み込んでセットアップするコルーチン
    /// </summary>
    /// <param name="vrmPath">VRMファイルのパス</param>
    /// <param name="spawnPosition">スポーン位置</param>
    /// <param name="animatorController">アニメーションコントローラー</param>
    /// <param name="physicsMaterial">Physics Material（CapsuleColliderに適用）</param>
    /// <param name="onComplete">完了時のコールバック（読み込まれたGameObjectを渡す）</param>
    /// <param name="onError">エラー時のコールバック（エラーメッセージを渡す）</param>
    public static IEnumerator LoadAndSetupVrmFromPath(
        string vrmPath,
        Vector3 spawnPosition,
        RuntimeAnimatorController animatorController = null,
        PhysicsMaterial physicsMaterial = null,
        System.Action<GameObject> onComplete = null,
        System.Action<string> onError = null)
    {
        if (EnableVerboseLog)
        {
            Debug.Log($"VRMUtility: VRM読み込みを開始します: {vrmPath}");
        }

        // VRMを非同期で読み込み
        Task<GameObject> loadTask = LoadVrmAsync(vrmPath);

        // Taskが完了するまで待機
        while (!loadTask.IsCompleted)
        {
            yield return null;
        }

        if (loadTask.Exception != null)
        {
            string errorMessage = $"VRM読み込み中にエラーが発生しました: {loadTask.Exception}";
            Debug.LogError($"VRMUtility: {errorMessage}");
            onError?.Invoke(errorMessage);
            yield break;
        }

        GameObject vrmCharacter = loadTask.Result;

        if (vrmCharacter == null)
        {
            string errorMessage = "VRMキャラクターの読み込みに失敗しました。";
            Debug.LogError($"VRMUtility: {errorMessage}");
            onError?.Invoke(errorMessage);
            yield break;
        }

        // デバッグログ出力
        LogVrmDebugInfo(vrmCharacter, "LoadAndSetupVrmFromPath");

        // VRMキャラクターをセットアップ
        SetupVrmCharacter(vrmCharacter, spawnPosition, animatorController, physicsMaterial);

        // コンポーネントの追加を確実に反映させるため、複数フレーム待機
        yield return null;
        yield return null;

        if (EnableVerboseLog)
        {
            Debug.Log($"VRMUtility: VRMキャラクター '{vrmCharacter.name}' のロードとセットアップが完了しました。");
        }

        onComplete?.Invoke(vrmCharacter);
    }

    /// <summary>
    /// バイトデータからVRMを読み込んでセットアップするコルーチン
    /// </summary>
    /// <param name="vrmData">VRMファイルのバイトデータ</param>
    /// <param name="fileName">一時ファイル名</param>
    /// <param name="spawnPosition">スポーン位置</param>
    /// <param name="animatorController">アニメーションコントローラー</param>
    /// <param name="physicsMaterial">Physics Material（CapsuleColliderに適用）</param>
    /// <param name="onComplete">完了時のコールバック（読み込まれたGameObjectを渡す）</param>
    /// <param name="onError">エラー時のコールバック（エラーメッセージを渡す）</param>
    public static IEnumerator LoadAndSetupVrmFromBytes(
        byte[] vrmData,
        string fileName,
        Vector3 spawnPosition,
        RuntimeAnimatorController animatorController = null,
        PhysicsMaterial physicsMaterial = null,
        System.Action<GameObject> onComplete = null,
        System.Action<string> onError = null)
    {
        // 一時ファイルに保存
        string tempPath = Path.Combine(Application.temporaryCachePath, fileName);

        try
        {
            File.WriteAllBytes(tempPath, vrmData);

            if (EnableVerboseLog)
            {
                Debug.Log($"VRMUtility: 一時ファイルに保存しました: {tempPath}");
            }
        }
        catch (System.Exception ex)
        {
            string errorMessage = $"一時ファイルの保存に失敗しました: {ex.Message}";
            Debug.LogError($"VRMUtility: {errorMessage}");
            onError?.Invoke(errorMessage);
            yield break;
        }

        // LoadAndSetupVrmFromPathを使用して読み込み
        yield return LoadAndSetupVrmFromPath(tempPath, spawnPosition, animatorController, physicsMaterial, onComplete, onError);
    }

    /// <summary>
    /// VRMのデバッグ情報をログ出力
    /// </summary>
    private static void LogVrmDebugInfo(GameObject vrmCharacter, string context)
    {
        if (!EnableVerboseLog) return;

        Debug.Log($"VRMUtility: [{context}] VRM読み込み直後の位置 = {vrmCharacter.transform.position}");

        // メッシュのバウンディングボックスを確認
        Renderer[] renderers = vrmCharacter.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            Debug.Log($"VRMUtility: [{context}] メッシュのバウンディングボックス center={bounds.center}, min.y={bounds.min.y:F2}, max.y={bounds.max.y:F2}");
        }
    }

    /// <summary>
    /// VRMファイルを非同期で読み込む
    /// </summary>
    /// <param name="path">VRMファイルのパス</param>
    /// <returns>読み込まれたVRMキャラクターのGameObject（失敗時はnull）</returns>
    public static async Task<GameObject> LoadVrmAsync(string path)
    {
        try
        {
            // VRMファイルをバイト配列として読み込み
            byte[] vrmBytes = await Task.Run(() => File.ReadAllBytes(path));

            // VRM10をインポート
            Vrm10Instance vrm10Instance = await Vrm10.LoadBytesAsync(
                vrmBytes,
                canLoadVrm0X: true,
                showMeshes: false,
                awaitCaller: new ImmediateCaller()
            );

            if (vrm10Instance != null)
            {
                // URP用のシェーダーに変換
                ConvertMaterialsToUrp(vrm10Instance.gameObject);

                // RuntimeGltfInstanceを取得してメッシュを表示
                var runtimeInstance = vrm10Instance.GetComponent<RuntimeGltfInstance>();
                if (runtimeInstance != null)
                {
                    runtimeInstance.ShowMeshes();
                    runtimeInstance.EnableUpdateWhenOffscreen();
                }

                return vrm10Instance.gameObject;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"VRMUtility: VRM読み込みエラー: {e.Message}\n{e.StackTrace}");
        }

        return null;
    }

    /// <summary>
    /// VRMマテリアルをURP用のシェーダーに変換
    /// </summary>
    /// <param name="vrmObject">VRMのGameObject</param>
    public static void ConvertMaterialsToUrp(GameObject vrmObject)
    {
        if (vrmObject == null) return;

        if (EnableVerboseLog)
        {
            Debug.Log("VRMUtility: マテリアルをURP用シェーダーに変換します...");
        }

        // URP用のMToon10シェーダーを検索
        Shader urpMtoonShader = Shader.Find("VRM10/Universal Render Pipeline/MToon10");
        if (urpMtoonShader == null)
        {
            Debug.LogError("VRMUtility: URP用MToon10シェーダーが見つかりません。VRM10パッケージが正しくインストールされているか確認してください。");
            return;
        }

        // すべてのSkinnedMeshRendererを取得してマテリアルを変換
        SkinnedMeshRenderer[] renderers = vrmObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        int convertedCount = 0;

        foreach (var renderer in renderers)
        {
            if (renderer.sharedMaterials == null) continue;

            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null) continue;

                // 現在のシェーダーがMToon10系の場合のみ変換
                if (material.shader != null &&
                    (material.shader.name.Contains("VRM10/MToon10") || material.shader.name.Contains("VRM/MToon")))
                {
                    if (EnableVerboseLog)
                    {
                        Debug.Log($"VRMUtility: マテリアル '{material.name}' のシェーダーを変換: {material.shader.name} -> {urpMtoonShader.name}");
                    }
                    material.shader = urpMtoonShader;
                    convertedCount++;
                }
            }
        }

        // MeshRendererも確認（一部のVRMモデルで使用される場合がある）
        MeshRenderer[] meshRenderers = vrmObject.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var renderer in meshRenderers)
        {
            if (renderer.sharedMaterials == null) continue;

            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null) continue;

                if (material.shader != null &&
                    (material.shader.name.Contains("VRM10/MToon10") || material.shader.name.Contains("VRM/MToon")))
                {
                    if (EnableVerboseLog)
                    {
                        Debug.Log($"VRMUtility: マテリアル '{material.name}' のシェーダーを変換: {material.shader.name} -> {urpMtoonShader.name}");
                    }
                    material.shader = urpMtoonShader;
                    convertedCount++;
                }
            }
        }

        if (EnableVerboseLog)
        {
            Debug.Log($"VRMUtility: {convertedCount}個のマテリアルをURP用シェーダーに変換しました。");
        }
    }

    /// <summary>
    /// VRMキャラクターに必要なコンポーネントをセットアップ
    /// </summary>
    /// <param name="vrmCharacter">VRMキャラクターのGameObject</param>
    /// <param name="spawnPosition">初期位置</param>
    /// <param name="animatorController">適用するアニメーションコントローラー</param>
    /// <param name="physicsMaterial">CapsuleColliderに適用するPhysics Material</param>
    public static void SetupVrmCharacter(
        GameObject vrmCharacter,
        Vector3 spawnPosition,
        RuntimeAnimatorController animatorController = null,
        PhysicsMaterial physicsMaterial = null)
    {
        if (vrmCharacter == null)
        {
            Debug.LogError("VRMUtility: vrmCharacterがnullです。");
            return;
        }

        if (EnableVerboseLog)
        {
            Debug.Log($"VRMUtility: VRMキャラクターのセットアップを開始します..." +
                $"\n  セットアップ前の位置: {vrmCharacter.transform.position}" +
                $"\n  設定する位置: {spawnPosition}");
        }

        // 位置を設定
        vrmCharacter.transform.position = spawnPosition;

        if (EnableVerboseLog)
        {
            Debug.Log($"VRMUtility: 位置設定後: {vrmCharacter.transform.position}");
        }

        // Rigidbodyを追加・設定
        SetupRigidbody(vrmCharacter);

        // CapsuleColliderを追加・設定（VRMキャラクター用）
        SetupCapsuleCollider(vrmCharacter, physicsMaterial);

        // Animatorを設定
        SetupAnimator(vrmCharacter, animatorController);

        // Animator設定後に位置を再確認・再設定（Animatorが位置を変更する可能性があるため）
        if (vrmCharacter.transform.position != spawnPosition)
        {
            if (EnableVerboseLog)
            {
                Debug.Log($"VRMUtility: Animator設定後に位置がずれました。{vrmCharacter.transform.position} -> {spawnPosition}");
            }
            vrmCharacter.transform.position = spawnPosition;
        }

        // GameCharacterCollisionTriggerを追加してサウンドを設定
        SetupCollisionTrigger(vrmCharacter);

        // AutoEyeBlinkForVRMを追加
        AutoEyeBlinkForVRM eyeBlink = vrmCharacter.GetComponent<AutoEyeBlinkForVRM>();
        if (eyeBlink == null)
        {
            eyeBlink = vrmCharacter.AddComponent<AutoEyeBlinkForVRM>();
            if (EnableVerboseLog)
            {
                Debug.Log("VRMUtility: AutoEyeBlinkForVRMを追加しました。");
            }
        }

        // 最後にもう一度位置を確実に設定（コンポーネント追加で位置がずれる可能性があるため）
        vrmCharacter.transform.position = spawnPosition;
        vrmCharacter.transform.rotation = Quaternion.identity;

        // 最終確認：すべてのコンポーネントが正しく追加されたか
        if (EnableVerboseLog)
        {
            Debug.Log($"VRMUtility: VRMキャラクターのセットアップが完了しました。" +
                $"\n  Rigidbody: {vrmCharacter.GetComponent<Rigidbody>() != null}" +
                $"\n  CapsuleCollider: {vrmCharacter.GetComponent<CapsuleCollider>() != null}" +
                $"\n  GameCharacterCollisionTrigger: {vrmCharacter.GetComponent<GameCharacterCollisionTrigger>() != null}" +
                $"\n  AutoEyeBlinkForVRM: {vrmCharacter.GetComponent<AutoEyeBlinkForVRM>() != null}" +
                $"\n  最終位置: {vrmCharacter.transform.position}");
        }
    }

    /// <summary>
    /// Rigidbodyを追加・設定
    /// </summary>
    private static void SetupRigidbody(GameObject vrmCharacter)
    {
        Rigidbody rb = vrmCharacter.GetComponent<Rigidbody>();
        if (rb == null)
        {
            if (EnableVerboseLog)
            {
                Debug.Log("VRMUtility: Rigidbodyが存在しないため、追加します。");
            }
            rb = vrmCharacter.AddComponent<Rigidbody>();
        }
        else if (EnableVerboseLog)
        {
            Debug.Log("VRMUtility: Rigidbodyは既に存在します。");
        }

        rb.mass = 1f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.None; // Interpolateだと回転がクリアされる問題が発生
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete; // 手作業モデルと同じ設定
        rb.freezeRotation = true;
        rb.constraints = RigidbodyConstraints.FreezeRotation; // 明示的に回転を凍結

        if (EnableVerboseLog)
        {
            Debug.Log($"VRMUtility: Rigidbodyのセットアップが完了しました。" +
                $"\n  GameObject: {vrmCharacter.name}" +
                $"\n  interpolation: {rb.interpolation}" +
                $"\n  collisionDetectionMode: {rb.collisionDetectionMode}" +
                $"\n  freezeRotation: {rb.freezeRotation}" +
                $"\n  constraints: {rb.constraints}");
        }
    }

    /// <summary>
    /// CapsuleColliderを追加・設定（VRMキャラクター用）
    /// ヒューマノイドのボーン情報から適切なサイズを自動計算
    /// </summary>
    /// <param name="vrmCharacter">VRMキャラクターのGameObject</param>
    /// <param name="physicsMaterial">適用するPhysics Material</param>
    private static void SetupCapsuleCollider(GameObject vrmCharacter, PhysicsMaterial physicsMaterial = null)
    {
        CapsuleCollider capsule = vrmCharacter.GetComponent<CapsuleCollider>();
        if (capsule == null)
        {
            if (EnableVerboseLog)
            {
                Debug.Log("VRMUtility: CapsuleColliderが存在しないため、追加します。");
            }
            capsule = vrmCharacter.AddComponent<CapsuleCollider>();
        }
        else if (EnableVerboseLog)
        {
            Debug.Log("VRMUtility: CapsuleColliderは既に存在します。");
        }

        // Animatorから身長を推定してCapsuleColliderを調整
        Animator animator = vrmCharacter.GetComponent<Animator>();
        if (animator != null && animator.avatar != null && animator.avatar.isHuman)
        {
            // ヒップとヘッドの位置から身長を計算
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);

            if (hips != null && head != null)
            {
                // ローカル座標での距離を計算（VRMキャラクターのtransformを基準とした相対位置）
                Vector3 hipsLocal = vrmCharacter.transform.InverseTransformPoint(hips.position);
                Vector3 headLocal = vrmCharacter.transform.InverseTransformPoint(head.position);
                float height = Vector3.Distance(hipsLocal, headLocal) + 0.3f; // 頭の高さを追加

                capsule.height = height;
                capsule.radius = height * 0.15f;
                capsule.center = new Vector3(0, height / 2f, 0);

                if (EnableVerboseLog)
                {
                    Debug.Log($"VRMUtility: CapsuleColliderをボーン情報から設定しました。(height={height:F2}, radius={capsule.radius:F2}, center={capsule.center})");
                }
            }
            else
            {
                // デフォルト値
                SetDefaultCapsuleCollider(capsule);
                Debug.LogWarning("VRMUtility: ボーン情報が取得できませんでした。デフォルト値を使用します。");
            }
        }
        else
        {
            // デフォルト値
            SetDefaultCapsuleCollider(capsule);
            Debug.LogWarning("VRMUtility: Animatorが見つかりませんでした。デフォルト値を使用します。");
        }

        // Physics Materialを設定
        if (physicsMaterial != null)
        {
            capsule.material = physicsMaterial;
            if (EnableVerboseLog)
            {
                Debug.Log($"VRMUtility: Physics Material '{physicsMaterial.name}' をCapsuleColliderに適用しました。");
            }
        }
        else
        {
            Debug.LogError("VRMUtility: Physics Materialが設定されていません。CapsuleColliderに摩擦が残るため、引っかかる可能性があります。");
        }
    }

    /// <summary>
    /// CapsuleColliderにデフォルト値を設定
    /// </summary>
    private static void SetDefaultCapsuleCollider(CapsuleCollider capsule)
    {
        capsule.height = 1.8f;
        capsule.radius = 0.3f;
        capsule.center = new Vector3(0, 0.9f, 0);
    }

    /// <summary>
    /// Animatorを設定
    /// </summary>
    private static void SetupAnimator(GameObject vrmCharacter, RuntimeAnimatorController animatorController)
    {
        Animator animator = vrmCharacter.GetComponent<Animator>();
        if (animator != null)
        {
            // Root Motionを無効化（AnimatorControllerを設定する前に行う必要がある）
            animator.applyRootMotion = false;
            if (EnableVerboseLog)
            {
                Debug.Log($"VRMUtility: Animator.applyRootMotion = {animator.applyRootMotion}");
            }

            // アニメーションコントローラーを設定
            if (animatorController != null)
            {
                animator.runtimeAnimatorController = animatorController;
                if (EnableVerboseLog)
                {
                    Debug.Log($"VRMUtility: AnimatorControllerを設定しました。({animatorController.name})");
                }
            }
            else
            {
                Debug.LogWarning("VRMUtility: Animator Controllerが設定されていません。");
            }
        }
        else
        {
            Debug.LogWarning("VRMUtility: Animatorが見つかりませんでした。");
        }
    }

    /// <summary>
    /// GameCharacterCollisionTriggerを追加してサウンドを設定
    /// </summary>
    private static void SetupCollisionTrigger(GameObject vrmCharacter)
    {
        GameCharacterCollisionTrigger collisionTrigger = vrmCharacter.GetComponent<GameCharacterCollisionTrigger>();
        if (collisionTrigger == null)
        {
            collisionTrigger = vrmCharacter.AddComponent<GameCharacterCollisionTrigger>();
            if (EnableVerboseLog)
            {
                Debug.Log("VRMUtility: GameCharacterCollisionTriggerを追加しました。");
            }
        }
    }
}
