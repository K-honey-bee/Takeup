using UnityEngine;
namespace TakeUp.Prototype
{
    [CreateAssetMenu(menuName = "TakeUp/Bull Boss Settings")]
    public sealed class BullBossSettings : ScriptableObject
    {
        [Min(1)] public float health = 2500;
        [Min(.1f)] public float bodyRadius = 1;
        [Header("앞발 구르기 / 전방 원형 충격파")]
        [Min(.2f)] public float stompWarning = 1.1f;
        [Range(30, 240)] public float waveAngle = 180;
        [Min(1)] public float waveRadius = 12;
        [Min(1)] public float waveSpeed = 9;
        [Min(.1f)] public float waveThickness = .45f;
        [Min(0)] public float waveDamage = 22;
        [Header("직선 돌진 / 방향은 예고 시작 시 고정")]
        [Min(.2f)] public float chargeWarning = 1.2f;
        [Min(1)] public float chargeSpeed = 16;
        [Min(.5f)] public float chargeTimeout = 3;
        [Min(0)] public float chargeDamage = 28;
        [Header("벽 충돌 / 기절")]
        [Min(.2f)] public float stunDuration = 2.5f;
        [Min(1)] public float stunnedDamageMultiplier = 1.5f;
        [Min(.1f)] public float recovery = 1;
    }
}
