using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TakeUp.Prototype
{
    public sealed class RoomWaveSmokeTest : MonoBehaviour
    {
        static bool started;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (started || (Array.IndexOf(Environment.GetCommandLineArgs(),"--takeup-room-smoke") < 0 && Array.IndexOf(Environment.GetCommandLineArgs(),"--takeup-room-preview") < 0)) return;
            started = true;
            Application.runInBackground = true;
            var obj = new GameObject("Room wave validation"); DontDestroyOnLoad(obj); obj.AddComponent<RoomWaveSmokeTest>();
        }
        IEnumerator Start()
        {
            yield return SceneManager.LoadSceneAsync("JoseonWaveOne"); yield return null;
            var game = FindFirstObjectByType<SurvivalGame>();
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"--takeup-room-preview") >= 0)
            {
                game.enabled = false;
                game.worldCamera.transform.position = new Vector3(24,0,-10);
                game.worldCamera.orthographicSize = 36;
                yield return new WaitForSecondsRealtime(1);
                var target = new RenderTexture(1280,800,24); target.Create();
                var request = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target };
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(game.worldCamera,request);
                var previous = RenderTexture.active; RenderTexture.active = target;
                var pixels = new Texture2D(1280,800,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,1280,800),0,0); pixels.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath,"../wave-one-overview.png"),pixels.EncodeToPNG());
                RenderTexture.active = previous; target.Release(); Destroy(target); Destroy(pixels);
                Application.Quit(0); yield break;
            }
            try
            {
                game.enabled = false; game.ValidateRoomWave();
                Debug.Log("PASS: finite encounters, gate lock, swept escape prevention, no early unlock, reward, optional seal, portal, no repeats.");
            }
            catch (Exception e) { Debug.LogException(e); Application.Quit(1); yield break; }
            yield return SceneManager.LoadSceneAsync("JoseonWaveOne"); yield return null;
            game = FindFirstObjectByType<SurvivalGame>(); game.enabled = false;
            if (game.RoomWaveComplete || game.ActiveRoom != -1 || game.Kills != 0)
            { Debug.LogError("FAIL: restart state"); Application.Quit(1); yield break; }
            Debug.Log("PASS: restarting resets room and reward state."); Application.Quit(0);
        }
    }
    public sealed partial class SurvivalGame
    {
        public void ValidateRoomWave()
        {
            void Check(bool condition,string reason) { if (!condition) throw new Exception(reason); }
            Check(RoomWaveActive && Wave == 1,"wave map initialized");
            Check(worldCamera.isActiveAndEnabled && scenePlayer.gameObject.activeInHierarchy,"camera and player active");
            Elapsed = settings.stageDuration + 100;
            TickRoomWave(10); Check(AliveEnemies == 0 && Wave == 1,"safe room has no timed spawner/scaling");
            player.position = roomWaveMap.portal.transform.position; InteractRoomWave();
            Check(!RoomWaveComplete,"portal locked before mandatory clears");
            player.position = roomWaveMap.rooms[0].bounds.center;
            for (int step=0;step<300 && ActiveRoom<0;step++) { MovePlayer(Vector2.down,.05f); TickRoomWave(0); }
            Check(ActiveRoom == 1,"walking through starting corridor activates first room");
            MovePlayer(Vector2.up,10);
            Check(roomWaveMap.rooms[1].bounds.Contains(player.position),"immediate retreat is blocked by entrance gate");
            foreach (int index in new[]{1,4,2})
            {
                var room = roomWaveMap.rooms[index];
                player.position = room.bounds.center; TickRoomWave(0);
                Check(ActiveRoom == index && AliveEnemies == 0,"entry warning before spawning");
                Check(MapWall.Blocks(room.bounds.center,room.bounds.center+Vector2.up*30),"closed room blocks escape");
                Vector2 constrained = MapWall.Constrain(player.position,(Vector2)player.position+Vector2.right*100,.5f);
                Check(room.bounds.Contains(constrained),"swept dash cannot leave");
                TickRoomWave(roomWaveMap.warningDuration+.01f);
                KillEnemy(enemies.Count-1); TickRoomWave(0);
                Check(ActiveRoom == index,"no early clear while enemies are still queued");
                for (int n=1;n<room.enemyCount;n++)
                {
                    TickRoomWave(roomWaveMap.spawnInterval+.01f);
                    Check(enemies.Count == 1,"finite spawns");
                    Check(room.bounds.Contains(enemies[0].body.position),"spawn inside room");
                    KillEnemy(0);
                }
                TickRoomWave(0);
                Check(ActiveRoom == -1 && room.cleared && State == RunState.ChoosingAugment,"all kills clear room and reward");
                foreach (var gate in roomWaveMap.gates) Check(!gate.activeSelf,"gates reopened");
                while (State == RunState.ChoosingAugment) ChooseAugment(0);
                TickRoomWave(10); Check(AliveEnemies == 0 && ActiveRoom == -1,"cleared room never retriggers");
            }
            player.position = roomWaveMap.seal.transform.position; InteractRoomWave();
            Check(sealClaimed && State == RunState.ChoosingAugment,"seal reward");
            while (State == RunState.ChoosingAugment) ChooseAugment(0);
            InteractRoomWave(); Check(State == RunState.Playing,"seal once only");
            player.position = roomWaveMap.portal.transform.position; InteractRoomWave();
            Check(RoomWaveComplete && State == RunState.BossReady,"portal completes prototype");
        }
    }
}
