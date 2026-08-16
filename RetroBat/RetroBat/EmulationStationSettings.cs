using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;

namespace RetroBat
{
    internal static class EmulationStationSettings
    {
        private static readonly Random _rand = new Random();

        public static void WriteLanguage(string esPath, CultureInfo culture)
        {
            string cultureText = culture.Name.ToString().Replace('-', '_');
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
                    languageNode.Attributes["value"].Value = cultureText;
                }
                else
                {
                    // Create the node
                    XmlElement newNode = xml.CreateElement("string");
                    newNode.SetAttribute("name", "Language");
                    newNode.SetAttribute("value", cultureText);

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

        public static void SetGLVersion(string esPath, bool oldOpenGL)
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

        public static void SetRandomTheme(string esPath, bool randomTheme)
        {
            if (!randomTheme)
                return;

            bool updated = false;

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
                XmlNode Theme = xml.SelectSingleNode("//string[@name='ThemeSet']");

                if (Theme != null && Theme.Attributes != null)
                {
                    string currentTheme = Theme.Attributes["value"]?.Value;
                    string themesPath = Path.Combine(esPath, ".emulationstation", "themes");

                    if (Directory.Exists(themesPath))
                    {
                        var themeDirs = Directory.GetDirectories(themesPath);
                        var candidates = themeDirs.Select(Path.GetFileName).Where(t => !string.Equals(t, currentTheme, StringComparison.OrdinalIgnoreCase)).ToArray();

                        if (candidates.Length > 0)
                        {
                            string randomThemeName = candidates[_rand.Next(candidates.Length)];
                            SimpleLogger.Instance.Info("es_settings.cfg, setting random theme: " + randomThemeName);
                            Theme.Attributes["value"].Value = randomThemeName;
                            updated = true;
                        }
                        else
                            SimpleLogger.Instance.Warning("No themes found in themes directory.");
                    }
                    else
                        SimpleLogger.Instance.Warning("Themes directory not found at: " + themesPath);
                }
                if (updated)
                    xml.Save(esSettingsPath);
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("Could not update EmulationStation theme: " + ex.Message); }
        }

        public static void ResetToDefaults(string path)
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
    }
}
