using UnityEngine;

namespace TakeUp.Prototype
{
    public sealed partial class SurvivalGame
    {
        // 0: chase/cooldown, 1: warning, 2: fixed-direction charge, 3: recovery.
        bool TickEnemyCharge(Enemy enemy, Vector2 targetDelta, float dt)
        {
            var config = enemy.definition;
            if (!config.chargeEnabled) return false;
            enemy.chargeTimer = Mathf.Max(0, enemy.chargeTimer - dt);
            if (enemy.chargePhase == 0)
            {
                if (enemy.chargeTimer > 0 || targetDelta.sqrMagnitude > config.chargeRange * config.chargeRange
                    || targetDelta.sqrMagnitude < .001f) return false;
                enemy.chargeDirection = targetDelta.normalized;
                enemy.chargePhase = 1;
                enemy.chargeTimer = Mathf.Max(.1f, config.chargeWarning);
                return true;
            }
            if (enemy.chargePhase == 1)
            {
                if (enemy.chargeTimer <= 0)
                {
                    enemy.chargePhase = 2;
                    enemy.chargeTimer = Mathf.Max(.05f, config.chargeDuration);
                }
                return true;
            }
            if (enemy.chargePhase == 2)
            {
                Vector2 from = enemy.body.position;
                Vector2 to = from + enemy.chargeDirection * Mathf.Max(.1f, config.chargeSpeed) * dt;
                bool blocked = MapWall.Cast(from, to, enemy.radius, out Vector2 stop, out _);
                enemy.body.position = blocked ? stop : to;
                if (blocked || enemy.chargeTimer <= 0)
                {
                    enemy.chargePhase = 3;
                    enemy.chargeTimer = Mathf.Max(.05f, config.chargeRecovery);
                }
                return true;
            }
            if (enemy.chargeTimer <= 0)
            {
                enemy.chargePhase = 0;
                enemy.chargeTimer = Mathf.Max(.1f, config.chargeCooldown);
            }
            return true;
        }

#if UNITY_EDITOR
        public static void ValidateEnemyChargeAndBuild()
        {
            var host = new GameObject("Charge validation");
            try
            {
                var game = host.AddComponent<SurvivalGame>();
                var body = new GameObject("Enemy").transform;
                body.SetParent(host.transform);
                var enemy = new Enemy { body = body, definition = new EnemyDefinition { chargeEnabled = true }, radius = .2f };
                void Require(bool condition, string message) { if (!condition) throw new System.Exception(message); }
                Require(game.TickEnemyCharge(enemy, Vector2.right * 4, .02f), "Must begin warning");
                Require(enemy.chargePhase == 1 && body.position == Vector3.zero, "Warning must stop movement");
                for (int i = 0; i < 34; i++) game.TickEnemyCharge(enemy, Vector2.up * 4, .02f);
                Require(enemy.chargePhase == 2 && enemy.chargeDirection == Vector2.right, "Aim must stay locked");
                for (int i = 0; i < 30; i++) game.TickEnemyCharge(enemy, Vector2.up * 4, .02f);
                Require(enemy.chargePhase == 3 && body.position.x > 0 && body.position.y == 0, "Charge must move straight then recover");
                Vector3 stopped = body.position;
                game.TickEnemyCharge(enemy, Vector2.up, .02f);
                Require(body.position == stopped, "Recovery must stop movement");
                enemy.definition.chargeEnabled = false;
                Require(!game.TickEnemyCharge(enemy, Vector2.up, .02f), "Disabled enemies must use original chase");
                Debug.Log("PASS: charge warning, locked direction, straight movement, recovery, disabled fallback");
            }
            finally { Object.DestroyImmediate(host); }
            MenuFlowSmokeTest.BuildValidationPlayer();
        }
#endif
    }
}
