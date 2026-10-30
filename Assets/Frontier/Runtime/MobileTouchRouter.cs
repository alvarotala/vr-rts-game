#if FRONTIER_BROWSER
using System.Collections.Generic;

namespace Frontier.Runtime
{
    // Removes the dedicated Shift contact before classifying map/UI gestures.
    // This keeps a held modifier from becoming the primary pointer or a pinch.
    public sealed class MobileTouchRouter
    {
        public enum ShiftMode { Normal, Attack, Queue }
        public const float DoubleTapSeconds=.32f;
        public struct Contact
        {
            public int Id;
            public float X,Y;
            public bool Held,Down,Up,Cancelled,OnShift;
        }
        private int shiftFinger=-1,pointerFinger=-1;
        private ShiftMode latched;
        private bool shiftUsed,queueAvailable=true;
        private float pointerX,pointerY,shiftPressedAt,lastShiftTap=-1;
        public ShiftMode Mode=>shiftFinger>=0?(queueAvailable?ShiftMode.Queue:ShiftMode.Attack):latched;
        public bool ShiftActive { get=>Mode!=ShiftMode.Normal;set{latched=value?ShiftMode.Attack:ShiftMode.Normal;lastShiftTap=-1;} }

        public void ResetShift(){latched=ShiftMode.Normal;shiftFinger=-1;shiftUsed=false;lastShiftTap=-1;}

        public MobileGesture.Sample Read(IReadOnlyList<Contact> contacts,float time,bool shiftAvailable,bool allowQueue=true)
        {
            queueAvailable=allowQueue;
            if(!allowQueue&&latched==ShiftMode.Queue){latched=ShiftMode.Attack;lastShiftTap=-1;}
            if(!shiftAvailable)ResetShift();
            if(shiftAvailable&&shiftFinger<0)
                for(int i=0;i<contacts.Count;i++)
                {
                    var contact=contacts[i];
                    if(!contact.Down||!contact.OnShift||contact.Cancelled)continue;
                    shiftFinger=contact.Id;shiftUsed=false;shiftPressedAt=time;break;
                }
            bool shifted=ShiftActive,append=Mode==ShiftMode.Queue,foundShift=false;
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
                if(contact.Held||contact.Down)
                {lastShiftTap=-1;if(shiftFinger>=0)shiftUsed=true;}
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
            // Tap: attack/lasso; double tap: contextual queue. Holding is a
            // momentary queue and restores the previous mode on release.
            if(modifier>=0)
            {
                for(int i=0;i<contacts.Count;i++)
                {
                    var contact=contacts[i];if(contact.Id!=modifier)continue;
                    if(contact.Up||contact.Cancelled)
                    {
                        if(contact.Up&&!contact.Cancelled&&!shiftUsed&&contact.OnShift
                            &&time>=shiftPressedAt&&time-shiftPressedAt<MobileGesture.LongPressSeconds)
                        {
                            bool twice=allowQueue&&latched==ShiftMode.Attack&&lastShiftTap>=0
                                &&time>=lastShiftTap&&time-lastShiftTap<=DoubleTapSeconds;
                            if(twice){latched=ShiftMode.Queue;lastShiftTap=-1;}
                            else if(latched==ShiftMode.Normal){latched=ShiftMode.Attack;lastShiftTap=time;}
                            else {latched=ShiftMode.Normal;lastShiftTap=-1;}
                        }
                        else lastShiftTap=-1;
                        shiftFinger=-1;shiftUsed=false;
                    }
                    break;
                }
                if(!foundShift){shiftFinger=-1;shiftUsed=false;lastShiftTap=-1;cancelled=true;}
            }
            var sample=new MobileGesture.Sample{Count=count,Time=time,Shift=shifted,Append=append,Cancelled=cancelled};
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
