using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;

namespace RetroBat
{
    [Flags]
    public enum IniOptions
    {
        UseSpaces = 1,
        KeepEmptyValues = 2,
        AllowDuplicateValues = 4,
        KeepEmptyLines = 8,
        UseDoubleEqual = 16,
        ManageKeysWithQuotes = 32
    }

    public class IniFile : IDisposable
    {
        public static IniFile FromFile(string path, IniOptions options = (IniOptions)0)
        {
            return new IniFile(path, options);
        }

        public static string GetOptionValue(IniFile ini, string section, string key, string defaultValue)
        {
            string value = ini.GetValue(section, key);

            if (!string.IsNullOrEmpty(value))
                return value.Trim('"');
            else
                return defaultValue;
        }

        public static string GetDefaultIniContent()
        {
            return @"; RETROBAT GLOBAL CONFIG FILE

[RetroBat]

; At startup RetroBat will detect or not the language used in Windows to set automatically the same language in the frontend and RetroArch emulator.
LanguageDetection=0

; At startup RetroBat will reset the default config files options of emulationstation and retrobat.ini.
; Use at your own risk.	
ResetConfigMode=0

; Run automatically RetroBat at Windows startup.
Autostart=0

; Set the Start Delay for RetroBat to start automatically at startup (1000 is one second).
AutoStartDelay=5000

; Run WiimoteGun at RetroBat's startup. You can use your wiimote as a gun and navigate through EmulationStation.
WiimoteGun=0

[SplashScreen]

; Set if video introduction is played before running the interface.
EnableIntro=1

; The name of the video file to play. RandomVideo must be set on 0 to take effect.
FileName=""retrobat-neon.mp4""

; If 'default' is set, RetroBat will use the default video path where video files are stored.
; Enter a full path to use a custom directory for video files.
FilePath=""default""

; Play video files randomly when RetroBat starts.
RandomVideo=1

; Set the delay between the start of the video and the start of the interface.
; Setting a longer delay can help if the video is not displayed in the foreground
VideoDelay=1000

; By default RetroBat loads EmulationStation in parallel of the intro video, setting this to '1' tells RetroBat to wait for the video to finish before loading ES
WaitForVideoEnd=1

; Set this to stop when video automatically when the interface has loaded
KillVideoWhenESReady=0

; Allow killing intro video with Gamepad press (this only works with XInput controllers)
GamepadVideoKill=1

[EmulationStation]

; Start the frontend in fullscreen or in windowed mode.
Fullscreen=1

; Borderless Fullscreen
FullscreenBorderless=1

; Force the fullscreen resolution with the parameters set at WindowXSize and WindowYSize.
ForceFullscreenRes=0

; The frontend will parse only the gamelist.xml files in roms directories to display available games.
; If files are added when this option is enabled, they will not appear in the gamelists of the frontend. The option must be enabled again to display new entries properly.
GameListOnly=0
 
; 0 = run the frontend normally.
; 1 = run the frontend in kiosk mode.
; 2 = run the frontend in kid mode.
InterfaceMode=0

; Set to which monitor index the frontend will be displayed.
MonitorIndex=0

; Disable to disable VSync in RetroBat interface.
VSync=1

; Set if the option to quit the frontend is displayed or not when the full menu is enabled.
NoExitMenu=0

; Set if you are using an old GPU not compatible with newest OpenGL version.
OpenGL2_1=0

; Set the windows width of the frontend.
WindowXSize=1280

; Set the windows height of the frontend.
WindowYSize=720

; Draw framerate in EmulationStation.
DrawFramerate=0";
}

        public void SetOptions(IniOptions options)
        {
            _options = options;
        }

