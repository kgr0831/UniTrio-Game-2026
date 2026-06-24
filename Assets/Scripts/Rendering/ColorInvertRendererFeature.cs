using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

/// <summary>
/// URP Renderer Feature: 화면 전체 색상 반전.
/// PC_Renderer.asset에 추가 후 ColorInvertRendererFeature.Instance.Intensity (0~1) 로 제어.
/// </summary>
[System.Serializable]
public class ColorInvertRendererFeature : ScriptableRendererFeature
{
    // 런타임에서 Intensity를 조작하기 위한 정적 참조
    public static ColorInvertRendererFeature Instance { get; private set; }

    [Range(0f, 1f)]
    public float Intensity = 0f;

    private ColorInvertPass _pass;
    private Material        _material;

    // ── ScriptableRendererFeature 구현 ────────────────────────────────────

    public override void Create()
    {
        Instance  = this;
        _material = CoreUtils.CreateEngineMaterial("Hidden/ScreenColorInvert");
        _pass     = new ColorInvertPass(_material);
        // 포스트 프로세싱 직전에 적용해 CA 등과 자연스럽게 합성
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

    private class ColorInvertPass : ScriptableRenderPass
    {
        private readonly Material _mat;
        private RTHandle          _tempRT;
        private static readonly int _intensityId = Shader.PropertyToID("_Intensity");

        public ColorInvertPass(Material mat)
        {
            _mat = mat;
            profilingSampler = new ProfilingSampler("ColorInvert");
        }

        public void Setup(float intensity) => _mat.SetFloat(_intensityId, intensity);

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            // 매 프레임 재사용: 크기가 달라졌을 때만 재할당
            RenderingUtils.ReAllocateIfNeeded(ref _tempRT, desc, name: "_ColorInvertTemp");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) { }

        // ── RenderGraph 지원 (URP 17+ 및 Unity 2023.1+) ────────────────────
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_mat == null) return;

            // 유니버설 리소스에서 가용한 카메라 색상 리소스를 가져옵니다.
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            TextureHandle src = resourceData.activeColorTexture;
            
            // 임시 텍스처(Blit 전용) 설명자 설정
            TextureDesc desc = renderGraph.GetTextureDesc(src);
            desc.depthBufferBits = 0;
            TextureHandle tempRT  = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("ColorInvertPass", out var passData, profilingSampler))
            {
                passData.material = _mat;
                passData.src      = src;

                // src는 읽기 전용, tempRT에 결과 쓰기
                builder.UseTexture(src, AccessFlags.Read);
                builder.SetRenderAttachment(tempRT, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    // Blitter를 사용하여 src -> tempRT로 블릿 (Material 적용)
                    Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            // 결과를 다시 src로 되돌리는 패스
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("ColorInvertApply", out var passData, profilingSampler))
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
