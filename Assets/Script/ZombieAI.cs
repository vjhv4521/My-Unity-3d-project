using System;
using UnityEngine;
using UnityEngine.AI;

namespace ZombiePrototype
{
    /// <summary>
    /// 僵尸 AI。
    ///
    /// 这个脚本负责三件事：
    /// 1. 用状态机决定僵尸当前行为：待机、追击、慢速靠近、攻击、死亡。
    /// 2. 用 NavMeshAgent 控制僵尸移动。
    /// 3. 用 Animator 参数控制动画。
    ///
    /// Animator Controller 里需要有这三个参数：
    /// Speed  - Float
    /// Attack - Trigger
    /// Dead   - Bool
    /// </summary>
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class ZombieAI : MonoBehaviour
    {
        public enum ZombieState
        {
            Idle,
            Chase,
            SlowApproach,
            Attack,
            Dead
        }

        [Header("Perception")]

        // 玩家进入这个距离后，僵尸从 Idle 进入 Chase。
        [SerializeField] private float detectionRange = 28f;

        // 僵尸已经发现玩家后，超过这个距离才丢失目标。
        // 这个值通常要比 detectionRange 大一点，避免状态来回抖动。
        [SerializeField] private float loseTargetRange = 36f;

        [Header("Movement")]

        // Chase 状态的移动速度。
        // 这个值也会写入 Animator 的 Speed 参数，所以 Run 阈值应低于它。
        [SerializeField] private float chaseSpeed = 5.2f;

        // SlowApproach 状态的移动速度。
        // 这个值也会写入 Animator 的 Speed 参数，所以 Walk 阈值应低于它，Run 阈值应高于它。
        [SerializeField] private float slowApproachSpeed = 1.35f;

        // 小于这个距离后，僵尸从快速追击切换成慢速靠近。
        [SerializeField] private float slowApproachRange = 5f;

        // 攻击状态下手动转向玩家的速度。
        [SerializeField] private float turnSpeed = 10f;

        [Header("Attack")]

        // 小于这个距离后进入攻击状态。
        [SerializeField] private float attackRange = 1.55f;

        // 每隔多少秒攻击一次。
        [SerializeField] private float attackInterval = 1.25f;

        // 每次攻击造成多少伤害。
        [SerializeField] private float attackDamage = 12f;

        // 攻击动画开始后，延迟多少秒才真正造成伤害。
        // 例如攻击动画前半段是抬手，后半段才打到玩家，就把这个值调到 0.45 到 0.7。
        [SerializeField] private float attackDamageDelay = 0.55f;

        // 攻击命中时允许的额外距离。
        // 这样可以避免玩家刚好在边界上时，因为浮点数误差导致明明被打到却没有伤害。
        [SerializeField] private float attackHitPadding = 0.25f;

        [Header("Pooling")]

        // 死亡后停留多久，再回收到对象池。
        [SerializeField] private float returnToPoolDelay = 5f;

        [Header("Visual")]

        // 僵尸模型根节点。
        // 刷怪器实例化模型后会调用 SetVisualRoot 自动赋值。
        [SerializeField] private Transform visualRoot;

        [Header("Animation")]

        // 僵尸模型上的 Animator。
        // 如果没手动拖，代码会从 visualRoot 的子物体里自动查找。
        [SerializeField] private Animator animator;

        // Animator Controller 里的待机状态名。
        // 如果你的 Animator 状态不叫 Idle，就把这里改成你的真实状态名。
        [SerializeField] private string idleStateName = "Idle";

        // Animator Controller 里的走路状态名。
        // SlowApproach 状态会直接切到这个动画。
        [SerializeField] private string walkStateName = "Walk";

        // Animator Controller 里的奔跑状态名。
        // Chase 状态会直接切到这个动画。
        [SerializeField] private string runStateName = "Run";

        // Animator Controller 里的攻击状态名。
        // 以前只依赖 Attack Trigger，现在也会直接 CrossFade，避免过渡线配置问题。
        [SerializeField] private string attackStateName = "Attack";

        // 死亡动画状态名。
        // 如果你的 Animator 里的死亡状态不叫 Death，就在 Inspector 里改成你的状态名。
        [SerializeField] private string deathStateName = "Death";

        // 代码主动切换动画状态时的过渡时间。
        // 0.05 到 0.12 通常比较合适。
        [SerializeField] private float animationFadeTime = 0.08f;

        // 死亡动画的切换时间。
        // 死亡不需要太平滑，时间短一点可以更明显地立刻进入死亡动作。
        [SerializeField] private float deathFadeTime = 0.03f;

        // 如果没有 Animator，就使用一个代码倒地假动作作为兜底。
        // 你现在已经有动画，建议保持 true，方便发现 Animator 没挂好。
        [SerializeField] private bool warnWhenAnimatorMissing = true;

        [Header("Grounding")]

        // 是否自动把模型视觉部分贴到地面。
        //
        // 有些 Mixamo 动画的 Root / Hips 高度和 Synty 模型不完全匹配，
        // 会导致 Idle / Run 时模型半截插进地面，但 Attack 又正常。
        // 这个开关会根据 Renderer.bounds.min.y 自动修正 visualRoot 的 Y 位置。
        [SerializeField] private bool alignVisualToGround = true;

        // 视觉模型离地面的微小偏移。
        // 0.02 表示模型最低点比地面略高一点，避免和地面闪烁穿插。
        [SerializeField] private float visualGroundOffset = 0.02f;

        // 每帧最多修正多少高度。
        // 限制最大值可以避免某些动画包围盒异常时，模型突然飞很高。
        [SerializeField] private float maxGroundCorrectionPerFrame = 0.25f;

        // 手动给模型整体增加的高度偏移。
        // 如果某个素材的 Idle / Run 动画根节点本来就偏低，自动贴地还不够时，就用这个值把模型抬高。
        [SerializeField] private float manualVisualYOffset;

        public event Action<ZombieAI> DiedForPool;

        public ZombieState State { get; private set; } = ZombieState.Idle;
        public float ReturnToPoolDelay => returnToPoolDelay;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int DeadHash = Animator.StringToHash("Dead");

        private Transform target;
        private Health targetHealth;
        private Health health;
        private NavMeshAgent agent;
        private Collider[] cachedColliders;
        private Vector3 visualStartLocalPosition;
        private Quaternion visualStartLocalRotation;
        private float nextAttackTime;
        private float pendingAttackDamageTime;
        private bool hasPendingAttackDamage;
        private bool deathHandled;
        private bool warnedMissingAnimator;
        private Renderer[] visualRenderers;
        private int currentAnimatorStateHash;

        private void Awake()
        {
            health = GetComponent<Health>();
            agent = GetComponent<NavMeshAgent>();
            cachedColliders = GetComponentsInChildren<Collider>(true);

            ConfigureAgent();

            health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Died -= OnDied;
            }
        }

