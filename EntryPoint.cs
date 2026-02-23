using System.Reflection;
using INIUtility;
using LSPD_First_Response.Mod.API;
using Rage;

namespace MTFOv4
{
    public class EntryPoint : Plugin
    {
        public static MtfoSettings Settings { get; set; }
        public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

        public override void Initialize()
        {
            Functions.OnOnDutyStateChanged += LSPDFRFunctions_OnOnDutyStateChanged;
            Game.LogTrivial($"MTFO: Plugin Initialized. Version: {Version}");
        }

        private void LSPDFRFunctions_OnOnDutyStateChanged(bool onduty)
        {
            if (onduty)
            {
                Settings = ConfigLoader.LoadSettings<MtfoSettings>("plugins/LSPDFR/MTFO.ini");
                Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "~w~MTFO", "~w~By: ~y~Guess1m~w~/~y~Rohan", "~w~Version: ~y~" + Version + " ~g~Loaded Successfully!");
                TrafficYieldController.Start();
            }
            else
            {
                TrafficYieldController.Stop();
            }
        }

        public override void Finally()
        {
            TrafficYieldController.Stop();
        }
    }
}