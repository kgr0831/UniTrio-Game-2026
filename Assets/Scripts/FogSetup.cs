using UnityEngine;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class FogSetup : MonoBehaviour
{
    [ContextMenu("Create Black PNG")] // 인스펙터에서 컴포넌트 우클릭 후 이 메뉴 클릭
    public void CreatePNG()
    {
        int size = 128; // 크기는 작아도 상관없음
        Texture2D tex = new Texture2D(size, size);

        // 전체를 검정색(알파 255)으로 채우기
        Color[] colors = new Color[size * size];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.black;
        }

        tex.SetPixels(colors);
        tex.Apply();

        // PNG로 인코딩
        byte[] bytes = tex.EncodeToPNG();
        
        // 경로 설정 (Assets 폴더 바로 아래)
        string path = Application.dataPath + "/Black.png";
        File.WriteAllBytes(path, bytes);

#if UNITY_EDITOR
        // 유니티가 파일을 인식하도록 새로고침
        AssetDatabase.Refresh();
        Debug.Log("형, Assets/Black.png 생성 완료했다!");
#endif
    }
}