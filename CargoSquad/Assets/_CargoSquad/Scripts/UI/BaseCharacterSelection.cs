using System.Linq;
using System.Collections;
using UnityEngine.Rendering;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using CargoSquad.Visuals;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CargoSquad.UI
{
    /// <summary>Isolated first-person deployment room; never moves or adds physics to the gameplay player.</summary>
    public sealed class BaseCharacterSelection : MonoBehaviour
    {
        private const int RoomLayer = 29;
        private CharacterSelection owner;
        private GameObject room, interfaceRoot;
        private Camera gameplayCamera;
        private bool cameraWasEnabled, closed;
        private bool mouseLook = true;
        public bool IsLooking => mouseLook;
        private int previousCameraMask;
        private CursorLockMode previousLock;
        private bool previousCursor;
        private Transform[] heads = new Transform[2];
        private LineRenderer[] rings = new LineRenderer[2];
        private Material dark, wall, glow, glass;
        private TMP_Text prompt, selected, controls;
        private float yaw = 180, pitch;
        private float deploymentElapsed;
        private int deployingIndex = -1;
        private Image transitionShade;
        private GameObject gameplayFade;
        private readonly List<Material> vanishMaterials = new List<Material>();
        private readonly List<Color> vanishColors = new List<Color>();
        public bool IsDeploying => deployingIndex >= 0;
        public float VanishProgress => Mathf.Clamp01(deploymentElapsed / .7f);
        private readonly List<RaycastResult> pointerHits = new List<RaycastResult>();
        private Vector3 origin = new Vector3(2000,2000,2000);
        public Camera View { get; private set; }
        public int FocusedIndex { get; private set; } = -1;
        public string[] HeadNames { get; private set; }
        public void Initialize(CharacterSelection selection, SurvivorVisual heavy, SurvivorVisual medic)
        {
            owner = selection; gameplayCamera = Camera.main; cameraWasEnabled = gameplayCamera && gameplayCamera.enabled;
            // Keep Camera.main discoverable for existing gameplay components while rendering only the base view.
            if (gameplayCamera) { previousCameraMask = gameplayCamera.cullingMask; gameplayCamera.cullingMask = 0; }
            previousLock = Cursor.lockState; previousCursor = Cursor.visible;
            SetMouseLook(true);
            dark = HologramGeometry.Material(new Color(.022f,.038f,.053f));
            wall = HologramGeometry.Material(new Color(.035f,.065f,.085f));
            glow = HologramGeometry.Material(new Color(.20f,.88f,1f));
            glass = HologramGeometry.Material(new Color(.10f,.65f,.85f,.045f), true);
            room = new GameObject("Deployment Base Preview"); room.transform.SetParent(transform, false); room.transform.position = origin;
            HologramGeometry.Shape("Base Floor", room.transform, PrimitiveType.Cube, new Vector3(0,-.15f,0), new Vector3(12,.3f,12), dark);
            HologramGeometry.Shape("Rear Bulkhead", room.transform, PrimitiveType.Cube, new Vector3(0,2,-4.5f), new Vector3(12,4,.2f), wall);
            foreach (float x in new[] {-6f,6f}) HologramGeometry.Shape("Side Bulkhead", room.transform, PrimitiveType.Cube,new Vector3(x,2,0),new Vector3(.2f,4,12),wall);
            for(int i=-5;i<=5;i++)
            {
                HologramGeometry.Shape("Floor Guide",room.transform,PrimitiveType.Cube,new Vector3(i,.012f,0),new Vector3(.012f,.01f,12),wall);
                HologramGeometry.Shape("Floor Guide",room.transform,PrimitiveType.Cube,new Vector3(0,.012f,i),new Vector3(12,.01f,.012f),wall);
            }
            HologramGeometry.Shape("Rear Light Strip",room.transform,PrimitiveType.Cube,new Vector3(0,3.5f,-4.35f),new Vector3(10,.035f,.025f),glow);
            WorldText("CARGO SQUAD",room.transform,new Vector3(0,3.05f,-4.3f),5f, new Vector2(9,1),Color.white);
            WorldText("SPECIALIST DEPLOYMENT  /  SECTOR 04",room.transform,new Vector3(0,2.55f,-4.3f),1.6f,new Vector2(8,.5f),new Color(.36f,.74f,.8f));
            HeadNames = new[] {"HV4_J_HEAD", "MR1_J_HEAD"};
            BuildStation(0,heavy); BuildStation(1,medic);
            var cameraObject = new GameObject("Base First Person Camera"); cameraObject.transform.SetParent(room.transform,false);
            View = cameraObject.AddComponent<Camera>(); View.cullingMask = 1<<RoomLayer; View.fieldOfView = 65;
            View.depth = gameplayCamera ? gameplayCamera.depth + 1f : 1f;
            View.nearClipPlane = .05f; View.farClipPlane = 30; View.clearFlags = CameraClearFlags.SolidColor; View.backgroundColor = new Color(.015f,.025f,.04f);
            var data = cameraObject.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = false; data.volumeLayerMask = 0;
            cameraObject.transform.localPosition = new Vector3(0,1.65f,4.6f); cameraObject.transform.rotation = Quaternion.Euler(0,yaw,0);
            AddLight(new Vector3(0,3,2), Color.white, 12);
            AddLight(new Vector3(-3,2,-.2f), new Color(.1f,.75f,1f), 8);
            AddLight(new Vector3(3,2,-.2f), new Color(.25f,1f,.7f), 8);
            foreach(var t in room.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = RoomLayer;
            BuildInterface(); RefreshSelection();
        }
        private void BuildStation(int index, SurvivorVisual visual)
        {
            var station = new GameObject(index == 0 ? "Heavy Hologram Bay" : "Medic Hologram Bay").transform;
            station.SetParent(room.transform,false); station.localPosition = new Vector3(index == 0 ? -2.1f : 2.1f,0,-1.2f);
            HologramGeometry.Shape("Projector Plinth",station,PrimitiveType.Cylinder,new Vector3(0,.18f,0),new Vector3(1.7f,.18f,1.7f),wall);
            rings[index] = HologramGeometry.Ring("Selection Halo",station,.88f,.38f,glow,.035f);
            HologramGeometry.Ring("Outer Orbit",station,1.05f,.035f,glow,.015f);
            HologramGeometry.Shape("Hologram Beam",station,PrimitiveType.Cylinder,new Vector3(0,1.45f,0),new Vector3(1.25f,1.05f,1.25f),glass);
            var source = visual.GetComponentsInChildren<Transform>(true).Single(t=>t.name==HeadNames[index]);
            var pivot = new GameObject(HeadNames[index]+" Hologram").transform; pivot.SetParent(station,false); pivot.localPosition = Vector3.up * 1.9f;
            var clone = Instantiate(source.gameObject).transform; clone.SetParent(pivot,false); clone.SetPositionAndRotation(pivot.position, source.rotation); clone.localScale = source.lossyScale;
            var renderers = clone.GetComponentsInChildren<Renderer>(true); Bounds bounds = renderers[0].bounds;
            foreach(var r in renderers) { r.enabled=true; bounds.Encapsulate(r.bounds); }
            float scale = 1.05f / Mathf.Max(.01f,bounds.size.y); clone.localScale *= scale;
            bounds = renderers[0].bounds; foreach(var r in renderers) bounds.Encapsulate(r.bounds);
            clone.position += pivot.position - bounds.center;
            heads[index] = pivot;
            WorldText(index == 0 ? "HEAVY" : "MEDIC",station,new Vector3(0,.95f,.65f),3.7f,new Vector2(2.8f,.6f),Color.white);
            WorldText(index == 0 ? "BREACH / PROTECT" : "RESTORE / SUPPORT",station,new Vector3(0,.62f,.65f),1.5f,new Vector2(3,.4f),new Color(.35f,.85f,.9f));
        }
        private TMP_Text WorldText(string value, Transform parent, Vector3 position, float size, Vector2 bounds, Color color)
        {
            var obj = new GameObject(value); obj.transform.SetParent(parent,false); obj.transform.localPosition = position; obj.transform.localRotation = Quaternion.Euler(0,180,0);
            var text = obj.AddComponent<TextMeshPro>(); text.font=UiTypography.Font; text.text=value; text.fontSize=size; text.alignment=TextAlignmentOptions.Center;
            text.color=color; text.rectTransform.sizeDelta=bounds; return text;
        }
        private void AddLight(Vector3 position, Color color, float intensity)
        {
            var obj = new GameObject("Base Light"); obj.transform.SetParent(room.transform,false); obj.transform.localPosition=position;
            var light=obj.AddComponent<Light>(); light.type=LightType.Point; light.range=12; light.intensity=intensity; light.color=color; light.cullingMask=1<<RoomLayer;
        }
        private RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var obj=new GameObject(name,typeof(RectTransform)); obj.layer=5; var rect=obj.GetComponent<RectTransform>(); rect.SetParent(parent,false);
            rect.sizeDelta=size; rect.anchoredPosition=position; return rect;
        }
        private TMP_Text Label(string name, Transform parent, Vector2 position, Vector2 size, string text, int font, Color color)
            => UiTypography.Apply(Rect(name,parent,position,size).gameObject,text,font,color);
        private void BuildInterface()
        {
            interfaceRoot=new GameObject("Base Deployment UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            interfaceRoot.transform.SetParent(transform,false); interfaceRoot.layer=5;
            var canvas=interfaceRoot.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=100;
            var scaler=interfaceRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1280,800); scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var top=Rect("Header",interfaceRoot.transform,new Vector2(0,-45),new Vector2(0,90)); top.anchorMin=new Vector2(0,1); top.anchorMax=Vector2.one;
            var headerBackground=top.gameObject.AddComponent<Image>(); headerBackground.color=new Color(.012f,.025f,.035f,.92f); headerBackground.raycastTarget=false;
            Label("Eyebrow",top,new Vector2(0,17),new Vector2(900,25),"CARGO SQUAD  /  FORWARD OPERATING BASE",14,new Color(.4f,.86f,.92f));
            Label("Title",top,new Vector2(0,-13),new Vector2(900,36),"Choose your specialist",30,Color.white);
            Label("Reticle",interfaceRoot.transform,Vector2.zero,new Vector2(20,20),"+",19,new Color(.5f,1f,1f,.85f));
            var bottom=Rect("Deployment Controls",interfaceRoot.transform,new Vector2(0,50),new Vector2(1000,100)); bottom.anchorMin=bottom.anchorMax=new Vector2(.5f,0);
            bottom.anchorMin=Vector2.zero; bottom.anchorMax=new Vector2(1,0); bottom.sizeDelta=new Vector2(0,100);
            var bottomBackground=bottom.gameObject.AddComponent<Image>(); bottomBackground.color=new Color(.012f,.025f,.035f,.96f); bottomBackground.raycastTarget=true;
            prompt=Label("Interact",bottom,new Vector2(0,25),new Vector2(960,25),"",19,Color.white);
            selected=Label("Selected",bottom,new Vector2(0,0),new Vector2(900,24),"",14,new Color(.5f,.94f,.75f));
            controls=Label("Movement",bottom,new Vector2(0,-27),new Vector2(1000,24),"WASD move  /  Mouse look  /  Walk into a hologram to deploy",14,new Color(.65f,.78f,.82f));
            var shade=Rect("Deployment Transition",interfaceRoot.transform,Vector2.zero,Vector2.zero);
            shade.anchorMin=Vector2.zero; shade.anchorMax=Vector2.one; shade.offsetMin=shade.offsetMax=Vector2.zero;
            transitionShade=shade.gameObject.AddComponent<Image>(); transitionShade.color=Color.clear; transitionShade.raycastTarget=false;
        }
        public void RefreshSelection()
        {
            if (!selected) return;
            selected.text=IsDeploying ? (owner.SelectedIndex==0?"HEAVY / SHOULDER BASH":"MEDIC / SECOND WIND")+"  /  DEPLOYING" : "HEAVY / SHOULDER BASH    -    MEDIC / SECOND WIND";
            for(int i=0;i<2;i++) if(rings[i]) rings[i].widthMultiplier=IsDeploying && i==owner.SelectedIndex?.05f:.018f;
        }
        private void Update()
        {
            if(closed || !View || !owner || owner.HasStarted) return;
            if(IsDeploying) { AdvanceDeployment(); return; }
            var keyboard=Keyboard.current; var mouse=Mouse.current;
            if(keyboard!=null && keyboard.escapeKey.wasPressedThisFrame)
            {
                SetMouseLook(!mouseLook);
            }
            if(mouse!=null && mouse.leftButton.wasPressedThisFrame && !mouseLook && !PointerOverInterface())
            {
                SetMouseLook(true);
            }
            controls.text=mouseLook ? "WASD move  /  Mouse look  /  Walk into a hologram to deploy" :
                "Click the room for mouse look  /  WASD move  /  Walk into a hologram to deploy";
            if(mouseLook && mouse!=null)
            {
                Vector2 look=mouse.delta.ReadValue(); yaw+=look.x*.10f; pitch=Mathf.Clamp(pitch-look.y*.10f,-35,45);
            }
            View.transform.rotation=Quaternion.Euler(pitch,yaw,0);
            Vector2 move=Vector2.zero;
            if(keyboard!=null)
            {
                move.x=(keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0); move.y=(keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0);
            }
            Vector3 forward=Quaternion.Euler(0,yaw,0)*Vector3.forward, right=Quaternion.Euler(0,yaw,0)*Vector3.right;
            View.transform.position+=(forward*move.y+right*move.x).normalized*2.6f*Time.unscaledDeltaTime;
            var p=View.transform.localPosition;
            // Preview-only plinth clearance; gameplay physics remains entirely on PlayerPlaceholder.
            for(int i=0;i<2;i++)
            {
                var center=new Vector2(i==0?-2.1f:2.1f,-1.2f); var away=new Vector2(p.x,p.z)-center;
                if(away.magnitude<1.3f) { away=away.sqrMagnitude>.001f?away.normalized:Vector2.up; p.x=center.x+away.x*1.3f; p.z=center.y+away.y*1.3f; }
            }
            p.x=Mathf.Clamp(p.x,-5.1f,5.1f); p.z=Mathf.Clamp(p.z,-2.8f,5.1f); p.y=1.65f; View.transform.localPosition=p;
            FocusedIndex=-1; float score=.82f;
            for(int i=0;i<2;i++)
            {
                heads[i].Rotate(Vector3.up,18f*Time.unscaledDeltaTime,Space.Self);
                Vector3 delta=heads[i].position-View.transform.position; float dot=Vector3.Dot(View.transform.forward,delta.normalized);
                if(delta.magnitude<3.4f && dot>score) {score=dot; FocusedIndex=i;}
            }
            prompt.text=FocusedIndex>=0?"WALK INTO "+(FocusedIndex==0?"HEAVY":"MEDIC")+" TO DEPLOY":"Walk into the hologram of your specialist";
            for(int i=0;i<2;i++)
            {
                var center=new Vector2(i==0?-2.1f:2.1f,-1.2f);
                if(Vector2.Distance(new Vector2(p.x,p.z),center)<=1.55f) { BeginDeployment(i); break; }
            }
        }
        private void BeginDeployment(int index)
        {
            if(IsDeploying || closed) return;
            deployingIndex=index; deploymentElapsed=0f; owner.SelectCharacter(index);
            prompt.text="DEPLOYING "+(index==0?"HEAVY":"MEDIC"); controls.text="";
            // Only preview copies receive transparent materials. Imported model materials remain untouched.
            foreach(var renderer in heads[index].GetComponentsInChildren<Renderer>())
            foreach(var material in renderer.materials)
            {
                vanishMaterials.Add(material);
                vanishColors.Add(material.HasProperty("_BaseColor")?material.GetColor("_BaseColor"):Color.white);
                material.SetFloat("_Surface",1); material.SetFloat("_ZWrite",0);
                material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha); material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue=3000;
            }
        }
        private void AdvanceDeployment()
        {
            deploymentElapsed+=Time.unscaledDeltaTime;
            float vanished=Mathf.SmoothStep(0,1,VanishProgress);
            heads[deployingIndex].localScale=Vector3.one*(1f-vanished);
            heads[deployingIndex].localPosition=Vector3.up*(1.9f+vanished*.65f);
            heads[deployingIndex].Rotate(Vector3.up,160f*Time.unscaledDeltaTime,Space.Self);
            rings[deployingIndex].transform.localScale=Vector3.one*(1f+vanished*.85f);
            rings[deployingIndex].widthMultiplier=Mathf.Lerp(.05f,0f,vanished);
            for(int i=0;i<vanishMaterials.Count;i++)
            {
                Color color=Color.Lerp(vanishColors[i],new Color(.35f,1f,1f,vanishColors[i].a),vanished*.65f);
                color.a=vanishColors[i].a*(1f-vanished); vanishMaterials[i].SetColor("_BaseColor",color);
            }
            transitionShade.color=new Color(0,0,0,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1f,deploymentElapsed)));
            if(deploymentElapsed>=1f) StartCoroutine(EnterGameplay());
        }
        private IEnumerator EnterGameplay()
        {
            // Keep a short black-to-game fade after the base camera and selection overlay are removed.
            gameplayFade=new GameObject("Deployment Fade",typeof(RectTransform),typeof(Canvas));
            gameplayFade.layer=5;
            var canvas=gameplayFade.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=110;
            var rect=Rect("Fade",gameplayFade.transform,Vector2.zero,Vector2.zero); rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero;
            var image=rect.gameObject.AddComponent<Image>(); image.color=Color.black; image.raycastTarget=false;
            owner.StartSelectedCharacter();
            float elapsed=0f;
            while(elapsed<.3f)
            {
                yield return null; elapsed+=Time.unscaledDeltaTime;
                image.color=new Color(0,0,0,1f-Mathf.SmoothStep(0,1,elapsed/.3f));
            }
            Destroy(gameplayFade); gameplayFade=null;
        }
        private void SetMouseLook(bool active)
        {
            mouseLook=active; Cursor.lockState=active?CursorLockMode.Locked:CursorLockMode.None; Cursor.visible=!active;
        }
        private void OnApplicationFocus(bool focus)
        {
            if(!focus && owner && !closed) SetMouseLook(false);
        }
        private bool PointerOverInterface()
        {
            if(!EventSystem.current || Mouse.current==null) return false;
            var pointer=new PointerEventData(EventSystem.current) {position=Mouse.current.position.ReadValue()};
            pointerHits.Clear(); EventSystem.current.RaycastAll(pointer,pointerHits); return pointerHits.Count>0;
        }
        public void Close()
        {
            if(closed) return; closed=true;
            if(room) {room.SetActive(false); Destroy(room);}
            if(interfaceRoot) {interfaceRoot.SetActive(false); Destroy(interfaceRoot);}
            if(gameplayCamera) { gameplayCamera.cullingMask=previousCameraMask; gameplayCamera.enabled=cameraWasEnabled; }
            Cursor.lockState=previousLock; Cursor.visible=previousCursor;
        }
        private void OnDestroy()
        {
            Close(); if(gameplayFade) Destroy(gameplayFade);
            foreach(var material in vanishMaterials) if(material) Destroy(material);
            if(dark)Destroy(dark); if(wall)Destroy(wall); if(glow)Destroy(glow); if(glass)Destroy(glass);
        }
    }
}
