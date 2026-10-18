using UnityEngine;

namespace TakeUp.Prototype
{
    [CreateAssetMenu(menuName = "TakeUp/Octopus Boss Settings")]
    public sealed class OctopusBossSettings : ScriptableObject
    {
        [Min(1)] public float health = 1400;
        [Min(0.1f)] public float bodyRadius = 1.6f;
        [Header("촉수 휘두르기")]
        [Min(0.2f)] public float warningTime = 1.5f;
        [Min(0.2f)] public float sweepDuration = 1.6f;
        [Min(1)] public float sweepLength = 9;
        [Min(0.1f)] public float sweepWidth = 0.8f;
        public float sweepStartAngle = -160;
        public float sweepEndAngle = -20;
        [Min(1)] public float tentacleDamage = 22;
        [Min(0.2f)] public float recoveryTime = 2;
        [Header("색상 기억: 빨강 → 파랑 → 노랑 → 흰색")]
        [Min(0.2f)] public float colorFlashTime = 0.8f;
        [Min(1)] public float hideTime = 3.5f;
        [Min(1)] public float memoryDamage = 35;
        [Header("잉크")]
        [Min(0.1f)] public float inkRadius = 1.8f;
        [Min(1)] public float inkLifetime = 8;
        [Range(0.1f, 1)] public float inkMoveMultiplier = 0.45f;
        [Min(0.1f)] public float inkWarningTime = 0.9f;
    }
}
