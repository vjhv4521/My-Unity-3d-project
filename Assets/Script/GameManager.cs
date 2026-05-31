using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ZombiePrototype
{
    /// <summary>
    /// 游戏总管理器。
    ///
    /// 这个脚本只负责“整局游戏”的规则，不负责玩家移动、射击、僵尸 AI。
    ///
    /// 它做的事情：
    /// 1. 保存当前击杀数。
    /// 2. 保存当前生成过多少只僵尸。
    /// 3. 判断玩家是否死亡。
    /// 4. 判断是否达成胜利。
    /// 5. 游戏结束后允许按 R 重新开始。
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        [Header("References")]

        // 玩家脚本引用。
        // 场景构建器会自动赋值；如果丢失，运行时也会自动查找。
        [SerializeField] private FirstPersonPlayer player;

        [Header("Win Condition")]

        // 击杀多少只僵尸后胜利。
        [SerializeField] private int targetKills = 20;

        /// <summary>
        /// 当前场景里的 GameManager 单例。
        ///
        /// 其他脚本可以这样调用：
        /// GameManager.Instance.RegisterZombieKill();
        /// </summary>
        public static GameManager Instance { get; private set; }

        public FirstPersonPlayer Player => player;
        public int Kills { get; private set; }
        public int Spawned { get; private set; }
        public int TargetKills => targetKills;
        public bool GameEnded { get; private set; }
        public string EndMessage { get; private set; }

        // 玩家身上的 Health。
        // 注意：这里单独缓存 Health，不直接依赖 player.Health。
        // 因为 Unity 的 Awake / OnEnable 顺序有时会让 player.Health 暂时还是 null。
        private Health playerHealth;

        // 是否已经订阅过玩家死亡事件。
        // 防止 Awake、OnEnable、Start 多次调用时重复订阅。
        private bool subscribedPlayerDeath;

        private void Awake()
        {
            Instance = this;
            ResolvePlayerReferences();
        }

        private void OnEnable()
        {
            ResolvePlayerReferences();
            SubscribePlayerDeath();
        }

        private void Start()
        {
            // Start 会在所有对象 Awake 之后执行。
            // 这里再做一次兜底，避免初始化顺序导致引用没准备好。
            ResolvePlayerReferences();
            SubscribePlayerDeath();
        }

        private void OnDisable()
        {
            UnsubscribePlayerDeath();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            // 游戏结束后按 R 重载当前场景。
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && GameEnded)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        /// <summary>
        /// 查找并缓存玩家相关引用。
        ///
        /// 这个方法可以重复调用。
        /// 如果已经找到玩家和玩家 Health，它不会做多余工作。
        /// </summary>
        private void ResolvePlayerReferences()
        {
            if (player == null)
            {
                player = FindFirstObjectByType<FirstPersonPlayer>();
            }

            if (player == null)
            {
                return;
            }

            Health foundHealth = player.GetComponent<Health>();
            if (foundHealth == null)
            {
                Debug.LogError("Player has no Health component. GameManager cannot detect player death.", player);
                return;
            }

            // 如果玩家 Health 发生变化，先取消旧事件订阅，再保存新引用。
            // 这样可以防止误订阅到旧对象或其他对象。
            if (playerHealth != null && playerHealth != foundHealth)
            {
                UnsubscribePlayerDeath();
            }

            playerHealth = foundHealth;
        }

        /// <summary>
        /// 订阅玩家死亡事件。
        ///
        /// 只有 playerHealth 确认来自真正的 FirstPersonPlayer 对象时才订阅。
        /// </summary>
        private void SubscribePlayerDeath()
        {
            if (subscribedPlayerDeath || player == null || playerHealth == null)
            {
                return;
            }

            playerHealth.Died += OnPlayerDied;
            subscribedPlayerDeath = true;
        }

        private void UnsubscribePlayerDeath()
        {
            if (!subscribedPlayerDeath || playerHealth == null)
            {
                return;
            }

            playerHealth.Died -= OnPlayerDied;
            subscribedPlayerDeath = false;
        }

        /// <summary>
        /// 刷怪器每成功生成一只僵尸，就调用这个方法。
        /// </summary>
        public void RegisterZombieSpawn()
        {
            Spawned++;
        }

        /// <summary>
        /// 僵尸死亡时调用。
        ///
        /// 注意：这个方法只处理击杀数和胜利，不应该让玩家死亡。
        /// </summary>
        public void RegisterZombieKill()
        {
            if (GameEnded)
            {
                return;
            }

            Kills++;

            if (Kills >= targetKills)
            {
                EndGame("YOU SURVIVED");
            }
        }

        /// <summary>
        /// 玩家死亡事件回调。
        ///
        /// 修复重点：
        /// 这里必须确认 deadHealth 就是 playerHealth。
        /// 如果某个僵尸的 Health 误触发到了这里，直接忽略。
        /// 这样可以防止“打死僵尸却显示玩家死亡”。
        /// </summary>
        private void OnPlayerDied(Health deadHealth)
        {
            if (deadHealth != playerHealth)
            {
                Debug.LogWarning("GameManager ignored a death event because it was not from the player.", deadHealth);
                return;
            }

            EndGame("YOU DIED");
        }

        private void EndGame(string message)
        {
            if (GameEnded)
            {
                return;
            }

            GameEnded = true;
            EndMessage = message;

            if (player != null)
            {
                player.IsInputLocked = true;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