        private void Update()
        {
            if (State == ZombieState.Dead)
            {
                UpdateAnimatorSpeed(0f);
                return;
            }

            if (target == null || targetHealth == null || targetHealth.IsDead)
            {
                StopMoving();
                UpdateAnimatorSpeed(0f);
                return;
            }

            float distance = Vector3.Distance(transform.position, target.position);
            SetState(ChooseState(distance));
            TickState(distance);
        }

        private void LateUpdate()
        {
            // 动画是在 Update 之后采样的。
            // 所以贴地修正放在 LateUpdate，能拿到这一帧动画更新后的 Renderer.bounds。
            if (State != ZombieState.Dead)
            {
                AlignVisualRootToGround(false);
            }
        }

        /// <summary>
        /// 初始化僵尸目标。
        /// 第一次创建和每次从对象池重新启用都会调用。
        /// </summary>
        public void Initialize(Transform playerTarget)
        {
            target = playerTarget;
            targetHealth = playerTarget.GetComponent<Health>();
        }

        /// <summary>
        /// 设置模型根节点。
        /// </summary>
        public void SetVisualRoot(Transform root)
        {
            visualRoot = root;

            if (visualRoot != null)
            {
                visualStartLocalPosition = visualRoot.localPosition;
                visualStartLocalRotation = visualRoot.localRotation;
                visualRenderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            }

            RefreshAnimatorReference();
        }

