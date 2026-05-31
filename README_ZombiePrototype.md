# Zombie Prototype

打开项目后，Unity 会编译 `Assets/Script` 和 `Assets/Editor` 下的新脚本，并自动重建一次 `Assets/Scenes/Zombie_Demo.unity`。

## 运行

1. 用 Unity 6000.2.10f1 打开 `3d` 目录。
2. 等待脚本编译完成。
3. 打开 `Assets/Scenes/Zombie_Demo.unity`。
4. 点击 Play。

## 操作

- WASD：移动
- 鼠标：视角
- 鼠标左键：射击
- Shift：冲刺
- Space：跳跃
- R：换弹；结束后重开
- Esc：释放鼠标

## 重建场景

如果想重新生成 Demo 场景，使用 Unity 菜单：

`Tools/Zombie Prototype/Rebuild Demo Scene`

构建器会创建玩家、射线武器、竞技场、刷怪器、HUD，并从 `Assets/PolygonZombies/Prefabs` 中自动挑选 `Zombie_*.prefab` 作为僵尸外观。

## 后续扩展顺序

1. 在 `ZombieSpawner` 上调 `maxAlive`、`totalToSpawn`、`spawnRadius`。
2. 在 `WeaponRaycaster` 上调伤害、射速、弹匣、换弹时间。
3. 在 `ZombieAI` 上调移动速度、攻击距离、攻击伤害。
4. 给 PolygonZombies 模型接 Animator Controller，让追击和死亡有动画。
5. 用正式枪械模型替换当前程序生成的第一人称占位枪。

## NavMesh 和状态机

当前僵尸已经改成 `NavMeshAgent` 寻路，并由 `ZombieAI.ZombieState` 状态机控制：

- `Idle`：玩家距离较远，原地不动。
- `Chase`：玩家进入发现范围，快速追击。
- `SlowApproach`：靠近玩家后降低速度。
- `Attack`：进入攻击范围，停止移动并攻击。
- `Dead`：死亡，关闭寻路和碰撞。

重建场景时，`ZombieDemoSceneBuilder` 会创建 `NavMesh Surface` 并烘焙 NavMesh。墙和掩体会影响可走区域，所以僵尸会绕路，不会直接穿过障碍物。

如果你改了地形、墙、障碍物，重新执行：

`Tools/Zombie Prototype/Rebuild Demo Scene`
