using System;          // 引入 System 命名空间，用于使用 Action 事件委托
using UnityEngine;     // 引入 UnityEngine，用于使用 MonoBehaviour、SerializeField、Mathf 等 Unity 功能

namespace ZombiePrototype
{
    /// <summary>
    /// Health 类用于管理一个游戏对象的生命值。
    /// 
    /// 主要功能：
    /// 1. 记录当前生命值和最大生命值
    /// 2. 受到伤害时减少生命值
    /// 3. 生命值变化时通知外部系统
    /// 4. 死亡时触发死亡事件
    /// 
    /// 这个脚本可以挂载到玩家、僵尸、怪物、可破坏物体等对象上。
    /// </summary>
    public sealed class Health : MonoBehaviour
    {
        /// <summary>
        /// 最大生命值。
        /// 
        /// [SerializeField] 的作用：
        /// 即使 maxHealth 是 private，
        /// 也可以在 Unity Inspector 面板中看到并修改它。
        /// 
        /// 默认最大生命值为 100。
        /// </summary>
        [SerializeField] private float maxHealth = 100f;

        /// <summary>
        /// 死亡事件。
        /// 
        /// 当生命值减少到 0 或以下时触发。
        /// 
        /// Action<Health> 表示：
        /// 这个事件会把当前 Health 组件本身传出去。
        /// 
        /// 例如外部可以这样监听：
        /// health.Died += OnZombieDied;
        /// </summary>
        public event Action<Health> Died;

        /// <summary>
        /// 生命值变化事件。
        /// 
        /// 当生命值发生变化时触发。
        /// 
        /// Action<float, float> 表示这个事件会传递两个 float 参数：
        /// 第一个参数：当前生命值 Current
        /// 第二个参数：最大生命值 maxHealth
        /// 
        /// 常用于更新血条 UI。
        /// </summary>
        public event Action<float, float> Changed;

        /// <summary>
        /// 当前生命值。
        /// 
        /// public get：
        /// 外部可以读取当前生命值。
        /// 
        /// private set：
        /// 只有 Health 类内部可以修改它，
        /// 防止其他脚本随便修改生命值。
        /// </summary>
        public float Current { get; private set; }

        /// <summary>
        /// 最大生命值的只读属性。
        /// 
        /// 外部可以通过 health.Max 读取最大生命值，
        /// 但不能直接修改 maxHealth。
        /// </summary>
        public float Max => maxHealth;

        /// <summary>
        /// 判断当前对象是否已经死亡。
        /// 
        /// 当 Current 小于等于 0 时，认为已经死亡。
        /// </summary>
        public bool IsDead => Current <= 0f;

        /// <summary>
        /// Awake 是 Unity 的生命周期函数。
        /// 
        /// 当脚本实例被加载时调用，
        /// 通常比 Start 更早执行。
        /// 
        /// 这里用于初始化当前生命值，
        /// 让 Current 一开始等于最大生命值。
        /// </summary>
        private void Awake()
        {
            Current = maxHealth;
        }

        /// <summary>
        /// 重置生命值。
        /// 
        /// 这个方法通常用于：
        /// 1. 角色复活
        /// 2. 新一局游戏开始
        /// 3. 重新设置怪物血量
        /// 4. 对象池重新启用对象
        /// 
        /// 参数 newMaxHealth：
        /// 新的最大生命值。
        /// </summary>
        public void ResetHealth(float newMaxHealth)
        {
            // 设置新的最大生命值。
            // Mathf.Max(1f, newMaxHealth) 的作用是：
            // 保证最大生命值至少为 1，不能是 0 或负数。
            maxHealth = Mathf.Max(1f, newMaxHealth);

            // 当前生命值恢复到最大生命值
            Current = maxHealth;

            // 触发生命值变化事件。
            // ?. 表示如果 Changed 不为空，才调用它。
            // 这样可以避免没有监听者时报空引用错误。
            Changed?.Invoke(Current, maxHealth);
        }

        /// <summary>
        /// 受到伤害。
        /// 
        /// 参数 amount：
        /// 受到的伤害数值。
        /// 
        /// 例如：
        /// TakeDamage(20f) 表示扣除 20 点生命值。
        /// </summary>
        public void TakeDamage(float amount)
        {
            // 如果已经死亡，就不再受到伤害。
            // 这样可以避免死亡事件被重复触发。
            if (IsDead)
            {
                return;
            }

            // 计算扣血后的生命值。
            //
            // Mathf.Max(0f, amount)：
            // 保证伤害值不能是负数。
            // 如果传入 -10，则会被当成 0 处理。
            //
            // Current - Mathf.Max(0f, amount)：
            // 用当前生命值减去伤害值。
            //
            // Mathf.Max(0f, ...)：
            // 保证当前生命值最低只能是 0，
            // 不会出现 -10、-50 这种负数生命值。
            Current = Mathf.Max(0f, Current - Mathf.Max(0f, amount));

            // 生命值发生变化后，通知外部系统。
            // 比如 UI 血条可以监听这个事件来刷新显示。
            Changed?.Invoke(Current, maxHealth);

            // 如果扣血后生命值小于等于 0，
            // 说明这个对象死亡了。
            if (IsDead)
            {
                // 触发死亡事件。
                // this 表示把当前 Health 组件传给监听者。
                Died?.Invoke(this);
            }
        }
    }
}