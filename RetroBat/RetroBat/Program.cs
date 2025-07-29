using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;

namespace RetroBat
{
    class Program
    {
        static void Main()
        {
            SimpleLogger.Instance.Info("--------------------------------------------------------------");
            SimpleLogger.Instance.Info("[Startup] RetroBat.exe");

            string appFolder = Directory.GetCurrentDirectory();

            // Ini file check and creation
            SimpleLogger.Instance.Info("[INFO] Check ini file");
            string iniPath = Path.Combine(appFolder, "retrobat.ini"); // Ensure this file is next to your executable
            if (!File.Exists(iniPath))
            {
                SimpleLogger.Instance.Info("[INFO] ini file does not exist yet, creating default file.");
                string iniDefault = IniFile.GetDefaultIniContent();
                try
                {
                    File.WriteAllText(iniPath, iniDefault);
                    SimpleLogger.Instance.Info("[INFO] ini file written to " + iniPath);
                }
                catch { SimpleLogger.Instance.Warning("[WARNING] Impossible to create ini file."); }
            }

            // Write path to registry
            RegistryTools.SetRegistryKey(appFolder + "\\");

            // Get values from ini file
            RetroBatConfig config = new RetroBatConfig();

            using (IniFile ini = new IniFile(iniPath))
            {
                SimpleLogger.Instance.Info("[INFO] Reading values from inifile: " + iniPath);
                config = GetConfigValues(ini);

                foreach (PropertyInfo prop in config.GetType().GetProperties())
                    try { SimpleLogger.Instance.Info($"[INFO]{prop.Name} = {prop.GetValue(config, null)}"); } catch { }
            }

            // Get emulationstation.exe path
            string esPath = Path.Combine(appFolder, "emulationstation");
            string emulationStationExe = Path.Combine(esPath, "emulationstation.exe");

            if (!File.Exists(emulationStationExe))
            {
                SimpleLogger.Instance.Info("[ERROR] Emulationstation executable not found in: " + emulationStationExe);
                return;
            }

            // Arguments
            SimpleLogger.Instance.Info("[INFO] Setting up arguments to run EmulationStation.");
            List<string> commandArray = new List<string>();

            if (config.Fullscreen && config.ForceFullscreenRes)
            {
                commandArray.Add("--resolution");
                commandArray.Add(config.WindowXSize.ToString());
                commandArray.Add(config.WindowYSize.ToString());
            }

            else if (!config.Fullscreen)
            {
                commandArray.Add("--windowed");
                commandArray.Add("--resolution");
                commandArray.Add(config.WindowXSize.ToString());
                commandArray.Add(config.WindowYSize.ToString());
            }

            if (config.GameListOnly)
                commandArray.Add("--gamelist-only");

            if (config.InterfaceMode == 2)
                commandArray.Add("--force-kid");
            else if (config.InterfaceMode == 1)
                commandArray.Add("--force-kiosk");

            if (config.MonitorIndex > 0)
            {
                commandArray.Add("--monitor");
                commandArray.Add(config.MonitorIndex.ToString());
            }

            if (config.NoExitMenu)
                commandArray.Add("--no-exit");

            commandArray.Add("--home");
            commandArray.Add(esPath);

            string args = string.Join(" ", commandArray);

            // Run EmulationStation
            SimpleLogger.Instance.Info("[INFO] Running " + emulationStationExe + " " + args);

            var start = new ProcessStartInfo()
            {
                FileName = emulationStationExe,
                WorkingDirectory = esPath,
                Arguments = args,
            };

            if (start == null)
                return;

            var exe = Process.Start(start);
        }

