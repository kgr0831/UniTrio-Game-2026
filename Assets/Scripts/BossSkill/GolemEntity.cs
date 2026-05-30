using System.Collections;
using UnityEngine;

/// <summary>
/// 골렘 보스의 피격 엔티티.
/// CharacterBase를 상속해 HealthSystem(HP)·StatSystem(방어력)을 사용하며, IDamageable로 무기 피해를 받는다.
///
/// 보스는 일반 몬스터와 달리 피격에 경직되지 않는다(흰색 점멸만, OnHit 미구독).
///
/// 사망 연출(3단계):
///   1) 내려찍기(Attack01) 공격 후 마지막 프레임으로 포즈 고정
///   2) BossDeathCharge 셰이더로 균열이 점점 커지며 푸른 에너지로 강하게 발광 +
///      주변까지 새어나오는 후광(AdditiveGlow)이 함께 커짐
///   3) 후광이 펑 터지며 ShatterEffect로 불규칙한 여러 조각으로 산산조각 → 제거
///
/// 피격 공식: max(1, RawDamage - Def) (CharacterBase 기본, 방어력은 StatSystem.TotalDef)
/// </summary>
[RequireComponent(typeof(BossAI))]
public class GolemEntity : CharacterBase
{
    [Header("Death Charge (균열·발광 차오름)")]
    [Tooltip("균열·발광이 차오르는 시간(초). 끝나면 폭발")]
    [SerializeField] private float _chargeDuration = 1.2f;
    [Tooltip("균열·후광 색 (푸른 에너지, HDR)")]
    [SerializeField] private Color _glowColor = new Color(0.3f, 0.7f, 1.4f, 1f);
    [Tooltip("균열 패턴 텍스처 (미지정 시 Resources/Textures/GroundCrack 자동 로드)")]
    [SerializeField] private Texture2D _crackTexture;

    [Header("Death Shatter (폭발 산산조각)")]
    [Tooltip("파편 시드 수(축당). 클수록 조각이 많고 잘게 부서짐")]
    [SerializeField] private int _shatterShards = 7;
    [SerializeField] private float _shatterDuration = 0.8f;
    [Tooltip("스프라이트 크기 대비 파편 비산 거리 비율(0.5≈절반 크기). 크게 잡으면 너무 멀리 흩어져 보임")]
    [SerializeField] private float _shatterScatterScale = 0.55f;
    [Tooltip("폭발 초반 조각이 띠는 푸른 발광 세기 (차오름과 연결)")]
    [SerializeField] private float _shatterGlowBoost = 2.5f;

    private bool _dying;

    // 후광용 공유 방사형 그라데이션 스프라이트
    private static Sprite _radialSprite;

    protected override void OnDeath()
    {
        if (_dying) return;
        _dying = true;

        // AI/물리 정지, 추가 피격 방지
        var ai = GetComponent<BossAI>();
        if (ai != null)
        {
            ai.AbortCurrentSkill(); // 진행 중이던 스킬의 인디케이터(원/박스)·파티클 정리
            ai.enabled = false;
        }

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;

        foreach (var col in GetComponentsInChildren<Collider2D>())
            col.enabled = false;

        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        var sr   = GetComponent<SpriteRenderer>();
        var anim = GetComponent<Animator>();

        // ── 1) 내려찍기 공격 후 마지막 프레임으로 포즈 고정 ──
        // Attack01 클립이 트랜스폼 스케일을 애니메이트하면 사망 시 보스가 갑자기 커진다.
        // → 프레임을 한 번 적용해 스프라이트만 얻은 뒤 Animator를 끄고 스케일을 복원한다.
        Vector3 originalScale = transform.localScale;
        if (anim != null)
        {
            anim.speed = 0f;
            anim.Play("Attack01", 0, 0.99f);
            anim.Update(0f);
            anim.enabled = false;
        }
        transform.localScale = originalScale;

        Debug.Log("[Golem] 보스 사망 → 균열 차오름 시작");

        // 후광(주변까지 새어나오는 빛) 생성
        Vector3 center = (sr != null) ? sr.bounds.center : transform.position;
        float bossSize = (sr != null) ? Mathf.Max(sr.bounds.size.x, sr.bounds.size.y) : 4f;
        Material glowMat;
        Transform glow = CreateGlowHalo(center, out glowMat);

        // ── 2) 균열 + 강한 푸른 발광 차오름 ──
        Material chargeMat = null;
        if (sr != null && sr.sprite != null)
        {
            Shader shader = Shader.Find("Custom/BossDeathCharge");
            if (shader != null)
            {
                if (_crackTexture == null)
                    _crackTexture = Resources.Load<Texture2D>("Textures/GroundCrack");

                chargeMat = new Material(shader);
                chargeMat.SetColor("_GlowColor", _glowColor);
                if (_crackTexture != null) chargeMat.SetTexture("_CrackTex", _crackTexture);

                Sprite sp = sr.sprite;
                if (sp.texture != null)
                {
                    Rect tr = sp.textureRect;
                    float tw = sp.texture.width, th = sp.texture.height;
                    chargeMat.SetVector("_SpriteRect", new Vector4(tr.x / tw, tr.y / th, tr.width / tw, tr.height / th));
                }
                sr.material = chargeMat;
            }
        }

        float endDiameter = bossSize;
        float endIntensity = 0f;
        float t = 0f;
        while (t < _chargeDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / _chargeDuration);

            if (chargeMat != null) chargeMat.SetFloat("_Progress", p);

            // 후광: 빛이 점점 커지고 밝아짐 (맥동) — 폭발과 연속되도록 값 보관
            if (glow != null)
            {
                float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * 22f);
                endIntensity = (0.4f + 2.2f * p * p) * pulse;
                endDiameter = bossSize * (1.0f + 1.3f * p);
                glow.localScale = Vector3.one * endDiameter;
                glowMat.SetColor("_Color", _glowColor * endIntensity);
            }

