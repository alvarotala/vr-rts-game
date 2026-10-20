using System;
using UnityEngine;

namespace Frontier.Runtime
{
    public enum SoundCue { Select, Move, Attack, Reject, Hover, Confirm, Kinetic, Organic, Melee, Siege, Ability, Ready, Start, Victory, Defeat }

    // Original synthesized sounds: generated once, reused by the bounded voice pool.
    public static class SoundPalette
    {
        public const int SampleRate=24000;
        private static readonly int[] ReadyNotes={60,67,72},StartNotes={50,57,62,69},VictoryNotes={62,65,69,74,77},DefeatNotes={62,60,57,50};
        public static AudioClip Effect(SoundCue cue)
        {
            float duration=cue==SoundCue.Victory?1.8f:cue==SoundCue.Defeat?1.7f:cue==SoundCue.Start?.8f:cue==SoundCue.Siege?.5f:cue==SoundCue.Ability?.6f:cue==SoundCue.Ready?.5f:.18f;
            var data=new float[Mathf.CeilToInt(duration*SampleRate)];uint noise=907u+(uint)cue*47;
            for(int i=0;i<data.Length;i++)
            {
                float t=i/(float)SampleRate,u=t/duration;
                noise^=noise<<13;noise^=noise>>17;noise^=noise<<5;float n=(noise&65535)/32767.5f-1;
                float envelope=Mathf.Min(1,t/.008f)*Mathf.Pow(1-u,2);
                float value;
                switch(cue)
                {
                    case SoundCue.Kinetic:value=(Wave(210*t-100*t*t)*.45f+n*.5f)*Mathf.Exp(-t*24);break;
                    case SoundCue.Organic:value=Wave(520*t-850*t*t+Wave(32*t)*.2f)*Mathf.Exp(-t*15)*.65f;break;
                    case SoundCue.Melee:value=(n*.4f+Wave(115*t)*.55f)*Mathf.Exp(-t*28);break;
                    case SoundCue.Siege:value=(Wave(72*t-32*t*t)*.65f+n*Mathf.Exp(-t*35)*.35f)*Mathf.Exp(-t*5);break;
                    case SoundCue.Ability:value=(Wave(300*t+350*t*t)*.35f+Wave(450*t)*.2f+n*.08f)*Mathf.Sin(Mathf.PI*u);break;
                    case SoundCue.Reject:value=Wave(155*t)*.35f+Wave(173*t)*.15f;break;
                    case SoundCue.Move:value=(Wave(540*t+420*t*t)*.38f+Wave(810*t)*.12f);break;
                    case SoundCue.Attack:value=(Wave(330*t-160*t*t)*.35f+Wave(495*t)*.2f);break;
                    case SoundCue.Hover:value=Wave(780*t)*Mathf.Exp(-t*40)*.25f;break;
                    case SoundCue.Confirm:value=Wave(660*t)*.35f+Wave(990*t)*.1f;break;
                    case SoundCue.Ready:value=Chime(t,.12f,ReadyNotes);break;
                    case SoundCue.Start:value=Chime(t,.18f,StartNotes);break;
                    case SoundCue.Victory:value=Chime(t,.22f,VictoryNotes);break;
                    case SoundCue.Defeat:value=Chime(t,.28f,DefeatNotes);break;
                    default:value=Wave(620*t)*Mathf.Exp(-t*18)*.3f;break;
                }
                data[i]=value*envelope*.65f;
            }
            return Clip(cue.ToString(),data,1);
        }
        private static float Chime(float time,float interval,int[] notes)
        {
            float value=0;
            for(int i=0;i<notes.Length;i++)
            {
                float t=time-i*interval;if(t<0)continue;
                float f=Frequency(notes[i]);value+=(Wave(f*t)+.2f*Wave(f*2*t))*Mathf.Min(1,t/.01f)*Mathf.Exp(-t*5)*.3f;
            }
            return value;
        }
        public static AudioClip Music()
        {
            // Sixteen bars at 96 BPM, a restrained D-minor / Bb / F / C progression.
            // Every voice ends inside the loop; the envelope is zero at the seam.
            const float beat=.625f,bar=beat*4;const int bars=16;
            var data=new float[(int)(SampleRate*bar*bars)*2];
            int[][] chords={new[]{50,57,62,65},new[]{46,53,58,62},new[]{48,53,57,60},new[]{48,55,60,64}};
            for(int b=0;b<bars;b++)
            {
                var chord=chords[(b/2)%4];float start=b*bar;
                for(int note=0;note<chord.Length;note++)AddNote(data,start,bar,chord[note],.11f,(note-1.5f)*.38f,false);
                AddNote(data,start,bar*.96f,chord[0]-12,.16f,0,false);
                for(int step=0;step<4;step++)
                {
                    int index=(step+b%3)%chord.Length;
                    AddNote(data,start+step*beat,beat*.9f,chord[index]+12,b%4==3?.035f:.06f,step%2==0?-.45f:.45f,true);
                }
                if(b%4==2)AddNote(data,start+beat*2,beat*1.8f,chord[3]+12,.05f,.2f,true);
            }
            float peak=0;foreach(float sample in data)peak=Mathf.Max(peak,Mathf.Abs(sample));
            if(peak>.8f)for(int i=0;i<data.Length;i++)data[i]*=.8f/peak;
            return Clip("Frontier - Quiet orbit",data,2);
        }
        private static void AddNote(float[] data,float start,float duration,int midi,float gain,float pan,bool bell)
        {
            int first=(int)(start*SampleRate),length=(int)(duration*SampleRate);float frequency=Frequency(midi);
            float left=Mathf.Sqrt((1-pan)*.5f),right=Mathf.Sqrt((1+pan)*.5f);
            for(int i=0;i<length&&(first+i)*2+1<data.Length;i++)
            {
                float t=i/(float)SampleRate,u=i/(float)length;
                float envelope=bell?Mathf.Min(1,t/.012f)*Mathf.Exp(-u*5)*(1-u):Mathf.Sin(Mathf.PI*u)*Mathf.Sin(Mathf.PI*u);
                float tone=Wave(frequency*t)+Wave(frequency*2*t)*.16f+Wave(frequency*1.003f*t)*.15f;
                float value=tone*envelope*gain;
                data[(first+i)*2]+=value*left;data[(first+i)*2+1]+=value*right;
            }
        }
        private static float Frequency(int midi)=>440*Mathf.Pow(2,(midi-69)/12f);
        private static float Wave(float phase)=>(float)Math.Sin(phase*Math.PI*2);
        private static AudioClip Clip(string name,float[] data,int channels)
        {var clip=AudioClip.Create(name,data.Length/channels,channels,SampleRate,false);clip.SetData(data,0);return clip;}
    }
}
