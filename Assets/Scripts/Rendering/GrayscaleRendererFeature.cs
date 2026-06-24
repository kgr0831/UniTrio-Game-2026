using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

/// <summary>
/// URP Renderer Feature: 화면 전체 흑백(그레이스케일).
/// PC_Renderer.asset에 추가 후 GrayscaleRendererFeature.Instance.Intensity (0~1) 로 제어.
/// 회피 저스트(위치타임) 슬로우모션 동안 화면을 흑백으로 만드는 데 사용.
/// </summary>
[System.Serializable]
public class GrayscaleRendererFeature : ScriptableRendererFeature
{
    // 런타임에서 Intensity를 조작하기 위한 정적 참조
    public static GrayscaleRendererFeature Instance { get; private set; }

    [Range(0f, 1f)]
    public float Intensity = 0f;

    [Header("방사형 (중심에서 퍼짐)")]
    public Vector2 Center   = new Vector2(0.5f, 0.5f); // 스크린 UV(0~1). 보통 플레이어 위치
    public float   Radius   = 10f;                     // UV 기준 반경. 크게 잡으면 화면 전체 흑백
    public float   Softness = 0.06f;

    [Header("포커스 (컬러 유지 영역 — 적 강조)")]
    public Vector2 FocusCenter   = new Vector2(0.5f, 0.5f); // 컬러로 남길 중심(스크린 UV)
    public float   FocusRadius   = 0f;                       // 0이면 비활성
    public float   FocusSoftness = 0.04f;

    private GrayscalePass _pass;
    private Material       _material;

    // ── ScriptableRendererFeature 구현 ────────────────────────────────────

    public override void Create()
    {
        Instance  = this;
        _material = CoreUtils.CreateEngineMaterial("Hidden/ScreenGrayscale");
        _pass     = new GrayscalePass(_material);
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
        // 오버레이 카메라(붉은 아웃라인 전용)는 흑백을 적용하지 않는다 → 아웃라인이 컬러로 남는다.
        if (renderingData.cameraData.renderType == CameraRenderType.Overlay) return;
        _pass.Setup(Intensity, Center, Radius, Softness, FocusCenter, FocusRadius, FocusSoftness);
        renderer.EnqueuePass(_pass);
    }

    // ── Inner Pass ────────────────────────────────────────────────────────

    private class GrayscalePass : ScriptableRenderPass
    {
        private readonly Material _mat;
        private RTHandle          _tempRT;
        private static readonly int _intensityId = Shader.PropertyToID("_Intensity");
        private static readonly int _centerId    = Shader.PropertyToID("_Center");
        private static readonly int _radiusId    = Shader.PropertyToID("_Radius");
        private static readonly int _softnessId  = Shader.PropertyToID("_Softness");
        private static readonly int _aspectId    = Shader.PropertyToID("_Aspect");
        private static readonly int _focusCenterId   = Shader.PropertyToID("_FocusCenter");
        private static readonly int _focusRadiusId   = Shader.PropertyToID("_FocusRadius");
        private static readonly int _focusSoftnessId = Shader.PropertyToID("_FocusSoftness");

        public GrayscalePass(Material mat)
        {
            _mat = mat;
            profilingSampler = new ProfilingSampler("Grayscale");
        }

        public void Setup(float intensity, Vector2 center, float radius, float softness,
                          Vector2 focusCenter, float focusRadius, float focusSoftness)
        {
            _mat.SetFloat(_intensityId, intensity);
            _mat.SetVector(_centerId, new Vector4(center.x, center.y, 0f, 0f));
            _mat.SetFloat(_radiusId, radius);
            _mat.SetFloat(_softnessId, softness);
            _mat.SetVector(_focusCenterId, new Vector4(focusCenter.x, focusCenter.y, 0f, 0f));
            _mat.SetFloat(_focusRadiusId, focusRadius);
            _mat.SetFloat(_focusSoftnessId, focusSoftness);
            float aspect = (Screen.height > 0) ? (float)Screen.width / Screen.height : 1.7777f;
            _mat.SetFloat(_aspectId, aspect);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            // 매 프레임 재사용: 크기가 달라졌을 때만 재할당
            RenderingUtils.ReAllocateIfNeeded(ref _tempRT, desc, name: "_GrayscaleTemp");
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
            TextureHandle tempRT = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("GrayscalePass", out var passData, profilingSampler))
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
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("GrayscaleApply", out var passData, profilingSampler))
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
