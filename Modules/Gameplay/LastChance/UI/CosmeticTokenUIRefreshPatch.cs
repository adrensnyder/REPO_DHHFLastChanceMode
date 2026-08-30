#nullable enable

using HarmonyLib;
using UnityEngine.UI;

namespace DHHFLastChanceMode.Modules.Gameplay.LastChance.UI
{
    // Vanilla Setup configures only newly instantiated elements. Refresh existing
    // elements as well so the persistent token stack cannot retain prefab/default colors.
    [HarmonyPatch(typeof(CosmeticTokenUI), nameof(CosmeticTokenUI.Setup))]
    internal static class CosmeticTokenUIRefreshPatch
    {
        [HarmonyPostfix]
        private static void Postfix(CosmeticTokenUI __instance)
        {
            if (__instance == null || MetaManager.instance == null || __instance.tokenObjects == null)
            {
                return;
            }

            var tokens = MetaManager.instance.cosmeticTokens;
            if (tokens == null)
            {
                return;
            }

            var tokenCount = tokens.Count;
            var refreshCount = tokenCount < __instance.tokenObjects.Count
                ? tokenCount
                : __instance.tokenObjects.Count;

            CosmeticTokenUIElement? previous = null;
            for (var index = 0; index < refreshCount; index++)
            {
                var element = __instance.tokenObjects[index];
                if (element == null)
                {
                    previous = null;
                    continue;
                }

                var rarity = (SemiFunc.Rarity)tokens[index];
                var image = element.GetComponentInChildren<RawImage>();
                var rarityColors = element.rarityColors;
                var rarityIndex = (int)rarity;
                var expectedColor = rarityColors != null && rarityIndex >= 0 && rarityIndex < rarityColors.Length
                    ? rarityColors[rarityIndex]
                    : default;
                var needsRefresh = element.index != index ||
                                   element.rarity != rarity ||
                                   image == null ||
                                   image.color != expectedColor;

                if (needsRefresh)
                {
                    element.index = index;
                    element.rarity = rarity;
                    element.Setup(previous);
                }

                previous = element;
            }
        }
    }
}
