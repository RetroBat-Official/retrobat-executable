using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

namespace RetroBat
{
    class Program
    {
        [STAThread]
        static void Main()
        {
            string exeName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
            if (!exeName.Equals("RetroBat-New.exe", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Executable name has been changed!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Environment.Exit(1);
            }

            File.WriteAllText("RetroBat-New.log", string.Empty); // Clear log file at startup
            SimpleLogger.Instance.Info("--------------------------------------------------------------");
            SimpleLogger.Instance.Info("[Startup] RetroBat-New.exe");

            CultureInfo windowsCulture = CultureInfo.CurrentUICulture;
            SimpleLogger.Instance.Info("Current culture: " + windowsCulture.ToString());

            string appFolder = AppDomain.CurrentDomain.BaseDirectory;
            string esPath = Path.Combine(appFolder, "emulationstation");

            // Ini file check and creation
            SimpleLogger.Instance.Info("Check ini file");
            string iniPath = Path.Combine(appFolder, "retrobat.ini");
            if (!File.Exists(iniPath))
            {
                SimpleLogger.Instance.Info("ini file does not exist yet, creating default file.");
                string iniDefault = IniFile.GetDefaultIniContent();
                try
                {
                    File.WriteAllText(iniPath, iniDefault);
                    SimpleLogger.Instance.Info("ini file written to " + iniPath);
                }
                catch { SimpleLogger.Instance.Warning("Impossible to create ini file."); }
            }

            // Check existence of required files
            SimpleLogger.Instance.Info("Checking availability of necessary files.");
            string templatepathES = Path.Combine(appFolder, "system", "templates", "emulationstation");

            if (!File.Exists(Path.Combine(esPath, "emulationstation.exe")))
            {
                SimpleLogger.Instance.Error("EmulationStation cannot be found at: " + Path.Combine(esPath, "emulationstation.exe"));
                throw new FileNotFoundException("EmulationStation executable not found.");
            }

            if (!File.Exists(Path.Combine(esPath, "emulatorlauncher.exe")))
            {
                SimpleLogger.Instance.Error("EmulatorLauncher cannot be found at: " + Path.Combine(esPath, "emulatorlauncher.exe"));
                throw new FileNotFoundException("EmulatorLauncher executable not found.");
            }

            if (!File.Exists(Path.Combine(esPath, ".emulationstation", "es_features.cfg")))
            {
                SimpleLogger.Instance.Error("es_features cannot be found at: " + Path.Combine(esPath, ".emulationstation", "es_features.cfg"));
                throw new FileNotFoundException("es_features not found.");
            }

            if (!File.Exists(Path.Combine(esPath, ".emulationstation", "es_systems.cfg")))
            {
                SimpleLogger.Instance.Warning("es_systems cannot be found, trying to copy template.");

                try { File.Copy(Path.Combine(templatepathES, "es_systems.cfg"), Path.Combine(esPath, ".emulationstation", "es_systems.cfg"), true); } catch { }

                if (!File.Exists(Path.Combine(esPath, ".emulationstation", "es_systems.cfg")))
                {
                    SimpleLogger.Instance.Error("es_systems cannot be found at: " + Path.Combine(esPath, ".emulationstation", "es_systems.cfg"));
                    throw new FileNotFoundException("es_systems not found.");
                }
            }

            if (!File.Exists(Path.Combine(esPath, "emulatorLauncher.cfg")))
            {
                SimpleLogger.Instance.Warning("emulatorLauncher.cfg cannot be found, trying to copy template.");

                try { File.Copy(Path.Combine(templatepathES, "emulatorLauncher.cfg"), Path.Combine(esPath, "emulatorLauncher.cfg"), true); } catch { }

                if (!File.Exists(Path.Combine(esPath, "emulatorLauncher.cfg")))
                {
                    SimpleLogger.Instance.Error("emulatorLauncher.cfg cannot be found at: " + Path.Combine(esPath, "emulatorLauncher.cfg"));
                    throw new FileNotFoundException("emulatorLauncher.cfg not found.");
                }
            }
            SimpleLogger.Instance.Info("All necessary files exist.");

            // Write path to registry
            RegistryTools.SetRegistryKey(appFolder);

            // Get values from ini file
            RetroBatConfig config = new RetroBatConfig();

            using (IniFile ini = new IniFile(iniPath))
            {
                SimpleLogger.Instance.Info("Reading values from inifile: " + iniPath);
                config = GetConfigValues(ini);

                foreach (PropertyInfo prop in config.GetType().GetProperties())
                    try { SimpleLogger.Instance.Info($"{prop.Name} = {prop.GetValue(config, null)}"); } catch { }
            }

            // Get emulationstation.exe path
            string emulationStationExe = Path.Combine(esPath, "emulationstation.exe");

            if (!File.Exists(emulationStationExe))
            {
                SimpleLogger.Instance.Error("Emulationstation executable not found in: " + emulationStationExe);
                return;
            }
            SimpleLogger.Instance.Info("EmulationStation.exe found.");

            // Language
            if (config.LanguageDetection)
                WriteLanguageToES(esPath, windowsCulture);

            // Set old OpenGL
            SetGLVersion(esPath, config.OpenGL2_1);

            // Set RetroBat to start at startup
            if (config.Autostart)
                AddToStartup("RetroBat", Path.Combine(appFolder, "RetroBat_New.exe"));
            else
                RemoveFromStartup("RetroBat");

            // Reset es_settings
            if (config.ResetConfigMode)
                ResetESConfig(appFolder);

            // Run splash video if enabled
            if (config.EnableIntro)
            {
                SplashVideo.RunIntroVideo(config, esPath);
                if (!config.FullscreenBorderless && !config.WaitForVideoEnd)
                    Thread.Sleep(config.VideoDelay);
            }

            // Arguments
            SimpleLogger.Instance.Info("Setting up arguments to run EmulationStation.");
            List<string> commandArray = new List<string>();

            bool borderless = config.FullscreenBorderless;

            if (config.Fullscreen && config.ForceFullscreenRes)
            {
                commandArray.Add("--resolution");
                commandArray.Add(config.WindowXSize.ToString());
                commandArray.Add(config.WindowYSize.ToString());
            }

            else if (!config.Fullscreen && !borderless)
            {
                commandArray.Add("--windowed");
                commandArray.Add("--resolution");
                commandArray.Add(config.WindowXSize.ToString());
                commandArray.Add(config.WindowYSize.ToString());
            }

            else if (borderless)
            {
                commandArray.Add("--fullscreen-borderless");
            }
            else
            {
                commandArray.Add("--fullscreen");
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

            if (config.VSync)
                commandArray.Add("--vsync 1");
            else
                commandArray.Add("--vsync 0");

            if (config.DrawFramerate)
                commandArray.Add("--draw-framerate");

            commandArray.Add("--home");
            commandArray.Add("\"" + esPath + "\"");

            string args = string.Join(" ", commandArray);

            // Run wiimoteGun if enabled
            if (config.WiimoteGun)
                RunWiimoteGun(esPath);

            // Run EmulationStation
            SimpleLogger.Instance.Info("Preparing to run emulationstation.");

            var start = new ProcessStartInfo()
            {
                FileName = emulationStationExe,
                WorkingDirectory = esPath,
                Arguments = args,
                UseShellExecute = false
            };

            if (start == null)
                return;

            TimeSpan uptime = TimeSpan.FromMilliseconds(Environment.TickCount);
            if (config.Autostart && !config.EnableIntro && uptime.TotalSeconds < 30)
            {
                SimpleLogger.Instance.Info("RetroBat set to run at startup, adding a 6 seconds delay.");
                System.Threading.Thread.Sleep(6000);
            }

            try
            {
                SimpleLogger.Instance.Info("Launching " + emulationStationExe + " " + args);

                var exe = Process.Start(start);
                /*exe.WaitForExit();
                
                if (exe != null)
                {
                    bool success = FocusHelper.BringProcessWindowToFrontWithRetry(exe);
                    if (!success)
                        SimpleLogger.Instance.Warning("Failed to bring EmulationStation window to front.");
                    else
                        SimpleLogger.Instance.Info("EmulationStation window is now in the foreground.");
                    Thread.Sleep(1000);
                }*/
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("Failed to start EmulationStation: " + ex.Message); }

            SimpleLogger.Instance.Info("All is good, enjoy, quitting RetroBat launcher.");
        }

        private static RetroBatConfig GetConfigValues(IniFile ini)
        {
            RetroBatConfig config = new RetroBatConfig
            {
                LanguageDetection = GetOptBoolean(IniFile.GetOptionValue(ini, "RetroBat", "LanguageDetection", "false")),
                ResetConfigMode = GetOptBoolean(IniFile.GetOptionValue(ini, "RetroBat", "ResetConfigMode", "false")),
                WiimoteGun = GetOptBoolean(IniFile.GetOptionValue(ini, "RetroBat", "WiimoteGun", "false")),
                EnableIntro = GetOptBoolean(IniFile.GetOptionValue(ini, "SplashScreen", "EnableIntro", "true")),
                RandomVideo = GetOptBoolean(IniFile.GetOptionValue(ini, "SplashScreen", "RandomVideo", "true")),
                GamepadVideoKill = GetOptBoolean(IniFile.GetOptionValue(ini, "SplashScreen", "GamepadVideoKill", "true")),
                KillVideoWhenESReady = GetOptBoolean(IniFile.GetOptionValue(ini, "SplashScreen", "KillVideoWhenESReady", "false")),
                WaitForVideoEnd = GetOptBoolean(IniFile.GetOptionValue(ini, "SplashScreen", "WaitForVideoEnd", "false")),
                FileName = IniFile.GetOptionValue(ini, "SplashScreen", "FileName", "retrobat-neon.mp4"),
                FilePath = IniFile.GetOptionValue(ini, "SplashScreen", "FilePath", "default"),
                Autostart = GetOptBoolean(IniFile.GetOptionValue(ini, "RetroBat", "Autostart", "false")),
                Fullscreen = GetOptBoolean(IniFile.GetOptionValue(ini, "EmulationStation", "Fullscreen", "true")),
                FullscreenBorderless = GetOptBoolean(IniFile.GetOptionValue(ini, "EmulationStation", "FullscreenBorderless", "true")),
                ForceFullscreenRes = GetOptBoolean(IniFile.GetOptionValue(ini, "EmulationStation", "ForceFullscreenRes", "false")),
                GameListOnly = GetOptBoolean(IniFile.GetOptionValue(ini, "EmulationStation", "GameListOnly", "false")),
                NoExitMenu = GetOptBoolean(IniFile.GetOptionValue(ini, "EmulationStation", "NoExitMenu", "false")),
                OpenGL2_1 = GetOptBoolean(IniFile.GetOptionValue(ini, "EmulationStation", "OpenGL2_1", "false")),
                VSync = GetOptBoolean(IniFile.GetOptionValue(ini, "EmulationStation", "VSync", "true")),
                DrawFramerate = GetOptBoolean(IniFile.GetOptionValue(ini, "EmulationStation", "DrawFramerate", "false")),
            };
            
            if (int.TryParse(IniFile.GetOptionValue(ini, "RetroBat", "AutoStartDelay", "5000"), out int startdelay))
                config.AutoStartDelay = startdelay;
            else
                config.AutoStartDelay = 5000;

            if (int.TryParse(IniFile.GetOptionValue(ini, "SplashScreen", "VideoDelay", "5000"), out int VideoDelay))
                config.VideoDelay = VideoDelay;
            else
                config.VideoDelay = 1000;

            if (int.TryParse(IniFile.GetOptionValue(ini, "EmulationStation", "InterfaceMode", "0"), out int interfaceMode))
                config.InterfaceMode = interfaceMode;
            else
                config.InterfaceMode = 0;

            if (int.TryParse(IniFile.GetOptionValue(ini, "EmulationStation", "MonitorIndex", "0"), out int monitorIndex))
                config.MonitorIndex = monitorIndex;
            else
                config.MonitorIndex = 0;

            if (int.TryParse(IniFile.GetOptionValue(ini, "EmulationStation", "WindowXSize", "1280"), out int windowX))
                config.WindowXSize = windowX;
            else
                config.WindowXSize = 1280;

            if (int.TryParse(IniFile.GetOptionValue(ini, "EmulationStation", "WindowYSize", "720"), out int windowY))
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

        private static void AddToStartup(string appName, string appPath)
        {
            SimpleLogger.Instance.Info("Setting RetroBat to launch at startup.");

            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                key.SetValue(appName, $"\"{appPath}\"");
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("Failed to set startup registry key: " + ex.Message);
            }
        }

        private static void RemoveFromStartup(string appName)
        {
            SimpleLogger.Instance.Info("Ensuring RetroBat does not launch at startup.");

            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                key.DeleteValue(appName);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("Failed to remove startup registry key: " + ex.Message);
            }
        }

        private static void RunWiimoteGun(string esPath)
        {
            SimpleLogger.Instance.Info("Running WiimoteGun.");

            string wgunExe = Path.Combine(esPath, "WiimoteGun.exe");

            if (!File.Exists(wgunExe))
            {
                SimpleLogger.Instance.Warning("WiimoteGun executable not found at: " + wgunExe);
                return;
            }

            try
            {
                var wgStart = new ProcessStartInfo
                {
                    FileName = wgunExe,
                    WorkingDirectory = esPath,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process.Start(wgStart);
                SimpleLogger.Instance.Info("WiimoteGun started successfully.");
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("Failed to start WiimoteGun: " + ex.Message); }
        }

        private static void ResetESConfig(string path)
        {
            SimpleLogger.Instance.Info("Resetting configuration.");

            List<string> filesToReset = new List<string>
            {
                "es_input.cfg",
                "es_padtokey.cfg",
                "es_settings.cfg",
                "es_systems.cfg"
            };

            string templatepathES = Path.Combine(path, "system", "templates", "emulationstation");
            string esPath = Path.Combine(path, "emulationstation");
            string targetPath = Path.Combine(esPath, ".emulationstation");

            foreach (var file in filesToReset)
            {
                string sourceFile = Path.Combine(templatepathES, file);
                string targetFile = Path.Combine(targetPath, file);

                if (File.Exists(sourceFile))
                {
                    try
                    {
                        string oldFile = targetFile + ".old";
                        File.Delete(oldFile);
                        File.Move(targetFile, oldFile);
                        File.Copy(sourceFile, targetFile, true);
                        SimpleLogger.Instance.Info($"Reset {file} to default.");
                    }
                    catch (Exception ex) { SimpleLogger.Instance.Warning($"Could not reset {file}: " + ex.Message); }
                }
                else
                    SimpleLogger.Instance.Warning($"Template file {sourceFile} does not exist.");
            }

            string rbIniFile = Path.Combine(path, "retrobat.ini");

            try
            {
                if (File.Exists(rbIniFile))
                {
                    try { File.Delete(rbIniFile); }
                    catch (Exception ex) { SimpleLogger.Instance.Warning("Could not delete RetroBat ini file: " + ex.Message); }

                    SimpleLogger.Instance.Info("Deleted RetroBat ini file: " + rbIniFile);
                }

                try
                {
                    string iniDefault = IniFile.GetDefaultIniContent();
                    File.WriteAllText(rbIniFile, iniDefault);
                    SimpleLogger.Instance.Info("ini file regenrated with default values.");
                }
                catch { SimpleLogger.Instance.Warning("Impossible to create ini file."); }
            }
            catch { SimpleLogger.Instance.Warning("Could not reinitialize ini file."); }
        }

        private static void WriteLanguageToES(string esPath, CultureInfo culture)
        {
            string esSettingsPath = Path.Combine(esPath, ".emulationstation", "es_settings.cfg");
            if (!File.Exists(esSettingsPath))
            {
                SimpleLogger.Instance.Error("es_settings.cfg cannot be found at: " + esSettingsPath);
                throw new FileNotFoundException("es_settings.cfg not found.");
            }
            else
                SimpleLogger.Instance.Info("es_settings.cfg path: " + esSettingsPath);

            SimpleLogger.Instance.Info("Updating EmulationStation language.");

            try
            {
                XmlDocument xml = new XmlDocument();
                xml.Load(esSettingsPath);
                XmlNode languageNode = xml.SelectSingleNode("//string[@name='Language']");

                if (languageNode != null && languageNode.Attributes != null)
                {
                    // Update existing node
                    languageNode.Attributes["value"].Value = culture.ToString();
                }
                else
                {
                    // Create the node
                    XmlElement newNode = xml.CreateElement("string");
                    newNode.SetAttribute("name", "Language");
                    newNode.SetAttribute("value", culture.ToString());

                    // Append to root <config> element
                    XmlNode configNode = xml.SelectSingleNode("/config");
                    if (configNode != null)
                        configNode.AppendChild(newNode);
                    else
                        SimpleLogger.Instance.Warning("Could not update EmulationStation language.");
                }
                xml.Save(esSettingsPath);
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("Could not update EmulationStation language: " + ex.Message); }
        }

        private static void SetGLVersion(string esPath, bool oldOpenGL)
        {
            string esSettingsPath = Path.Combine(esPath, ".emulationstation", "es_settings.cfg");
            if (!File.Exists(esSettingsPath))
            {
                SimpleLogger.Instance.Error("es_settings.cfg cannot be found at: " + esSettingsPath);
                throw new FileNotFoundException("es_settings.cfg not found.");
            }
            else
                SimpleLogger.Instance.Info("es_settings.cfg path: " + esSettingsPath);

            try
            {
                XmlDocument xml = new XmlDocument();
                xml.Load(esSettingsPath);
                XmlNode GLNode = xml.SelectSingleNode("//string[@name='Renderer']");

                if (GLNode != null && GLNode.Attributes != null)
                {
                    if (oldOpenGL)
                    {
                        SimpleLogger.Instance.Info("es_settings.cfg, setting old renderer");
                        GLNode.Attributes["value"].Value = "OPENGL 2.1";
                    }
                    else
                        GLNode.RemoveAll();
                }
                else if (oldOpenGL)
                {
                    // Create the node
                    XmlElement newNode = xml.CreateElement("string");
                    newNode.SetAttribute("name", "Renderer");
                    newNode.SetAttribute("value", "OPENGL 2.1");

                    // Append to root <config> element
                    XmlNode configNode = xml.SelectSingleNode("/config");
                    if (configNode != null)
                        configNode.AppendChild(newNode);
                    else
                        SimpleLogger.Instance.Warning("Could not update EmulationStation renderer.");
                }
                xml.Save(esSettingsPath);
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("Could not update EmulationStation renderer: " + ex.Message); }
        }
    }
}