        public IniFile(string path, IniOptions options = (IniOptions)0)
        {
            _options = options;
            _path = path;
            _dirty = false;

            if (!File.Exists(_path))
                return;

            try
            {
                using (TextReader iniFile = new StreamReader(_path))
                {
                    Section currentSection = null;

                    var namesInSection = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    string strLine = iniFile.ReadLine();
                    while (strLine != null)
                    {
                        strLine = strLine.Trim();

                        if (strLine != "" || _options.HasFlag(IniOptions.KeepEmptyLines))
                        {
                            if (strLine.StartsWith("["))
                            {
                                int end = strLine.IndexOf("]");
                                if (end > 0)
                                {
                                    namesInSection.Clear();
                                    currentSection = _sections.GetOrAddSection(strLine.Substring(1, end - 1));
                                }
                            }
                            else
                            {
                                string[] keyPair = _options.HasFlag(IniOptions.UseDoubleEqual) ? strLine.Split(new string[] { "==" }, 2, StringSplitOptions.None) : strLine.Split(new char[] { '=' }, 2);

                                if (currentSection == null)
                                {
                                    namesInSection.Clear();
                                    currentSection = _sections.GetOrAddSection(null);
                                }

                                var key = new Key();

                                string keyName = keyPair[0].Trim();

                                if (_options.HasFlag(IniOptions.ManageKeysWithQuotes))
                                {
                                    // If the key is surrounded by quotes, remove them
                                    if (keyName.StartsWith("\"") && keyName.EndsWith("\""))
                                    {
                                        keyName = keyName.Substring(1, keyName.Length - 2);  // Remove quotes
                                    }
                                }

                                key.Name = keyName;

                                if (!key.IsComment && !_options.HasFlag(IniOptions.AllowDuplicateValues) && namesInSection.Contains(key.Name))
                                {
                                    strLine = iniFile.ReadLine();
                                    continue;
                                }

                                if (key.IsComment)
                                {
                                    key.Name = strLine;
                                    key.Value = null;
                                }
                                else if (keyPair.Length > 1)
                                {
                                    namesInSection.Add(key.Name);

                                    var commentIdx = keyPair[1].IndexOf(";");
                                    if (commentIdx > 0)
                                    {
                                        key.Comment = keyPair[1].Substring(commentIdx);
                                        keyPair[1] = keyPair[1].Substring(0, commentIdx);
                                    }

                                    key.Value = keyPair[1].Trim();
                                }

                                currentSection.Add(key);
                            }
                        }

                        strLine = iniFile.ReadLine();
                    }

                    iniFile.Close();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public IniSection GetOrCreateSection(string key)
        {
            return new PrivateIniSection(key, this);
        }

        class PrivateIniSection : IniSection { public PrivateIniSection(string name, IniFile ini) : base(name, ini) { } }

        public string[] EnumerateSections()
        {
            return _sections.Select(s => s.Name).Distinct().ToArray();
        }

        public string[] EnumerateKeys(string sectionName)
        {
            var section = _sections.Get(sectionName);
            if (section != null)
                return section.Select(k => k.Name).ToArray();

            return new string[] { };
        }

        public KeyValuePair<string, string>[] EnumerateValues(string sectionName)
        {
            var ret = new List<KeyValuePair<string, string>>();

            var section = _sections.Get(sectionName);
            if (section != null)
            {
                foreach (var item in section)
                {
                    if (item.IsComment || string.IsNullOrEmpty(item.Name))
                        continue;

                    ret.Add(new KeyValuePair<string, string>(item.Name, item.Value));
                }
            }

            return ret.ToArray();
        }

        public void ClearSection(string sectionName)
        {
            var section = _sections.Get(sectionName);
            if (section != null && section.Any())
            {
                _dirty = true;
                section.Clear();
            }
        }

        public string GetValue(string sectionName, string key)
        {
            var section = _sections.Get(sectionName);
            if (section != null)
                return section.GetValue(key);

            return null;
        }

        public void WriteValue(string sectionName, string keyName, string value)
        {
            var section = _sections.GetOrAddSection(sectionName);

            var key = section.Get(keyName);
            if (key != null && key.Value == value)
                return;

            if (key == null)
                key = section.Add(keyName);

            key.Value = value;

            _dirty = true;
        }

        public void AppendValue(string sectionName, string keyName, string value)
        {
            if (!_options.HasFlag(IniOptions.AllowDuplicateValues))
            {
                WriteValue(sectionName, keyName, value);
                return;
            }

            var section = _sections.GetOrAddSection(sectionName);
            section.Add(keyName, value);

            _dirty = true;
        }

        public void Remove(string sectionName, string keyName)
        {
            var section = _sections.Get(sectionName);
            if (section != null)
            {
                foreach (var key in section.Where(k => k.Name.Equals(keyName, StringComparison.InvariantCultureIgnoreCase)).ToArray())
                {
                    _dirty = true;
                    section.Remove(key);
                }
            }
        }

        public bool IsDirty { get { return _dirty; } }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            foreach (var section in _sections)
            {
                if (!string.IsNullOrEmpty(section.Name) && section.Name != "ROOT" && section.Any())
                    sb.AppendLine("[" + section.Name + "]");

                foreach (Key entry in section)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        if (!string.IsNullOrEmpty(entry.Comment))
                            sb.AppendLine(entry.Comment);
                        else if (_options.HasFlag(IniOptions.KeepEmptyLines))
                            sb.AppendLine();

                        continue;
                    }

                    if (entry.IsComment)
                    {
                        sb.AppendLine(entry.Name);
                        continue;
                    }

                    if (string.IsNullOrEmpty(entry.Value) && !_options.HasFlag(IniOptions.KeepEmptyValues))
                        continue;

                    if (string.IsNullOrEmpty(entry.Name))
                        continue;

                    if (_options.HasFlag(IniOptions.ManageKeysWithQuotes))
                    {
                        sb.Append("\"" + entry.Name + "\"");  // Add quotes around the key
                    }

                    else
                    {
                        sb.Append(entry.Name);
                    }

                    if (_options.HasFlag(IniOptions.UseSpaces))
                        sb.Append(" ");

                    if (_options.HasFlag(IniOptions.UseDoubleEqual))
                        sb.Append("==");
                    else
                        sb.Append("=");

                    if (_options.HasFlag(IniOptions.UseSpaces))
                        sb.Append(" ");

                    sb.Append(entry.Value);

                    if (!string.IsNullOrEmpty(entry.Comment))
                    {
                        sb.Append("\t\t\t");
                        sb.Append(entry.Comment);
                    }

                    sb.AppendLine();
                }

                if (!_options.HasFlag(IniOptions.KeepEmptyLines))
                    sb.AppendLine();
            }

            return sb.ToString();
        }

