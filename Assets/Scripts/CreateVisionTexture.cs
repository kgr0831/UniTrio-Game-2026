#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public class CreateVisionTexture : EditorWindow
{
    [MenuItem("Tools/Create Vision Texture")]
    public static void CreateTexture()
    {
        int size = 256; // 텍스처 크기 (충분함)
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] colors = new Color[size * size];

        float center = size / 2f;
        float radius = size / 2f;
        float blurFactor = 0.5f; // 부드러움 정도 (0.1 ~ 0.9 조절 가능)

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                
                // 원의 외곽으로 갈수록 부드럽게 투명해지는 알파값 계산
                float alpha = Mathf.Clamp01((radius - dist) / (radius * blurFactor));
                
                // 핵심: 흰색 원형이며, 알파값만 조절
                colors[y * size + x] = new Color(1, 1, 1, alpha); 
            }
        }

        texture.SetPixels(colors);
        texture.Apply();

        // PNG 파일로 저장
        byte[] bytes = texture.EncodeToPNG();
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(path)) path = "Assets";
        string filePath = Path.Combine(path, "VisionHole.png");
        File.WriteAllBytes(filePath, bytes);
        
        AssetDatabase.Refresh();
        Debug.Log("비전 텍스처 생성 완료: " + filePath);
    }
}
#endif