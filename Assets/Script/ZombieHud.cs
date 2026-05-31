using UnityEngine;  // 引入 Unity 核心命名空间，用于 MonoBehaviour、GUI、GUIStyle、Screen 等

namespace ZombiePrototype
{
    /// <summary>
    /// ZombieHud 用于绘制游戏 HUD 界面。
    /// 
    /// HUD 指的是游戏画面上的状态显示界面，
    /// 比如：
    /// 1. 准星
    /// 2. 玩家血量
    /// 3. 当前子弹数量
    /// 4. 击杀数量
    /// 5. 操作提示
    /// 6. 游戏结束提示
    /// 
    /// 这个脚本使用的是 Unity 的 OnGUI 系统。
    /// OnGUI 比较适合做简单原型，
    /// 如果以后做正式 UI，建议改成 Canvas + TextMeshPro。
    /// </summary>
    public sealed class ZombieHud : MonoBehaviour
    {
        /// <summary>
        /// 玩家脚本引用。
        /// 
        /// 用于获取玩家血量。
        /// 如果没有在 Inspector 面板手动指定，
        /// Awake 中会自动查找场景里的 FirstPersonPlayer。
        /// </summary>
        [SerializeField] private FirstPersonPlayer player;

        /// <summary>
        /// 武器脚本引用。
        /// 
        /// 用于获取当前子弹数、弹匣大小、是否正在换弹。
        /// 如果没有在 Inspector 面板手动指定，
        /// Awake 中会尝试从 player 的子物体中查找 WeaponRaycaster。
        /// </summary>
        [SerializeField] private WeaponRaycaster weapon;
        private Health playerHealth;

        /// <summary>
        /// 左上角和底部提示文字的样式。
        /// 
        /// GUIStyle 可以控制字体大小、颜色、对齐方式等。
        /// </summary>
        private GUIStyle labelStyle;

        /// <summary>
        /// 居中文字的样式。
        /// 
        /// 用于准星、游戏结束文字、重新开始提示等。
        /// </summary>
        private GUIStyle centerStyle;

        /// <summary>
        /// Awake 是 Unity 生命周期函数。
        /// 
        /// 当脚本实例被加载时调用。
        /// 这里主要用于自动寻找 player 和 weapon。
        /// </summary>
        private void Awake()
        {
            // 如果没有在 Inspector 中手动绑定 player，
            // 就自动在场景中查找第一个 FirstPersonPlayer。
            if (player == null)
            {
                player = FindFirstObjectByType<FirstPersonPlayer>();
            }

            // 如果没有手动绑定 weapon，
            // 并且已经找到了 player，
            // 就从 player 的子物体中查找 WeaponRaycaster。
            //
            // 一般 FPS 武器会挂在玩家摄像机或玩家子物体上，
            // 所以用 GetComponentInChildren 比较合适。
            if (weapon == null && player != null)
            {
                weapon = player.GetComponentInChildren<WeaponRaycaster>();
            }

            if (player != null)
            {
                playerHealth = player.GetComponent<Health>();
            }
        }

