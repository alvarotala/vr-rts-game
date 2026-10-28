using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier.Runtime
{
    public sealed partial class CommandPanel
    {
        private const string WindowScaleKey = "frontier.panel-scale.v1.";
        private const float MinimumWindowScale = .65f, MaximumWindowScale = 1.6f;
        private readonly Dictionary<MenuPage, float> windowScaleFactors = new Dictionary<MenuPage, float>();
        private float baseWindowScale;

        private void LoadWindowScales()
        {
            foreach (MenuPage page in Enum.GetValues(typeof(MenuPage)))
            {
                float factor = PlayerPrefs.GetFloat(WindowScaleKey + page, 1);
                windowScaleFactors[page] = float.IsNaN(factor) || float.IsInfinity(factor) ? 1
                    : Mathf.Clamp(factor, MinimumWindowScale, MaximumWindowScale);
            }
        }

        private void ApplyWindowScale()
        {
#if FRONTIER_BROWSER
            if(root!=null)root.localScale=Vector3.one;
#else
            windowScale = baseWindowScale * (windowScaleFactors.TryGetValue(Page, out float factor) ? factor : 1);
            if (root != null) root.localScale = Vector3.one * windowScale;
#endif
        }

        private void AdjustWindowScale(float horizontal, float deltaTime)
        {
            if (!moving || Mathf.Abs(horizontal) <= .15f || float.IsNaN(horizontal) || float.IsInfinity(horizontal)) return;
            float input = Mathf.Sign(horizontal) * Mathf.Clamp01((Mathf.Abs(horizontal) - .15f) / .85f);
            float factor = windowScaleFactors.TryGetValue(Page, out float saved) ? saved : 1;
            // Grow around the shared center without changing other pages. Bound
            // frame time so a hitch cannot cause a sudden size jump.
            windowScaleFactors[Page] = Mathf.Clamp(factor * Mathf.Exp(input * .65f * Mathf.Clamp(deltaTime, 0, .05f)), MinimumWindowScale, MaximumWindowScale);
            ApplyWindowScale();
        }

        private void SaveWindowScale()
        {
            if (windowScaleFactors.TryGetValue(Page, out float factor)) PlayerPrefs.SetFloat(WindowScaleKey + Page, factor);
        }
    }
}
