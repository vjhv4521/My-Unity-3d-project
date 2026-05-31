using UnityEngine;              // 引入 Unity 核心命名空间，用于 MonoBehaviour、Camera、Vector3、Mathf 等
using UnityEngine.InputSystem;  // 引入 Unity 新版 Input System，用于读取键盘、鼠标输入

namespace ZombiePrototype
{
    /// <summary>
    /// FirstPersonPlayer 用于控制第一人称玩家。
    /// 
    /// 主要功能：
    /// 1. WASD 控制移动
    /// 2. 鼠标控制视角
    /// 3. Shift 加速奔跑
    /// 4. Space 跳跃
    /// 5. 处理重力
    /// 6. 控制鼠标锁定和显示
    /// 7. 玩家死亡或输入锁定时禁止操作
    /// 
    /// 这个脚本需要挂载在玩家根对象上。
    /// 玩家对象上需要有 CharacterController 和 Health 组件。
    /// </summary>

    // RequireComponent 表示：
    // 当前游戏对象必须拥有 CharacterController 组件。
    // 如果没有，Unity 会自动添加。
    [RequireComponent(typeof(CharacterController))]

    // 当前游戏对象也必须拥有 Health 组件。
    // Health 用于记录玩家生命值和死亡状态。
    [RequireComponent(typeof(Health))]
    public sealed class FirstPersonPlayer : MonoBehaviour
    {
        /// <summary>
        /// 第一人称摄像机。
        /// 
        /// 用于显示玩家视角。
        /// 鼠标上下移动时，会旋转这个摄像机。
        /// 
        /// 如果没有在 Inspector 面板手动绑定，
        /// Awake 中会自动从子物体中查找 Camera。
        /// </summary>
        [SerializeField] private Camera viewCamera;

        /// <summary>
        /// 普通行走速度。
        /// 
        /// 玩家没有按 Shift 时使用这个速度。
        /// </summary>
        [SerializeField] private float walkSpeed = 5f;

        /// <summary>
        /// 奔跑速度。
        /// 
        /// 玩家按住 Left Shift 时使用这个速度。
        /// </summary>
        [SerializeField] private float sprintSpeed = 8f;

        /// <summary>
        /// 跳跃高度。
        /// 
        /// 数值越大，玩家跳得越高。
        /// </summary>
        [SerializeField] private float jumpHeight = 1.25f;

        /// <summary>
        /// 重力。
        /// 
        /// 这里使用负数，表示向下加速。
        /// 
        /// Unity 默认重力大约是 -9.81，
        /// 这里设置成 -22f，会让玩家下落更快，
        /// 手感更像 FPS 游戏。
        /// </summary>
        [SerializeField] private float gravity = -22f;

        /// <summary>
        /// 鼠标灵敏度。
        /// 
        /// 数值越大，鼠标移动同样距离时视角旋转越快。
        /// </summary>
        [SerializeField] private float mouseSensitivity = 0.13f;

        /// <summary>
        /// CharacterController 组件引用。
        /// 
        /// CharacterController 是 Unity 提供的角色控制器，
        /// 常用于 FPS / TPS 角色移动。
        /// 
        /// 它不需要 Rigidbody，
        /// 可以通过 controller.Move() 来移动角色。
        /// </summary>
        private CharacterController controller;

        /// <summary>
        /// 玩家当前的垂直速度。
        /// 
        /// 用于处理跳跃和重力。
        /// 
        /// 正数表示向上运动，
        /// 负数表示向下掉落。
        /// </summary>
        private float verticalVelocity;

        /// <summary>
        /// 摄像机的上下旋转角度。
        /// 
        /// 也就是玩家视角的俯仰角。
        /// 
        /// pitch 会被限制在 -82 到 82 度之间，
        /// 防止视角翻转。
        /// </summary>
        private float pitch;

        /// <summary>
        /// 对外提供当前玩家摄像机。
        /// 
        /// 其他脚本可以通过 player.ViewCamera 获取玩家摄像机。
        /// 例如武器脚本可以用它来发射射线。
        /// </summary>
        public Camera ViewCamera => viewCamera;

        /// <summary>
        /// 玩家生命值组件。
        /// 
        /// 外部可以读取这个属性，
        /// 例如 HUD 可以通过 player.Health.Current 显示玩家血量。
        /// 
        /// private set 表示只有本类内部可以赋值。
        /// </summary>
        public Health Health { get; private set; }

