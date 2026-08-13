using System;
using System.Reflection;

namespace RetroBat
{
    internal static class RetroBatConfigLoader
    {
        public static RetroBatConfig Load(string iniPath)
        {
            RetroBatConfig config;

            using (IniFile ini = new IniFile(iniPath))
            {
                SimpleLogger.Instance.Info("Reading values from inifile: " + iniPath);
                config = GetConfigValues(ini);

                foreach (PropertyInfo prop in config.GetType().GetProperties())
                    try { SimpleLogger.Instance.Info($"{prop.Name} = {prop.GetValue(config, null)}"); }
                    catch (Exception ex) { SimpleLogger.Instance.Warning($"Failed to log config property '{prop.Name}': " + ex.Message); }
            }

            return config;
        }

        private static RetroBatConfig GetConfigValues(IniFile ini)
        {
            return new RetroBatConfig
            {
                LanguageDetection = GetOptBool(ini, "RetroBat", "LanguageDetection", true),
                ResetConfigMode = GetOptBool(ini, "RetroBat", "ResetConfigMode", false),
                Autostart = GetOptInt(ini, "RetroBat", "Autostart", 0),
                AutoStartDelay = GetOptInt(ini, "RetroBat", "AutoStartDelay", 0),
                WiimoteGun = GetOptBool(ini, "RetroBat", "WiimoteGun", false),
                EnableIntro = GetOptBool(ini, "SplashScreen", "EnableIntro", true),
                RandomVideo = GetOptBool(ini, "SplashScreen", "RandomVideo", true),
                GamepadVideoKill = GetOptBool(ini, "SplashScreen", "GamepadVideoKill", true),
                KillVideoWhenESReady = GetOptBool(ini, "SplashScreen", "KillVideoWhenESReady", false),
                WaitForVideoEnd = GetOptBool(ini, "SplashScreen", "WaitForVideoEnd", true),
                FileName = IniFile.GetOptionValue(ini, "SplashScreen", "FileName", "retrobat-neon.mp4"),
                FilePath = IniFile.GetOptionValue(ini, "SplashScreen", "FilePath", "default"),
                VideoDelay = GetOptInt(ini, "SplashScreen", "VideoDelay", 1000),
                Fullscreen = GetOptBool(ini, "EmulationStation", "Fullscreen", true),
                FullscreenBorderless = GetOptBool(ini, "EmulationStation", "FullscreenBorderless", true),
                ForceFullscreenRes = GetOptBool(ini, "EmulationStation", "ForceFullscreenRes", false),
                GameListOnly = GetOptBool(ini, "EmulationStation", "GameListOnly", false),
                NoExitMenu = GetOptBool(ini, "EmulationStation", "NoExitMenu", false),
                OpenGL2_1 = GetOptBool(ini, "EmulationStation", "OpenGL2_1", false),
                VSync = GetOptBool(ini, "EmulationStation", "VSync", true),
                DrawFramerate = GetOptBool(ini, "EmulationStation", "DrawFramerate", false),
                RandomTheme = GetOptBool(ini, "EmulationStation", "RandomTheme", false),
                FocusDelay = GetOptInt(ini, "EmulationStation", "FocusDelay", 2000),
                InterfaceMode = GetOptInt(ini, "EmulationStation", "InterfaceMode", 0),
                MonitorIndex = GetOptInt(ini, "EmulationStation", "MonitorIndex", 0),
                WindowXSize = GetOptInt(ini, "EmulationStation", "WindowXSize", 1280),
                WindowYSize = GetOptInt(ini, "EmulationStation", "WindowYSize", 720)
            };
        }

        private static bool GetOptBool(IniFile ini, string section, string key, bool defaultValue)
        {
            return GetOptBoolean(IniFile.GetOptionValue(ini, section, key, defaultValue ? "true" : "false"));
        }

        private static int GetOptInt(IniFile ini, string section, string key, int defaultValue)
        {
            string raw = IniFile.GetOptionValue(ini, section, key, defaultValue.ToString());
            return int.TryParse(raw, out int value) ? value : defaultValue;
        }

        private static bool GetOptBoolean(string input)
        {
            if (input == "1" || input == "true" || input == "yes")
                return true;
            else
                return false;
        }
    }
}