        /// <summary>
        /// OnGUI 是 Unity 的 IMGUI 绘制函数。
        /// 
        /// Unity 会在需要绘制 GUI 时调用它。
        /// 
        /// 注意：
        /// OnGUI 可能一帧被调用多次，
        /// 所以不要在里面做太重的逻辑。
        /// 当前这里只是画简单文字，问题不大。
        /// </summary>
        private void OnGUI()
        {
            // 确保 GUIStyle 已经创建。
            // 如果还没有创建，就初始化样式。
            EnsureStyles();

            // 计算屏幕中心点 x 坐标。
            // Screen.width 是当前游戏窗口宽度。
            float cx = Screen.width * 0.5f;

            // 计算屏幕中心点 y 坐标。
            // Screen.height 是当前游戏窗口高度。
            float cy = Screen.height * 0.5f;

            // 在屏幕中心绘制一个简单准星。
            //
            // Rect 参数说明：
            // x：绘制区域左上角 x
            // y：绘制区域左上角 y
            // width：宽度
            // height：高度
            //
            // 这里用 "+" 号作为准星。
            GUI.Label(new Rect(cx - 8f, cy - 10f, 16f, 20f), "+", centerStyle);

            // 获取玩家当前血量。
            //
            // 如果 player 不为空，就读取 player.Health.Current。
            // 如果 player 为空，就显示 0。
            //
            // 这里使用了三元运算符：
            // 条件 ? 条件成立时的值 : 条件不成立时的值
            float health = playerHealth != null ? playerHealth.Current : 0f;

            // 获取当前武器子弹显示文本。
            //
            // 如果 weapon 不为空，显示 当前子弹 / 弹匣容量。
            // 例如：8/12
            //
            // 如果 weapon 为空，就显示 "-"。
            string ammo = weapon != null ? $"{weapon.Ammo}/{weapon.MagazineSize}" : "-";

            // 如果正在换弹，就显示 "RELOADING"。
            // 如果没有换弹，就显示空字符串。
            //
            // string.Empty 等价于 ""，表示空文本。
            string reload = weapon != null && weapon.IsReloading ? "  RELOADING" : string.Empty;

            // 获取当前击杀数。
            //
            // 如果 GameManager.Instance 不为空，
            // 就读取 GameManager.Instance.Kills。
            // 如果为空，说明当前场景没有 GameManager，就显示 0。
            int kills = GameManager.Instance != null ? GameManager.Instance.Kills : 0;

            // 获取目标击杀数。
            //
            // 比如目标是击杀 30 只僵尸，
            // 那么 target 就是 30。
            int target = GameManager.Instance != null ? GameManager.Instance.TargetKills : 0;

            // 绘制屏幕左上角 HUD 信息。
            //
            // 显示内容包括：
            // HP：玩家血量
            // AMMO：当前子弹 / 弹匣容量
            // RELOADING：换弹提示
            // KILLS：当前击杀数 / 目标击杀数
            //
            // {health:0} 表示血量不显示小数。
            GUI.Label(
                new Rect(18f, 16f, 500f, 32f),
                $"HP {health:0}    AMMO {ammo}{reload}    KILLS {kills}/{target}",
                labelStyle
            );

            // 绘制屏幕底部操作提示。
            //
            // Screen.height - 42f 表示距离屏幕底部 42 像素。
            GUI.Label(
                new Rect(18f, Screen.height - 42f, 700f, 28f),
                "WASD move  Mouse look/fire  Shift sprint  Space jump  R reload/restart  Esc cursor",
                labelStyle
            );

            // 如果 GameManager 存在，并且游戏已经结束，
            // 就显示游戏结束界面。
            if (GameManager.Instance != null && GameManager.Instance.GameEnded)
            {
                // 在屏幕中间偏上的位置显示游戏结束信息。
                //
                // EndMessage 可能是：
                // "YOU WIN"
                // "YOU DIED"
                // 或其他 GameManager 设置的文本。
                GUI.Label(
                    new Rect(0f, Screen.height * 0.5f - 72f, Screen.width, 70f),
                    GameManager.Instance.EndMessage,
                    centerStyle
                );

                // 在游戏结束信息下面显示重新开始提示。
                GUI.Label(
                    new Rect(0f, Screen.height * 0.5f, Screen.width, 36f),
                    "Press R to restart",
                    centerStyle
                );
            }
        }

        /// <summary>
        /// 确保 GUI 样式已经初始化。
        /// 
        /// 这个方法只会真正创建一次样式。
        /// 后续 OnGUI 再调用时，如果 labelStyle 已经存在，
        /// 就直接 return，不重复创建。
        /// 
        /// 这样可以避免每次 OnGUI 都 new GUIStyle，
        /// 减少不必要的开销。
        /// </summary>
        private void EnsureStyles()
        {
            // 如果 labelStyle 已经创建过，
            // 说明 centerStyle 也已经创建过了，
            // 直接返回。
            if (labelStyle != null)
            {
                return;
            }

            // 创建普通文本样式。
            //
            // GUI.skin.label 是 Unity 默认 Label 样式。
            // new GUIStyle(GUI.skin.label) 表示在默认样式基础上复制一份并修改。
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                // 设置字体大小为 22
                fontSize = 22,

                // 设置文字颜色为白色。
                //
                // normal 表示普通状态下的样式。
                normal = { textColor = Color.white }
            };

            // 创建居中文本样式。
            centerStyle = new GUIStyle(GUI.skin.label)
            {
                // 设置文本居中对齐。
                alignment = TextAnchor.MiddleCenter,

                // 设置字体大小为 34
                fontSize = 34,

                // 设置字体为粗体。
                fontStyle = FontStyle.Bold,

                // 设置文字颜色为白色。
                normal = { textColor = Color.white }
            };
        }
    }
}