        /// <summary>
        /// 是否锁定玩家输入。
        /// 
        /// 如果 IsInputLocked 为 true，
        /// 玩家不能移动，也不能转动视角。
        /// 
        /// 这个属性可以被外部脚本设置，
        /// 例如游戏结束、暂停、打开菜单时可以锁定输入。
        /// </summary>
        public bool IsInputLocked { get; set; }

        /// <summary>
        /// Awake 是 Unity 生命周期函数。
        /// 
        /// 当脚本实例被加载时调用。
        /// 通常用于获取组件引用和初始化。
        /// </summary>
        private void Awake()
        {
            // 获取当前对象上的 CharacterController 组件。
            // 因为上面有 [RequireComponent(typeof(CharacterController))]，
            // 所以理论上这里一定能获取到。
            controller = GetComponent<CharacterController>();

            // 获取当前对象上的 Health 组件。
            // 用于判断玩家是否死亡。
            Health = GetComponent<Health>();

            // 如果没有在 Inspector 中手动设置摄像机，
            // 就从当前对象的子物体中查找 Camera 组件。
            if (viewCamera == null)
            {
                viewCamera = GetComponentInChildren<Camera>();
            }
        }

        /// <summary>
        /// OnEnable 是 Unity 生命周期函数。
        /// 
        /// 当这个脚本被启用时调用。
        /// 
        /// 这里用于锁定鼠标光标，
        /// 让玩家可以正常用鼠标控制视角。
        /// </summary>
        private void OnEnable()
        {
            // 把鼠标锁定在游戏窗口中央。
            // FPS 游戏通常都会这样处理鼠标。
            Cursor.lockState = CursorLockMode.Locked;

            // 隐藏鼠标光标。
            Cursor.visible = false;
        }

        /// <summary>
        /// Update 是 Unity 生命周期函数。
        /// 
        /// 每一帧都会执行一次。
        /// 
        /// 这里主要负责：
        /// 1. 检查键盘鼠标是否存在
        /// 2. 控制鼠标锁定 / 解锁
        /// 3. 判断是否允许输入
        /// 4. 更新视角
        /// 5. 更新移动
        /// </summary>
        private void Update()
        {
            // 如果当前没有键盘或鼠标设备，
            // 就直接返回，避免空引用错误。
            //
            // 这种情况可能出现在：
            // 1. 没有启用新版 Input System
            // 2. 当前平台没有鼠标键盘
            if (Keyboard.current == null || Mouse.current == null)
            {
                return;
            }

            // 如果这一帧按下了 Esc 键，
            // 就解锁鼠标并显示光标。
            //
            // 这样玩家可以退出鼠标锁定状态，
            // 方便点击编辑器、菜单或其他 UI。
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // 如果这一帧按下鼠标左键，
            // 就重新锁定鼠标并隐藏光标。
            //
            // 这通常用于玩家点击游戏画面后重新进入控制状态。
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // 如果输入被锁定，或者玩家已经死亡，
            // 就不再处理视角和移动。
            //
            // 例如：
            // 游戏结束后可以设置 IsInputLocked = true。
            if (IsInputLocked || Health.IsDead)
            {
                return;
            }

            // 更新鼠标视角控制。
            UpdateLook();

            // 更新玩家移动、跳跃和重力。
            UpdateMove();
        }

