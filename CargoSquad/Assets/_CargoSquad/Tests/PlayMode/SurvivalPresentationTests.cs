#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using CargoSquad.Combat;
using CargoSquad.Player;
using CargoSquad.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CargoSquad.Tests
{
    public sealed class SurvivalPresentationTests
    {
        private CharacterSelection selection;
        private Health health;
        private Keyboard keyboard;
        private Mouse mouse;
        private CharacterDefinition temporaryDefinition;
        private InputSettings.EditorInputBehaviorInPlayMode previousInput;
        private InputSettings.BackgroundBehavior previousBackground;
        [UnitySetUp]
        public IEnumerator Load()
        {
            previousInput=InputSystem.settings.editorInputBehaviorInPlayMode; previousBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            EditorSceneManager.LoadSceneInPlayMode("Assets/_CargoSquad/Scenes/IndustrialDistrict.unity",new LoadSceneParameters());
            yield return null; MissionResultTestSupport.KeepSimulationRunning(); yield return null;
            selection=Object.FindFirstObjectByType<CharacterSelection>(); health=GameObject.Find("PlayerPlaceholder").GetComponent<Health>();
            keyboard=InputSystem.AddDevice<Keyboard>(); mouse=InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse,new MouseState {position=Vector2.zero}); yield return null;
            foreach(var wolf in Object.FindObjectsByType<CargoSquad.AI.MutatedWolfAI>(FindObjectsSortMode.None)) wolf.enabled=false;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if(temporaryDefinition) Object.Destroy(temporaryDefinition);
            Time.timeScale=1; Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            InputSystem.settings.editorInputBehaviorInPlayMode=previousInput; InputSystem.settings.backgroundBehavior=previousBackground;
            yield return null;
        }
        private IEnumerator Deploy(int index)
        {
            selection.SelectCharacter(index); selection.StartSelectedCharacter(); yield return null; yield return null;
        }
        [UnityTest] public IEnumerator MedicInspectorHealthIsIndependentOfHeavy() => CheckIndependentHealth(1);
        [UnityTest] public IEnumerator HeavyInspectorHealthIsIndependentOfMedic() => CheckIndependentHealth(0);
        private IEnumerator CheckIndependentHealth(int index)
        {
            var references=new SerializedObject(selection);
            var selectedVisual=(CargoSquad.Visuals.SurvivorVisual)references.FindProperty(index==0?"heavy":"medic").objectReferenceValue;
            var otherVisual=(CargoSquad.Visuals.SurvivorVisual)references.FindProperty(index==0?"medic":"heavy").objectReferenceValue;
            Assert.That(selectedVisual.Definition,Is.Not.Null); Assert.That(otherVisual.Definition,Is.Not.Null);
            Assert.That(selectedVisual.Definition,Is.Not.SameAs(otherVisual.Definition));
            float otherMaximum=otherVisual.Definition.MaxHealth;
            temporaryDefinition=Object.Instantiate(selectedVisual.Definition);
            var data=new SerializedObject(temporaryDefinition); data.FindProperty("maxHealth").floatValue=175f; data.ApplyModifiedPropertiesWithoutUndo();
            data=new SerializedObject(selectedVisual); data.FindProperty("definition").objectReferenceValue=temporaryDefinition; data.ApplyModifiedPropertiesWithoutUndo();
            var inspector=UnityEditor.Editor.CreateEditor(selectedVisual);
            Assert.That(inspector.GetType().Name,Is.EqualTo("SurvivorVisualInspector")); Object.DestroyImmediate(inspector);
            inspector=UnityEditor.Editor.CreateEditor(health);
            Assert.That(inspector.GetType().Name,Is.EqualTo("HealthInspector")); Object.DestroyImmediate(inspector);
            yield return Deploy(index);
            Assert.That(health.Maximum,Is.EqualTo(175)); Assert.That(health.Current,Is.EqualTo(175));
            Assert.That(health.GetComponent<PlayerStamina>().Definition,Is.SameAs(temporaryDefinition));
            Assert.That(otherVisual.Definition.MaxHealth,Is.EqualTo(otherMaximum));
            var hud=Object.FindFirstObjectByType<SurvivorHud>();
            Assert.That(hud.HealthFraction,Is.EqualTo(1));
            Assert.That(hud.GetComponentsInChildren<TMPro.TMP_Text>().Single(t=>t.name=="Health").text,Is.EqualTo("HEALTH   175 / 175 HP"));
        }
        [UnityTest]
        public IEnumerator LowHealthFadesToGreyRedAndHealingRestoresView()
        {
            yield return Deploy(0);
            var effect=Camera.main.GetComponent<LowHealthFeedback>(); Assert.That(effect,Is.Not.Null); Assert.That(effect.Intensity,Is.Zero);
            health.ApplyDamage(health.Current-40); yield return new WaitForSeconds(.15f); Assert.That(effect.Intensity,Is.Zero,"Threshold is strictly below 40 HP.");
            health.ApplyDamage(1); yield return null; yield return null;
            Assert.That(effect.Intensity,Is.GreaterThan(0).And.LessThan(1),"Fade starts smoothly.");
            yield return new WaitForSeconds(1.2f); Assert.That(effect.Intensity,Is.GreaterThan(.85f)); Assert.That(effect.Saturation,Is.LessThan(-60));
            Capture(Camera.main,"low-health-grey-red.png");
            Assert.That(health.Restore(25),Is.EqualTo(25)); Assert.That(health.Current,Is.EqualTo(64));
            yield return new WaitForSeconds(1.5f); Assert.That(effect.Intensity,Is.LessThan(.08f));
            Capture(Camera.main,"health-restored.png");
            Assert.That(health.Restore(10000),Is.EqualTo(66)); Assert.That(health.Current,Is.EqualTo(130));
        }
        [UnityTest]
        public IEnumerator TwoHealthHologramsHealExactly25AndRespawnAtRandomClearLocations()
        {
            Assert.That(Object.FindFirstObjectByType<HealthHologramSpawner>().ActiveCount,Is.Zero,"No pickups while in selection.");
            yield return Deploy(1); yield return new WaitForSeconds(.2f);
            var spawner=Object.FindFirstObjectByType<HealthHologramSpawner>(); Assert.That(spawner.ActiveCount,Is.EqualTo(2));
            var settings=new SerializedObject(spawner); settings.FindProperty("minimumRespawnSeconds").floatValue=.25f;
            settings.FindProperty("maximumRespawnSeconds").floatValue=.25f; settings.ApplyModifiedPropertiesWithoutUndo();
            var pickup=spawner.Pickups[0]; var oldPosition=pickup.transform.position;
            var view=Camera.main; var follow=view.GetComponent<TopDownCameraFollow>(); follow.enabled=false;
            Vector3 oldCamera=view.transform.position; Quaternion oldRotation=view.transform.rotation;
            view.transform.position=pickup.transform.position+new Vector3(0,4,-4); view.transform.LookAt(pickup.transform.position+Vector3.up*.7f);
            yield return null; Capture(view,"health-hologram-pickup.png"); view.transform.SetPositionAndRotation(oldCamera,oldRotation); follow.enabled=true;
            Assert.That(pickup.TryCollect(health),Is.False,"Full health does not waste a pickup.");
            health.ApplyDamage(60); Assert.That(pickup.TryCollect(health),Is.True); Assert.That(health.Current,Is.EqualTo(60));
            Assert.That(spawner.ActiveCount,Is.EqualTo(1)); Assert.That(pickup.TryCollect(health),Is.False,"Cannot collect twice.");
            yield return new WaitForSeconds(.1f); Assert.That(spawner.ActiveCount,Is.EqualTo(1));
            yield return new WaitForSeconds(.3f); Assert.That(spawner.ActiveCount,Is.EqualTo(2)); Assert.That(pickup.transform.position,Is.Not.EqualTo(oldPosition));
            Assert.That(spawner.GetComponentsInChildren<HealthHologram>(true).Length,Is.EqualTo(2));
            var body=health.GetComponent<Rigidbody>(); body.position=pickup.transform.position+Vector3.up; body.linearVelocity=Vector3.zero; Physics.SyncTransforms();
            yield return null; yield return null; Assert.That(health.Current,Is.EqualTo(85),"Walking into it heals automatically.");
            health.Restore(10000); Assert.That(health.Current,Is.EqualTo(95));
            health.ApplyDamage(10000); Assert.That(health.Restore(25),Is.Zero); Assert.That(spawner.Pickups.Any(p=>p.Available && p.TryCollect(health)),Is.False,"No resurrection.");
        }
        [UnityTest] public IEnumerator BaseWalkChooseMedicAndDeployWithoutMovingGameplayPlayer() => CheckBaseChoice(1);
        [UnityTest] public IEnumerator BaseWalkChooseHeavyAndDeployWithoutMovingGameplayPlayer() => CheckBaseChoice(0);
        private IEnumerator CheckBaseChoice(int index)
        {
            var preview=selection.BasePreview; Assert.That(preview,Is.Not.Null); Assert.That(Time.timeScale,Is.Zero);
            Assert.That(preview.HeadNames,Is.EqualTo(new[]{"HV4_J_HEAD","MR1_J_HEAD"}));
            Assert.That(Camera.main.cullingMask,Is.Zero); var before=health.transform.position;
            Capture(preview.View,"base-selection-preview.png");
            Capture(preview.View,"base-selection-small.png",800,600);
            Capture(preview.View,"base-selection-1080.png",1920,1080);
            Vector3 start=preview.View.transform.position;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W)); yield return new WaitForSecondsRealtime(.3f);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
            Assert.That(Vector3.Distance(start,preview.View.transform.position),Is.GreaterThan(.3f)); Assert.That(health.transform.position,Is.EqualTo(before));
            preview.View.transform.localPosition=new Vector3(index==0?-2.1f:2.1f,1.65f,1f); yield return null; yield return null;
            Assert.That(preview.FocusedIndex,Is.EqualTo(index));
            Assert.That(selection.GetComponentsInChildren<Button>(),Is.Empty,"Base has no buttons.");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E,Key.Enter)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
            Assert.That(selection.HasStarted,Is.False,"E and Enter do not confirm selection.");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));
            float timeout=Time.realtimeSinceStartup+2f;
            while(!preview.IsDeploying && Time.realtimeSinceStartup<timeout) yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            Assert.That(preview.IsDeploying,Is.True,"Contact starts deployment automatically.");
            Assert.That(selection.SelectedIndex,Is.EqualTo(index)); Assert.That(selection.HasStarted,Is.False);
            Assert.That(Time.timeScale,Is.Zero,"Gameplay stays paused while the hologram vanishes.");
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(preview.VanishProgress,Is.GreaterThan(.2f));
            var head=selection.GetComponentsInChildren<Transform>().Single(t=>t.name==preview.HeadNames[index]+" Hologram");
            Assert.That(head.localScale.x,Is.LessThan(.95f).And.GreaterThan(0f));
            Capture(preview.View,index==0?"base-heavy-vanishing.png":"base-medic-vanishing.png");
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.That(selection.HasStarted,Is.True); Assert.That(Camera.main.enabled,Is.True); Assert.That(Time.timeScale,Is.GreaterThan(0));
            Assert.That(health.GetComponent<CharacterAbility>().Kind,Is.EqualTo((SurvivorKind)index)); Assert.That(health.Maximum,Is.EqualTo(index==0?130:95));
            Assert.That(health.GetComponent<CharacterAbility>().CooldownRemaining,Is.Zero);
            Assert.That(health.GetComponents<Rigidbody>().Length,Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator BaseMouseLookHasNoButtonsOrConfirmationKeys()
        {
            var preview=selection.BasePreview;
            InputSystem.QueueStateEvent(mouse,new MouseState {position=new Vector2(Screen.width*.5f,Screen.height*.5f),buttons=1}); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState {position=new Vector2(Screen.width*.5f,Screen.height*.5f)}); yield return null;
            Assert.That(preview.IsLooking,Is.True);
            var before=preview.View.transform.rotation;
            InputSystem.QueueDeltaStateEvent(mouse.delta,new Vector2(120,20)); yield return null; yield return null;
            Assert.That(Quaternion.Angle(before,preview.View.transform.rotation),Is.GreaterThan(5));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
            Assert.That(Cursor.lockState,Is.EqualTo(CursorLockMode.None));
            Assert.That(selection.GetComponentsInChildren<Button>(),Is.Empty);
            Assert.That(selection.StartButton,Is.Null);
            Assert.That(selection.HasStarted,Is.False); Assert.That(Time.timeScale,Is.Zero);
        }
        [UnityTest]
        public IEnumerator ClassicSelectionCanBeRestoredWithoutCodeChanges()
        {
            selection.ShowClassicSelection(); yield return null;
            Assert.That(selection.BasePreview,Is.Null); Assert.That(Time.timeScale,Is.Zero); Assert.That(selection.Cards.Length,Is.EqualTo(2));
            Assert.That(selection.GetComponentsInChildren<HologramHeadPreview>().Length,Is.EqualTo(2));
            selection.SelectCharacter(1); yield return Deploy(1); Assert.That(health.Maximum,Is.EqualTo(95));
        }
        private void Capture(Camera view,string name,int width=1280,int height=800)
        {
            Directory.CreateDirectory("ValidationResults");
            var canvases=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas && c.isActiveAndEnabled && c.renderMode!=RenderMode.WorldSpace).ToArray();
            var modes=canvases.Select(c=>c.renderMode).ToArray(); var cameras=canvases.Select(c=>c.worldCamera).ToArray(); var distances=canvases.Select(c=>c.planeDistance).ToArray();
            var target=new RenderTexture(width,height,24); target.Create(); var oldTarget=view.targetTexture; var oldActive=RenderTexture.active; int oldMask=view.cullingMask; view.cullingMask |= 1<<5; view.targetTexture=target;
            foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=view;canvas.planeDistance=.5f;}
            Canvas.ForceUpdateCanvases(); view.Render(); RenderTexture.active=target;
            var texture=new Texture2D(width,height,TextureFormat.RGB24,false); texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
            File.WriteAllBytes("ValidationResults/"+name,texture.EncodeToPNG()); RenderTexture.active=oldActive; view.targetTexture=oldTarget; view.cullingMask=oldMask;
            for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=distances[i];}
            Object.Destroy(texture); target.Release(); Object.Destroy(target);
        }
    }
}
#endif
