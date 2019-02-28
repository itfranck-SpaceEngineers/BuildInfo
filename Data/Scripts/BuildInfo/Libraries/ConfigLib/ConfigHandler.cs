﻿using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI;

namespace Digi.ConfigLib
{
    public sealed class ConfigHandler
    {
        public readonly string FileName;

        public readonly IntegerSetting ConfigVersion;

        /// <summary>
        /// NOTE: Already contains some comments, feel free to remove them.
        /// </summary>
        public readonly List<string> HeaderComments = new List<string>();
        public readonly List<string> FooterComments = new List<string>();

        internal readonly Dictionary<string, ISetting> Settings = new Dictionary<string, ISetting>(StringComparer.InvariantCultureIgnoreCase);

        private readonly char[] separatorCache = { VALUE_SEPARATOR };
        private StringBuilder sb = new StringBuilder();

        public const char VALUE_SEPARATOR = '=';
        public const string COMMENT_PREFIX = "# ";
        public const char MULTILINE_PREFIX = '|';
        public const StringComparison KEY_COMPARE_TYPE = StringComparison.InvariantCultureIgnoreCase;

        /// <summary>
        /// After settings were succesfully loaded.
        /// </summary>
        public event Action SettingsLoaded;

        /// <summary>
        /// Before settings are serialized and saved to file.
        /// </summary>
        public event Action SettingsSaving;

        public ConfigHandler(string fileName, int configVersion)
        {
            FileName = fileName;

            HeaderComments.Add($"Config for {Log.ModName}.");
            HeaderComments.Add($"Lines starting with {COMMENT_PREFIX} are comments. All values are case and space insensitive unless otherwise specified.");
            HeaderComments.Add("NOTE: This file gets overwritten after being loaded.");

            ConfigVersion = new IntegerSetting(this, "ConfigVersion", defaultValue: configVersion, min: int.MaxValue, max: int.MaxValue, commentLines: new string[]
                {
                    "Used for partial config edits for compatibility.",
                    "Do not change."
                });
            ConfigVersion.AddDefaultValueComment = false;
            ConfigVersion.AddValidRangeComment = false;
        }

        public void ResetToDefaults()
        {
            foreach(var setting in Settings.Values)
            {
                if(object.ReferenceEquals(setting, ConfigVersion))
                    continue; // don't affect config version

                setting.ResetToDefault();
            }
        }

        public bool LoadFromFile()
        {
            bool success = false;

            try
            {
                ResetToDefaults();

                if(MyAPIGateway.Utilities.FileExistsInLocalStorage(FileName, typeof(ConfigHandler)))
                {
                    using(var file = MyAPIGateway.Utilities.ReadFileInLocalStorage(FileName, typeof(ConfigHandler)))
                    {
                        string line;
                        int lineNumber = 0;
                        ISetting setting = null;

                        while((line = file.ReadLine()) != null)
                        {
                            ++lineNumber;

                            if(line.Length == 0)
                                continue;

                            var index = line.IndexOf(COMMENT_PREFIX);

                            if(index > -1)
                                line = (index == 0 ? "" : line.Substring(0, index));

                            if(line.Length == 0)
                                continue;

                            if(setting != null && setting.IsMultiLine && line[0] == MULTILINE_PREFIX)
                            {
                                var value = line.Substring(1);
                                ReadLine(setting, value, lineNumber);
                            }
                            else
                            {
                                setting = null;
                                var args = line.Split(separatorCache, 2);

                                if(args.Length != 2)
                                {
                                    Log.Error($"{FileName} unknown format on line #{lineNumber}: '{line}'", Log.PRINT_MSG);
                                    continue;
                                }

                                var key = args[0].Trim();

                                if(Settings.TryGetValue(key, out setting))
                                {
                                    if(!setting.IsMultiLine) // only send the subsequent lines for multi-line settings
                                    {
                                        var value = args[1];
                                        ReadLine(setting, value, lineNumber);
                                    }
                                }
                            }
                        }
                    }

                    SettingsLoaded?.Invoke();
                    success = true;
                }

                ConfigVersion.ResetToDefault(); // update config version
            }
            catch(Exception e)
            {
                Log.Error(e);
            }

            return success;
        }

        private void ReadLine(ISetting setting, string value, int lineNumber)
        {
            string error;
            setting.ReadValue(value, out error);

            if(error != null)
                Log.Error($"{FileName} line #{lineNumber} has an error: {error}", Log.PRINT_MSG);
        }

        public void SaveToFile()
        {
            try
            {
                SettingsSaving?.Invoke();

                sb.Clear();

                for(int i = 0; i < HeaderComments.Count; i++)
                {
                    sb.Append(ConfigHandler.COMMENT_PREFIX).Append(HeaderComments[i]).AppendLine();
                }

                sb.AppendLine();
                sb.AppendLine();

                foreach(var setting in Settings.Values)
                {
                    if(object.ReferenceEquals(setting, ConfigVersion))
                        continue; // config version is added last

                    setting.SaveSetting(sb);

                    sb.AppendLine();
                    sb.AppendLine();
                }

                for(int i = 0; i < FooterComments.Count; i++)
                {
                    sb.Append(ConfigHandler.COMMENT_PREFIX).Append(FooterComments[i]).AppendLine();
                }

                sb.AppendLine();
                sb.AppendLine();

                ConfigVersion.SaveSetting(sb);

                using(var file = MyAPIGateway.Utilities.WriteFileInLocalStorage(FileName, typeof(ConfigHandler)))
                {
                    file.Write(sb.ToString());
                }

                sb.Clear();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