        /// <summary>
        /// 从对象池取出僵尸时调用。
        /// 这里会把上一次死亡留下的状态全部重置。
        /// </summary>
        public void PrepareForSpawn(Vector3 position, Quaternion rotation, Transform playerTarget, float maxHealth)
        {
            gameObject.SetActive(true);

            deathHandled = false;
            warnedMissingAnimator = false;
            nextAttackTime = 0f;
            pendingAttackDamageTime = 0f;
            hasPendingAttackDamage = false;
            currentAnimatorStateHash = 0;
            State = ZombieState.Idle;

            transform.SetPositionAndRotation(position, rotation);
            Initialize(playerTarget);

            if (visualRoot != null)
            {
                visualRoot.localPosition = visualStartLocalPosition;
                visualRoot.localRotation = visualStartLocalRotation;
            }

            RefreshAnimatorReference();
            ResetAnimatorForSpawn();
            PlayAnimatorState(idleStateName, 0f);
            SetCollidersEnabled(true);
            AlignVisualRootToGround(true);

            if (agent != null)
            {
                agent.enabled = true;
                ConfigureAgent();

                // Warp 会把 NavMeshAgent 放到指定 NavMesh 位置。
                // 这里不要依赖 transform.position，否则 Agent 可能还停留在池里旧位置。
                agent.Warp(position);

                if (agent.isOnNavMesh)
                {
                    agent.ResetPath();
                    agent.isStopped = true;
                }
            }

            AlignVisualRootToGround(true);

            health.ResetHealth(maxHealth);
            enabled = true;
        }

        /// <summary>
        /// 由 ZombieSpawner 调用，用来把刷怪器 Inspector 里的贴地参数传给运行时生成的僵尸。
        ///
        /// 这样做的原因：
        /// ZombieAI 是代码运行时 AddComponent 出来的，平时在场景 Inspector 里看不到。
        /// 把参数放到 ZombieSpawner 上之后，你选中刷怪器就能直接调模型高度。
        /// </summary>
        public void ConfigureVisualGrounding(bool alignToGround, float groundOffset, float maxCorrection, float manualYOffset)
        {
            alignVisualToGround = alignToGround;
            visualGroundOffset = groundOffset;
            maxGroundCorrectionPerFrame = maxCorrection;
            manualVisualYOffset = manualYOffset;
        }

        /// <summary>
        /// 回收到对象池时调用。
        /// </summary>
        public void DeactivateForPool()
        {
            StopMoving();
            gameObject.SetActive(false);
        }

        private void ConfigureAgent()
        {
            if (agent == null)
            {
                return;
            }

            agent.speed = chaseSpeed;
            agent.angularSpeed = 720f;
            agent.acceleration = 18f;
            agent.stoppingDistance = attackRange;
            agent.autoBraking = true;
        }

        /// <summary>
        /// 查找 Animator 并做必要设置。
        /// </summary>
        private void RefreshAnimatorReference()
        {
            // 对象池创建顺序是：
            // 1. root.AddComponent<ZombieAI>()，这会先执行 Awake。
            // 2. Instantiate 僵尸模型。
            // 3. ai.SetVisualRoot(visual.transform)。
            //
            // 所以 Awake 阶段 visualRoot 还没有值。
            // 这种情况不是错误，直接返回，不要报警。
            if (visualRoot == null)
            {
                return;
            }

            if (animator == null && visualRoot != null)
            {
                animator = visualRoot.GetComponentInChildren<Animator>(true);
            }

            if (animator == null)
            {
                if (warnWhenAnimatorMissing && !warnedMissingAnimator)
                {
                    warnedMissingAnimator = true;
                    Debug.LogWarning("ZombieAI cannot find Animator under visualRoot. Death will use fallback pose.", this);
                }

                return;
            }

            // 必须关闭 Root Motion。
            // 现在僵尸的位置由 NavMeshAgent 控制，不应该让动画自己移动角色。
            // 如果开着 Root Motion，很容易出现模型下沉、滑动或和 NavMeshAgent 打架。
            animator.applyRootMotion = false;
        }

