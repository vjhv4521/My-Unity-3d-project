using UnityEngine;

namespace ZombiePrototype
{
    /// <summary>
    /// 命中闪烁反馈。
    ///
    /// 用法：
    /// 把这个脚本挂到会被攻击的对象上，或者挂到它的父物体上。
    /// 当武器命中这个对象时，调用 PlayFlash()，模型会短暂变成指定颜色。
    ///
    /// 这里使用 MaterialPropertyBlock，而不是直接修改 renderer.material。
    /// 好处是：
    /// 1. 不会复制材质，性能更好。
    /// 2. 不会永久改坏原来的贴图和材质。
    /// 3. 同一个材质可以被很多僵尸共享，每只僵尸又能单独闪红。
    /// </summary>
    public sealed class HitFlashFeedback : MonoBehaviour
    {
        [Header("Flash")]

        // 被击中时闪烁的颜色。
        [SerializeField] private Color flashColor = new Color(1f, 0.08f, 0.04f, 1f);

        // 闪烁持续时间。
        // 0.08 到 0.15 比较适合枪械命中反馈。
        [SerializeField] private float flashDuration = 0.1f;

        // 需要被改色的 Renderer。
        // 如果不手动拖，Awake 会自动查找当前物体和子物体上的所有 Renderer。
        [SerializeField] private Renderer[] renderers;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        private float flashEndTime;
        private bool flashing;

        private void Awake()
        {
            CacheRenderersIfNeeded();
        }

        private void OnEnable()
        {
            // 对象池复用时，确保上一轮闪红不会残留到下一只僵尸。
            StopFlash();
        }

        private void Update()
        {
            if (!flashing || Time.time < flashEndTime)
            {
                return;
            }

            StopFlash();
        }

        /// <summary>
        /// 播放一次受击闪烁。
        /// </summary>
        public void PlayFlash()
        {
            CacheRenderersIfNeeded();

            flashing = true;
            flashEndTime = Time.time + flashDuration;

            foreach (Renderer targetRenderer in renderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.GetPropertyBlock(propertyBlock);

                // URP Lit 通常使用 _BaseColor。
                // Built-in Standard 通常使用 _Color。
                // 两个都设置，可以兼容更多材质。
                propertyBlock.SetColor(BaseColorId, flashColor);
                propertyBlock.SetColor(ColorId, flashColor);

                targetRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        /// <summary>
        /// 停止闪烁，恢复材质原本显示。
        /// </summary>
        private void StopFlash()
        {
            flashing = false;

            if (renderers == null)
            {
                return;
            }

            foreach (Renderer targetRenderer in renderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                // 传入 null 会清掉当前 Renderer 上的 MaterialPropertyBlock。
                // 清掉之后，Renderer 会重新使用材质本身的颜色和贴图。
                targetRenderer.SetPropertyBlock(null);
            }
        }

        private void CacheRenderersIfNeeded()
        {
            if (renderers != null && renderers.Length > 0)
            {
                return;
            }

            renderers = GetComponentsInChildren<Renderer>(true);
        }
    }
}
