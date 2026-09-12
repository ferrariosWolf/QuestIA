using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace DrumSaber
{
    public enum DrumInstrument { Kick, HiHat, Snare, Tom1, Tom2, Tom3, Crash, Ride }
    public enum HitGrade { Perfect, Good, Miss }

    [Serializable] public struct DrumEvent { public double time; public DrumInstrument instrument; public int hand; public DrumEvent(double t, DrumInstrument i, int h) { time=t; instrument=i; hand=h; } }

    public sealed class SongClock : MonoBehaviour
    {
        public double SongTime { get { return started ? AudioSettings.dspTime - startDsp - audioOffset : 0d; } }
        public double audioOffset;
        bool started; double startDsp;
        public void StartSong() { startDsp = AudioSettings.dspTime; started = true; Debug.Log("[SongClock] started"); }
        public void ResetSong() { started = false; }
    }

    public sealed class DrumSaberRuntime : MonoBehaviour
    {
        const float Approach = 2.0f, PerfectWindow = .05f, GoodWindow = .10f, MissWindow = .15f;
        readonly List<DrumEvent> chart = new List<DrumEvent>();
        readonly List<RuntimeNote> active = new List<RuntimeNote>();
        SongClock clock; int nextEvent, score, combo, multiplier = 1; string last = "READY";
        GUIStyle label;

        sealed class RuntimeNote { public DrumEvent e; public GameObject go; public bool resolved; }

        void Awake()
        {
            Application.targetFrameRate = 90;
            clock = gameObject.AddComponent<SongClock>();
            BuildChart(); BuildStage();
            Debug.Log("[GameManager] DrumSaber v0.1 ready; Auto Kick ON");
        }

        void BuildChart()
        {
            DrumInstrument[] pattern = { DrumInstrument.HiHat, DrumInstrument.Snare, DrumInstrument.HiHat, DrumInstrument.Snare, DrumInstrument.Crash, DrumInstrument.Tom1, DrumInstrument.Tom2, DrumInstrument.Tom3 };
            for (int i=0;i<pattern.Length;i++) chart.Add(new DrumEvent(i*.5, pattern[i], i%2));
            for (int i=0;i<16;i++) chart.Add(new DrumEvent(4+i*.5, i%2==0 ? DrumInstrument.HiHat : DrumInstrument.Snare, i%2));
        }

        void BuildStage()
        {
            Camera cam = Camera.main; if (cam == null) { var c = new GameObject("Main Camera"); cam=c.AddComponent<Camera>(); c.tag="MainCamera"; }
            cam.transform.position = new Vector3(0,1.6f,-4.2f); cam.transform.LookAt(new Vector3(0,1.1f,0));
            CreatePiece("Snare", DrumInstrument.Snare, new Vector3(0,1.05f,0), .36f, Color.red);
            CreatePiece("HiHat", DrumInstrument.HiHat, new Vector3(-1.25f,1.25f,.1f), .32f, Color.yellow);
            CreatePiece("Tom1", DrumInstrument.Tom1, new Vector3(-.72f,1.38f,.2f), .32f, Color.cyan);
            CreatePiece("Tom2", DrumInstrument.Tom2, new Vector3(.72f,1.38f,.2f), .32f, Color.blue);
            CreatePiece("Tom3", DrumInstrument.Tom3, new Vector3(1.25f,1.2f,.1f), .32f, Color.magenta);
            CreatePiece("Crash", DrumInstrument.Crash, new Vector3(-1.7f,1.85f,.1f), .27f, Color.white);
            CreatePiece("Ride", DrumInstrument.Ride, new Vector3(1.7f,1.85f,.1f), .27f, Color.green);
            CreatePiece("Kick (AUTO)", DrumInstrument.Kick, new Vector3(0,.55f,.25f), .42f, new Color(.8f,.3f,.1f));
            CreateStick("Left Stick", -1); CreateStick("Right Stick", 1);
        }

        void CreatePiece(string name, DrumInstrument instrument, Vector3 pos, float radius, Color color)
        { var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name=name; go.transform.SetPositionAndRotation(pos, Quaternion.Euler(90,0,0)); go.transform.localScale=new Vector3(radius,radius,.12f); var m=go.GetComponent<Renderer>().material; m.color=color; var hit=go.AddComponent<DrumPiece>(); hit.instrument=instrument; hit.runtime=this; }
        void CreateStick(string name, int hand) { var go=GameObject.CreatePrimitive(PrimitiveType.Capsule); go.name=name; go.transform.position=new Vector3(hand*.7f,1.35f,-.9f); go.transform.localScale=new Vector3(.035f,.35f,.035f); go.AddComponent<DrumStick>().hand=hand; }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space) || (!clock.enabled && Input.GetKeyDown(KeyCode.Return))) { clock.StartSong(); nextEvent=0; score=combo=0; }
            if (Input.GetKeyDown(KeyCode.A)) SimulatedHit(-1); if (Input.GetKeyDown(KeyCode.D)) SimulatedHit(1);
            if (!clock.enabled || clock.SongTime < 0) return;
            while (nextEvent < chart.Count && chart[nextEvent].time <= clock.SongTime + Approach) Spawn(chart[nextEvent++]);
            for (int i=active.Count-1;i>=0;i--) { var n=active[i]; float delta=(float)(clock.SongTime-n.e.time); if (!n.resolved && delta>MissWindow && n.e.instrument!=DrumInstrument.Kick) Resolve(n,HitGrade.Miss); if (n.go) n.go.transform.position=PiecePosition(n.e.instrument)+Vector3.up*(1.4f-delta*0.7f); if (n.resolved && n.go) Destroy(n.go); if (n.resolved) active.RemoveAt(i); }
        }
        Vector3 PiecePosition(DrumInstrument i) { return i==DrumInstrument.Snare?new Vector3(0,1.5f,2):i==DrumInstrument.HiHat?new Vector3(-1.25f,1.7f,2):i==DrumInstrument.Tom1?new Vector3(-.72f,1.82f,2):i==DrumInstrument.Tom2?new Vector3(.72f,1.82f,2):i==DrumInstrument.Tom3?new Vector3(1.25f,1.7f,2):i==DrumInstrument.Crash?new Vector3(-1.7f,2.25f,2):new Vector3(1.7f,2.25f,2); }
        void Spawn(DrumEvent e) { if(e.instrument==DrumInstrument.Kick) return; var n=new RuntimeNote{e=e,go=GameObject.CreatePrimitive(PrimitiveType.Sphere)}; n.go.name="Note_"+e.instrument; n.go.transform.position=PiecePosition(e.instrument)+Vector3.up*1.4f; n.go.transform.localScale=Vector3.one*.16f; n.go.GetComponent<Renderer>().material.color=Color.Lerp(Color.white,Color.red,.4f); active.Add(n); }
        void SimulatedHit(int hand) { double best=999; RuntimeNote candidate=null; foreach(var n in active) if(!n.resolved && n.e.hand==hand){double d=Math.Abs(clock.SongTime-n.e.time);if(d<best){best=d;candidate=n;}} if(candidate!=null && best<=GoodWindow) Resolve(candidate,best<=PerfectWindow?HitGrade.Perfect:HitGrade.Good); else { last="EARLY/LATE"; combo=0; } }
        public void RegisterHit(DrumInstrument instrument,int hand,float velocity) { RuntimeNote best=null; double delta=999; foreach(var n in active) if(!n.resolved&&n.e.instrument==instrument&&n.e.hand==hand){var d=Math.Abs(clock.SongTime-n.e.time);if(d<delta){delta=d;best=n;}} if(best!=null&&delta<=GoodWindow) Resolve(best,delta<=PerfectWindow?HitGrade.Perfect:HitGrade.Good); }
        void Resolve(RuntimeNote n,HitGrade grade){n.resolved=true;if(grade==HitGrade.Miss){combo=0;last="MISS";}else{combo++;multiplier=combo>=16?8:combo>=8?4:combo>=4?2:1;score+=(grade==HitGrade.Perfect?100:50)*multiplier;last=grade.ToString();} Debug.Log("[HitDetector] "+last+" combo="+combo);}
        void OnGUI(){if(label==null)label=new GUIStyle(GUI.skin.label){fontSize=24};GUI.Label(new Rect(24,24,700,40),"DRUM SABER   SCORE "+score+"   COMBO "+combo+"   x"+multiplier,label);GUI.Label(new Rect(24,60,700,40),"["+last+"]   SPACE iniciar | A/D simular baquetas",label);}
    }
    public sealed class DrumPiece : MonoBehaviour { public DrumInstrument instrument; public DrumSaberRuntime runtime; }
    public sealed class DrumStick : MonoBehaviour { public int hand; Vector3 previous; InputDevice device;
        void Start(){previous=transform.position; FindDevice();}
        void FindDevice(){var devices=new List<InputDevice>(); InputDevices.GetDevicesAtXRNode(hand<0?XRNode.LeftHand:XRNode.RightHand,devices);if(devices.Count>0)device=devices[0];}
        void Update(){if(!device.isValid)FindDevice(); Vector3 p; Quaternion r; if(device.TryGetFeatureValue(CommonUsages.devicePosition,out p))transform.position=p; if(device.TryGetFeatureValue(CommonUsages.deviceRotation,out r))transform.rotation=r; float v=(transform.position-previous).magnitude/Mathf.Max(Time.deltaTime,.001f);previous=transform.position;if(v>1.2f){var hit=Physics.OverlapSphere(transform.position,.12f);foreach(var c in hit){var piece=c.GetComponent<DrumPiece>();if(piece!=null)piece.runtime.RegisterHit(piece.instrument,hand,v);}}} }
}
