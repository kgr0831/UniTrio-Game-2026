using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[AddComponentMenu("UI/Effects/UIGradient")]
public class UIGradient : BaseMeshEffect
{
    [Tooltip("그라데이션 위쪽(또는 오른쪽) 색상")]
    public Color colorTop = Color.white;
    [Tooltip("그라데이션 아래쪽(또는 왼쪽) 색상")]
    public Color colorBottom = Color.black;
    [Tooltip("가로 방향으로 그라데이션을 적용할지 여부")]
    public bool isHorizontal = true;

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;

        List<UIVertex> vertices = new List<UIVertex>();
        vh.GetUIVertexStream(vertices);

        if (vertices.Count == 0) return;

        Rect rect = GetComponent<RectTransform>().rect;
        float min = isHorizontal ? rect.xMin : rect.yMin;
        float max = isHorizontal ? rect.xMax : rect.yMax;
        float length = max - min;

        if (length == 0f) return;

        for (int i = 0; i < vh.currentVertCount; i++)
        {
            UIVertex vertex = new UIVertex();
            vh.PopulateUIVertex(ref vertex, i);
            
            float val = isHorizontal ? vertex.position.x : vertex.position.y;
            float t = (val - min) / length;
            
            vertex.color *= Color.Lerp(colorBottom, colorTop, t);
            vh.SetUIVertex(vertex, i);
        }
    }
}
