using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombiePrototype
{
    /// <summary>
    /// 使用 UGUI 显示游戏 HUD。
    ///
    /// 这个脚本用来替代旧的 ZombieHud.OnGUI。
    /// 你需要把它挂到 Canvas 上，然后在 Inspector 里拖入：
    /// 1. 血条 Slider
    /// 2. 血量文字 TextMeshProUGUI
    /// 3. 子弹文字 TextMeshProUGUI
    /// 4. 击杀文字 TextMeshProUGUI
    /// 5. 换弹提示 TextMeshProUGUI
    /// 6. 游戏结束面板 GameObject
    /// 7. 游戏结束文字 TextMeshProUGUI
    ///
    /// UGUI 的核心思路：
    /// 脚本不直接画 UI，而是修改场景里已经存在的 UI 组件的值。
    /// </summary>
    public sealed class ZombieUGUIHud : MonoBehaviour
    {
        [Header("Game References")]

        // 玩家脚本引用。
        // 如果你没有手动拖，Awake 会自动在场景里找 FirstPersonPlayer。
        [SerializeField] private FirstPersonPlayer player;

        // 武器脚本引用。
        // 如果没有手动拖，Awake 会从玩家子物体里自动查找 WeaponRaycaster。
        [SerializeField] private WeaponRaycaster weapon;

        [Header("Health UI")]

        // 血条。
        // Slider.value 会显示当前血量比例，例如 0.75 表示 75%。
        [SerializeField] private Slider healthSlider;

        // 血量文字。
        // 示例：HP 85 / 100
        [SerializeField] private TextMeshProUGUI healthText;

        [Header("Weapon UI")]

        // 子弹文字。
        // 示例：AMMO 8 / 12
        [SerializeField] private TextMeshProUGUI ammoText;

        // 换弹提示。
        // 换弹时显示 RELOADING，不换弹时隐藏。
        [SerializeField] private TextMeshProUGUI reloadText;

        [Header("Kill UI")]

        // 击杀进度文字。
        // 示例：KILLS 5 / 20
        [SerializeField] private TextMeshProUGUI killsText;

        [Header("Game Over UI")]

        // 游戏结束面板。
        // 平时隐藏，胜利或死亡时显示。
        [SerializeField] private GameObject gameOverPanel;

        // 游戏结束大标题。
        // 示例：YOU DIED / YOU SURVIVED
        [SerializeField] private TextMeshProUGUI endMessageText;

        // 重开提示文字。
        // 示例：Press R to restart
        [SerializeField] private TextMeshProUGUI restartText;

        private Health playerHealth;

        private void Awake()
        {
            ResolveReferences();
            InitializeStaticUi();
        }

        private void Update()
        {
            // 玩家、武器可能因为场景重载或初始化顺序暂时为空。
            // 每帧轻量检查一次，可以让 UI 更稳，不会因为 Awake 找不到就永远失效。
            ResolveReferences();

            RefreshHealthUi();
            RefreshWeaponUi();
            RefreshKillUi();
            RefreshGameOverUi();
        }

        /// <summary>
        /// 查找玩家、武器和玩家血量组件。
        /// </summary>
        private void ResolveReferences()
        {
            if (player == null)
            {
                player = FindFirstObjectByType<FirstPersonPlayer>();
            }

            if (weapon == null && player != null)
            {
                weapon = player.GetComponentInChildren<WeaponRaycaster>();
            }

            if (playerHealth == null && player != null)
            {
                playerHealth = player.GetComponent<Health>();
            }
        }

        /// <summary>
        /// 初始化一些固定显示内容。
        /// </summary>
        private void InitializeStaticUi()
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(false);
            }

            if (reloadText != null)
            {
                reloadText.gameObject.SetActive(false);
            }

            if (restartText != null)
            {
                restartText.text = "Press R to restart";
            }
        }

        /// <summary>
        /// 刷新血量 UI。
        /// </summary>
        private void RefreshHealthUi()
        {
            if (playerHealth == null)
            {
                return;
            }

            float current = playerHealth.Current;
            float max = Mathf.Max(1f, playerHealth.Max);
            float percent = current / max;

            if (healthSlider != null)
            {
                healthSlider.minValue = 0f;
                healthSlider.maxValue = 1f;
                healthSlider.value = percent;
            }

            if (healthText != null)
            {
                healthText.text = $"HP {current:0} / {max:0}";
            }
        }

        /// <summary>
        /// 刷新子弹和换弹 UI。
        /// </summary>
        private void RefreshWeaponUi()
        {
            if (weapon == null)
            {
                return;
            }

            if (ammoText != null)
            {
                ammoText.text = $"AMMO {weapon.Ammo} / {weapon.MagazineSize}";
            }

            if (reloadText != null)
            {
                reloadText.gameObject.SetActive(weapon.IsReloading);
            }
        }

        /// <summary>
        /// 刷新击杀数 UI。
        /// </summary>
        private void RefreshKillUi()
        {
            if (killsText == null || GameManager.Instance == null)
            {
                return;
            }

            killsText.text = $"KILLS {GameManager.Instance.Kills} / {GameManager.Instance.TargetKills}";
        }

        /// <summary>
        /// 刷新胜利 / 死亡面板。
        /// </summary>
        private void RefreshGameOverUi()
        {
            if (GameManager.Instance == null)
            {
                return;
            }

            bool gameEnded = GameManager.Instance.GameEnded;

            if (gameOverPanel != null && gameOverPanel.activeSelf != gameEnded)
            {
                gameOverPanel.SetActive(gameEnded);
            }

            if (endMessageText != null)
            {
                endMessageText.text = GameManager.Instance.EndMessage;
            }
        }
    }
}
