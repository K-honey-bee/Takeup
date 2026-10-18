using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TakeUp.Prototype
{
    public enum RunState { Playing, ChoosingAugment, Paused, Dead, BossReady, Dancing, Codex }

    public sealed partial class SurvivalGame : MonoBehaviour
    {
        [Tooltip("전투 수치와 증강 설정. 씬 모드에서는 외형과 배치를 Hierarchy / Prefab에서 편집합니다.")]
        public SurvivalSettings settings;
        public Camera worldCamera;
        public SurvivalHud hud;
        public RunState State { get; private set; }
        public int Level { get; private set; } = 1;
        public int Kills { get; private set; }
        public int Experience { get; private set; }
        public int RequiredExperience => settings.firstLevelExperience + (Level - 1) * settings.experienceGrowth;
        public float Health { get; private set; }
        public float MaximumHealth { get; private set; }
        public float Elapsed { get; private set; }
        public int Wave => 1 + Mathf.FloorToInt(Elapsed / settings.waveDuration);
        public int AliveEnemies => enemies.Count;
        public int PendingChoices { get; private set; }
        public bool HasSpear { get; private set; }
        public Vector2 PlayerPosition => player.position;
        public readonly List<AugmentDefinition> Choices = new List<AugmentDefinition>();

        sealed class Enemy
        {
            public Transform body;
            public SpriteRenderer renderer;
            public float health, speed, damage, radius, flash;
            public Color color;
            public int experience;
        }
        sealed class Shot
        {
            public Transform body;
            public Vector2 direction;
            public float speed, damage, radius, life;
            public int pierce;
            public readonly HashSet<Enemy> hit = new HashSet<Enemy>();
        }
        sealed class Gem { public Transform body; public int value; }
        readonly List<Enemy> enemies = new List<Enemy>();
        readonly List<Shot> shots = new List<Shot>();
        readonly List<Gem> gems = new List<Gem>();
        readonly Dictionary<AugmentKind, int> stacks = new Dictionary<AugmentKind, int>();
        readonly List<Sprite> generatedSprites = new List<Sprite>();
        Transform player, world;
        SpriteRenderer playerRenderer;
        Sprite circle, diamond, square;
        float moveSpeed, damageMultiplier = 1, attackSpeed = 1, pickupRadius, regeneration;
        float stoneTimer, spearTimer, spawnTimer, invulnerability;
        int stoneCount = 1;
        bool smokeMode;

        void Start()
        {
            if (settings == null || settings.enemies == null || settings.enemies.Length == 0)
            {
                Debug.LogError("TakeUp: SurvivalSettings와 몬스터 설정을 연결하세요.");
                enabled = false;
                return;
            }
            circle = MakeSprite(0); diamond = MakeSprite(1); square = MakeSprite(2);
            world = runtimeRoot;
            if (world == null) { world = new GameObject("Runtime World").transform; world.SetParent(transform); }
            MaximumHealth = Health = settings.maxHealth;
            moveSpeed = settings.moveSpeed;
            pickupRadius = settings.pickupRadius;
            if (scenePlayer == null) BuildArena();
            playerRenderer = scenePlayer != null ? scenePlayer : Visual("Player", settings.playerSprite, circle,
                settings.playerTint, Vector2.one * settings.playerSize, Vector2.zero, 10);
            player = playerRenderer.transform;
            playerColor = playerRenderer.color;
            if (worldCamera == null) worldCamera = Camera.main;
            worldCamera.orthographic = true;
            if (scenePlayer == null)
            {
                worldCamera.orthographicSize = settings.cameraSize;
                worldCamera.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
                worldCamera.clearFlags = CameraClearFlags.SolidColor;
                worldCamera.backgroundColor = new Color(0.035f, 0.06f, 0.055f);
            }
            baseCameraSize = worldCamera.orthographicSize;
            cameraOffset = worldCamera.transform.position - player.position;
            if (settings.dance == null) settings.dance = new StompDanceSettings();
            if (hud == null) hud = gameObject.AddComponent<SurvivalHud>();
            hud.Initialize(this);
            State = RunState.Playing;
            InitializeSkills();
            InitializeBossMode();
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--takeup-boss-transition") >= 0)
                gameObject.AddComponent<BossTransitionSmokeTest>().game = this;
            smokeMode = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--takeup-smoke") >= 0;
            if (smokeMode) gameObject.AddComponent<SurvivalSmokeTest>().game = this;
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--takeup-dance-smoke") >= 0)
                gameObject.AddComponent<DanceSmokeTest>().game = this;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) TogglePause();
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame) ToggleCodex();
            if (State == RunState.ChoosingAugment && keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) ChooseAugment(0);
                else if (keyboard.digit2Key.wasPressedThisFrame) ChooseAugment(1);
                else if (keyboard.digit3Key.wasPressedThisFrame) ChooseAugment(2);
            }
            if (State == RunState.Dancing) { TickDance(Time.unscaledDeltaTime); return; }
            if (State != RunState.Playing) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            TickSkills(dt);
            if (keyboard != null && keyboard.qKey.wasPressedThisFrame && StartDance()) return;
            Elapsed += dt;
            invulnerability = Mathf.Max(0, invulnerability - dt);
            Health = Mathf.Min(MaximumHealth, Health + regeneration * dt);
            Vector2 input = Vector2.zero;
            if (keyboard != null)
            {
                input.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                input.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0)
                    - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            }
            if (Gamepad.current != null && Gamepad.current.leftStick.ReadValue().sqrMagnitude > input.sqrMagnitude)
                input = Gamepad.current.leftStick.ReadValue();
            if (input.sqrMagnitude > 0.01f) lastDirection = input.normalized;
            if (keyboard != null && (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame)) StartDash();
            if (dashRemaining > 0) TickDash(dt); else MovePlayer(input, dt);
            playerRenderer.color = invulnerability > 0 ? Color.Lerp(playerColor, Color.white, 0.65f) : playerColor;
            if (bossEncounter != null) bossEncounter.Tick(dt);
            else TickSpawner(dt);
            TickEnemies(dt);
            if (State != RunState.Playing) return;
            TickWeapons(dt);
            TickShots(dt);
            TickGems(dt);
            if (bossEncounter == null && State == RunState.Playing && Elapsed >= settings.stageDuration) FinishStage();
        }

        void LateUpdate()
        {
            if (player == null || worldCamera == null) return;
            Vector3 target = player.position + cameraOffset;
            if (followPlayer) worldCamera.transform.position = Vector3.Lerp(worldCamera.transform.position, target, 1 - Mathf.Exp(-10 * Time.unscaledDeltaTime));
            UpdateSkillCamera();
        }

        public void MovePlayer(Vector2 input, float dt)
        {
            if (State != RunState.Playing) return;
            float slow = bossEncounter != null ? bossEncounter.MovementMultiplier(player.position) : 1;
            Vector2 position = (Vector2)player.position + Vector2.ClampMagnitude(input, 1) * moveSpeed * slow * dt;
            player.position = ClampToArena(position);
            if (Mathf.Abs(input.x) > 0.01f) playerRenderer.flipX = input.x < 0;
        }

        void TickSpawner(float dt)
        {
            spawnTimer -= dt;
            if (spawnTimer > 0 || enemies.Count >= settings.maxAliveEnemies) return;
            spawnTimer = Mathf.Max(settings.minimumSpawnInterval,
                settings.initialSpawnInterval * Mathf.Pow(settings.spawnIntervalMultiplierPerWave, Wave - 1));
            int batch = 1 + (Wave - 1) / 2;
            for (int i = 0; i < batch && enemies.Count < settings.maxAliveEnemies; i++)
            {
                float total = 0;
                foreach (var type in settings.enemies) if (type.firstWave <= Wave) total += type.spawnWeight;
                if (total <= 0) return;
                float roll = Random.value * total;
                foreach (var type in settings.enemies)
                {
                    if (type.firstWave > Wave) continue;
                    roll -= type.spawnWeight;
                    if (roll > 0) continue;
                    Vector2 direction = Random.insideUnitCircle.normalized;
                    if (direction.sqrMagnitude < 0.1f) direction = Vector2.up;
                    float halfHeight = worldCamera.orthographicSize + 2;
                    float halfWidth = halfHeight * worldCamera.aspect + 2;
                    float radius = 1 / Mathf.Max(Mathf.Abs(direction.x) / halfWidth, Mathf.Abs(direction.y) / halfHeight);
                    SpawnEnemy(type, (Vector2)worldCamera.transform.position + direction * radius);
                    break;
                }
            }
        }

        void SpawnEnemy(EnemyDefinition type, Vector2 position)
        {
            var renderer = type.prefab != null ? SpawnVisual(type.prefab, position) : Visual(type.displayName, type.sprite, diamond, type.tint,
                Vector2.one * type.size, position, 9);
            enemies.Add(new Enemy { body = renderer.transform, renderer = renderer, color = renderer.color,
                health = type.health * (1 + (Wave - 1) * settings.healthGrowthPerWave),
                speed = type.speed, damage = type.damage * (1 + (Wave - 1) * settings.damageGrowthPerWave),
                radius = type.prefab != null ? VisualRadius(renderer) : type.size * 0.42f, experience = type.experience });
        }

        void TickEnemies(float dt)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                Vector2 delta = (Vector2)player.position - (Vector2)enemy.body.position;
                Vector2 separation = Vector2.zero;
                // Bounded enemy count keeps this simple local separation inexpensive in the prototype.
                for (int j = 0; j < enemies.Count; j++)
                {
                    if (i == j) continue;
                    Vector2 apart = (Vector2)enemy.body.position - (Vector2)enemies[j].body.position;
                    float distance = apart.magnitude;
                    float desired = enemy.radius + enemies[j].radius;
                    if (distance < desired && distance > 0.001f) separation += apart / distance * (desired - distance);
                }
                enemy.body.position += (Vector3)(Vector2.ClampMagnitude(delta.normalized + separation * 2, 1) * enemy.speed * dt);
                enemy.renderer.flipX = delta.x < 0;
                enemy.flash = Mathf.Max(0, enemy.flash - dt);
                enemy.renderer.color = enemy.flash > 0 ? Color.white : enemy.color;
                if (delta.magnitude < enemy.radius + VisualRadius(playerRenderer)) DamagePlayer(enemy.damage);
                if (State == RunState.Dead) return;
            }
        }

        Enemy NearestEnemy()
        {
            Enemy result = null;
            float distance = settings.attackRange * settings.attackRange;
            foreach (Enemy enemy in enemies)
            {
                float candidate = ((Vector2)(enemy.body.position - player.position)).sqrMagnitude;
                if (candidate <= distance) { distance = candidate; result = enemy; }
            }
            return result;
        }

        void TickWeapons(float dt)
        {
            stoneTimer -= dt; spearTimer -= dt;
            Enemy target = NearestEnemy();
            if (target == null && (bossEncounter == null || bossEncounter.IsDefeated || Vector2.Distance(player.position, bossEncounter.Position) > settings.attackRange)) return;
            Vector2 direction = target != null ? (Vector2)(target.body.position - player.position).normalized : (bossEncounter.Position - (Vector2)player.position).normalized;
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.right;
            if (stoneTimer <= 0)
            {
                stoneTimer = settings.stoneInterval / attackSpeed;
                for (int i = 0; i < stoneCount; i++)
                {
                    Vector2 spread = Quaternion.Euler(0, 0, (i - (stoneCount - 1) * 0.5f) * 12) * direction;
                    Fire(spread, false);
                }
            }
            if (HasSpear && spearTimer <= 0) { spearTimer = settings.spearInterval / attackSpeed; Fire(direction, true); }
        }

        void Fire(Vector2 direction, bool spear)
        {
            var prefab = spear ? spearPrefab : stonePrefab;
            var renderer = prefab != null ? SpawnVisual(prefab, player.position) : Visual(spear ? "Spear" : "Stone", spear ? settings.spearSprite : settings.stoneSprite,
                spear ? square : circle, spear ? settings.spearTint : settings.stoneTint,
                spear ? settings.spearSize : Vector2.one * settings.stoneSize, player.position, 12);
            renderer.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg) * renderer.transform.rotation;
            shots.Add(new Shot { body = renderer.transform, direction = direction,
                speed = spear ? settings.spearSpeed : settings.stoneSpeed,
                damage = (spear ? settings.spearDamage : settings.stoneDamage) * damageMultiplier,
                radius = prefab != null ? VisualRadius(renderer) : spear ? 0.22f : settings.stoneSize * 0.5f, pierce = spear ? 3 : 1, life = 2 });
        }

        void TickShots(float dt)
        {
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                Shot shot = shots[i];
                Vector2 from = shot.body.position;
                Vector2 to = from + shot.direction * shot.speed * dt;
                shot.body.position = to;
                shot.life -= dt;
                if (bossEncounter != null && bossEncounter.HitBySegment(from, to, shot.radius))
                { bossEncounter.Damage(shot.damage); shot.pierce = 0; }
                for (int j = enemies.Count - 1; j >= 0 && shot.pierce > 0; j--)
                {
                    Enemy enemy = enemies[j];
                    if (shot.hit.Contains(enemy)) continue;
                    Vector2 segment = to - from;
                    float fraction = segment.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot((Vector2)enemy.body.position - from, segment) / segment.sqrMagnitude) : 0;
                    if (Vector2.Distance(from + segment * fraction, enemy.body.position) > enemy.radius + shot.radius) continue;
                    shot.hit.Add(enemy); shot.pierce--;
                    enemy.health -= shot.damage; enemy.flash = 0.1f;
                    enemy.body.position += (Vector3)(shot.direction * 0.12f);
                    if (enemy.health <= 0)
                    {
                        DropExperience(enemy.body.position, enemy.experience);
                        Destroy(enemy.body.gameObject); enemies.RemoveAt(j); Kills++;
                    }
                }
                if (shot.life <= 0 || shot.pierce <= 0) { Destroy(shot.body.gameObject); shots.RemoveAt(i); }
            }
        }

        void DropExperience(Vector2 position, int value)
        {
            // Merge excess drops without losing earned experience.
            if (gems.Count >= 250)
            {
                Gem nearest = gems[0];
                foreach (Gem gem in gems) if (Vector2.Distance(gem.body.position, position) < Vector2.Distance(nearest.body.position, position)) nearest = gem;
                nearest.value += value;
                return;
            }
            var renderer = experiencePrefab != null ? SpawnVisual(experiencePrefab, position) : Visual("Experience", settings.experienceSprite, diamond, settings.experienceTint,
                Vector2.one * 0.25f, position, 5);
            gems.Add(new Gem { body = renderer.transform, value = value });
        }

        void TickGems(float dt)
        {
            for (int i = gems.Count - 1; i >= 0; i--)
            {
                Gem gem = gems[i];
                float distance = Vector2.Distance(player.position, gem.body.position);
                if (distance < pickupRadius) gem.body.position = Vector3.MoveTowards(gem.body.position, player.position, settings.pickupSpeed * dt);
                if (Vector2.Distance(player.position, gem.body.position) > 0.4f) continue;
                int value = gem.value;
                Destroy(gem.body.gameObject); gems.RemoveAt(i);
                AddExperience(value);
                if (State != RunState.Playing) break;
            }
        }

        public void AddExperience(int amount)
        {
            if (State == RunState.Dead || State == RunState.BossReady || amount <= 0) return;
            Experience += amount;
            while (Experience >= RequiredExperience)
            {
                Experience -= RequiredExperience; Level++; PendingChoices++;
            }
            if (PendingChoices > 0 && State == RunState.Playing) OfferAugments();
        }

        void OfferAugments()
        {
            Choices.Clear();
            var available = new List<AugmentDefinition>();
            if (settings.augments != null)
                foreach (var augment in settings.augments)
                    if (augment != null && StackCount(augment.kind) < augment.maxStacks) available.Add(augment);
            while (Choices.Count < 3 && available.Count > 0)
            {
                int index = Random.Range(0, available.Count);
                Choices.Add(available[index]); available.RemoveAt(index);
            }
            if (Choices.Count == 0)
                Choices.Add(new AugmentDefinition { title = "생존 본능", description = "최대 체력 +10, 체력 10 회복", kind = AugmentKind.MaxHealth, amount = 10 });
            State = RunState.ChoosingAugment;
            hud.ShowChoices();
        }

        public int StackCount(AugmentKind kind) => stacks.TryGetValue(kind, out int count) ? count : 0;

        public void ChooseAugment(int index)
        {
            if (State != RunState.ChoosingAugment || index < 0 || index >= Choices.Count) return;
            ApplyAugment(Choices[index]);
            PendingChoices--;
            if (PendingChoices > 0) OfferAugments();
            else { State = RunState.Playing; hud.HideOverlay(); }
        }

        void ApplyAugment(AugmentDefinition augment)
        {
            stacks[augment.kind] = StackCount(augment.kind) + 1;
            switch (augment.kind)
            {
                case AugmentKind.Damage: damageMultiplier += augment.amount; break;
                case AugmentKind.AttackSpeed: attackSpeed += augment.amount; break;
                case AugmentKind.MoveSpeed: moveSpeed += settings.moveSpeed * augment.amount; break;
                case AugmentKind.MaxHealth: MaximumHealth += augment.amount; Health = Mathf.Min(MaximumHealth, Health + augment.amount); break;
                case AugmentKind.PickupRadius: pickupRadius += augment.amount; break;
                case AugmentKind.ExtraStone: stoneCount += Mathf.Max(1, Mathf.RoundToInt(augment.amount)); break;
                case AugmentKind.Spear: HasSpear = true; break;
                case AugmentKind.Regeneration: regeneration += augment.amount; break;
                case AugmentKind.StompDance: break;
            }
        }

        public void DamagePlayer(float amount)
        {
            if (State != RunState.Playing || invulnerability > 0 || dashRemaining > 0 || amount <= 0) return;
            Health = Mathf.Max(0, Health - amount); invulnerability = settings.hurtInvulnerability;
            if (Health <= 0) { State = RunState.Dead; hud.ShowEnd(false); }
        }

        public void TogglePause()
        {
            if (State == RunState.Codex) { ToggleCodex(); return; }
            if (State == RunState.Playing || State == RunState.Dancing)
            {
                pausedState = State; PauseSkillAudio(); State = RunState.Paused; hud.ShowPause();
            }
            else if (State == RunState.Paused)
            {
                State = pausedState; ResumeSkillAudio(); hud.HideOverlay();
            }
        }

        public void FinishStage()
        {
            if (State != RunState.Playing) return;
            State = RunState.BossReady; hud.ShowEnd(true);
        }

        public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        SpriteRenderer Visual(string label, Sprite sprite, Sprite fallback, Color tint, Vector2 size, Vector2 position, int order)
        {
            var go = new GameObject(label);
            go.transform.SetParent(world); go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite != null ? sprite : fallback;
            renderer.color = tint; renderer.sortingOrder = order;
            Vector2 bounds = renderer.sprite.bounds.size;
            // Keep supplied character art's aspect ratio; spear and arena are explicitly sized.
            if (Mathf.Approximately(size.x, size.y))
                go.transform.localScale = Vector3.one * (size.x / Mathf.Max(bounds.x, bounds.y, 0.001f));
            else go.transform.localScale = new Vector3(size.x / Mathf.Max(bounds.x, 0.001f), size.y / Mathf.Max(bounds.y, 0.001f), 1);
            return renderer;
        }

        void BuildArena()
        {
            Vector2 half = settings.arenaHalfSize;
            Visual("Ground", settings.groundSprite, square, settings.groundTint, half * 2, Vector2.zero, -20);
            Color grid = new Color(0.18f, 0.26f, 0.21f, 0.5f);
            if (settings.groundSprite == null)
            {
                for (float x = -half.x; x <= half.x; x += 2) Visual("Grid", null, square, grid, new Vector2(0.025f, half.y * 2), new Vector2(x, 0), -19);
                for (float y = -half.y; y <= half.y; y += 2) Visual("Grid", null, square, grid, new Vector2(half.x * 2, 0.025f), new Vector2(0, y), -19);
            }
            Color border = new Color(0.55f, 0.49f, 0.29f);
            Visual("North boundary", null, square, border, new Vector2(half.x * 2, 0.15f), new Vector2(0, half.y), -10);
            Visual("South boundary", null, square, border, new Vector2(half.x * 2, 0.15f), new Vector2(0, -half.y), -10);
            Visual("East boundary", null, square, border, new Vector2(0.15f, half.y * 2), new Vector2(half.x, 0), -10);
            Visual("West boundary", null, square, border, new Vector2(0.15f, half.y * 2), new Vector2(-half.x, 0), -10);
        }

        Sprite MakeSprite(int shape)
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(x - 15.5f, y - 15.5f) / 16;
                bool inside = shape == 2 || (shape == 0 ? point.sqrMagnitude < 0.88f : Mathf.Abs(point.x) + Mathf.Abs(point.y) < 0.95f);
                pixels[y * size + x] = inside ? Color.white : Color.clear;
            }
            texture.SetPixels(pixels); texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            generatedSprites.Add(sprite);
            return sprite;
        }

        void OnDestroy()
        {
            CleanupSkills();
            foreach (var sprite in generatedSprites) if (sprite != null) { Destroy(sprite.texture); Destroy(sprite); }
        }
    }
}
