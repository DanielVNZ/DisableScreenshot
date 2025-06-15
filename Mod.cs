using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Game.UI;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine.Experimental.Rendering;

namespace DisableScreenshot
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(DisableScreenshot)}.{nameof(Mod)}").SetShowsErrorsInUI(false);
        private Harmony harmony;

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            harmony = new Harmony("com.danielvnz.disablescreenshot");
            harmony.PatchAll();
            log.Info("Harmony patches applied.");
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
            harmony?.UnpatchSelf();
            log.Info("Harmony patches removed.");
        }
    }

    // Shared transpiler logic for removing screenshot + render target creation from AutoSaveSystem methods
    public static class AutoSaveScreenshotRemover
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var captureMethod = AccessTools.Method(typeof(ScreenCaptureHelper), nameof(ScreenCaptureHelper.CaptureScreenshot));
            var createTargetMethod = AccessTools.Method(typeof(ScreenCaptureHelper), nameof(ScreenCaptureHelper.CreateRenderTarget));

            for (int i = 0; i < codes.Count; i++)
            {
                // Remove CreateRenderTarget and its arguments
                if (codes[i].opcode == OpCodes.Call && codes[i].operand as MethodInfo == createTargetMethod)
                {
                    Mod.log.Info("Removing ScreenCaptureHelper.CreateRenderTarget call...");

                    int start = i - 3;
                    if (start >= 0)
                    {
                        codes.RemoveRange(start, 4); // 3 args + call
                        i = start - 1;
                        continue;
                    }
                    else
                    {
                        Mod.log.Error("Failed to remove CreateRenderTarget properly.");
                    }
                }

                // Remove CaptureScreenshot and its arguments
                if (codes[i].opcode == OpCodes.Call && codes[i].operand as MethodInfo == captureMethod)
                {
                    Mod.log.Info("Removing ScreenCaptureHelper.CaptureScreenshot call...");

                    int start = i - 3;
                    if (start >= 0)
                    {
                        codes.RemoveRange(start, 4); // 3 args + call
                        i = start - 1;
                        continue;
                    }
                    else
                    {
                        Mod.log.Error("Failed to remove CaptureScreenshot properly.");
                    }
                }
            }

            return codes;
        }
    }

    [HarmonyPatch(typeof(Game.AutoSaveSystem), "SafeAutoSave")]
    public static class Patch_SafeAutoSave
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => AutoSaveScreenshotRemover.Transpiler(instructions);
    }

    [HarmonyPatch(typeof(Game.AutoSaveSystem), "AutoSave")]
    public static class Patch_AutoSave
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => AutoSaveScreenshotRemover.Transpiler(instructions);
    }

    // Patch ScreenCaptureHelper to disable actual screenshot creation but prevent null refs
    [HarmonyPatch(typeof(ScreenCaptureHelper))]
    public static class Patch_ScreenCaptureHelper
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(ScreenCaptureHelper.CaptureScreenshot))]
        public static bool CaptureScreenshot_Prefix()
        {
            // Skip screenshot capturing completely
            Mod.log.Info("Skipped CaptureScreenshot call.");
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(ScreenCaptureHelper.CreateRenderTarget))]
        public static bool CreateRenderTarget_Prefix(ref RenderTexture __result, string name, int width, int height, GraphicsFormat format = GraphicsFormat.R8G8B8A8_UNorm)
        {
            Mod.log.Info("Returning dummy RenderTexture for CreateRenderTarget call.");
            __result = new RenderTexture(width, height, 0, format, 0)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave
            };
            __result.Create();
            return false; // skip original method
        }
    }
}
