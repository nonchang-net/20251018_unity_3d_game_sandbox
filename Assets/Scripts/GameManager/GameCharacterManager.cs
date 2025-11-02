using UnityEngine;
using System;
using R3;
using NaughtyAttributes;

/// <summary>
/// キャラクターの移動・アニメーション・状態管理を担当
/// GameInputManagerから入力を受け取り、キャラクターを制御する
///
/// キャラクター実装の要件:
/// - パターン1: CharacterController
/// - パターン2: Rigidbody + Collider (BoxCollider, SphereCollider, CapsuleColliderなど)
///
/// 使い方:
/// 1. 空のGameObjectにこのコンポーネントをアタッチ
/// 2. 制御対象のキャラクターを設定
/// 3. GameInputManagerからSetTargetCharacter()を呼び出す
/// </summary>
public class GameCharacterManager : MonoBehaviour
{
    [Header("GameManager")]
    [Required("GameManagerの参照が必要です")]
    [SerializeField] private GameManager gameManager;

    [Header("制御対象")]
    [Required("制御対象のキャラクターが必要です")]
    [SerializeField] private GameObject targetCharacter;

    [Header("移動設定")]
    [SerializeField] private string animatorSpeedKey = "Speed";
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 10f;
    [Tooltip("キャラクターの回転速度（度/秒）。0以下で即座に回転（推奨）")]
    [SerializeField] private float rotationSpeed = 0f;
    [SerializeField] private string animatorIsCrouchedKey = "isCrouched";
    [SerializeField] private string animatorInAirKey = "inAir";
    [SerializeField] private string animatorIsDeadKey = "isDead";

