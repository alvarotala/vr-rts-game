using UnityEngine;

namespace Frontier.Runtime
{
    public sealed class AudioFeedback:MonoBehaviour
    {
        public GameSession Session;
        private AudioSource source;
        private AudioClip accepted,rejected;
        private float last;
        private void Start()
        {
            source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.volume=.14f;
            accepted=Tone(640,.055f);rejected=Tone(180,.13f);Session.Feedback+=Play;
        }
        private static AudioClip Tone(float frequency,float duration)
        {
            const int rate=24000;var data=new float[(int)(rate*duration)];
            for(int i=0;i<data.Length;i++){float t=i/(float)rate;float envelope=Mathf.Sin(Mathf.PI*i/data.Length);data[i]=Mathf.Sin(2*Mathf.PI*frequency*t)*envelope*.4f;}
            var clip=AudioClip.Create("Command feedback",data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        private void Play(bool success){if(Time.unscaledTime-last<.05f)return;last=Time.unscaledTime;source.PlayOneShot(success?accepted:rejected);}
        private void OnDestroy(){if(Session!=null)Session.Feedback-=Play;if(accepted!=null)Destroy(accepted);if(rejected!=null)Destroy(rejected);}
    }
}
