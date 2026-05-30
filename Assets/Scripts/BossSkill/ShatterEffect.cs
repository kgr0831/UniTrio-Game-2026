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
    public static ShatterEffect Spawn(SpriteRenderer src, float duration = 0.5f, int cells = 5,
        float scatterDistance = 1.2f, float rotateAmount = 4f, float gravity = 1.5f)
    {
        if (src == null || src.sprite == null) return null;

        var go = new GameObject("RockShatter");
        var t = go.transform;
        t.position = src.transform.position;
        t.rotation = src.transform.rotation;
        t.localScale = src.transform.lossyScale;

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();

        mf.sharedMesh = BuildShatterMesh(src.sprite, cells);

        Shader shader = Shader.Find("Custom/RockShatter");
        var mat = new Material(shader)
        {
            mainTexture = src.sprite.texture
        };
        mat.SetFloat("_ScatterDistance", scatterDistance);
        mat.SetFloat("_RotateAmount", rotateAmount);
        mat.SetFloat("_Gravity", gravity);
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

    // 스프라이트 영역을 cells×cells 그리드로 분할, 각 셀을 독립 정점(강체)으로 구성한 메시 생성.
    private static Mesh BuildShatterMesh(Sprite sprite, int cells)
    {
        cells = Mathf.Max(1, cells);

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

        int cellCount = cells * cells;
        var vertices = new Vector3[cellCount * 4];
        var uvs = new Vector2[cellCount * 4];
        var centers = new Vector2[cellCount * 4]; // TEXCOORD1: 셀 중심
        var tris = new int[cellCount * 6];

        float inv = 1f / cells;
        int vi = 0, ti = 0;

        for (int y = 0; y < cells; y++)
        {
            for (int x = 0; x < cells; x++)
            {
                float fx0 = x * inv, fx1 = (x + 1) * inv;
                float fy0 = y * inv, fy1 = (y + 1) * inv;

                // 셀 4코너 (오브젝트 공간)
                Vector3 p00 = new Vector3(min.x + size.x * fx0, min.y + size.y * fy0, 0f);
                Vector3 p10 = new Vector3(min.x + size.x * fx1, min.y + size.y * fy0, 0f);
                Vector3 p01 = new Vector3(min.x + size.x * fx0, min.y + size.y * fy1, 0f);
                Vector3 p11 = new Vector3(min.x + size.x * fx1, min.y + size.y * fy1, 0f);

                Vector2 center = new Vector2((p00.x + p11.x) * 0.5f, (p00.y + p11.y) * 0.5f);

                // 대응 UV
                Vector2 u00 = new Vector2(uvMin.x + uvSize.x * fx0, uvMin.y + uvSize.y * fy0);
                Vector2 u10 = new Vector2(uvMin.x + uvSize.x * fx1, uvMin.y + uvSize.y * fy0);
                Vector2 u01 = new Vector2(uvMin.x + uvSize.x * fx0, uvMin.y + uvSize.y * fy1);
                Vector2 u11 = new Vector2(uvMin.x + uvSize.x * fx1, uvMin.y + uvSize.y * fy1);

                int baseV = vi;
                vertices[vi] = p00; uvs[vi] = u00; centers[vi] = center; vi++;
                vertices[vi] = p10; uvs[vi] = u10; centers[vi] = center; vi++;
                vertices[vi] = p01; uvs[vi] = u01; centers[vi] = center; vi++;
                vertices[vi] = p11; uvs[vi] = u11; centers[vi] = center; vi++;

                tris[ti++] = baseV + 0;
                tris[ti++] = baseV + 2;
                tris[ti++] = baseV + 1;
                tris[ti++] = baseV + 1;
                tris[ti++] = baseV + 2;
                tris[ti++] = baseV + 3;
            }
        }

        var mesh = new Mesh { name = "ShatterMesh" };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.SetUVs(1, centers); // TEXCOORD1 → 셀 중심
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }
}