    [Header("ジャンプ設定")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -9.81f;

    [Header("ノックバック設定")]
    [Tooltip("ノックバック時にキャラクターを弾け飛ばす")]
    [SerializeField] private string animatorIsKnockbackKey = "isKnockback";
    [SerializeField] private bool enableKnockbackMovement = true;

    [Tooltip("ノックバック時の移動距離")]
    [SerializeField] private float knockbackDistance = 2f;

    [Tooltip("ノックバック移動の速度")]
    [SerializeField] private float knockbackSpeed = 10f;


    [Header("キャラクター生成時に必要なデータ")]

    [Tooltip("操作キャラクター用アニメーションコントローラー")]
    [SerializeField] private RuntimeAnimatorController characterAnimatorController;

    [Tooltip("操作キャラクター用Physics Material")]
    [SerializeField] private PhysicsMaterial characterPhysicsMaterial;

    /// <summary>
    /// 操作キャラクター用AnimatorControllerを取得
    /// </summary>
    public RuntimeAnimatorController CharacterAnimatorController => characterAnimatorController;

    /// <summary>
    /// 操作キャラクター用Physics Materialを取得
    /// </summary>
    public PhysicsMaterial CharacterPhysicsMaterial => characterPhysicsMaterial;

    /// <summary>
    /// 詳細ログを有効にするかどうか（GameManagerから設定される）
    /// </summary>
    public static bool EnableVerboseLog { get; set; } = false;

    // R3購読用disposable
    private IDisposable disposable;

    // キャラクターの実装タイプ
    private enum CharacterImplementationType
    {
        None,
        CharacterController,
        RigidbodyAndCollider
    }

    private CharacterImplementationType implementationType = CharacterImplementationType.None;

    // コンポーネント参照（実装タイプに応じて使い分け）
    private CharacterController characterController;
    private Rigidbody characterRigidbody;
    private Collider characterCollider;
    private Animator animator;

    // 移動関連
    private Vector3 velocity;
    private Vector3 moveDirection;
    private float playerSpeed;
    private bool isCrouched = false;
    public bool IsCrouched => isCrouched;

    // 地面判定（Rigidbody実装用）
    private bool isGroundedRigidbody = false;
    private float groundCheckDistance = 0.1f;

    // MovingPlatform追従用
    private Transform currentPlatform = null;
    private Vector3 previousPlatformPosition = Vector3.zero;

    // ノックバック関連
    private bool isKnockback = false;
    private Vector3 knockbackDirection = Vector3.zero;
    private float knockbackProgress = 0f;
    private Vector3 knockbackStartPosition = Vector3.zero;
    private Vector3 knockbackTargetPosition = Vector3.zero;

    // 入力値（GameInputManagerから設定される）
    private Vector2 movement;
    private bool requestJump = false;
    private bool requestSprint = false;

    void Start()
    {
        if (gameManager == null)
        {
            Debug.LogError($"GameCharacterManager: GameManager参照が設定されていません。修正してください。");
            return;
        }

        if (gameManager.CharacterTracker == null)
        {
            Debug.LogWarning($"GameCharacterManager: CharacterTracker参照が設定されていません。v0.0.2現在ではCharacterTracker未指定時の動作は検証対象外です。");
            return;
        }

        // targetCharacter初期化
        if (targetCharacter != null)
        {
            InitializeCharacter();
        }

        // イベント購読
        var damageSubscription = gameManager.StateManager.State.OnDamageReceived.Subscribe(damageInfo =>
        {
            OnDamageReceived(damageInfo);
        });

        var deadSubscription = gameManager.StateManager.State.IsDead.Subscribe(isDead =>
        {
            if (isDead)
            {
                OnDead();
            }
            else
            {
                OnRespawn();
            }
        });

        var highJumpSubscription = gameManager.StateManager.State.OnHighJump.Subscribe(highJumpInfo =>
        {
            OnHighJump(highJumpInfo);
        });

        // disposable登録
        disposable = Disposable.Combine(
            damageSubscription,
            deadSubscription,
            highJumpSubscription
        );
    }

    void OnDestroy()
    {
        disposable?.Dispose();
    }

    /// <summary>
    /// キャラクターを初期化して実装タイプを判定
    /// </summary>
    void InitializeCharacter()
    {
        if (targetCharacter == null)
        {
            Debug.LogError("GameCharacterManager: targetCharacterが設定されていません。");
            implementationType = CharacterImplementationType.None;
            enabled = false;
            return;
        }

        // Animatorを自動検出
        animator = targetCharacter.GetComponent<Animator>();
        if (animator == null)
        {
            // 子オブジェクトも検索
            animator = targetCharacter.GetComponentInChildren<Animator>();
        }

        if (animator == null)
        {
            Debug.LogWarning($"GameCharacterManager: キャラクター '{targetCharacter.name}' にAnimatorが見つかりませんでした。" +
                "アニメーションは再生されません。");
        }
        else if (EnableVerboseLog)
        {
            Debug.Log($"GameCharacterManager: Animatorを検出しました。({targetCharacter.name})");
        }

        // CharacterControllerの確認
        characterController = targetCharacter.GetComponent<CharacterController>();
        if (characterController != null)
        {
            implementationType = CharacterImplementationType.CharacterController;
            if (EnableVerboseLog)
            {
                Debug.Log($"GameCharacterManager: CharacterController実装を検出しました。({targetCharacter.name})");
            }
            return;
        }

        // Rigidbody + Colliderの確認
        characterRigidbody = targetCharacter.GetComponent<Rigidbody>();
        characterCollider = targetCharacter.GetComponent<Collider>();

        if (characterRigidbody != null && characterCollider != null)
        {
            implementationType = CharacterImplementationType.RigidbodyAndCollider;
            if (EnableVerboseLog)
            {
                string colliderType = characterCollider.GetType().Name;
                Debug.Log($"GameCharacterManager: Rigidbody+{colliderType}実装を検出しました。({targetCharacter.name})");
            }

            // Rigidbodyの設定
            characterRigidbody.freezeRotation = true; // 回転を凍結
            characterRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // 衝突検出精度を向上
            characterRigidbody.interpolation = RigidbodyInterpolation.Interpolate; // 移動を滑らかに
            return;
        }

        // どちらの実装も満たさない場合はエラー
        Debug.LogError($"GameCharacterManager: キャラクター '{targetCharacter.name}' は要件を満たしていません。\n" +
            "要件: CharacterController または (Rigidbody + Collider)");
        implementationType = CharacterImplementationType.None;
        enabled = false;
    }

    void Update()
    {
        if (implementationType == CharacterImplementationType.None || targetCharacter == null)
            return;

        // ノックバック移動処理（入力処理より優先）
        if (isKnockback && enableKnockbackMovement)
        {
            HandleKnockbackMovement();
        }
        else
        {
            HandlePlayerMovement();
        }

        UpdateAnimator();

        // ポストプロセッシングを更新
        gameManager.PostProcessingManager.SetCurrentSpeed(playerSpeed);
    }

    void FixedUpdate()
    {
        // MovingPlatform追従処理（物理演算と同期させるためFixedUpdateで実行）
        if (implementationType != CharacterImplementationType.None && targetCharacter != null)
        {
            HandlePlatformMovement();
        }

        // Rigidbody実装の場合は地面判定を更新
        if (implementationType == CharacterImplementationType.RigidbodyAndCollider)
        {
            CheckGroundRigidbody();
        }
    }

    /// <summary>
    /// プレイヤーの移動を処理
    /// </summary>
    void HandlePlayerMovement()
    {
        // ノックバック中は入力を無効化
        if (isKnockback)
        {
            return;
        }

        // 実装タイプに応じて処理を分岐
        if (implementationType == CharacterImplementationType.CharacterController)
        {
            HandleMovementCharacterController();
        }
        else if (implementationType == CharacterImplementationType.RigidbodyAndCollider)
        {
            HandleMovementRigidbody();
        }
    }

    /// <summary>
    /// CharacterController実装の移動処理
    /// </summary>
    void HandleMovementCharacterController()
    {
        // MovingPlatform追従処理はFixedUpdate()で実行されるため、ここでは呼び出さない

        // ジャンプ処理（移動処理の前に実行）
        if (requestJump && characterController.isGrounded && !isCrouched)
        {
            requestJump = false; // 次のperformedまでtrueにさせない
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // WASD / ゲームパッド左スティック入力を取得
        float horizontal = movement.x;
        float vertical = movement.y;

        Vector3 inputDirection = new Vector3(horizontal, 0f, vertical).normalized;

        if (inputDirection.magnitude >= 0.1f)
        {
            // カメラの向きを基準に移動方向を計算
            Vector3 cameraForward = Vector3.forward;
            Vector3 cameraRight = Vector3.right;

            if (gameManager != null && gameManager.CharacterTracker != null)
            {
                cameraForward = gameManager.CharacterTracker.GetCameraForward();
                cameraRight = gameManager.CharacterTracker.GetCameraRight();
            }

            // カメラの向きを基準に移動方向を計算
            moveDirection = (cameraForward * inputDirection.z + cameraRight * inputDirection.x).normalized;

            // 移動速度を設定（Run/Crouch対応）
            float currentMoveSpeed = walkSpeed;
            if (requestSprint)
            {
                currentMoveSpeed = runSpeed;
            }
            else
            {
                isCrouched = false;
            }

            // キャラクターを移動方向に向ける
            float targetAngle = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);

            // rotationSpeed <= 0の場合は即座に回転（カニ歩き防止、推奨）
            // rotationSpeed > 0の場合はQuaternion.RotateTowardsで徐々に回転
            if (rotationSpeed <= 0f)
            {
                targetCharacter.transform.rotation = targetRotation;
            }
            else
            {
                targetCharacter.transform.rotation = Quaternion.RotateTowards(
                    targetCharacter.transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }

            // 移動を適用
            Vector3 move = moveDirection * currentMoveSpeed * Time.deltaTime;

            // アニメーター用の速度を更新
            float lerpSpeed = Mathf.Max(5f, currentMoveSpeed * 0.8f);
            playerSpeed = Mathf.Lerp(playerSpeed, currentMoveSpeed * inputDirection.magnitude, Time.deltaTime * lerpSpeed);

            // 重力を適用
            if (!characterController.isGrounded && velocity.y < 0)
            {
                velocity.y = -0.5f;
            }
            velocity.y += gravity * Time.deltaTime;
            move.y = velocity.y * Time.deltaTime;

            characterController.Move(move);
        }
        else
        {
            // 停止時の処理
            isCrouched = false;
            playerSpeed = Mathf.Lerp(playerSpeed, 0f, Time.deltaTime * 10f);

            // 重力処理（停止時も含む）
            if (!characterController.isGrounded && velocity.y < 0)
            {
                velocity.y = -0.5f;
            }
            velocity.y += gravity * Time.deltaTime;
            characterController.Move(new Vector3(0, velocity.y * Time.deltaTime, 0));
        }
    }

    /// <summary>
    /// Rigidbody実装の移動処理
    /// </summary>
    void HandleMovementRigidbody()
    {
        // MovingPlatform追従処理はFixedUpdate()で実行されるため、ここでは呼び出さない

        // ジャンプ処理
        if (requestJump && isGroundedRigidbody && !isCrouched)
        {
            requestJump = false; // 次のperformedまでtrueにさせない
            characterRigidbody.AddForce(Vector3.up * Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y), ForceMode.VelocityChange);
        }

        // WASD / ゲームパッド左スティック入力を取得
        float horizontal = movement.x;
        float vertical = movement.y;

        Vector3 inputDirection = new Vector3(horizontal, 0f, vertical).normalized;

        if (inputDirection.magnitude >= 0.1f)
        {
            // カメラの向きを基準に移動方向を計算
            Vector3 cameraForward = Vector3.forward;
            Vector3 cameraRight = Vector3.right;

            if (gameManager != null && gameManager.CharacterTracker != null)
            {
                cameraForward = gameManager.CharacterTracker.GetCameraForward();
                cameraRight = gameManager.CharacterTracker.GetCameraRight();
            }

            // カメラの向きを基準に移動方向を計算
            moveDirection = (cameraForward * inputDirection.z + cameraRight * inputDirection.x).normalized;

            // 移動速度を設定（Run/Crouch対応）
            float currentMoveSpeed = walkSpeed;
            if (requestSprint)
            {
                currentMoveSpeed = runSpeed;
            }
            else
            {
                isCrouched = false;
            }

            // キャラクターを移動方向に向ける
            float targetAngle = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);

            // rotationSpeed <= 0の場合は即座に回転（カニ歩き防止、推奨）
            // rotationSpeed > 0の場合はQuaternion.RotateTowardsで徐々に回転
            if (rotationSpeed <= 0f)
            {
                targetCharacter.transform.rotation = targetRotation;
            }
            else
            {
                targetCharacter.transform.rotation = Quaternion.RotateTowards(
                    targetCharacter.transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }

            // 移動を適用（Rigidbodyの速度を直接設定）
            Vector3 targetVelocity = moveDirection * currentMoveSpeed;
            targetVelocity.y = characterRigidbody.linearVelocity.y; // Y軸速度は維持
            characterRigidbody.linearVelocity = targetVelocity;

            // アニメーター用の速度を更新
            float lerpSpeed = Mathf.Max(5f, currentMoveSpeed * 0.8f);
            playerSpeed = Mathf.Lerp(playerSpeed, currentMoveSpeed * inputDirection.magnitude, Time.deltaTime * lerpSpeed);
        }
        else
        {
            // 停止時の処理
            isCrouched = false;
            playerSpeed = Mathf.Lerp(playerSpeed, 0f, Time.deltaTime * 10f);

            // 水平方向の速度を減衰
            Vector3 horizontalVelocity = characterRigidbody.linearVelocity;
            horizontalVelocity.x = Mathf.Lerp(horizontalVelocity.x, 0f, Time.deltaTime * 10f);
            horizontalVelocity.z = Mathf.Lerp(horizontalVelocity.z, 0f, Time.deltaTime * 10f);
            characterRigidbody.linearVelocity = horizontalVelocity;
        }
    }

    /// <summary>
    /// Rigidbody実装の地面判定
    /// </summary>
    void CheckGroundRigidbody()
    {
        if (characterCollider == null) return;

        // Colliderの種類に応じて地面判定の位置とレイキャスト距離を計算
        Vector3 origin;
        float checkDistance;

        if (characterCollider is CapsuleCollider capsule)
        {
            // CapsuleCollider: カプセルの底から判定
            origin = targetCharacter.transform.position + Vector3.up * (capsule.radius + 0.01f);
            checkDistance = capsule.radius + groundCheckDistance;
        }
        else if (characterCollider is BoxCollider box)
        {
            // BoxCollider: ボックスの底から判定
            Vector3 center = box.center;
            Vector3 size = box.size;
            float halfHeight = size.y / 2f;
            origin = targetCharacter.transform.position + center + Vector3.down * (halfHeight - 0.01f);
            checkDistance = groundCheckDistance;
        }
        else if (characterCollider is SphereCollider sphere)
        {
            // SphereCollider: 球の底から判定
            origin = targetCharacter.transform.position + sphere.center + Vector3.down * (sphere.radius - 0.01f);
            checkDistance = groundCheckDistance;
        }
        else
        {
            // その他のCollider: Boundsを使用
            Bounds bounds = characterCollider.bounds;
            origin = new Vector3(bounds.center.x, bounds.min.y + 0.01f, bounds.center.z);
            checkDistance = groundCheckDistance;
        }

        // Raycastで地面判定とMovingPlatform検出
        RaycastHit hit;
        isGroundedRigidbody = Physics.Raycast(origin, Vector3.down, out hit, checkDistance);

        // MovingPlatformタグの検出
        if (isGroundedRigidbody && hit.collider != null)
        {
            if (hit.collider.CompareTag("MovingPlatform"))
            {
                // 新しいプラットフォームに乗った
                if (currentPlatform != hit.collider.transform)
                {
                    currentPlatform = hit.collider.transform;
                    previousPlatformPosition = currentPlatform.position;
                }
            }
            else
            {
                // 通常の地面に着地
                currentPlatform = null;
            }
        }
        else
        {
            // 空中にいる
            currentPlatform = null;
        }
    }

    /// <summary>
    /// アニメーターを更新
    /// </summary>
    void UpdateAnimator()
    {
        if (animator != null && animator.runtimeAnimatorController != null && animator.isInitialized)
        {
            animator.SetFloat(animatorSpeedKey, playerSpeed);
            animator.SetBool(animatorInAirKey, !IsGrounded());
            animator.SetBool(animatorIsCrouchedKey, isCrouched);
        }
    }

    /// <summary>
    /// 地面に接地しているか
    /// </summary>
    private bool IsGrounded()
    {
        return implementationType switch
        {
            CharacterImplementationType.CharacterController => characterController?.isGrounded ?? false,
            CharacterImplementationType.RigidbodyAndCollider => isGroundedRigidbody,
            _ => false
        };
    }

    /// <summary>
    /// 制御対象のキャラクターを設定
    /// </summary>
    public void SetTargetCharacter(GameObject character)
    {
        targetCharacter = character;

        if (targetCharacter != null)
        {
            // キャラクターを初期化（Animator、移動コンポーネントなどを自動検出）
            InitializeCharacter();

            // カメラトラッカーが既に設定されている場合、ターゲットを更新
            if (gameManager != null && gameManager.CharacterTracker != null)
            {
                gameManager.CharacterTracker.SetTarget(targetCharacter.transform);
            }

            // コンポーネントを有効化
            enabled = true;
        }
        else
        {
            Debug.LogWarning("GameCharacterManager: targetCharacterがnullです。");
            enabled = false;
        }
    }

    /// <summary>
    /// GameInputManagerから入力値を設定
    /// </summary>
    public void SetMovementInput(Vector2 movementInput)
    {
        movement = movementInput;
    }

    /// <summary>
    /// GameInputManagerからジャンプリクエストを設定
    /// </summary>
    public void SetJumpRequest(bool jumpRequest)
    {
        requestJump = jumpRequest;
    }

    /// <summary>
    /// GameInputManagerからスプリントリクエストを設定
    /// </summary>
    public void SetSprintRequest(bool sprintRequest)
    {
        requestSprint = sprintRequest;
    }

    /// <summary>
    /// 死亡時の処理
    /// </summary>
    void OnDead()
    {
        if (EnableVerboseLog)
        {
            Debug.Log($"GameCharacterManager: 死亡しました。アニメーション遷移");
        }

        // 死亡アニメーション遷移
        if (animator != null && animator.runtimeAnimatorController != null && animator.isInitialized)
        {
            animator.SetBool(animatorInAirKey, false);
            animator.SetBool(animatorIsKnockbackKey, false);
            animator.SetBool(animatorIsDeadKey, true);
        }

        // 入力を無効化
        enabled = false;
    }

    /// <summary>
    /// リスポーン時の処理
    /// </summary>
    void OnRespawn()
    {
        if (EnableVerboseLog)
        {
            Debug.Log($"GameCharacterManager: リスポーンしました。アニメーション復帰");
        }

        // 死亡アニメーションを解除
        if (animator != null && animator.runtimeAnimatorController != null && animator.isInitialized)
        {
            animator.SetBool(animatorIsDeadKey, false);
        }

        // 入力を再有効化
        enabled = true;
    }

    /// <summary>
    /// ハイジャンプ時の処理
    /// </summary>
    void OnHighJump(HighJumpInfo highJumpInfo)
    {
        if (EnableVerboseLog)
        {
            Debug.Log($"GameCharacterManager: ハイジャンプを発動しました。高さ: {highJumpInfo.JumpHeight}m, 速度: {highJumpInfo.JumpSpeed}");
        }

        // CharacterController実装の場合
        if (implementationType == CharacterImplementationType.CharacterController && characterController != null)
        {
            // 垂直方向の速度を設定
            velocity.y = highJumpInfo.JumpSpeed;
        }
        // Rigidbody実装の場合
        else if (implementationType == CharacterImplementationType.RigidbodyAndCollider && characterRigidbody != null)
        {
            // 垂直方向の速度をリセットしてからジャンプ力を加える
            Vector3 currentVelocity = characterRigidbody.linearVelocity;
            currentVelocity.y = highJumpInfo.JumpSpeed;
            characterRigidbody.linearVelocity = currentVelocity;
        }
    }

    /// <summary>
    /// ダメージを受けたときのノックバック処理
    /// </summary>
    void OnDamageReceived(DamageInfo damageInfo)
    {
        if (gameManager.StateManager.State.IsDead.CurrentValue) return;

        if (EnableVerboseLog)
        {
            Debug.Log($"GameCharacterManager: ダメージを受けました。ノックバックアニメーション開始");
        }

        // ノックバックアニメーション遷移
        if (animator != null && animator.runtimeAnimatorController != null && animator.isInitialized)
        {
            animator.SetBool(animatorIsKnockbackKey, true);
            isKnockback = true;

            // ノックバック移動が有効な場合、方向を計算
            if (enableKnockbackMovement && damageInfo.Source != null && targetCharacter != null)
            {
                // ダメージソースからキャラクターへの方向を計算
                Vector3 damageSourcePosition = damageInfo.Source.transform.position;
                Vector3 characterPosition = targetCharacter.transform.position;

                // Y軸を無視した水平方向のみで計算
                damageSourcePosition.y = characterPosition.y;

                // ダメージソースからキャラクターへの方向（ノックバック方向）
                knockbackDirection = (characterPosition - damageSourcePosition).normalized;

                // ノックバック開始位置と目標位置を設定
                knockbackStartPosition = targetCharacter.transform.position;
                knockbackTargetPosition = knockbackStartPosition + knockbackDirection * knockbackDistance;
                knockbackProgress = 0f;

                if (EnableVerboseLog)
                {
                    Debug.Log($"GameCharacterManager: ノックバック方向: {knockbackDirection}, 距離: {knockbackDistance}");
                }
            }

            // ノックバックアニメーション終了後に状態をリセット
            StartCoroutine(ResetKnockbackAfterDelay(0.5f));
        }
        else
        {
            Debug.Log($"knock-back再生不能: animator is null? {animator == null} : runtime animator is null? {animator.runtimeAnimatorController == null} : isInitialized? {animator?.isInitialized}");
        }
    }

    /// <summary>
    /// ノックバック移動処理
    /// CharacterControllerとRigidbody両方に対応
    /// </summary>
    void HandleKnockbackMovement()
    {
        // 進行度を更新（0.0 → 1.0）
        knockbackProgress += Time.deltaTime * knockbackSpeed;

        if (knockbackProgress >= 1f)
        {
            // ノックバック移動完了
            knockbackProgress = 1f;
        }

        // Lerpで滑らかに移動
        Vector3 currentTargetPosition = Vector3.Lerp(knockbackStartPosition, knockbackTargetPosition, knockbackProgress);

        // 実装タイプに応じて移動処理を分岐
        if (implementationType == CharacterImplementationType.CharacterController && characterController != null)
        {
            // CharacterController: Move()で移動
            Vector3 movement = currentTargetPosition - targetCharacter.transform.position;
            characterController.Move(movement);
        }
        else if (implementationType == CharacterImplementationType.RigidbodyAndCollider && characterRigidbody != null)
        {
            // Rigidbody: MovePosition()で移動
            characterRigidbody.MovePosition(currentTargetPosition);
        }
    }

    /// <summary>
    /// ノックバック状態を一定時間後にリセット
    /// </summary>
    System.Collections.IEnumerator ResetKnockbackAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (animator != null && animator.runtimeAnimatorController != null && animator.isInitialized)
        {
            animator.SetBool(animatorIsKnockbackKey, false);
        }

        isKnockback = false;

        if (EnableVerboseLog)
        {
            Debug.Log($"GameCharacterManager: ノックバック終了");
        }
    }

