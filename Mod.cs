using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using HarmonyLib;
using Game.UI;

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
            log.Info("Harmony patch applied to ScreenCaptureHelper.CaptureScreenshot.");
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
            harmony?.UnpatchSelf();
            log.Info("Harmony patch removed.");
        }
    }

    [HarmonyPatch(typeof(ScreenCaptureHelper), nameof(ScreenCaptureHelper.CaptureScreenshot))]
    public static class ScreenCaptureHelperPatch
    {
        static bool Prefix()
        {
            Mod.log.Info("Blocked ScreenCaptureHelper.CaptureScreenshot()");
            return false;
        }
    }
}
