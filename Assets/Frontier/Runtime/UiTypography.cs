using System;
using TMPro;
using UnityEngine;

namespace Frontier.Runtime
{
    // Shared, prebuilt resources: no per-label materials or runtime font baking.
    public static class UiTypography
    {
        private static TMP_FontAsset font;
        public static TMP_FontAsset Font => font != null ? font : font = Load<TMP_FontAsset>("UI/LegionSansSDF");

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = Resources.Load<T>(path);
            if (asset == null) throw new InvalidOperationException("Missing UI typography resource: " + path);
            return asset;
        }
    }
}
