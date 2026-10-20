using UnityEngine;

namespace TakeUp.Prototype
{
    public sealed partial class OctopusBossEncounter
    {
        public bool IsEnraged => Health < settings.health * .5f;
        public bool IsFinale => Phase == OctopusPhase.FinaleShow || Phase == OctopusPhase.FinaleChoose || Phase == OctopusPhase.FinaleImpact;
        public int CurrentMemoryColor => memorySequence[MemoryStep];
        readonly int[] memorySequence = new int[5];
        readonly Mesh[] radialMeshes = new Mesh[8];
        readonly MeshRenderer[] radialVisuals = new MeshRenderer[8];
        Material radialMaterial, warningMaterial;
        SpriteRenderer rageArm;
        Vector2 rageCenter, rageDirection;
        int rageIndex, radialOffset;
        float ArenaRadius => (game.settings.arenaHalfSize + new Vector2(Mathf.Abs(Position.x), Mathf.Abs(Position.y))).magnitude + 2;

        void RandomizeSequence(int count)
        {
            for (int i = 0; i < count; i++) memorySequence[i] = Random.Range(0, Colors.Length);
        }
        void InitializeNewVisuals()
        {
            warningMaterial = new Material(sweepWarning.GetComponent<MeshRenderer>().sharedMaterial);
            warningMaterial.color = new Color(1, .03f, .06f, .35f);
            sweepWarning.GetComponent<MeshRenderer>().sharedMaterial = warningMaterial;
            rageArm = Instantiate(sweepArm, effectsRoot);
            rageArm.name = "Rage Tentacle - Uses Sweep Sprite";
            radialMaterial = new Material(Shader.Find("Sprites/Default"));
            for (int sector = 0; sector < 8; sector++)
            {
                var obj = new GameObject("Final Tentacle " + (sector + 1), typeof(MeshFilter), typeof(MeshRenderer));
                obj.transform.SetParent(effectsRoot, false);
                var vertices = new Vector3[10]; var triangles = new int[24]; var colors = new Color[10];
                for (int j = 0; j <= 8; j++)
                {
                    float angle = (sector * 45 + j * 45f / 8) * Mathf.Deg2Rad;
                    vertices[j + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * ArenaRadius;
                    if (j < 8) { triangles[j * 3] = 0; triangles[j * 3 + 1] = j + 1; triangles[j * 3 + 2] = j + 2; }
                }
                var mesh = new Mesh { name = "Radial Tentacle Sector " + sector };
                mesh.vertices = vertices; mesh.triangles = triangles; mesh.colors = colors; mesh.RecalculateBounds();
                obj.GetComponent<MeshFilter>().sharedMesh = mesh; radialMeshes[sector] = mesh;
                var renderer = obj.GetComponent<MeshRenderer>(); renderer.sharedMaterial = radialMaterial; renderer.sortingOrder = 3;
                radialVisuals[sector] = renderer; obj.SetActive(false);
            }
        }
        void BeginRage()
        {
            HideAttackVisuals();
            var angles = settings.rageAngles;
            float angle = angles == null || angles.Length == 0 ? (rageIndex % 4) * 45 : angles[rageIndex % angles.Length];
            rageIndex++;
            rageDirection = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            rageCenter = game.PlayerPosition;
            rageArm.transform.SetPositionAndRotation(rageCenter, Quaternion.Euler(0, 0, angle));
            Size(rageArm, new Vector2(ArenaRadius * 2, settings.rageWidth));
            rageArm.color = new Color(1, .03f, .05f, .4f); rageArm.gameObject.SetActive(true);
            SetPhase(OctopusPhase.RageWarning, Mathf.Max(.2f, settings.rageWarningTime), "폭주! 빨간 촉수 예고 범위에서 벗어나세요");
        }
        void BeginFinale()
        {
            ClearInk(); HideAttackVisuals(); RandomizeSequence(5); MemoryStep = 0;
            radialOffset = Random.Range(0, 4);
            SetPhase(OctopusPhase.FinaleShow, Mathf.Max(.2f, settings.finaleFlashTime), "마지막 발악! 5개 색의 순서를 기억하세요");
        }
        void BeginFinaleChoice()
        {
            bodyArtwork.color = baseColor; SetLights(-1); ShowRadial(.22f, 1);
            SetPhase(OctopusPhase.FinaleChoose, Mathf.Max(.5f, settings.finaleMoveTime), $"{MemoryStep + 1}/5 · 기억한 색의 촉수 위로! 펼쳐질 때 맞으세요");
        }
        public int FinaleColorAt(Vector2 point)
        {
            Vector2 delta = point - Position;
            float angle = Mathf.Repeat(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, 360);
            return (Mathf.FloorToInt(angle / 45) + radialOffset) % 4;
        }
        bool AdvanceNewPatterns()
        {
            switch (Phase)
            {
                case OctopusPhase.RageWarning:
                    rageArm.color = new Color(1, .15f, .25f, .95f);
                    if (SegmentDistance(game.PlayerPosition, rageCenter - rageDirection * ArenaRadius, rageCenter + rageDirection * ArenaRadius) <= settings.rageWidth * .5f + .3f)
                        game.DamagePlayer(settings.tentacleDamage);
                    SetPhase(OctopusPhase.RageImpact, Mathf.Max(.05f, settings.rageImpactTime), "촉수 강타!"); return true;
                case OctopusPhase.RageImpact:
                    HideAttackVisuals(); SetPhase(OctopusPhase.Recovery, Mathf.Max(.05f, settings.rageInterval), "다음 촉수가 옵니다!"); return true;
                case OctopusPhase.FinaleShow:
                    MemoryStep++;
                    if (MemoryStep < 5) SetPhase(OctopusPhase.FinaleShow, Mathf.Max(.2f, settings.finaleFlashTime), "5개 색의 순서를 기억하세요!");
                    else { MemoryStep = 0; BeginFinaleChoice(); }
                    return true;
                case OctopusPhase.FinaleChoose:
                    if (FinaleColorAt(game.PlayerPosition) != memorySequence[MemoryStep])
                    {
                        HideAttackVisuals(); Health = settings.health * .3f;
                        SetPhase(OctopusPhase.Recovery, 1.2f, "발악 실패! 먹물 요괴 체력 30% 회복 · 촉수 폭주 재개");
                    }
                    else
                    {
                        ShowRadial(.9f, 1);
                        SetPhase(OctopusPhase.FinaleImpact, Mathf.Max(.1f, settings.finaleImpactTime), $"{MemoryStep + 1}/5 색 촉수 받기 성공!");
                    }
                    return true;
                case OctopusPhase.FinaleImpact:
                    MemoryStep++;
                    if (MemoryStep < 5) BeginFinaleChoice();
                    else
                    {
                        HideAttackVisuals(); bodyArtwork.color = new Color(.3f, .3f, .35f);
                        SetPhase(OctopusPhase.Defeated, 0, "발악 돌파! 먹물 요괴를 물리쳤습니다!"); game.CompleteBossFight();
                    }
                    return true;
            }
            return false;
        }
        void TickNewVisuals()
        {
            if (Phase == OctopusPhase.FinaleShow)
            {
                bool on = Remaining > settings.finaleFlashTime * .25f;
                bodyArtwork.color = on ? Colors[memorySequence[MemoryStep]] : baseColor;
                SetLights(on ? memorySequence[MemoryStep] : -1);
            }
            else if (Phase == OctopusPhase.FinaleChoose) ShowRadial(.2f + .1f * Mathf.Sin(Remaining * 8), 1);
            else if (Phase == OctopusPhase.FinaleImpact)
                ShowRadial(.85f, Mathf.Lerp(.15f, 1, Mathf.Clamp01((1 - Remaining / Mathf.Max(.1f, settings.finaleImpactTime)) * 4)));
            else if (Phase == OctopusPhase.RageWarning)
                rageArm.color = new Color(1, .03f, .05f, .4f + .15f * Mathf.Sin(Remaining * 18));
        }
        void ShowRadial(float alpha, float scale)
        {
            for (int i = 0; i < radialVisuals.Length; i++)
            {
                var visual = radialVisuals[i]; visual.gameObject.SetActive(true);
                visual.transform.position = Position; visual.transform.localScale = Vector3.one * scale;
                var color = Colors[(i + radialOffset) % 4]; color.a = alpha;
                var colors = new Color[10]; for (int j = 0; j < colors.Length; j++) colors[j] = color;
                radialMeshes[i].colors = colors;
            }
        }
        void ClearInk() { foreach (var p in puddles) Destroy(p.visual.gameObject); puddles.Clear(); }
        void HideNewVisuals()
        {
            if (rageArm != null) rageArm.gameObject.SetActive(false);
            foreach (var visual in radialVisuals) if (visual != null) visual.gameObject.SetActive(false);
        }
        void DestroyNewVisuals()
        {
            foreach (var mesh in radialMeshes) if (mesh != null) Destroy(mesh);
            if (radialMaterial != null) Destroy(radialMaterial);
            if (warningMaterial != null) Destroy(warningMaterial);
        }
    }
}
