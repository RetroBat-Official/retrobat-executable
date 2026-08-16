using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RetroBat
{
    internal static class StartupFileChecker
    {
        public static void EnsureRequiredFilesExist(string appFolder, string esPath)
        {
            SimpleLogger.Instance.Info("Checking availability of necessary files.");
            string templatepathES = Path.Combine(appFolder, "system", "templates", "emulationstation");

            var esFiles = new HashSet<string>(Directory.EnumerateFiles(esPath).Select(Path.GetFileName), System.StringComparer.OrdinalIgnoreCase);

            if (!esFiles.Contains("about.info"))
            {
                SimpleLogger.Instance.Warning("Creating file 'about.info'");
                try { File.WriteAllText(Path.Combine(esPath, "about.info"), "RETROBAT"); }
                catch { SimpleLogger.Instance.Warning("Impossible to create about.info file."); }
            }

            if (!esFiles.Contains("emulationstation.exe"))
            {
                SimpleLogger.Instance.Error("EmulationStation cannot be found at: " + Path.Combine(esPath, "emulationstation.exe"));
                throw new FileNotFoundException("EmulationStation executable not found.");
            }

            if (!esFiles.Contains("emulatorlauncher.exe"))
            {
                SimpleLogger.Instance.Error("EmulatorLauncher cannot be found at: " + Path.Combine(esPath, "emulatorlauncher.exe"));
                throw new FileNotFoundException("EmulatorLauncher executable not found.");
            }

            if (!esFiles.Contains("batocera-store.exe"))
                SimpleLogger.Instance.Warning("Batocera-store executable not found, continuing without it.");

            if (!esFiles.Contains("batocera-systems.exe"))
                SimpleLogger.Instance.Warning("Batocera-systems executable not found, continuing without it.");

            if (!esFiles.Contains("es-update.exe"))
                SimpleLogger.Instance.Warning("es-update executable not found, continuing without it.");

            if (!esFiles.Contains("es-checkversion.exe"))
                SimpleLogger.Instance.Warning("es-checkversion executable not found, continuing without it.");

            if (!esFiles.Contains("emulatorlauncher.common.dll"))
            {
                SimpleLogger.Instance.Error("emulatorlauncher common DLL does not exist");
                throw new FileNotFoundException("emulatorlauncher common DLL not found.");
            }

            if (!File.Exists(Path.Combine(esPath, ".emulationstation", "es_features.cfg")))
            {
                SimpleLogger.Instance.Error("es_features cannot be found at: " + Path.Combine(esPath, ".emulationstation", "es_features.cfg"));
                throw new FileNotFoundException("es_features not found.");
            }

            if (!File.Exists(Path.Combine(esPath, ".emulationstation", "es_systems.cfg")))
            {
                SimpleLogger.Instance.Warning("es_systems cannot be found, trying to copy template.");

                try { File.Copy(Path.Combine(templatepathES, "es_systems.cfg"), Path.Combine(esPath, ".emulationstation", "es_systems.cfg"), true); }
                catch (System.Exception ex) { SimpleLogger.Instance.Warning("Failed to copy es_systems.cfg template: " + ex.Message); }

                if (!File.Exists(Path.Combine(esPath, ".emulationstation", "es_systems.cfg")))
                {
                    SimpleLogger.Instance.Error("es_systems cannot be found at: " + Path.Combine(esPath, ".emulationstation", "es_systems.cfg"));
                    throw new FileNotFoundException("es_systems not found.");
                }
            }

            if (!File.Exists(Path.Combine(esPath, "emulatorLauncher.cfg")))
            {
                SimpleLogger.Instance.Warning("emulatorLauncher.cfg cannot be found, trying to copy template.");

                try { File.Copy(Path.Combine(templatepathES, "emulatorLauncher.cfg"), Path.Combine(esPath, "emulatorLauncher.cfg"), true); }
                catch (System.Exception ex) { SimpleLogger.Instance.Warning("Failed to copy emulatorLauncher.cfg template: " + ex.Message); }

                if (!File.Exists(Path.Combine(esPath, "emulatorLauncher.cfg")))
                {
                    SimpleLogger.Instance.Error("emulatorLauncher.cfg cannot be found at: " + Path.Combine(esPath, "emulatorLauncher.cfg"));
                    throw new FileNotFoundException("emulatorLauncher.cfg not found.");
                }
            }
            SimpleLogger.Instance.Info("All necessary files exist.");
        }
    }
}
