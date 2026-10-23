#ifndef FRONTIER_TUTORIAL_FOCUS_INCLUDED
#define FRONTIER_TUTORIAL_FOCUS_INCLUDED

// Shared only by map shaders: retain relief/coverage while making the paused
// board recede behind the lesson. UI, passthrough and teaching cues stay intact.
float _FrontierTutorialFocus;
half3 FrontierTutorialFocus(half3 color)
{
    half focus=saturate(_FrontierTutorialFocus);
    half luminance=dot(color,half3(.2126,.7152,.0722));
    return lerp(color,luminance.xxx,.88*focus)*(1-.1*focus);
}

#endif