            // 차오를수록 카메라 흔들림 강해짐
            CameraShakeController.Instance?.Shake(0.05f, 0.05f + p * 0.25f);
            yield return null;
        }

        // ── 3) 펑! 폭발 + 불규칙 산산조각 ──
        CameraShakeController.Instance?.Shake(0.45f, 1.1f);
        HitStopManager.Instance?.TriggerHitStop(0.1f);
        ScreenFlashEffect.Instance?.Flash(new Color(0.45f, 0.72f, 1f, 0.7f), 0.2f);

        if (sr != null && sr.sprite != null)
        {
            // 폭발 조각이 차오름의 푸른 과열을 이어받아 빛난 채 흩어지다 식음 → 두 연출이 연결됨
            ShatterEffect.Spawn(sr, _shatterDuration, _shatterShards, _shatterScatterScale,
                rotateAmount: 4f, gravity: 1.5f, glowColor: _glowColor, glowBoost: _shatterGlowBoost);
            sr.enabled = false;
        }

        Debug.Log("[Golem] 보스 폭발");

        // 후광 버스트 후 페이드 (차오름 끝값에서 연속적으로 이어짐)
        if (glow != null) StartCoroutine(GlowBurst(glow, glowMat, endDiameter, endIntensity));

        Destroy(gameObject, _shatterDuration + 0.1f);
    }

    // 폭발 순간 후광이 끝값에서 한 번 더 밝게 번졌다가 사그라든다(연속적).
    private IEnumerator GlowBurst(Transform glow, Material mat, float startDiameter, float startIntensity)
    {
        float dur = _shatterDuration;
        float peak = startIntensity + 3.5f; // 끝값 위로 한 번 더 번쩍
        float t = 0f;
        while (t < dur && glow != null)
        {
            t += Time.deltaTime;
            float k = t / dur;                                  // 0→1
            float intensity = Mathf.Lerp(peak, 0f, k);          // 번쩍 후 사그라듦
            float diameter = Mathf.Lerp(startDiameter, startDiameter * 1.6f, k);
            glow.localScale = Vector3.one * diameter;
            mat.SetColor("_Color", _glowColor * intensity);
            yield return null;
        }
        if (glow != null) Destroy(glow.gameObject);
    }

    // 가산 후광 오브젝트 생성 (방사형 그라데이션 + AdditiveGlow 셰이더)
    private Transform CreateGlowHalo(Vector3 worldCenter, out Material mat)
    {
        EnsureRadialSprite();

        var go = new GameObject("BossDeathGlow");
        go.transform.position = worldCenter;

        var gsr = go.AddComponent<SpriteRenderer>();
        gsr.sprite = _radialSprite;
        gsr.sortingLayerID = 0;
        gsr.sortingOrder = 9; // 보스 스프라이트(10) 바로 뒤 — 실루엣 주변으로 빛이 번지게

        Shader glowShader = Shader.Find("Custom/AdditiveGlow");
        mat = new Material(glowShader != null ? glowShader : Shader.Find("Sprites/Default"));
        mat.SetColor("_Color", _glowColor * 0.3f);
        gsr.material = mat;

        return go.transform;
    }

    // 부드러운 방사형(중심 1 → 가장자리 0) 알파 그라데이션 스프라이트 (1회 생성·공유)
    private static void EnsureRadialSprite()
    {
        if (_radialSprite != null) return;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var px = new Color[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - r) / r;
                float dy = (y + 0.5f - r) / r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);          // 0(중심)~1(가장자리)
                float a = Mathf.Clamp01(1f - d);
                a = a * a;                                         // 부드러운 falloff
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, true);

        // pixelsPerUnit = size → 스케일 1일 때 스프라이트가 월드 1유닛 → localScale = 지름
        _radialSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
