using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombiePrototype
{
    /// <summary>
    /// 第一人称射线武器。
    ///
    /// 这个脚本的核心是 Raycast：
    /// 开枪时从摄像机中心向前发射一条射线，如果射线打到带 Health 的对象，
    /// 就调用 Health.TakeDamage 造成伤害。
    ///
    /// 这次修复的重点：
    /// 1. 子弹不能打到玩家自己。
    /// 2. 换弹完成后必须正确补满弹匣。
    /// </summary>
    public sealed class WeaponRaycaster : MonoBehaviour
    {
        [Header("References")]

        // 射线从这个摄像机的位置发出，并沿着摄像机 forward 方向前进。
        [SerializeField] private Camera sourceCamera;

        // 枪口位置，只用于画子弹轨迹的起点。
        [SerializeField] private Transform muzzle;

        // 子弹轨迹显示组件。
        [SerializeField] private LineRenderer tracer;

        [Header("Weapon")]

        // 每发子弹造成的伤害。
        [SerializeField] private float damage = 34f;

        // 射线最大检测距离。
        [SerializeField] private float range = 85f;

        // 两次射击之间的最短间隔。
        [SerializeField] private float fireInterval = 0.14f;

        // 一个弹匣能装多少发子弹。
        [SerializeField] private int magazineSize = 12;

        // 换弹需要多少秒。
        [SerializeField] private float reloadTime = 1.1f;

        // 射线可以打中的 Layer。~0 表示所有 Layer。
        [SerializeField] private LayerMask hitMask = ~0;

        // 玩家自己的 Health。
        // 开枪时如果射线先打到自己，就跳过，不对自己造成伤害。
        private Health ownerHealth;

        // 下一次允许开火的时间。
        private float nextFireTime;

        // 当前这次换弹结束的时间。
        private float reloadEndTime;

        // 子弹轨迹应该隐藏的时间。
        private float tracerHideTime;

        // 是否正在换弹。
        // 用 bool 明确记录换弹状态，避免时间判断互相矛盾。
        private bool isReloading;

        public int Ammo { get; private set; }
        public int MagazineSize => magazineSize;
        public bool IsReloading => isReloading;

        private void Awake()
        {
            Ammo = magazineSize;

            if (sourceCamera == null)
            {
                sourceCamera = GetComponentInChildren<Camera>();
            }

            // 武器是玩家的子物体，所以可以从父物体中找到玩家 Health。
            ownerHealth = GetComponentInParent<Health>();
        }

        private void Update()
        {
            if (Mouse.current == null || Keyboard.current == null)
            {
                return;
            }

            UpdateTracerVisibility();
            UpdateReload();

            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                StartReload();
            }

            if (Mouse.current.leftButton.isPressed)
            {
                TryFire();
            }
        }

        private void UpdateTracerVisibility()
        {
            if (tracer != null && tracer.enabled && Time.time >= tracerHideTime)
            {
                tracer.enabled = false;
            }
        }

        /// <summary>
        /// 检查换弹是否完成。
        ///
        /// 换弹开始时 isReloading = true。
        /// 到达 reloadEndTime 后补满 Ammo，然后把 isReloading 改回 false。
        /// </summary>
        private void UpdateReload()
        {
            if (!isReloading || Time.time < reloadEndTime)
            {
                return;
            }

            Ammo = magazineSize;
            isReloading = false;
        }

        private void TryFire()
        {
            if (sourceCamera == null)
            {
                Debug.LogWarning("WeaponRaycaster has no sourceCamera.", this);
                return;
            }

            if (Time.time < nextFireTime || isReloading)
            {
                return;
            }

            if (Ammo <= 0)
            {
                StartReload();
                return;
            }

            nextFireTime = Time.time + fireInterval;
            Ammo--;

            Ray ray = new Ray(sourceCamera.transform.position, sourceCamera.transform.forward);
            Vector3 endpoint = ray.origin + ray.direction * range;

            if (TryFindValidHit(ray, out RaycastHit hit))
            {
                endpoint = hit.point;

                Health health = hit.collider.GetComponentInParent<Health>();
                if (health != null)
                {
                    health.TakeDamage(damage);

                    // 命中带 Health 的对象后，尝试播放受击反馈。
                    // 这里使用 GetComponentInChildren 是为了兼容当前僵尸结构：
                    // Health 在 Pooled Zombie 根物体上，模型 Renderer 在它的子物体上。
                    HitFlashFeedback hitFeedback = health.GetComponentInChildren<HitFlashFeedback>();
                    if (hitFeedback != null)
                    {
                        hitFeedback.PlayFlash();
                    }
                }
            }

            ShowTracer(endpoint);
        }

        /// <summary>
        /// 找到一个真正应该被子弹命中的目标。
        ///
        /// 为什么不用 Physics.Raycast？
        /// 因为普通 Raycast 只返回第一个命中的碰撞体。
        /// 如果第一个命中的是玩家自己的 CharacterController，
        /// 子弹就会打到自己。
        ///
        /// 这里使用 RaycastAll 获取所有命中点，按距离排序后逐个检查。
        /// 如果命中的是玩家自己，就跳过，继续看后面的目标。
        /// </summary>
        private bool TryFindValidHit(Ray ray, out RaycastHit validHit)
        {
            RaycastHit[] hits = Physics.RaycastAll(ray, range, hitMask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                if (IsOwnerHit(hit))
                {
                    continue;
                }

                validHit = hit;
                return true;
            }

            validHit = default;
            return false;
        }

        /// <summary>
        /// 判断这次命中是不是打到了玩家自己。
        /// </summary>
        private bool IsOwnerHit(RaycastHit hit)
        {
            if (ownerHealth == null)
            {
                return false;
            }

            Health hitHealth = hit.collider.GetComponentInParent<Health>();
            if (hitHealth == ownerHealth)
            {
                return true;
            }

            return hit.collider.transform.IsChildOf(ownerHealth.transform);
        }

        private void StartReload()
        {
            if (Ammo >= magazineSize || isReloading)
            {
                return;
            }

            isReloading = true;
            reloadEndTime = Time.time + reloadTime;
        }

        private void ShowTracer(Vector3 endpoint)
        {
            if (tracer == null)
            {
                return;
            }

            Vector3 start = muzzle != null ? muzzle.position : sourceCamera.transform.position;
            tracer.SetPosition(0, start);
            tracer.SetPosition(1, endpoint);
            tracer.enabled = true;
            tracerHideTime = Time.time + 0.045f;
        }
    }
}