    /// <summary>
    /// CharacterController実装でのコライダー接触検出
    /// MovingPlatformタグを検出
    /// </summary>
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (implementationType != CharacterImplementationType.CharacterController)
            return;

        // 下方向の接触（地面に着地）かどうかを判定
        if (hit.normal.y > 0.5f && characterController.isGrounded)
        {
            // MovingPlatformタグの検出
            if (hit.collider.CompareTag("MovingPlatform"))
            {
                // 新しいプラットフォームに乗った
                if (currentPlatform != hit.collider.transform)
                {
                    currentPlatform = hit.collider.transform;
                    previousPlatformPosition = currentPlatform.position;
                }
            }
            else
            {
                // 通常の地面に着地
                currentPlatform = null;
            }
        }
    }

    /// <summary>
    /// MovingPlatform追従処理
    /// プラットフォームの移動量をキャラクターに適用
    /// </summary>
    void HandlePlatformMovement()
    {
        // プラットフォームに乗っていない場合は何もしない
        if (currentPlatform == null || !IsGrounded())
        {
            currentPlatform = null;
            return;
        }

        // プラットフォームの移動量を計算
        Vector3 platformMovement = currentPlatform.position - previousPlatformPosition;

        // キャラクターをプラットフォームの移動量分移動させる
        if (implementationType == CharacterImplementationType.CharacterController && characterController != null)
        {
            characterController.Move(platformMovement);
        }
        else if (implementationType == CharacterImplementationType.RigidbodyAndCollider && characterRigidbody != null)
        {
            // Rigidbodyの場合は位置を直接加算
            targetCharacter.transform.position += platformMovement;
        }

        // 次フレーム用にプラットフォームの位置を記録
        previousPlatformPosition = currentPlatform.position;
    }

    /// <summary>
    /// Rigidbodyの物理状態をリセット（速度、角速度をゼロにする）
    /// リスポーンやワープ時に使用
    /// </summary>
    public void ResetPhysicsState()
    {
        if (implementationType == CharacterImplementationType.RigidbodyAndCollider && characterRigidbody != null)
        {
            // 速度をゼロにする
            characterRigidbody.linearVelocity = Vector3.zero;
            characterRigidbody.angularVelocity = Vector3.zero;

            // 物理エンジンの状態を即座に同期（Interpolation対策）
            Physics.SyncTransforms();

            if (EnableVerboseLog)
            {
                Debug.Log("GameCharacterManager: Rigidbodyの物理状態をリセットしました。");
            }
        }
    }

    /// <summary>
    /// キャラクターを指定位置にテレポート
    /// CharacterControllerとRigidbody両方に対応し、物理状態も適切にリセット
    /// </summary>
    /// <param name="position">テレポート先の位置</param>
    /// <param name="rotation">テレポート先の回転</param>
    public void TeleportTo(Vector3 position, Quaternion rotation)
    {
        if (targetCharacter == null)
        {
            Debug.LogWarning("GameCharacterManager: targetCharacterがnullです。テレポートできません。");
            return;
        }

        if (implementationType == CharacterImplementationType.CharacterController && characterController != null)
        {
            // CharacterControllerの場合は無効化してから移動
            characterController.enabled = false;
            targetCharacter.transform.position = position;
            targetCharacter.transform.rotation = rotation;
            characterController.enabled = true;

            if (EnableVerboseLog)
            {
                Debug.Log($"GameCharacterManager: CharacterControllerをテレポートしました。位置: {position}");
            }
        }
        else if (implementationType == CharacterImplementationType.RigidbodyAndCollider && characterRigidbody != null)
        {
            // Rigidbodyの位置を直接設定（transform.positionではなくRigidbody.positionを使用）
            characterRigidbody.position = position;
            characterRigidbody.rotation = rotation;

            // 物理状態をリセット
            ResetPhysicsState();

            if (EnableVerboseLog)
            {
                Debug.Log($"GameCharacterManager: Rigidbodyをテレポートしました。位置: {position}");
            }
        }
        else
        {
            Debug.LogWarning("GameCharacterManager: 実装タイプが不明、またはコンポーネントがnullです。");
        }
    }
}