        public void Save()
        {
            if (!_dirty)
                return;

            try
            {
                string dir = Path.GetDirectoryName(_path);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using (TextWriter tw = new StreamWriter(_path))
                {
                    tw.Write(ToString());
                    tw.Close();
                }

                _dirty = false;
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[IniFile] Save failed " + ex.Message, ex);
            }
        }

        public void Dispose()
        {
            Save();
        }

        private IniOptions _options;
        private bool _dirty;
        private string _path;

        #region Private classes
        class Key
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public string Comment { get; set; }

            public bool IsComment
            {
                get
                {
                    return Name == null || Name.StartsWith(";") || Name.StartsWith("#");
                }
            }

            public override string ToString()
            {
                if (string.IsNullOrEmpty(Name))
                    return "";

                if (string.IsNullOrEmpty(Value))
                    return Name + "=";

                return Name + "=" + Value;
            }
        }

        class KeyList : List<Key>
        {

        }

        class Section : IEnumerable<Key>
        {
            public Section()
            {
                _keys = new KeyList();
            }

            public string Name { get; set; }


            public override string ToString()
            {
                if (string.IsNullOrEmpty(Name))
                    return "";

                return "[" + Name + "]";
            }

            public bool Exists(string keyName)
            {
                foreach (var key in _keys)
                    if (key.Name.Equals(keyName, StringComparison.InvariantCultureIgnoreCase))
                        return true;

                return false;
            }

            public Key Get(string keyName)
            {
                foreach (var key in _keys)
                    if (key.Name.Equals(keyName, StringComparison.InvariantCultureIgnoreCase))
                        return key;

                return null;
            }

            public string GetValue(string keyName)
            {
                foreach (var key in _keys)
                    if (key.Name.Equals(keyName, StringComparison.InvariantCultureIgnoreCase))
                        return key.Value;

                return null;
            }

            private KeyList _keys;

            public IEnumerator<Key> GetEnumerator()
            {
                return _keys.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return _keys.GetEnumerator();
            }

            public Key Add(string keyName, string value = null)
            {
                var key = new Key() { Name = keyName, Value = value };
                _keys.Add(key);
                return key;
            }

            public Key Add(Key key)
            {
                _keys.Add(key);
                return key;
            }

            internal void Clear()
            {
                _keys.Clear();
            }

            internal void Remove(Key key)
            {
                _keys.Remove(key);
            }
        }

        class Sections : IEnumerable<Section>
        {
            public Sections()
            {
                _sections = new List<Section>();
            }

            public Section Get(string sectionName)
            {
                if (sectionName == null)
                    sectionName = string.Empty;

                return _sections.FirstOrDefault(s => s.Name.Equals(sectionName, StringComparison.InvariantCultureIgnoreCase));
            }

            public Section GetOrAddSection(string sectionName)
            {
                if (sectionName == null)
                    sectionName = string.Empty;

                var section = Get(sectionName);
                if (section == null)
                {
                    section = new Section() { Name = sectionName };

                    if ((string.IsNullOrEmpty(sectionName) || sectionName == "ROOT") && _sections.Count > 0)
                        _sections.Insert(0, section);
                    else
                        _sections.Add(section);
                }

                return section;
            }

            private List<Section> _sections;

            public IEnumerator<Section> GetEnumerator()
            {
                return _sections.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return _sections.GetEnumerator();
            }
        }

        private Sections _sections = new Sections();
        #endregion
    }

    public class IniSection
    {
        private IniFile _ini;
        private string _sectionName;

        protected IniSection(string name, IniFile ini)
        {
            _ini = ini;
            _sectionName = name;
        }

        public string this[string key]
        {
            get
            {
                return _ini.GetValue(_sectionName, key);
            }
            set
            {
                _ini.WriteValue(_sectionName, key, value);
            }
        }

        public void Clear()
        {
            _ini.ClearSection(_sectionName);
        }

        public string[] Keys
        {
            get
            {
                return _ini.EnumerateKeys(_sectionName);
            }
        }
    }

    public class RetroBatConfig
    {
        public bool LanguageDetection { get; set; }
        public bool ResetConfigMode { get; set; }
        public bool Autostart { get; set; }
        public int AutoStartDelay { get; set; }
        public bool WiimoteGun { get; set; }
        public bool EnableIntro { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public bool RandomVideo { get; set; }
        public int VideoDelay { get; set; }
        public bool KillVideoWhenESReady { get; set; }
        public bool WaitForVideoEnd { get; set; }
        public bool GamepadVideoKill { get; set; }
        public bool Fullscreen { get; set; }
        public bool FullscreenBorderless { get; set; }
        public bool ForceFullscreenRes { get; set; }
        public bool GameListOnly { get; set; }
        public int InterfaceMode { get; set; }
        public int MonitorIndex { get; set; }
        public bool NoExitMenu { get; set; }
        public bool OpenGL2_1 { get; set;}
        public bool VSync { get; set; }
        public bool DrawFramerate { get; set; }
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