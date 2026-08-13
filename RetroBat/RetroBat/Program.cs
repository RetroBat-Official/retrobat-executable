using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace RetroBat
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var esProcess = Process.GetProcessesByName("emulationstation").FirstOrDefault();
            if (esProcess != null)
            {
                SimpleLogger.Instance.Warning("EmulationStation already running");
                DialogResult result = MessageBox.Show(
                "RetroBat already running! Do you want to continue?",
                "Warning",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
                );

                if (result == DialogResult.No)
                {
                    // Quit the program
                    return;
                }
            }

            bool isExternalLauncher = args.Contains("--external-launcher", StringComparer.OrdinalIgnoreCase);

            string appFolder = AppDomain.CurrentDomain.BaseDirectory;
            Directory.SetCurrentDirectory(appFolder);

            File.WriteAllText(Path.Combine(appFolder, "RetroBat.log"), string.Empty); // Clear log file at startup
            SimpleLogger.Instance.Info("--------------------------------------------------------------");

            string actualPath = Process.GetCurrentProcess().MainModule.FileName;
            string actual = Path.GetFileName(actualPath).Trim().Normalize(NormalizationForm.FormC);

            SimpleLogger.Instance.Info("Actual executable name: " + actual);

            if (!actual.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !string.Equals(actual, "RetroBat.exe", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Executable name has been changed!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            SimpleLogger.Instance.Info("[Startup] RetroBat.exe");

            CultureInfo windowsCulture = CultureInfo.CurrentUICulture;
            SimpleLogger.Instance.Info("Current culture: " + windowsCulture.ToString());

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
            StartupFileChecker.EnsureRequiredFilesExist(appFolder, esPath);

            // Write path to registry
            RegistryTools.SetRegistryKey(appFolder);

            // Get values from ini file
            RetroBatConfig config = RetroBatConfigLoader.Load(iniPath);

            // Launch companion apps as early as possible, in parallel with the rest of the startup sequence
            EmulationStationLauncher.RunExternalApps(config.AppLaunchers);

            // Get emulationstation.exe path
            string emulationStationExe = Path.Combine(esPath, "emulationstation.exe");

            if (!File.Exists(emulationStationExe))
            {
                SimpleLogger.Instance.Error("Emulationstation executable not found in: " + emulationStationExe);
                return;
            }
            SimpleLogger.Instance.Info("EmulationStation.exe found.");

            // DPI Awareness
            DpiAwarenessManager.ApplyOverridesIfNeeded(appFolder);

            // Language
            if (config.LanguageDetection)
                EmulationStationSettings.WriteLanguage(esPath, windowsCulture);

            // Set old OpenGL
            EmulationStationSettings.SetGLVersion(esPath, config.OpenGL2_1);

            // Set theme to random if enabled
            EmulationStationSettings.SetRandomTheme(esPath, config.RandomTheme);

            // Set RetroBat to start at startup
            AutostartManager.CleanupLegacyShortcut();
            AutostartManager.Apply(config.Autostart, appFolder, "RetroBat.exe");

            // Reset es_settings
            if (config.ResetConfigMode)
                EmulationStationSettings.ResetToDefaults(appFolder);

            // Run splash video if enabled
            var screens = Screen.AllScreens;
            Screen targetScreen = Screen.PrimaryScreen;

            if (config.MonitorIndex > 0 && config.MonitorIndex < screens.Length)
            {
                targetScreen = screens[config.MonitorIndex];
                SimpleLogger.Instance.Info($"Using monitor index {config.MonitorIndex} ({targetScreen.DeviceName}).");
            }
            else
            {
                SimpleLogger.Instance.Info("Monitor index out of range or 0, using primary screen.");
            }

            bool canRunIntro = SplashVideo.CanRunIntroVideo(config, esPath);

            try
            {
                if (canRunIntro)
                {
                    SplashVideo.ShowBlackSplash(targetScreen);
                    var splashStart = DateTime.UtcNow;

                    var videoDone = SplashVideo.RunIntroVideo(config, esPath, targetScreen, isExternalLauncher);

                    // Wait depending on mode
                    if (config.WaitForVideoEnd)
                    {
                        videoDone.WaitOne();
                    }
                    else if (config.VideoDelay > 0)
                    {
                        videoDone.WaitOne(config.VideoDelay);
                    }

                    // Ensure total splash duration >= VideoDelay
                    int elapsed = (int)(DateTime.UtcNow - splashStart).TotalMilliseconds;
                    int remaining = config.VideoDelay - elapsed;

                    if (remaining > 0)
                    {
                        Thread.Sleep(remaining);
                    }
                }

                // Arguments
                SimpleLogger.Instance.Info("Setting up arguments to run EmulationStation.");
                string elargs = EmulationStationLauncher.BuildArguments(config, esPath, screens);

                // Run wiimoteGun if enabled
                if (config.WiimoteGun)
                    EmulationStationLauncher.RunWiimoteGun(esPath);

                // Run EmulationStation
                SimpleLogger.Instance.Info("Preparing to run emulationstation.");

                var start = new ProcessStartInfo()
                {
                    FileName = emulationStationExe,
                    WorkingDirectory = esPath,
                    Arguments = elargs,
                    UseShellExecute = false
                };

                TimeSpan uptime = TimeSpan.FromMilliseconds(Environment.TickCount);
                if (config.Autostart != 0 && uptime.TotalSeconds < 10 && config.AutoStartDelay > 0)
                {
                    SimpleLogger.Instance.Info("RetroBat set to run at startup, adding a delay.");
                    int delay = config.AutoStartDelay;
                    Thread.Sleep(delay);
                }

                if (!EmulationStationLauncher.LaunchAndFocus(start, config, isExternalLauncher))
                    return;
            }

            finally
            {
                SplashVideo.CloseBlackSplash();
            }

            SimpleLogger.Instance.Info("All is good, enjoy, quitting RetroBat launcher.");
        }
    }
}
