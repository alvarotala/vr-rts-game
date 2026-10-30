#if FRONTIER_BROWSER
using System.Collections.Generic;

namespace Frontier.Runtime
{
    // Removes the dedicated Shift contact before classifying map/UI gestures.
    // This keeps a held modifier from becoming the primary pointer or a pinch.
    public sealed class MobileTouchRouter
    {
        public struct Contact
        {
            public int Id;
            public float X,Y;
            public bool Held,Down,Up,Cancelled,OnShift;
        }
        private int shiftFinger=-1,pointerFinger=-1;
        private bool latched,shiftUsed;
        private float pointerX,pointerY;
        public bool ShiftActive { get=>latched||shiftFinger>=0;set=>latched=value; }

        public void ResetShift(){latched=false;shiftFinger=-1;shiftUsed=false;}

        public MobileGesture.Sample Read(IReadOnlyList<Contact> contacts,float time,bool shiftAvailable)
        {
            if(!shiftAvailable)ResetShift();
            if(shiftAvailable&&shiftFinger<0)
                for(int i=0;i<contacts.Count;i++)
                {
                    var contact=contacts[i];
                    if(!contact.Down||!contact.OnShift||contact.Cancelled)continue;
                    shiftFinger=contact.Id;shiftUsed=false;break;
                }
            bool shifted=ShiftActive,foundShift=false;
            int modifier=shiftFinger,count=0;
            Contact primary=default,secondary=default,release=default;
            bool hasPrimary=false,hasRelease=false,cancelled=false;
            for(int i=0;i<contacts.Count;i++)
            {
                var contact=contacts[i];
                if(contact.Id==modifier)
                {
                    foundShift=true;
                    if(contact.Cancelled)cancelled=true;
                    continue;
                }
                if(shiftFinger>=0&&(contact.Held||contact.Down))shiftUsed=true;
                cancelled|=contact.Cancelled;
                if(contact.Held)
                {
                    count++;
                    if(!hasPrimary){primary=contact;hasPrimary=true;}
                    else if(contact.Id==pointerFinger){secondary=primary;primary=contact;}
                    else if(count==2)secondary=contact;
                }
                else if(contact.Up&&(contact.Id==pointerFinger||!hasRelease&&contact.Down))
                {release=contact;hasRelease=true;}
            }
            // A Shift tap latches; using another finger while it is held makes
            // it momentary, retaining whichever latched state existed before.
            if(modifier>=0)
            {
                for(int i=0;i<contacts.Count;i++)
                {
                    var contact=contacts[i];if(contact.Id!=modifier)continue;
                    if(contact.Up||contact.Cancelled)
                    {
                        if(contact.Up&&!contact.Cancelled&&!shiftUsed&&contact.OnShift)latched=!latched;
                        shiftFinger=-1;shiftUsed=false;
                    }
                    break;
                }
                if(!foundShift){shiftFinger=-1;shiftUsed=false;cancelled=true;}
            }
            var sample=new MobileGesture.Sample{Count=count,Time=time,Shift=shifted,Cancelled=cancelled};
            if(hasPrimary)
            {
                sample.Finger=primary.Id;sample.X=primary.X;sample.Y=primary.Y;sample.Down=primary.Down;
                sample.OtherFinger=secondary.Id;sample.OtherX=secondary.X;sample.OtherY=secondary.Y;
                pointerFinger=primary.Id;pointerX=primary.X;pointerY=primary.Y;
            }
            else if(hasRelease)
            {
                sample.Finger=release.Id;sample.X=release.X;sample.Y=release.Y;sample.Down=release.Down;sample.Up=true;
                pointerFinger=-1;pointerX=release.X;pointerY=release.Y;
            }
            else {sample.X=pointerX;sample.Y=pointerY;pointerFinger=-1;}
            return sample;
        }
    }
}
#endif
