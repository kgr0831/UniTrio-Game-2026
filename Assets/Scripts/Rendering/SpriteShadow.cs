using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스프라이트와 동일한 모양의 실루엣 그림자를 발밑에 그립니다.
/// 모든 스프라이트가 같은 평면의 빌보드라 실제 섀도 맵핑이 불가능하므로,
/// 매 프레임 본체 스프라이트를 복제해 눕힌 검은 반투명 실루엣으로 연출합니다.
///
/// 그림자는 항상 '가장 강한 광원 하나'만 따릅니다:
///  - Directional Light(태양): 라이트 forward 방향으로 고정된 그림자 (낮)
///  - Point Light(화톳불 등): 광원 반대 방향, 가까울수록 길고 진함 (밤)
///  - 투사체처럼 빠르게 움직이는 광원(_maxLightSpeed 초과)은 무시 → 그림자가 휙휙 돌지 않음
///  주광원이 바뀔 때는 이전 그림자 페이드아웃 + 새 그림자 페이드인(크로스페이드)으로
///  부드럽게 교체됩니다 (렌더러 슬롯 최대 2개는 이 전환 순간에만 동시 표시).
///
/// 기준점은 스프라이트 '중앙 50% 폭의 불투명 바닥'(Physics Shape 기반)을 사용해
/// 지팡이 같은 돌출부에 영향받지 않고 발에 붙습니다. (스프라이트별 캐시)
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteShadow : MonoBehaviour
{
    [Header("기본 모양")]
    [Tooltip("광원이 충분히 높거나 멀 때 그림자 세로 비율")]
    [SerializeField, Range(0.1f, 1.5f)] private float _minSquash = 0.35f;
    [Tooltip("광원이 낮거나 아주 가까울 때 그림자 세로 비율")]
    [SerializeField, Range(0.1f, 1.5f)] private float _maxSquash = 0.8f;
    [Tooltip("최대 진하기")]
    [SerializeField, Range(0f, 1f)] private float _baseAlpha = 0.4f;

    [Header("광원 반응")]
    [Tooltip("이 광량 합에 도달하면 그림자가 최대 진하기가 됨 (한낮 태양 강도 기준)")]
    [SerializeField, Min(0.05f)] private float _fullShadowWeight = 0.6f;
    [Tooltip("광원 변화에 그림자가 따라가는 속도 (클수록 빠릿함)")]
    [SerializeField, Min(0.5f)] private float _smoothSpeed = 2.5f;
    [Tooltip("이 속도(유닛/초)보다 빨리 움직이는 광원(투사체 등)은 그림자에 영향 없음")]
    [SerializeField, Min(0f)] private float _maxLightSpeed = 3f;

    // ── 광원별 그림자 슬롯 ───────────────────────────────────────
    private class ShadowSlot
    {
        public Light          light;          // 추적 중인 광원 (파괴되면 null)
        public SpriteRenderer sr;
        public Transform      tr;
        public Vector2        dir = Vector2.down;
        public float          smoothedWeight;
        public float          smoothedLen;
        public float          targetWeight;
        public float          targetLen;
    }

    private struct LightContribution
    {
        public Light   light;
        public Vector2 dir;
        public float   weight;
        public float   lenFactor;
    }

    // ── 스프라이트별 '보이는 발바닥' 로컬 Y 캐시 ─────────────────
    private static readonly Dictionary<Sprite, float> FeetCache = new Dictionary<Sprite, float>();

    // ── 씬 라이트 캐시 (모든 인스턴스 공유, 10프레임마다 갱신) ──
    private static readonly List<Light> SceneLights = new List<Light>();
    private static int _nextLightRefreshFrame = -1;

    // ── 광원 이동 속도 추적 (투사체 광원 필터용, 프레임당 1회 갱신) ──
    private static readonly Dictionary<Light, Vector3> LightPrevPos = new Dictionary<Light, Vector3>();
    private static readonly Dictionary<Light, float>   LightSpeeds  = new Dictionary<Light, float>();
    private static int _lastVelocityFrame = -1;

    private SpriteRenderer _source;
    private readonly List<ShadowSlot>         _slots         = new List<ShadowSlot>(4);
    private readonly List<LightContribution>  _contributions = new List<LightContribution>(8);

    private float _smoothedFeetY;
    private bool  _feetInitialized;

    private void Awake()
    {
        _source = GetComponent<SpriteRenderer>();
    }

    private ShadowSlot CreateSlot()
    {
        var go = new GameObject("SpriteShadow");
        var tr = go.transform;
        tr.SetParent(transform, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.spriteSortPoint = _source.spriteSortPoint;
        // 머티리얼은 파이프라인 기본 스프라이트 머티리얼(언릿) 사용
        // → 그림자가 라이팅의 영향을 받지 않고 항상 일정한 검은 실루엣 유지

        var slot = new ShadowSlot { sr = sr, tr = tr };
        _slots.Add(slot);
        return slot;
    }

    private void LateUpdate()
    {
        var sprite = _source.sprite;
        if (sprite == null || !_source.enabled)
        {
            for (int i = 0; i < _slots.Count; i++)
                SetSlotVisible(_slots[i], false);
            return;
        }

        // 1) 광원별 기여도 수집 (강한 순으로 상위 _maxShadows개)
        CollectLightContributions();

        // 2) 기존 슬롯과 광원 매칭 (사라진 광원은 target 0으로 페이드아웃)
        for (int i = 0; i < _slots.Count; i++)
        {
            _slots[i].targetWeight = 0f;
        }
        for (int c = 0; c < _contributions.Count; c++)
        {
            var contrib = _contributions[c];
            ShadowSlot slot = null;

            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].light == contrib.light) { slot = _slots[i]; break; }

            if (slot == null)
            {
                // 비어있는(페이드아웃 끝난) 슬롯 재사용 → 없으면 생성
                // 슬롯은 최대 2개: 현재 주광원 + 교체 중 페이드아웃되는 이전 광원 (크로스페이드)
                for (int i = 0; i < _slots.Count; i++)
                    if (_slots[i].smoothedWeight < 0.01f && _slots[i].targetWeight <= 0f) { slot = _slots[i]; break; }
                if (slot == null && _slots.Count < 2)
                    slot = CreateSlot();
                if (slot == null) continue; // 슬롯 부족 → 약한 광원 무시

                slot.light          = contrib.light;
                slot.smoothedWeight = 0f; // 새 광원은 0에서 페이드인
                slot.smoothedLen    = contrib.lenFactor;
            }

            slot.targetWeight = contrib.weight;
            slot.targetLen    = contrib.lenFactor;
            slot.dir          = contrib.dir; // 방향은 광원 기하 그대로 (광원이 연속 이동하므로 스무딩 불필요)
        }

        // 3) 발 위치 (애니메이션 프레임 간 스무딩)
        float feetY = GetVisibleFeetY(sprite);
        float k = 1f - Mathf.Exp(-_smoothSpeed * Time.deltaTime);
        if (!_feetInitialized) { _smoothedFeetY = feetY; _feetInitialized = true; }
        else                   { _smoothedFeetY = Mathf.Lerp(_smoothedFeetY, feetY, k); }

        // 4) 슬롯별 광량 스무딩 → 총광량 계산
        float totalWeight = 0f;
        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            s.smoothedWeight = Mathf.Lerp(s.smoothedWeight, s.targetWeight, k);
            s.smoothedLen    = Mathf.Lerp(s.smoothedLen, s.targetLen, k);
            totalWeight     += s.smoothedWeight;
        }

        // 전체 진하기: 광량이 없으면(깊은 밤·광원 밖) 모두 사라짐
        float globalStrength = Mathf.Clamp01(totalWeight / _fullShadowWeight);

        // 5) 슬롯 렌더링 갱신
        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];

            // 개별 광량 기반 진하기 + 비중(w/W)에 따른 완만한 희석
            // (완전 비례 배분은 광원 2개만 돼도 그림자가 안 보일 만큼 옅어짐)
            float share    = totalWeight > 1e-4f ? s.smoothedWeight / totalWeight : 0f;
            float ownPower = Mathf.Clamp01(s.smoothedWeight / _fullShadowWeight);
            float alpha    = _baseAlpha * globalStrength * ownPower * (0.55f + 0.45f * share) * _source.color.a;

            if (alpha <= 0.01f)
            {
                SetSlotVisible(s, false);
                if (s.targetWeight <= 0f) s.light = null; // 페이드아웃 완료 → 슬롯 비움
                continue;
            }
            SetSlotVisible(s, true);

            // 본체 따라가기
            s.sr.sprite = sprite;
            s.sr.flipX  = _source.flipX;
            s.sr.flipY  = _source.flipY;
            s.sr.color  = new Color(0f, 0f, 0f, alpha);

            // 본체 바로 뒤에 그려지도록 정렬 동기화
            s.sr.sortingLayerID = _source.sortingLayerID;
            s.sr.sortingOrder   = _source.sortingOrder - 1;

            // 광원 위치에 따른 길이: 태양이 낮을수록 / 포인트 라이트가 가까울수록 길어짐
            float squash = Mathf.Lerp(_minSquash, _maxSquash, s.smoothedLen);

            // 발(불투명 픽셀 바닥)을 축으로, 광원 반대 방향으로 눕힘
            // Z축 명시 회전: FromToRotation은 정반대 방향 근처에서 축이 뒤집혀 급회전함
            float angleDeg = Mathf.Atan2(s.dir.x, -s.dir.y) * Mathf.Rad2Deg;
            var rot = Quaternion.Euler(0f, 0f, angleDeg);

            // 미러 후 그림자의 발 지점이 본체 발 지점과 일치하도록 위치 보정
            Vector3 anchor       = new Vector3(0f, _smoothedFeetY, 0f);
            Vector3 mirroredFeet = rot * new Vector3(0f, -squash * _smoothedFeetY, 0f);

            s.tr.localPosition = anchor - mirroredFeet;
            s.tr.localRotation = rot;
            s.tr.localScale    = new Vector3(1f, -squash, 1f);
        }
    }

    private static void SetSlotVisible(ShadowSlot slot, bool visible)
    {
        if (slot.sr.enabled != visible)
            slot.sr.enabled = visible;
    }

    // ── 광원별 기여도 수집 (강한 순 상위 _maxShadows개) ──────────
    private void CollectLightContributions()
    {
        RefreshSceneLights();
        UpdateLightVelocities();
        _contributions.Clear();

        Vector3 pos = transform.position;

        for (int i = 0; i < SceneLights.Count; i++)
        {
            var l = SceneLights[i];
            if (l == null || !l.enabled || !l.gameObject.activeInHierarchy) continue;
            if (l.transform.IsChildOf(transform)) continue; // 자기 자신에 달린 광원 제외 (플레이어 NightGlow 등)

            LightContribution c;
            c.light = l;

            if (l.type == LightType.Directional)
            {
                Vector3 f = l.transform.forward;
                Vector2 d = new Vector2(f.x, f.y);
                if (d.sqrMagnitude < 1e-4f) d = Vector2.down; // 화면 정면 광원이면 아래로

                c.dir       = d.normalized;
                c.weight    = l.intensity;
                // 태양 고도: forward.z가 클수록(화면 안쪽으로 향할수록) 높이 떠 있음 → 짧은 그림자
                c.lenFactor = 1f - Mathf.Clamp01(f.z);
            }
            else if (l.type == LightType.Point)
            {
                // 투사체처럼 빠르게 움직이는 광원은 그림자에 영향 없음 (방향 휙휙 도는 문제 방지)
                float speed;
                if (!LightSpeeds.TryGetValue(l, out speed) || speed > _maxLightSpeed) continue;

                Vector2 delta = new Vector2(pos.x - l.transform.position.x, pos.y - l.transform.position.y);
                float dist = delta.magnitude;
                if (dist >= l.range || dist < 0.05f) continue;

                float closeness = 1f - dist / l.range;
                c.dir       = delta / dist;
                c.weight    = l.intensity * closeness; // 거리 감쇠
                // 광원이 가까울수록(바닥에 가까운 빛) 그림자가 길어짐
                c.lenFactor = closeness;
            }
            else continue;

            if (c.weight < 0.02f) continue;
            _contributions.Add(c);
        }

        // 가장 강한 광원 '하나'만 그림자를 만듦 (다중 그림자 방지)
        // 주광원이 바뀌면 슬롯 크로스페이드로 부드럽게 교체됨
        _contributions.Sort((a, b) => b.weight.CompareTo(a.weight));
        if (_contributions.Count > 1)
            _contributions.RemoveRange(1, _contributions.Count - 1);
    }

    private static void RefreshSceneLights()
    {
        if (Time.frameCount < _nextLightRefreshFrame) return;
        _nextLightRefreshFrame = Time.frameCount + 10;

        SceneLights.Clear();
        SceneLights.AddRange(Object.FindObjectsByType<Light>(FindObjectsSortMode.None));

        // 파괴된 광원의 속도 기록 정리
        if (LightPrevPos.Count > SceneLights.Count * 2)
        {
            LightPrevPos.Clear();
            LightSpeeds.Clear();
        }
    }

    // 광원별 이동 속도 추정 (프레임당 1회, 모든 인스턴스 공유)
    private static void UpdateLightVelocities()
    {
        if (Time.frameCount == _lastVelocityFrame) return;
        _lastVelocityFrame = Time.frameCount;

        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        for (int i = 0; i < SceneLights.Count; i++)
        {
            var l = SceneLights[i];
            if (l == null) continue;

            Vector3 cur = l.transform.position;
            Vector3 prev;
            if (LightPrevPos.TryGetValue(l, out prev))
                LightSpeeds[l] = (cur - prev).magnitude / dt;
            // 처음 본 광원은 속도 기록 없음 → 다음 프레임부터 판정 (1프레임 지연)
            LightPrevPos[l] = cur;
        }
    }

    // ── 스프라이트의 '보이는 발바닥' 로컬 Y (투명 여백 제외) ─────
    private static float GetVisibleFeetY(Sprite sprite)
    {
        float feet;
        if (FeetCache.TryGetValue(sprite, out feet))
            return feet;

        feet = sprite.bounds.min.y; // 기본값: 렉트 바닥

        // 발 위치는 '중앙 영역(몸통)'의 최저점을 사용.
        // 지팡이·무기처럼 옆으로 뻗은 부분이 전체 최저점이면 몸통 아래에 틈이 생기므로 제외.
        // (그림자 위쪽이 본체와 겹쳐도 본체 뒤에 그려져 보이지 않음)
        float bandHalfWidth = sprite.bounds.size.x * 0.25f; // 중앙 50% 폭

        // 1순위: 피직스 셰이프(알파 외곽선) — 텍스처 Read/Write 없이도 동작
        int shapeCount = sprite.GetPhysicsShapeCount();
        if (shapeCount > 0)
        {
            float minYCenter = float.MaxValue;
            float minYAll    = float.MaxValue;
            var points = new List<Vector2>(16);
            for (int i = 0; i < shapeCount; i++)
            {
                sprite.GetPhysicsShape(i, points);
                for (int p = 0; p < points.Count; p++)
                {
                    if (points[p].y < minYAll) minYAll = points[p].y;
                    if (Mathf.Abs(points[p].x) <= bandHalfWidth && points[p].y < minYCenter)
                        minYCenter = points[p].y;
                }
            }
            float minY = minYCenter < float.MaxValue ? minYCenter : minYAll;
            if (minY < float.MaxValue)
            {
                FeetCache[sprite] = minY;
                return minY;
            }
        }

        // 2순위: 텍스처 알파 스캔 (Read/Write 가능할 때)
        var tex = sprite.texture;
        bool tightPacked = sprite.packed && sprite.packingMode != SpritePackingMode.Rectangle;

        if (tex != null && tex.isReadable && !tightPacked)
        {
            // 아래에서 위로 스캔해 첫 불투명 행을 찾음 (중앙 50% 폭만 검사)
            var rect = sprite.textureRect;
            int x0 = (int)rect.x, y0 = (int)rect.y;
            int w  = (int)rect.width, h = (int)rect.height;
            int xStart = w / 4, xEnd = w - w / 4;

            int foundRow = -1;
            for (int y = 0; y < h && foundRow < 0; y++)
                for (int x = xStart; x < xEnd; x += 2)
                    if (tex.GetPixel(x0 + x, y0 + y).a > 0.1f) { foundRow = y; break; }

            if (foundRow > 0)
                feet = sprite.bounds.min.y + (sprite.textureRectOffset.y + foundRow) / sprite.pixelsPerUnit;
        }
        else
        {
            // 3순위: 메시 정점으로 추정 (Tight 메시일 때 유효)
            var verts = sprite.vertices;
            if (verts != null && verts.Length > 0)
            {
                float minY = float.MaxValue;
                for (int i = 0; i < verts.Length; i++)
                    if (verts[i].y < minY) minY = verts[i].y;
                feet = minY;
            }
        }

        FeetCache[sprite] = feet;
        return feet;
    }
}
