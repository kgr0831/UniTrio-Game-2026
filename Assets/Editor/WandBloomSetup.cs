using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Global Volume에 Bloom VolumeProfile을 자동으로 생성·할당하는 에디터 유틸.
/// 메뉴: Tools > Setup Wand Bloom
/// </summary>
public static class WandBloomSetup
{
    [MenuItem("Tools/Setup Wand Bloom")]
    public static void Setup()
    {
        // 1. VolumeProfile 에셋 생성
        string profilePath = "Assets/Settings/BloomProfile.asset";
        System.IO.Directory.CreateDirectory("Assets/Settings");

        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }

        // 2. Bloom 오버라이드 추가 (없으면 추가, 있으면 재사용)
        if (!profile.TryGet<Bloom>(out Bloom bloom))
            bloom = profile.Add<Bloom>(false);

        bloom.active            = true;
        bloom.threshold.Override(0.8f);   // 이 밝기 이상의 픽셀이 번집니다
        bloom.intensity.Override(2f);     // 번짐 강도
        bloom.scatter.Override(0.7f);
        bloom.tint.Override(new Color(0.8f, 0.85f, 1f)); // 약간 파란빛 보정

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        // 3. 씬의 Global Volume에 프로필 할당
        Volume volume = Object.FindObjectOfType<Volume>();
        if (volume != null)
        {
            volume.sharedProfile = profile;
            volume.isGlobal      = true;
            EditorUtility.SetDirty(volume.gameObject);
            Debug.Log("[WandBloomSetup] Global Volume에 BloomProfile을 할당했습니다.");
        }
        else
        {
            Debug.LogWarning("[WandBloomSetup] 씬에서 Volume 컴포넌트를 찾지 못했습니다. Global Volume을 먼저 생성하세요.");
        }

        AssetDatabase.Refresh();
        Debug.Log("[WandBloomSetup] Bloom 설정 완료 — Threshold: 0.8, Intensity: 2.0");
    }
}