        /// <summary>
        /// 更新玩家视角。
        /// 
        /// 鼠标左右移动：
        /// 控制玩家身体左右旋转。
        /// 
        /// 鼠标上下移动：
        /// 控制摄像机上下旋转。
        /// </summary>
        private void UpdateLook()
        {
            // 读取鼠标这一帧的移动量。
            //
            // Mouse.current.delta.ReadValue() 返回 Vector2：
            // x 表示鼠标水平移动量
            // y 表示鼠标垂直移动量
            //
            // 乘以 mouseSensitivity 后，
            // 可以控制鼠标灵敏度。
            Vector2 lookDelta = Mouse.current.delta.ReadValue() * mouseSensitivity;

            // 玩家身体左右旋转。
            //
            // Vector3.up 表示绕世界 Y 轴旋转。
            // lookDelta.x 表示鼠标水平移动量。
            //
            // 鼠标向右移动，玩家向右转。
            // 鼠标向左移动，玩家向左转。
            transform.Rotate(Vector3.up * lookDelta.x);

            // 计算摄像机上下视角。
            //
            // pitch - lookDelta.y：
            // 鼠标向上移动时，通常希望视角向上抬。
            // 因为 Unity 的屏幕坐标和旋转方向关系，
            // 所以这里用减法。
            //
            // Mathf.Clamp：
            // 把 pitch 限制在 -82 到 82 度之间。
            // 防止玩家视角翻过头，比如看到自己背后。
            pitch = Mathf.Clamp(pitch - lookDelta.y, -82f, 82f);

            // 设置摄像机的本地旋转。
            //
            // 这里只旋转 X 轴，
            // 因为左右旋转已经交给玩家身体 transform 处理了。
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        /// <summary>
        /// 更新玩家移动。
        /// 
        /// 包括：
        /// 1. WASD 输入
        /// 2. 地面检测
        /// 3. 跳跃
        /// 4. 重力
        /// 5. 行走 / 奔跑速度
        /// 6. 使用 CharacterController 移动玩家
        /// </summary>
        private void UpdateMove()
        {
            // 创建一个二维输入向量。
            //
            // input.x 表示左右移动：
            // A = -1
            // D = +1
            //
            // input.y 表示前后移动：
            // S = -1
            // W = +1
            Vector2 input = Vector2.zero;

            // 按 W，向前移动。
            if (Keyboard.current.wKey.isPressed)
            {
                input.y += 1f;
            }

            // 按 S，向后移动。
            if (Keyboard.current.sKey.isPressed)
            {
                input.y -= 1f;
            }

            // 按 D，向右移动。
            if (Keyboard.current.dKey.isPressed)
            {
                input.x += 1f;
            }

            // 按 A，向左移动。
            if (Keyboard.current.aKey.isPressed)
            {
                input.x -= 1f;
            }

            // 限制输入向量长度最大为 1。
            //
            // 如果不这样做，同时按 W + D 时，
            // input 会变成 (1, 1)，长度约为 1.414，
            // 导致斜着走比直走更快。
            //
            // ClampMagnitude 可以解决这个问题。
            input = Vector2.ClampMagnitude(input, 1f);

            // 判断玩家是否站在地面上。
            //
            // controller.isGrounded 是 CharacterController 自带的地面检测。
            bool grounded = controller.isGrounded;

            // 如果玩家在地面上，并且垂直速度小于 0，
            // 说明玩家正在向下落或者贴着地面。
            if (grounded && verticalVelocity < 0f)
            {
                // 给一个小的向下速度。
                //
                // 这样可以让 CharacterController 更稳定地贴在地面上，
                // 避免因为数值误差导致 isGrounded 抖动。
                verticalVelocity = -2f;
            }

            // 如果玩家在地面上，并且这一帧按下了空格键，
            // 就执行跳跃。
            if (grounded && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                // 根据跳跃高度和重力计算初速度。
                //
                // 公式来自物理运动：
                // v = sqrt(h * -2 * g)
                //
                // h 是跳跃高度 jumpHeight
                // g 是重力 gravity，注意 gravity 是负数
                //
                // 得到的 verticalVelocity 是一个正数，
                // 表示向上的初速度。
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            // 每一帧都叠加重力。
            //
            // gravity 是负数，
            // 所以 verticalVelocity 会越来越小，
            // 玩家会从上升逐渐变成下落。
            //
            // 乘以 Time.deltaTime，
            // 可以保证重力效果和帧率无关。
            verticalVelocity += gravity * Time.deltaTime;

            // 判断当前是否按住左 Shift。
            //
            // 如果按住，就使用奔跑速度 sprintSpeed。
            // 如果没有按住，就使用行走速度 walkSpeed。
            float speed = Keyboard.current.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;

            // 根据玩家朝向计算移动方向。
            //
            // transform.right * input.x：
            // 控制左右移动。
            //
            // transform.forward * input.y：
            // 控制前后移动。
            //
            // 这样玩家移动方向会跟随玩家身体朝向变化。
            Vector3 move = transform.right * input.x + transform.forward * input.y;

            // 给水平移动乘以速度，
            // 再加上垂直速度。
            //
            // Vector3.up * verticalVelocity：
            // 控制跳跃和下落。
            move = move * speed + Vector3.up * verticalVelocity;

            // 使用 CharacterController 移动玩家。
            //
            // controller.Move 接收的是这一帧的位移量，
            // 所以要乘以 Time.deltaTime。
            controller.Move(move * Time.deltaTime);
        }
    }
}