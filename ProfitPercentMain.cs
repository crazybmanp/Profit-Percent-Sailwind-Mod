using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

// poorly written by pr0skynesis (discord username)
// unofficial community patches (v1.2.2+) clumsily maintained by crazybmanp

namespace ProfitPercent
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class ProfitPercentMain : BaseUnityPlugin
    {
        // Necessary plugin info
        public const string pluginGuid = "pr0skynesis.profitpercent";
        public const string pluginName = "Profit Percent";
        public const string pluginVersion = "1.2.3";

        //COLORED TEXT
        public static ConfigEntry<bool> coloredTextConfig;
        public static ConfigEntry<int> greenThresholdConfig;
        public static ConfigEntry<int> blueThresholdConfig;
        //BEST DEALS
        public static ConfigEntry<bool> showBestDealsConfig;
        //AUTO RECEIPT
        public static ConfigEntry<bool> autoReceiptConfig;

        public void Awake()
        {   //Apply the patches
            //Patching
            Harmony harmony = new Harmony(pluginGuid);

            //Awake patch
            MethodInfo original0 = AccessTools.Method(typeof(EconomyUI), "Awake");
            MethodInfo patch0 = AccessTools.Method(typeof(ProfitPercentPatches), "AwakePatch");
            harmony.Patch(original0, new HarmonyMethod(patch0));

            //ShowGoodPage patch
            MethodInfo original1 = AccessTools.Method(typeof(EconomyUI), "ShowGoodPage");
            MethodInfo patch1 = AccessTools.Method(typeof(ProfitPercentPatches), "MainPatch");
            harmony.Patch(original1, null, new HarmonyMethod(patch1));

            //Auto-Receipt Patch
            MethodInfo original2 = AccessTools.Method(typeof(EconomyUIButton), "OnActivate");
            MethodInfo patch2 = AccessTools.Method(typeof(ProfitPercentPatches), "ButtonPatch");
            harmony.Patch(original2, new HarmonyMethod(patch2));

            //Config file
            //COLORED TEXT
            coloredTextConfig = Config.Bind("A) Colored text", "coloredText", true, "Enables the coloring of the profit in red (loss), green (profit), yellow (neither) and blue (high profit)");
            greenThresholdConfig = Config.Bind("A) Colored text", "greenThreshold", 0, "Sets the value above which the profit % will be colored in green. E.g.: set to 30 to have the text be displayed as green only above 30% profit.");
            blueThresholdConfig = Config.Bind("A) Colored text", "blueThreshold", 100, "Sets the value above which the profit % will be colored in blue, used to identify high profit margins.");
            //BEST DEALS
            showBestDealsConfig = Config.Bind("B) Best Deals", "showBestDeals", true, "Display the best deals from the current port to the destinations in the currently selected bookmark. Set to false to disable.");
            //AUTO RECEIPT
            autoReceiptConfig = Config.Bind("C) Automatic Receipts", "autoReceipts", true, "Get the receipt automatically when closing the trade book, if one is available. Set to false to disable.");
        }
    }
}