        private static RetroBatConfig GetConfigValues(IniFile ini)
        {
            RetroBatConfig config = new RetroBatConfig();

            if (int.TryParse(GetOptionValue(ini, "RetroBat", "LanguageDetection", "0"), out int result))
                config.LanguageDetection = result;
            else
                config.LanguageDetection = 0;

            config.ResetConfigMode = GetOptBoolean(GetOptionValue(ini, "RetroBat", "ResetConfigMode", "false"));
            config.Autostart = GetOptBoolean(GetOptionValue(ini, "RetroBat", "Autostart", "false"));
            config.WiimoteGun = GetOptBoolean(GetOptionValue(ini, "RetroBat", "WiimoteGun", "false"));
            config.EnableIntro = GetOptBoolean(GetOptionValue(ini, "SplashScreen", "EnableIntro", "true"));
            config.FileName = GetOptionValue(ini, "SplashScreen", "FileName", "RetroBat-neon.mp4");
            config.FilePath = GetOptionValue(ini, "SplashScreen", "FilePath", "default");
            config.RandomVideo = GetOptBoolean(GetOptionValue(ini, "SplashScreen", "RandomVideo", "true"));

            if (int.TryParse(GetOptionValue(ini, "SplashScreen", "VideoDuration", "6500"), out int duration))
                config.VideoDuration = duration;
            else
                config.VideoDuration = 6500;

            config.Fullscreen = GetOptBoolean(GetOptionValue(ini, "EmulationStation", "Fullscreen", "true"));
            config.ForceFullscreenRes = GetOptBoolean(GetOptionValue(ini, "EmulationStation", "ForceFullscreenRes", "false"));
            config.GameListOnly = GetOptBoolean(GetOptionValue(ini, "EmulationStation", "GameListOnly", "false"));

            if (int.TryParse(GetOptionValue(ini, "EmulationStation", "InterfaceMode", "0"), out int interfaceMode))
                config.InterfaceMode = interfaceMode;
            else
                config.InterfaceMode = 0;

            if (int.TryParse(GetOptionValue(ini, "EmulationStation", "MonitorIndex", "0"), out int monitorIndex))
                config.MonitorIndex = monitorIndex;
            else
                config.MonitorIndex = 0;

            config.NoExitMenu = GetOptBoolean(GetOptionValue(ini, "EmulationStation", "NoExitMenu", "false"));

            if (int.TryParse(GetOptionValue(ini, "EmulationStation", "WindowXSize", "1280"), out int windowX))
                config.WindowXSize = windowX;
            else
                config.WindowXSize = 1280;

            if (int.TryParse(GetOptionValue(ini, "EmulationStation", "WindowYSize", "720"), out int windowY))
                config.WindowYSize = windowY;
            else
                config.WindowYSize = 720;

            return config;
        }

        public static bool GetOptBoolean(string input)
        {
            if (input == "1" || input == "true" || input == "yes")
                return true;
            else
                return false;
        }

        private static string GetOptionValue(IniFile ini, string section, string key, string defaultValue)
        {
            string value = ini.GetValue(section, key);

            if (!string.IsNullOrEmpty(value))
                return value;
            else
                return defaultValue;
        }
    }

    class RetroBatConfig
    {
        public int LanguageDetection { get; set; }
        public bool ResetConfigMode { get; set; }
        public bool Autostart { get; set; }
        public bool WiimoteGun { get; set; }
        public bool EnableIntro { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public bool RandomVideo { get; set; }
        public int VideoDuration { get; set; }
        public bool Fullscreen { get; set; }
        public bool ForceFullscreenRes { get; set; }
        public bool GameListOnly { get; set; }
        public int InterfaceMode { get; set; }
        public int MonitorIndex { get; set; }
        public bool NoExitMenu { get; set; }
        public int WindowXSize { get; set; }
        public int WindowYSize { get; set; }
    }

    /*
    "Command line arguments:"
		"--resolution [width] [height]	try and force a particular resolution"
        "--fullscreen-borderless"
        "--fullscreen"
        "--windowed"
		"--gamelist-only			skip automatic game search, only read from gamelist.xml"
		"--ignore-gamelist		ignore the gamelist (useful for troubleshooting)"
		"--draw-framerate		display the framerate"
		"--no-exit			don't show the exit option in the menu"
		"--no-splash			don't show the splash screen"
		"--debug				more logging, show console on Windows"				
		"--windowed			not fullscreen, should be used with --resolution"
		"--vsync [1/on or 0/off]		turn vsync on or off (default is on)"
		"--max-vram [size]		Max VRAM to use in Mb before swapping. 0 for unlimited"
		"--force-kid		Force the UI mode to be Kid"
		"--force-kiosk		Force the UI mode to be Kiosk"
		"--force-disable-filters		Force the UI to ignore applied filters in gamelist"
		"--home [path]		Directory to use as home path"
        "--videoduration"
        "--video"
		"--help, -h			summon a sentient, angry tuba"
		"--monitor [index]			monitor index
        "--screenoffset"
        "--screenrotate"
        "--show-hidden-files"
        "--exit-on-reboot-required"
        "--no-startup-game"
        "--splash-image"
    */
}