        private ZombieState ChooseState(float distance)
        {
            if (distance <= attackRange)
            {
                return ZombieState.Attack;
            }

            if (distance <= slowApproachRange)
            {
                return ZombieState.SlowApproach;
            }

            if (State != ZombieState.Idle && distance <= loseTargetRange)
            {
                return ZombieState.Chase;
            }

            if (distance <= detectionRange)
            {
                return ZombieState.Chase;
            }

            return ZombieState.Idle;
        }

        private void SetState(ZombieState nextState)
        {
            if (State == nextState)
            {
                return;
            }

            State = nextState;
            PlayAnimationForState(State);

            // 如果僵尸离开攻击状态，说明这一下攻击被打断或目标离开了。
            // 这时取消等待中的伤害，避免玩家跑开后下一次靠近又被旧攻击补伤害。
            if (State != ZombieState.Attack)
            {
                hasPendingAttackDamage = false;
            }
        }

        private void TickState(float distance)
        {
            switch (State)
            {
                case ZombieState.Idle:
                    StopMoving();
                    UpdateAnimatorSpeed(0f);
                    break;

                case ZombieState.Chase:
                    MoveToTarget(chaseSpeed);
                    UpdateAnimatorSpeed(chaseSpeed);
                    break;

                case ZombieState.SlowApproach:
                    MoveToTarget(slowApproachSpeed);
                    UpdateAnimatorSpeed(slowApproachSpeed);
                    break;

                case ZombieState.Attack:
                    StopMoving();
                    FaceTarget();
                    TryAttack(distance);
                    UpdateAnimatorSpeed(0f);
                    break;
            }
        }

