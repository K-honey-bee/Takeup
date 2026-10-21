using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TakeUp.Prototype
{
    [CustomEditor(typeof(DispatchTutorial))]
    public sealed class DispatchLayoutEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("배경·캐릭터는 Hierarchy의 Sprite Renderer에서 이미지/색/크기를 편집하세요. 위치 지점은 Layout Points 아래에 있습니다. 실행 전 Scene에서 편집한 배치를 사용합니다.", MessageType.Info);
            var flow = (DispatchTutorial)target;
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("사무실 위치 보기")) Focus(flow.office);
            if (GUILayout.Button("출동실 위치 보기")) Focus(flow.room);
            EditorGUILayout.EndHorizontal();
            DrawDefaultInspector();
        }
        static void Focus(SpriteRenderer renderer)
        {
            if (renderer == null) return;
            Selection.activeGameObject = renderer.gameObject;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Frame(renderer.bounds, false);
        }
    }

    public static class DispatchLayoutMigration
    {
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene("Assets/TakeUpPrototype/Scenes/DispatchTutorial.unity");
            var flow = Object.FindFirstObjectByType<DispatchTutorial>();
            if (flow.editableLayoutInstalled) return;
            const string imagePath = "Assets/TakeUpPrototype/Art/DispatchPlaceholder.png";
            if (!System.IO.File.Exists(imagePath))
            {
                var texture = new Texture2D(16,16,TextureFormat.RGBA32,false);
                var pixels = new Color[256]; for(int i=0;i<pixels.Length;i++) pixels[i]=Color.white;
                texture.SetPixels(pixels); texture.Apply(); System.IO.File.WriteAllBytes(imagePath,texture.EncodeToPNG()); Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(imagePath);
            }
            var importer=(TextureImporter)AssetImporter.GetAtPath(imagePath);
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=16; importer.filterMode=FilterMode.Point;
            importer.textureCompression=TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            var placeholder=AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
            InitializeArt(flow.office,flow.officeImage,placeholder,new Vector2(22,12),new Color(.85f,.8f,.74f));
            InitializeArt(flow.room,flow.dispatchRoomImage,placeholder,new Vector2(22,12),new Color(.32f,.42f,.47f));
            InitializeArt(flow.player,flow.playerImage,placeholder,Vector2.one*.9f,new Color(.5f,.95f,.85f));
            InitializeArt(flow.jaehee,flow.jaeheeImage,placeholder,Vector2.one,new Color(1,.85f,.35f));
            InitializeArt(flow.coffin,flow.coffinImage,placeholder,new Vector2(1.7f,2.8f),new Color(.35f,.22f,.3f));
            InitializeArt(flow.goal,null,placeholder,Vector2.one*1.3f,new Color(.3f,.9f,.7f,.5f));
            InitializeArt(flow.target,null,placeholder,Vector2.one*1.2f,new Color(1,.4f,.4f));
            var backgrounds = Group(flow.transform,"Backgrounds - Edit Sprite And Scale");
            flow.office.transform.SetParent(backgrounds,true); flow.room.transform.SetParent(backgrounds,true);
            flow.simulation = Object.Instantiate(flow.room,backgrounds); flow.simulation.name="Simulation Background";
            if(flow.simulationImage!=null) { flow.simulation.sprite=null; InitializeArt(flow.simulation,flow.simulationImage,placeholder,new Vector2(22,12),Color.white); }
            flow.simulation.gameObject.SetActive(false);
            var actors=Group(flow.transform,"Actors - Edit Sprite And Scale");
            flow.player.transform.SetParent(actors,true);flow.jaehee.transform.SetParent(actors,true);flow.coffin.transform.SetParent(actors,true);
            var training=Group(flow.transform,"Training - Move Goal And Target");
            flow.goal.transform.SetParent(training,true);flow.target.transform.SetParent(training,true);
            flow.projectileTemplate=new GameObject("Projectile Template - Edit Sprite And Scale",typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            flow.projectileTemplate.transform.SetParent(training,false);flow.projectileTemplate.transform.position=new Vector3(20,-3,0);
            flow.projectileTemplate.sortingOrder=12;
            InitializeArt(flow.projectileTemplate,null,placeholder,Vector2.one*.18f,Color.white);
            flow.projectileTemplate.gameObject.SetActive(false);
            var points=Group(flow.transform,"Layout Points - Move These In Scene");
            flow.officeCameraPoint=Point(points,"01 Office Camera",flow.OfficeCameraPosition);
            flow.roomCameraPoint=Point(points,"02 Dispatch Camera",flow.RoomCameraPosition);
            flow.playerTrainingPoint=Point(points,"03 Player Training Start",new Vector3(21,-2,0));
            flow.jaeheeRoomPoint=Point(points,"04 Jaehee Room Position",new Vector3(26,-2,0));
            flow.playerMissionPoint=Point(points,"05 Player Mission Position",new Vector3(23,-2,0));
            flow.coffinStartPoint=Point(points,"06 Coffin Descend From",flow.coffin.transform.position);
            flow.coffinDockPoint=Point(points,"07 Coffin Boarding Position",new Vector3(24,1.4f,0));
            flow.coffinExitPoint=Point(points,"08 Coffin Exit To",new Vector3(24,11,0));
            flow.trainingMinPoint=Point(points,"09 Training Bottom Left",new Vector3(15,-4,0));
            flow.trainingMaxPoint=Point(points,"10 Training Top Right",new Vector3(33,2.5f,0));
            flow.arrival.gameObject.SetActive(false);
            flow.guide.gameObject.SetActive(false);
            flow.editableLayoutInstalled=true;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        static Transform Group(Transform parent,string name) { var t=new GameObject(name).transform;t.SetParent(parent,false);return t; }
        static Transform Point(Transform parent,string name,Vector3 position) { var t=Group(parent,name);t.position=position;return t; }
        static void InitializeArt(SpriteRenderer renderer,Sprite sprite,Sprite placeholder,Vector2 size,Color color)
        {
            // Existing renderer edits take precedence; initialize only previously empty artwork.
            if(renderer.sprite!=null) return;
            renderer.sprite=sprite!=null?sprite:placeholder;renderer.color=sprite!=null?Color.white:color;
            Vector2 bounds=renderer.sprite.bounds.size;
            float scale=Mathf.Min(size.x/Mathf.Max(.001f,bounds.x),size.y/Mathf.Max(.001f,bounds.y));
            renderer.transform.localScale=sprite!=null?Vector3.one*scale:new Vector3(size.x,size.y,1);
        }
        public static void ApplyAndBuild()
        {
            Apply();
            DispatchTutorialSetup.InstallAndBuild();
        }
    }
}
