using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Frontier.Runtime
{
    public sealed class FrameMetrics:MonoBehaviour
    {
        public GameSession Session;
        private readonly List<float> intervals=new List<float>(150000),cpu=new List<float>(),gpu=new List<float>();
        private readonly FrameTiming[] timings=new FrameTiming[1];
        private bool saved;
        private void Start(){Session.NewMatch+=Reset;}
        private void Reset(){intervals.Clear();cpu.Clear();gpu.Clear();saved=false;}
        private void Update()
        {
            if(!Session.Running||Session.IsPaused)return;
            if(Session.Match.Winner!=-1){if(!saved)Save();return;}
            if(Session.Match.Tick<100)return;
            intervals.Add(Time.unscaledDeltaTime*1000);
            FrameTimingManager.CaptureFrameTimings();
            if(Time.frameCount%72==0&&FrameTimingManager.GetLatestTimings(1,timings)>0)
            {if(timings[0].cpuFrameTime>0)cpu.Add((float)timings[0].cpuFrameTime);if(timings[0].gpuFrameTime>0)gpu.Add((float)timings[0].gpuFrameTime);}
        }
        [Serializable] private class Report
        {
            public string device,unityVersion,map;
            public int samples,framesOverBudget;
            public float measuredSeconds,averageFps,frameIntervalP95Ms,cpuP95Ms,gpuP95Ms;
            public string caveat="CPU/GPU -1 means unavailable. Editor/desktop measurements do not establish Quest performance. Pauses and the first five simulation seconds are excluded.";
        }
        private static float P95(List<float> samples){if(samples.Count==0)return -1;var copy=samples.ToArray();Array.Sort(copy);return copy[(int)((copy.Length-1)*.95f)];}
        private void Save()
        {
            if(intervals.Count==0)return;saved=true;float total=0;int over=0;foreach(float ms in intervals){total+=ms;if(ms>1000f/72*1.5f)over++;}
            var report=new Report{device=SystemInfo.deviceModel,unityVersion=Application.unityVersion,map=Session.MapId,samples=intervals.Count,framesOverBudget=over,measuredSeconds=total/1000,averageFps=intervals.Count*1000/total,frameIntervalP95Ms=P95(intervals),cpuP95Ms=P95(cpu),gpuP95Ms=P95(gpu)};
            string directory=Path.Combine(Application.persistentDataPath,"Reports");Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory,"performance-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".json"),JsonUtility.ToJson(report,true));
        }
        private void OnApplicationQuit(){if(!saved)Save();}
    }
}
