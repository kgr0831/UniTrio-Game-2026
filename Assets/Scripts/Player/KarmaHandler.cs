using System;
using UnityEngine;

/// <summary>
/// 카르마 포인트를 관리하는 컴포넌트.
/// 플레이어 사망 시 포인트가 증가하고, 포인트에 비례해 피격 데미지가 증폭됩니다.
/// (1점당 10% 증폭 – DamageCalculator.GetKarmaMultiplier에 위임)
/// </summary>
public sealed class KarmaHandler : MonoBehaviour
{
    [Header("Karma Settings")]
    [Tooltip("카르마 최대값. 이 값을 초과하지 않습니다.")]
    [SerializeField] private int _maxKarma = 10;

    public int KarmaPoints { get; private set; } = 0;

    /// <summary>카르마 변경 시 발생. UI 업데이트 용도로 구독하세요.</summary>
    public event Action<int> OnKarmaChanged;

    /// <summary>사망 1회당 1포인트 증가 (기본값).</summary>
    public void AddKarma(int amount = 1)
    {
        KarmaPoints = Mathf.Min(KarmaPoints + amount, _maxKarma);
        OnKarmaChanged?.Invoke(KarmaPoints);
    }

    public void ResetKarma()
    {
        KarmaPoints = 0;
        OnKarmaChanged?.Invoke(KarmaPoints);
    }

    /// <summary>현재 카르마 포인트에 해당하는 데미지 배율을 반환합니다.</summary>
    public float GetMultiplier() => DamageCalculator.GetKarmaMultiplier(KarmaPoints);
}
