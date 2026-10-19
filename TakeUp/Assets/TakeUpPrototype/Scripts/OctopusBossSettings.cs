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
        [Header("랜덤 색상 기억")]
        [Min(0.2f)] public float colorFlashTime = 0.8f;
        [Min(1)] public float hideTime = 3.5f;
        [Min(1)] public float memoryDamage = 35;
        [Header("50% 미만: 촉수 연속 공격 (각도 0=가로, 90=세로)")]
        public float[] rageAngles = { 0, 90, 45, -45, 0, 90, -45, 45 };
        [Min(.2f)] public float rageWarningTime = .65f;
        [Min(.05f)] public float rageImpactTime = .18f;
        [Min(.05f)] public float rageInterval = .16f;
        [Min(.2f)] public float rageWidth = 1.4f;
        [Header("체력 0: 5색 발악")]
        [Min(.2f)] public float finaleFlashTime = .8f;
        [Min(.5f)] public float finaleMoveTime = 3.5f;
        [Min(.1f)] public float finaleImpactTime = .35f;
        [Header("잉크")]

        [Min(0.1f)] public float inkRadius = 1.8f;
        [Min(1)] public float inkLifetime = 8;
        [Range(0.1f, 1)] public float inkMoveMultiplier = 0.45f;
        [Min(0.1f)] public float inkWarningTime = 0.9f;
        [Header("이동 / 추가 촉수와 먹물")]
        public bool mobilePatterns = true;
        [Min(.2f)] public float glideDuration = .85f;
        [Min(.1f)] public float glidePause = .8f;
        [Min(.5f)] public float glideDistance = 4;
        [Min(.2f)] public float trackingWarning = .7f;
        [Min(.15f)] public float trackingInterval = .38f;
        [Min(.3f)] public float trackingRadius = 1.1f;
        [Min(.2f)] public float waveWarning = .9f;
        [Min(.1f)] public float waveInterval = .22f;
        [Min(.5f)] public float pressureInterval = 2.6f;
        [Min(.2f)] public float bombFuse = 1.1f;
        [Min(1)] public float bombInterval = 4.2f;
        [Min(.1f)] public float inkShotSpeed = 4.5f;
        [Min(1)] public float inkDamage = 12;
        [Min(.2f)] public float screenInkDuration = 1.35f;
    }
}
