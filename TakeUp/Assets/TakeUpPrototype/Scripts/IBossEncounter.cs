using UnityEngine;
namespace TakeUp.Prototype
{
    public interface IBossEncounter
    {
        string DisplayName { get; }
        float Health { get; }
        float MaximumHealth { get; }
        bool IsDefeated { get; }
        bool IsFinale { get; }
        Vector2 Position { get; }
        float Radius { get; }
        Transform Body { get; }
        void Initialize();
        void Tick(float dt);
        void Damage(float amount);
        bool HitBySegment(Vector2 from, Vector2 to, float radius);
        Vector2 ConstrainMovement(Vector2 from, Vector2 to, float radius);
        float MovementMultiplier(Vector2 point);
    }
}
