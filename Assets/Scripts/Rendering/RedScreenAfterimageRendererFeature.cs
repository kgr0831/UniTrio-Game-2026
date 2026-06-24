using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

/// <summary>
/// URP Renderer Feature: 화면 전체 붉은 잔상 (Radial Blur Afterimage).
/// PC_Renderer.asset에 추가 후 RedScreenAfterimageRendererFeature.Instance.Intensity (0~1) 로 제어.
/// </summary>
[System.Serializable]
public class RedScreenAfterimageRendererFeature : ScriptableRendererFeature
{
    // 정적 참조 (컨트롤러 접근용)
    public static RedScreenAfterimageRendererFeature Instance { get; private set; }

    [Range(0f, 1f)]
    public float Intensity = 0f;

    private RedAfterimagePass _pass;
    private Material          _material;

    // ── ScriptableRendererFeature 구현 ────────────────────────────────────

    public override void Create()
    {
        Instance  = this;
        _material = CoreUtils.CreateEngineMaterial("Hidden/RedScreenAfterimage");
        _pass     = new RedAfterimagePass(_material);
        
        // 포스트 프로세싱 직전에 적용
        _pass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_material);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (Intensity <= 0.001f) return;
        _pass.Setup(Intensity);
        renderer.EnqueuePass(_pass);
    }

    // ── Inner Pass ────────────────────────────────────────────────────────

    private class RedAfterimagePass : ScriptableRenderPass
    {
        private readonly Material _mat;
        private RTHandle          _tempRT;
        private static readonly int _intensityId = Shader.PropertyToID("_Intensity");

        public RedAfterimagePass(Material mat)
        {
            _mat = mat;
            profilingSampler = new ProfilingSampler("RedScreenAfterimage");
        }

        public void Setup(float intensity) => _mat.SetFloat(_intensityId, intensity);

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref _tempRT, desc, name: "_RedAfterimageTemp");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) { }

        // ── Render Graph 지원 ──────────────────────────────────────────────
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_mat == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            TextureHandle src = resourceData.activeColorTexture;
            
            TextureDesc desc = renderGraph.GetTextureDesc(src);
            desc.depthBufferBits = 0;
            TextureHandle tempRT  = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("RedAfterimagePass", out var passData, profilingSampler))
            {
                passData.material = _mat;
                passData.src      = src;

                builder.UseTexture(src, AccessFlags.Read);
                builder.SetRenderAttachment(tempRT, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("RedAfterimageApply", out var passData, profilingSampler))
            {
                passData.src = tempRT;
                builder.UseTexture(tempRT, AccessFlags.Read);
                builder.SetRenderAttachment(src, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), 0, false);
                });
            }
        }

        private class PassData
        {
            public Material      material;
            public TextureHandle src;
        }

        public void Dispose() => _tempRT?.Release();
    }
}
