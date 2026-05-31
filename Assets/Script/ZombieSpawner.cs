using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace ZombiePrototype
{
    /// <summary>
    /// 僵尸刷怪器。
    ///
    /// 当前版本使用对象池：
    /// 1. 需要僵尸时，从池里取一个未使用的对象。
    /// 2. 如果池里没有可用对象，才创建新的僵尸。
    /// 3. 僵尸死亡后不 Destroy，而是延迟隐藏并放回池中。
    /// 4. 下次刷怪时复用这个对象。
    ///
    /// 对象池的好处：
    /// Destroy / Instantiate 很频繁时会造成卡顿和 GC。
    /// 对象池复用旧对象，可以让运行更稳定。
    /// </summary>
    public sealed class ZombieSpawner : MonoBehaviour
    {
        [Header("References")]

        // PolygonZombies 的模型预制体，只负责外观。
        [SerializeField] private GameObject[] zombieVisualPrefabs;

        // 玩家位置，僵尸会围绕玩家生成。
        [SerializeField] private Transform player;

        [Header("Spawn Rules")]

        // 场上最多同时存在多少只活僵尸。
        [SerializeField] private int maxAlive = 8;

        // 本局最多激活生成多少只僵尸。
        [SerializeField] private int totalToSpawn = 30;

        // 僵尸距离玩家多远生成。
        [SerializeField] private float spawnRadius = 23f;

        // 每隔多少秒尝试生成一只。
        [SerializeField] private float spawnInterval = 1.35f;

        // 每只僵尸刷新时的血量。
        [SerializeField] private float zombieHealth = 100f;

        // 在候选点附近多大范围内寻找最近 NavMesh 点。
        [SerializeField] private float navMeshSampleDistance = 12f;

        [Header("Visual Grounding")]

        // 是否让僵尸模型自动贴到地面。
        // 这个只移动模型子物体，不移动真正负责寻路的 NavMeshAgent 根物体。
        [SerializeField] private bool alignZombieVisualToGround = true;

        // 模型最低点离地面的距离。
        // 稍微大于 0 可以避免脚底和地面闪烁穿插。
        [SerializeField] private float zombieVisualGroundOffset = 0.03f;

        // 每帧最多修正的高度。
        // 数值越大，动画导致的高度错误修正越快；太大可能会让模型高度跳动明显。
        [SerializeField] private float zombieMaxGroundCorrectionPerFrame = 0.6f;

        // 手动额外抬高模型。
        // 如果僵尸出生时仍然半截入地，就优先调这个值，例如 0.3、0.6、0.9。
        [SerializeField] private float zombieManualVisualYOffset;

        [Header("Pool")]

        // 游戏开始时预先创建多少只僵尸放进池子。
        [SerializeField] private int initialPoolSize = 8;

        // 池子最多允许创建多少只僵尸。
        [SerializeField] private int maxPoolSize = 20;

        private readonly HashSet<ZombieAI> aliveZombies = new HashSet<ZombieAI>();
        private readonly Queue<ZombieAI> pooledZombies = new Queue<ZombieAI>();
        private readonly List<ZombieAI> allZombies = new List<ZombieAI>();

        private float nextSpawnTime;
        private float nextWarningTime;
        private int spawned;
        private bool navMeshChecked;

        private void Start()
        {
            ResolvePlayer();
            EnsureNavMeshReady();
            WarmPool();
        }

        private void Update()
        {
            CleanupAliveSet();

            if (player == null)
            {
                ResolvePlayer();
                WarnEveryFewSeconds("ZombieSpawner cannot find Player.");
                return;
            }

            if (!HasValidPrefab())
            {
                WarnEveryFewSeconds("ZombieSpawner has no valid zombie prefabs.");
                return;
            }

            if (spawned >= totalToSpawn || Time.time < nextSpawnTime || aliveZombies.Count >= maxAlive)
            {
                return;
            }

            if (TrySpawnZombie())
            {
                nextSpawnTime = Time.time + spawnInterval;
            }
        }

        private void ResolvePlayer()
        {
            if (player != null)
            {
                return;
            }

            FirstPersonPlayer foundPlayer = FindFirstObjectByType<FirstPersonPlayer>();
            if (foundPlayer != null)
            {
                player = foundPlayer.transform;
            }
        }

        private void EnsureNavMeshReady()
        {
            if (navMeshChecked)
            {
                return;
            }

            navMeshChecked = true;

            Vector3 sampleCenter = player != null ? player.position : transform.position;
            if (NavMesh.SamplePosition(sampleCenter, out _, 5f, NavMesh.AllAreas))
            {
                return;
            }

            NavMeshSurface surface = FindFirstObjectByType<NavMeshSurface>();
            if (surface == null)
            {
                GameObject surfaceObject = new GameObject("Runtime NavMesh Surface");
                surface = surfaceObject.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.All;
                surface.layerMask = ~0;
                surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            }

            surface.BuildNavMesh();
        }

        /// <summary>
        /// 预热对象池。
        ///
        /// 游戏开始时先创建几只僵尸并隐藏。
        /// 这样第一次刷怪时不需要临时 Instantiate。
        /// </summary>
        private void WarmPool()
        {
            if (!HasValidPrefab())
            {
                return;
            }

            int count = Mathf.Min(initialPoolSize, maxPoolSize);
            for (int i = 0; i < count; i++)
            {
                ZombieAI zombie = CreateZombieForPool();
                ReturnZombieToPoolImmediately(zombie);
            }
        }

        private bool TrySpawnZombie()
        {
            if (!TryFindSpawnPoint(out Vector3 position))
            {
                WarnEveryFewSeconds("ZombieSpawner cannot find a NavMesh spawn point.");
                nextSpawnTime = Time.time + 0.5f;
                return false;
            }

            ZombieAI zombie = GetZombieFromPool();
            if (zombie == null)
            {
                WarnEveryFewSeconds("ZombieSpawner pool is full and has no available zombies.");
                return false;
            }

            Quaternion rotation = Quaternion.LookRotation(
                new Vector3(player.position.x - position.x, 0f, player.position.z - position.z).normalized,
                Vector3.up
            );

            zombie.PrepareForSpawn(position, rotation, player, zombieHealth);
            aliveZombies.Add(zombie);
            spawned++;

            GameManager.Instance?.RegisterZombieSpawn();
            return true;
        }

        /// <summary>
        /// 从池里取一个僵尸。
        /// 如果池子为空，但总数量还没超过 maxPoolSize，就创建新的。
        /// </summary>
        private ZombieAI GetZombieFromPool()
        {
            while (pooledZombies.Count > 0)
            {
                ZombieAI zombie = pooledZombies.Dequeue();
                if (zombie != null)
                {
                    return zombie;
                }
            }

            if (allZombies.Count >= maxPoolSize)
            {
                return null;
            }

            return CreateZombieForPool();
        }

        /// <summary>
        /// 创建一个新的僵尸对象。
        ///
        /// 注意：这个方法只在池子不够用时调用。
        /// 平时刷怪会优先复用池里的旧对象。
        /// </summary>
        private ZombieAI CreateZombieForPool()
        {
            GameObject root = new GameObject("Pooled Zombie");

            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.95f, 0f);
            collider.height = 1.9f;
            collider.radius = 0.38f;

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.height = 1.9f;
            agent.radius = 0.38f;
            agent.baseOffset = 0f;

            Health health = root.AddComponent<Health>();
            health.ResetHealth(zombieHealth);

            ZombieAI ai = root.AddComponent<ZombieAI>();
            ai.ConfigureVisualGrounding(
                alignZombieVisualToGround,
                zombieVisualGroundOffset,
                zombieMaxGroundCorrectionPerFrame,
                zombieManualVisualYOffset
            );
            ai.DiedForPool += OnZombieDiedForPool;

            GameObject prefab = PickRandomZombiePrefab();
            GameObject visual = Instantiate(prefab, root.transform);
            visual.name = prefab.name;
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            // 给每个运行时生成的僵尸模型加受击闪烁反馈。
            // 这样武器命中僵尸 Health 时，就能通过子物体找到 HitFlashFeedback 并播放闪红。
            if (visual.GetComponent<HitFlashFeedback>() == null)
            {
                visual.AddComponent<HitFlashFeedback>();
            }

            ai.SetVisualRoot(visual.transform);

            allZombies.Add(ai);
            return ai;
        }

        private void OnZombieDiedForPool(ZombieAI zombie)
        {
            aliveZombies.Remove(zombie);
            StartCoroutine(ReturnZombieToPoolAfterDelay(zombie));
        }

        private IEnumerator ReturnZombieToPoolAfterDelay(ZombieAI zombie)
        {
            yield return new WaitForSeconds(zombie.ReturnToPoolDelay);
            ReturnZombieToPoolImmediately(zombie);
        }

        private void ReturnZombieToPoolImmediately(ZombieAI zombie)
        {
            if (zombie == null)
            {
                return;
            }

            aliveZombies.Remove(zombie);
            zombie.DeactivateForPool();
            pooledZombies.Enqueue(zombie);
        }

        private bool TryFindSpawnPoint(out Vector3 position)
        {
            for (int attempt = 0; attempt < 24; attempt++)
            {
                Vector2 direction = Random.insideUnitCircle;
                if (direction.sqrMagnitude <= 0.001f)
                {
                    direction = Vector2.right;
                }

                direction.Normalize();
                Vector3 candidate = player.position + new Vector3(direction.x, 0f, direction.y) * spawnRadius;

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
                {
                    position = hit.position;
                    return true;
                }
            }

            position = default;
            return false;
        }

        private bool HasValidPrefab()
        {
            if (zombieVisualPrefabs == null)
            {
                return false;
            }

            foreach (GameObject prefab in zombieVisualPrefabs)
            {
                if (prefab != null)
                {
                    return true;
                }
            }

            return false;
        }

        private GameObject PickRandomZombiePrefab()
        {
            List<GameObject> validPrefabs = new List<GameObject>();
            foreach (GameObject prefab in zombieVisualPrefabs)
            {
                if (prefab != null)
                {
                    validPrefabs.Add(prefab);
                }
            }

            return validPrefabs[Random.Range(0, validPrefabs.Count)];
        }

        private void CleanupAliveSet()
        {
            aliveZombies.RemoveWhere(zombie => zombie == null || zombie.State == ZombieAI.ZombieState.Dead);
        }

        private void WarnEveryFewSeconds(string message)
        {
            if (Time.time < nextWarningTime)
            {
                return;
            }

            nextWarningTime = Time.time + 2f;
            Debug.LogWarning(message, this);
        }
    }
}