        private void MoveToTarget(float speed)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                return;
            }

            agent.isStopped = false;
            agent.speed = speed;
            agent.stoppingDistance = attackRange;
            agent.SetDestination(target.position);
        }

        private void StopMoving()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
        }

        private void FaceTarget()
        {
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        private void TryAttack(float distance)
        {
            // 如果当前没有等待结算的攻击，并且攻击冷却已经结束，就开始一次新的挥击。
            // 注意：这里“开始攻击”只播放动画，不立刻扣血。
            if (!hasPendingAttackDamage && Time.time >= nextAttackTime && distance <= attackRange)
            {
                StartAttackSwing();
            }

            // 攻击动画还没播放到命中帧时，不造成伤害。
            if (!hasPendingAttackDamage || Time.time < pendingAttackDamageTime)
            {
                return;
            }

            hasPendingAttackDamage = false;

            // 到命中帧时再检查一次距离。
            // 如果玩家已经后退离开攻击范围，就说明这一下挥空，不扣血。
            if (distance <= attackRange + attackHitPadding)
            {
                targetHealth.TakeDamage(attackDamage);
            }
        }

        /// <summary>
        /// 开始一次攻击挥击。
        ///
        /// 这个函数只负责：
        /// 1. 重新播放攻击动画。
        /// 2. 记录真正造成伤害的时间点。
        /// 3. 设置下一次攻击最早什么时候可以开始。
        ///
        /// 真正扣血发生在 TryAttack 里，等 Time.time 到达 pendingAttackDamageTime 之后才执行。
        /// </summary>
        private void StartAttackSwing()
        {
            hasPendingAttackDamage = true;
            pendingAttackDamageTime = Time.time + attackDamageDelay;
            nextAttackTime = Time.time + attackInterval;

            if (animator == null)
            {
                return;
            }

            animator.ResetTrigger(AttackHash);
            animator.SetTrigger(AttackHash);

            // 攻击状态需要允许重复从第 0 帧播放。
            // 如果不清掉 currentAnimatorStateHash，PlayAnimatorState 会认为还在 Attack 状态，从而跳过播放。
            currentAnimatorStateHash = 0;
            PlayAnimatorState(attackStateName, 0.03f);
        }

        private void OnDied(Health deadHealth)
        {
            if (deathHandled)
            {
                return;
            }

            deathHandled = true;
            State = ZombieState.Dead;
            currentAnimatorStateHash = 0;

            GameManager.Instance?.RegisterZombieKill();

            StopMoving();

            if (agent != null)
            {
                agent.enabled = false;
            }

            PlayDeathAnimationOrFallbackPose();
            SetCollidersEnabled(false);

            DiedForPool?.Invoke(this);
        }

        /// <summary>
        /// 根据 ZombieAI 的状态，直接切换 Animator 里的动画状态。
        ///
        /// 这样做是为了降低 Animator 过渡线配置错误带来的影响：
        /// 只要 Animator Controller 里存在 Idle / Walk / Run / Attack / Death 这些状态，
        /// 代码就可以直接播放对应动画，不再完全依赖 Speed 条件过渡。
        /// </summary>
        private void PlayAnimationForState(ZombieState state)
        {
            switch (state)
            {
                case ZombieState.Idle:
                    PlayAnimatorState(idleStateName, animationFadeTime);
                    break;

                case ZombieState.Chase:
                    PlayAnimatorState(runStateName, animationFadeTime);
                    break;

                case ZombieState.SlowApproach:
                    PlayAnimatorState(walkStateName, animationFadeTime);
                    break;

                case ZombieState.Attack:
                    PlayAnimatorState(attackStateName, animationFadeTime);
                    break;

                case ZombieState.Dead:
                    PlayAnimatorState(deathStateName, animationFadeTime);
                    break;
            }
        }

        /// <summary>
        /// 安全播放 Animator 里的某个状态。
        ///
        /// layer 0 是默认 Base Layer。
        /// 如果状态名写错，HasState 会返回 false，代码不会报错，只会跳过播放。
        /// </summary>
        private void PlayAnimatorState(string stateName, float fadeTime)
        {
            if (animator == null || string.IsNullOrEmpty(stateName))
            {
                return;
            }

            int stateHash = Animator.StringToHash(stateName);
            if (!animator.HasState(0, stateHash))
            {
                Debug.LogWarning($"ZombieAI cannot find Animator state '{stateName}' on layer 0.", this);
                return;
            }

            // 避免每一帧重复 CrossFade 同一个状态。
            // 重复 CrossFade 会让动画一直从开头重播，看起来就像动画没有正常走。
            if (currentAnimatorStateHash == stateHash)
            {
                return;
            }

            currentAnimatorStateHash = stateHash;
            animator.CrossFade(stateHash, fadeTime, 0);
        }

        /// <summary>
        /// 设置 Animator 的 Speed 参数。
        ///
        /// 这里不再用 agent.velocity.magnitude。
        /// 原因是 Agent 刚开始寻路时 velocity 可能很小，导致 Run 条件触发不了。
        ///
        /// 现在按状态直接写入：
        /// Chase        -> chaseSpeed，例如 3.6
        /// SlowApproach -> slowApproachSpeed，例如 1.35
        /// Idle/Attack  -> 0
        /// </summary>
        private void UpdateAnimatorSpeed(float speed)
        {
            if (animator == null)
            {
                return;
            }

            animator.SetFloat(SpeedHash, speed);
        }

        /// <summary>
        /// 对象池复用时重置 Animator。
        /// </summary>
        private void ResetAnimatorForSpawn()
        {
            if (animator == null)
            {
                return;
            }

            animator.ResetTrigger(AttackHash);
            animator.SetBool(DeadHash, false);
            animator.SetFloat(SpeedHash, 0f);
            animator.Rebind();
            animator.Update(0f);
        }

        private void SetCollidersEnabled(bool enabledValue)
        {
            cachedColliders = GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in cachedColliders)
            {
                collider.enabled = enabledValue;
            }
        }

        private void PlayDeathAnimationOrFallbackPose()
        {
            RefreshAnimatorReference();

            if (animator != null)
            {
                // 确保对象池复用、攻击动画或旧状态不会把死亡动画卡住。
                animator.enabled = true;
                animator.speed = 1f;
                animator.SetFloat(SpeedHash, 0f);
                animator.ResetTrigger(AttackHash);
                animator.SetBool(DeadHash, true);

                int deathStateHash = Animator.StringToHash(deathStateName);
                if (!animator.HasState(0, deathStateHash))
                {
                    Debug.LogWarning($"ZombieAI cannot find Animator death state '{deathStateName}' on layer 0.", this);
                    return;
                }

                // 死亡动画不需要过渡混合，直接从第 0 帧播放最可靠。
                // 这样可以避开 Any State 过渡线、Attack 触发器和 CrossFade 混合造成的干扰。
                currentAnimatorStateHash = deathStateHash;
                animator.Play(deathStateHash, 0, 0f);

                // 立刻更新一帧，让死亡姿势在当前帧就生效。
                // 否则对象池、禁用 NavMeshAgent 或当前攻击过渡可能让你看起来像没触发。
                animator.Update(0f);

                return;
            }

            // 只有完全没有 Animator 时，才使用代码倒地兜底。
            // 如果你看到半截进地，通常说明 Animator 没找到，或者死亡动画本身的 Root Transform Y 没处理好。
            if (visualRoot == null)
            {
                return;
            }

            visualRoot.localRotation = Quaternion.Euler(84f, 0f, 0f);
        }

        /// <summary>
        /// 把僵尸“看起来的模型”自动对齐到地面。
        ///
        /// 重要区别：
        /// - root GameObject 的位置由 NavMeshAgent 控制，不能随便改。
        /// - visualRoot 只是模型子物体，可以上下微调。
        ///
        /// 这里用所有 Renderer 的 bounds 算出模型最低点。
        /// 如果最低点低于地面，就把 visualRoot 往上抬。
        /// 如果最低点高于地面太多，就把 visualRoot 往下放。
        /// </summary>
        private void AlignVisualRootToGround(bool forceFullCorrection)
        {
            if (!alignVisualToGround || visualRoot == null)
            {
                return;
            }

            if (visualRenderers == null || visualRenderers.Length == 0)
            {
                visualRenderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            }

            bool foundBottom = TryGetFootBottomY(out float minY);

            // 如果 Animator 是 Humanoid，优先用左右脚骨骼判断贴地高度。
            // 有些 SkinnedMeshRenderer 的 bounds 不会精确跟着当前动画姿势更新，
            // 这就是之前“看起来半截入地，但代码没修正明显”的常见原因。
            if (!foundBottom)
            {
                foreach (Renderer renderer in visualRenderers)
                {
                    if (renderer == null || !renderer.enabled)
                    {
                        continue;
                    }

                    foundBottom = true;
                    minY = Mathf.Min(minY, renderer.bounds.min.y);
                }
            }

            if (!foundBottom)
            {
                return;
            }

            float targetMinY = transform.position.y + visualGroundOffset;
            float correction = targetMinY - minY + manualVisualYOffset;

            // 出生那一刻要一次性贴地，否则玩家会先看到僵尸从地下慢慢升上来。
            // 普通帧再用最大修正值限制，避免动画异常时视觉模型突然大幅跳动。
            if (!forceFullCorrection)
            {
                correction = Mathf.Clamp(correction, -maxGroundCorrectionPerFrame, maxGroundCorrectionPerFrame);
            }

            if (Mathf.Abs(correction) <= 0.001f)
            {
                return;
            }

            visualRoot.localPosition += new Vector3(0f, correction, 0f);
        }

        /// <summary>
        /// 尝试用左右脚骨骼计算当前动画姿势下的最低高度。
        ///
        /// 这个方法只在 Humanoid 动画有效。
        /// 如果素材不是 Humanoid，方法会返回 false，后面再退回到 Renderer.bounds 的方案。
        /// </summary>
        private bool TryGetFootBottomY(out float bottomY)
        {
            bottomY = float.PositiveInfinity;

            if (animator == null || !animator.isHuman)
            {
                return false;
            }

            bool foundFoot = false;
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);

            if (leftFoot != null)
            {
                foundFoot = true;
                bottomY = Mathf.Min(bottomY, leftFoot.position.y);
            }

            if (rightFoot != null)
            {
                foundFoot = true;
                bottomY = Mathf.Min(bottomY, rightFoot.position.y);
            }

            return foundFoot;
        }
    }
}
