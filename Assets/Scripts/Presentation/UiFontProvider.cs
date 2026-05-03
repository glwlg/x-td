using UnityEngine;

namespace XTD.Presentation
{
    internal static class UiFontProvider
    {
        private const string BundledChineseFontPath = "Fonts/NotoSansCJKsc-Regular";

        private static Font cachedFont;

        public static Font DefaultFont(int size = 18)
        {
            if (cachedFont != null)
            {
                return cachedFont;
            }

            cachedFont = Resources.Load<Font>(BundledChineseFontPath);
            if (cachedFont != null)
            {
                return cachedFont;
            }

            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                cachedFont = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", size);
                if (cachedFont == null)
                {
                    cachedFont = Font.CreateDynamicFontFromOSFont("SimHei", size);
                }
            }

            cachedFont ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cachedFont ??= Resources.GetBuiltinResource<Font>("Arial.ttf");
            return cachedFont;
        }
    }
}
