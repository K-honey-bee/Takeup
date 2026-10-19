using System;
using UnityEditor;
using UnityEngine;

namespace TakeUp.Prototype.Editor
{
    public static class MapWallSetup
    {
        const string Path = "Assets/TakeUpPrototype/Prefabs/MapWall.prefab";
        [MenuItem("TakeUp/Create Map Wall Prefab")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Path) != null) return;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TakeUpPrototype/Art/Placeholders/Square.png");
            if (sprite == null) throw new Exception("Wall placeholder sprite missing");
            var obj = new GameObject("MapWall", typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(MapWall));
            try
            {
                var renderer = obj.GetComponent<SpriteRenderer>(); renderer.sprite = sprite;
                renderer.color = new Color(.37f, .32f, .27f); renderer.sortingOrder = 8;
                obj.GetComponent<BoxCollider2D>().size = sprite.bounds.size;
                obj.transform.localScale = new Vector3(4 / sprite.bounds.size.x, 1 / sprite.bounds.size.y, 1);
                PrefabUtility.SaveAsPrefabAsset(obj, Path);
            }
            finally { UnityEngine.Object.DestroyImmediate(obj); }
        }

        public static void CreateAndBuild()
        {
            Create();
            var test = new GameObject("Wall collision validation", typeof(BoxCollider2D), typeof(MapWall));
            try
            {
                test.transform.localScale = new Vector3(2, 4, 1);
                test.SendMessage("OnEnable");
                Require(MapWall.Cast(new Vector2(-10, 0), new Vector2(10, 0), .25f, out Vector2 hit, out _), "fast projectile hits wall");
                Require(Mathf.Abs(hit.x + 1.25f) < .001f, "projectile radius included");
                Require(!MapWall.Blocks(new Vector2(-3, 3), new Vector2(3, 3)), "clear path remains open");
                Require(MapWall.Constrain(new Vector2(-3, 0), new Vector2(3, 0), .4f).x < -1.39f, "dash cannot tunnel");
                test.transform.rotation = Quaternion.Euler(0, 0, 90);
                Require(MapWall.Cast(new Vector2(-10, 0), new Vector2(10, 0), .25f, out hit, out _) && Mathf.Abs(hit.x + 2.25f) < .001f, "rotated scaled wall");
                test.SetActive(false);
                Require(!MapWall.Blocks(Vector2.left * 3, Vector2.right * 3), "disabled wall stops blocking");
                Debug.Log("PASS: wall prefab, swept hit, radius, clear path, dash blocking, rotation/scale, disabled cleanup.");
            }
            finally { test.SendMessage("OnDisable"); UnityEngine.Object.DestroyImmediate(test); }
            MenuFlowSmokeTest.BuildValidationPlayer();
        }
        static void Require(bool value, string label) { if (!value) throw new Exception(label); }
    }
}
