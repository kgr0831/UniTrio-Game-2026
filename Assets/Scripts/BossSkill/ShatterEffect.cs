using UnityEngine;

// 스프라이트를 세분화 메시(셀마다 정점 분리)로 만들어 RockShatter 쉐이더로 비산시키는 일회성 효과.
// ShatterEffect.Spawn(원본 SpriteRenderer)로 생성하면 스스로 _Progress를 0→1로 애니메이트 후 파괴된다.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ShatterEffect : MonoBehaviour
{
    private static readonly int ProgressID = Shader.PropertyToID("_Progress");
    private static readonly int ColorID = Shader.PropertyToID("_Color");

    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock mpb;
    private float duration;
    private float elapsed;

    // 원본 SpriteRenderer로부터 비산 효과를 생성한다.
    // scatterScale: 스프라이트 크기에 대한 비산 거리 비율(0.5 ≈ 스프라이트 절반 크기만큼) → 스케일 무관하게 일관.
    // glowColor/glowBoost: 폭발 초반 조각이 띠는 발광(차오름 연출과 연결). 기본값은 발광 없음.
    public static ShatterEffect Spawn(SpriteRenderer src, float duration = 0.5f, int shards = 5,
        float scatterScale = 0.5f, float rotateAmount = 4f, float gravity = 1.5f,
        Color glowColor = default, float glowBoost = 0f)
    {
        if (src == null || src.sprite == null) return null;

        var go = new GameObject("RockShatter");
        var t = go.transform;
        t.position = src.transform.position;
        t.rotation = src.transform.rotation;
        t.localScale = src.transform.lossyScale;

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();

        mf.sharedMesh = BuildShatterMesh(src.sprite, shards);

        Shader shader = Shader.Find("Custom/RockShatter");
        var mat = new Material(shader)
        {
            mainTexture = src.sprite.texture
        };
        // 오브젝트 공간 비산량 = 비율 × 스프라이트 로컬 크기 → 트랜스폼 스케일 곱해져도 화면상 일관
        float localMax = Mathf.Max(src.sprite.bounds.size.x, src.sprite.bounds.size.y);
        mat.SetFloat("_ScatterDistance", scatterScale * localMax);
        mat.SetFloat("_RotateAmount", rotateAmount);
        mat.SetFloat("_Gravity", gravity);
        mat.SetColor("_GlowColor", glowColor);
        mat.SetFloat("_GlowBoost", glowBoost);
        mr.material = mat;

        // 원본 스프라이트의 정렬 정보 복사 (URP가 sortingOrder/Layer를 따른다)
        mr.sortingLayerID = src.sortingLayerID;
        mr.sortingOrder = src.sortingOrder;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        var fx = go.AddComponent<ShatterEffect>();
        fx.meshRenderer = mr;
        fx.duration = Mathf.Max(0.01f, duration);
        fx.mpb = new MaterialPropertyBlock();
        fx.mpb.SetColor(ColorID, src.color);
        fx.meshRenderer.SetPropertyBlock(fx.mpb);

        return fx;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / duration);

        meshRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(ProgressID, progress);
        meshRenderer.SetPropertyBlock(mpb);

        if (progress >= 1f)
            Destroy(gameObject);
    }

    void OnDestroy()
    {
        // 런타임 생성한 머티리얼/메시 정리 (누수 방지)
        if (meshRenderer != null && meshRenderer.material != null)
            Destroy(meshRenderer.material);
        var mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
            Destroy(mf.sharedMesh);
    }

    // 스프라이트 영역을 불규칙 파편(지터드 보로노이)으로 분할한 메시 생성.
    // - 미세 격자(res×res)로 잘게 나누고, 지터된 시드(shards×shards)들 중 가장 가까운 시드에 각 미세 셀을 귀속.
    // - 같은 시드의 셀들은 동일한 cellCenter(TEXCOORD1)를 공유 → RockShatter 셰이더에서 한 덩어리(불규칙 파편)로 비산.
    // → 정사각형이 아닌 랜덤한 형태의 조각으로 부서진다.
    private static Mesh BuildShatterMesh(Sprite sprite, int shards)
    {
        shards = Mathf.Max(2, shards);
        int res = Mathf.Clamp(shards * 3, 8, 48); // 미세 격자 해상도(파편 경계의 불규칙함 정도)

        // 스프라이트의 오브젝트 공간 사각형 (피벗 기준, SpriteRenderer 렌더와 동일)
        Bounds b = sprite.bounds;
        Vector2 min = b.min;
        Vector2 size = b.size;

        // 텍스처 UV 사각형 (아틀라스/단일 텍스처 모두 대응)
        Rect tr = sprite.textureRect;
        float texW = sprite.texture.width;
        float texH = sprite.texture.height;
        Vector2 uvMin = new Vector2(tr.x / texW, tr.y / texH);
        Vector2 uvSize = new Vector2(tr.width / texW, tr.height / texH);

        // ── 지터된 시드 생성 (0~1 정규화 좌표 + 대응 오브젝트 공간 중심) ──
        int seedN = shards;
        int seedCount = seedN * seedN;
        var seedN01 = new Vector2[seedCount];          // 0~1 좌표 (귀속 판정용)
        var seedCenterObj = new Vector2[seedCount];    // 오브젝트 공간 중심 (cellCenter)
        float seedInv = 1f / seedN;
        for (int sy = 0; sy < seedN; sy++)
        {
            for (int sx = 0; sx < seedN; sx++)
            {
                int si = sy * seedN + sx;
                // 격자 중심에서 ±0.5칸 범위로 지터 (결정론적 해시)
                float jx = (Hash01(sx * 13 + sy * 71 + 1) - 0.5f) * 0.9f;
                float jy = (Hash01(sx * 53 + sy * 31 + 7) - 0.5f) * 0.9f;
                float nx = Mathf.Clamp01((sx + 0.5f + jx) * seedInv);
                float ny = Mathf.Clamp01((sy + 0.5f + jy) * seedInv);
                seedN01[si] = new Vector2(nx, ny);
                seedCenterObj[si] = new Vector2(min.x + size.x * nx, min.y + size.y * ny);
            }
        }

        // ── 지터된 격자 정점(코너) 0~1 좌표 미리 계산 ──
        // 내부 코너만 흔들어 파편 경계를 들쭉날쭉하게(사각형 탈피), 외곽 코너는 고정(실루엣 유지).
        float inv = 1f / res;
        float jitterAmt = 0.42f * inv; // 한 칸의 ±42%까지
        var lattice = new Vector2[(res + 1) * (res + 1)];
        for (int gy = 0; gy <= res; gy++)
        {
            for (int gx = 0; gx <= res; gx++)
            {
                float nx = gx * inv;
                float ny = gy * inv;
                bool border = (gx == 0 || gy == 0 || gx == res || gy == res);
                if (!border)
                {
                    nx += (Hash01(gx * 1973 + gy * 9277 + 3) - 0.5f) * 2f * jitterAmt;
                    ny += (Hash01(gx * 6131 + gy * 1543 + 11) - 0.5f) * 2f * jitterAmt;
                }
                lattice[gy * (res + 1) + gx] = new Vector2(nx, ny);
            }
        }

        int cellCount = res * res;
        var vertices = new Vector3[cellCount * 4];
        var uvs = new Vector2[cellCount * 4];
        var centers = new Vector2[cellCount * 4]; // TEXCOORD1: 소속 파편(시드) 중심
        var tris = new int[cellCount * 6];

        int vi = 0, ti = 0;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                // 이 미세 셀의 중심(0~1)에서 가장 가까운 시드를 찾음 (보로노이 귀속)
                float cnx = (x + 0.5f) * inv;
                float cny = (y + 0.5f) * inv;
                int nearest = 0;
                float best = float.MaxValue;
                for (int s = 0; s < seedCount; s++)
                {
                    float dx = seedN01[s].x - cnx;
                    float dy = seedN01[s].y - cny;
                    float d = dx * dx + dy * dy;
                    if (d < best) { best = d; nearest = s; }
                }
                Vector2 shardCenter = seedCenterObj[nearest];

                // 지터된 코너 좌표(0~1)
                Vector2 g00 = lattice[y * (res + 1) + x];
                Vector2 g10 = lattice[y * (res + 1) + (x + 1)];
                Vector2 g01 = lattice[(y + 1) * (res + 1) + x];
                Vector2 g11 = lattice[(y + 1) * (res + 1) + (x + 1)];

                // 오브젝트 공간 위치
                Vector3 p00 = new Vector3(min.x + size.x * g00.x, min.y + size.y * g00.y, 0f);
                Vector3 p10 = new Vector3(min.x + size.x * g10.x, min.y + size.y * g10.y, 0f);
                Vector3 p01 = new Vector3(min.x + size.x * g01.x, min.y + size.y * g01.y, 0f);
                Vector3 p11 = new Vector3(min.x + size.x * g11.x, min.y + size.y * g11.y, 0f);

                // 대응 UV (위치와 동일 좌표 → 텍스처 정합 유지)
                Vector2 u00 = new Vector2(uvMin.x + uvSize.x * g00.x, uvMin.y + uvSize.y * g00.y);
                Vector2 u10 = new Vector2(uvMin.x + uvSize.x * g10.x, uvMin.y + uvSize.y * g10.y);
                Vector2 u01 = new Vector2(uvMin.x + uvSize.x * g01.x, uvMin.y + uvSize.y * g01.y);
                Vector2 u11 = new Vector2(uvMin.x + uvSize.x * g11.x, uvMin.y + uvSize.y * g11.y);

                int baseV = vi;
                vertices[vi] = p00; uvs[vi] = u00; centers[vi] = shardCenter; vi++;
                vertices[vi] = p10; uvs[vi] = u10; centers[vi] = shardCenter; vi++;
                vertices[vi] = p01; uvs[vi] = u01; centers[vi] = shardCenter; vi++;
                vertices[vi] = p11; uvs[vi] = u11; centers[vi] = shardCenter; vi++;

                tris[ti++] = baseV + 0;
                tris[ti++] = baseV + 2;
                tris[ti++] = baseV + 1;
                tris[ti++] = baseV + 1;
                tris[ti++] = baseV + 2;
                tris[ti++] = baseV + 3;
            }
        }

        var mesh = new Mesh { name = "ShatterMesh" };
        mesh.indexFormat = (vertices.Length > 65535)
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.SetUVs(1, centers); // TEXCOORD1 → 파편 중심
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }

    // 결정론적 0~1 난수 (정수 시드)
    private static float Hash01(int n)
    {
        return Mathf.Abs(Mathf.Sin(n * 12.9898f) * 43758.5453f) % 1f;
    }
}
