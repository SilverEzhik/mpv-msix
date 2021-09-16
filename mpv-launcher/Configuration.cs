using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace mpv_launcher
{
    static class ConfigurationParser
    {
        public static Dictionary<string, string> Parse(IEnumerable<string> lines)
        {
            Regex ConfigRegex = new(@"^\s*(?<Key>[^#=\s]+)(?:\s*=\s*(?:(?<QuotedValue>""(?:[^""\\]|\\.)*"")|(?<Value>[^#=]*)))?");

            var dict = new Dictionary<string, string>();
            foreach (var line in lines)
            {
                var match = ConfigRegex.Match(line);
                if (match.Success && match.Groups.ContainsKey("Key"))
                {
                    string key = match.Groups["Key"].Value;
                    string value;
                    if ((value = match.Groups["QuotedValue"].Value).Length > 0)
                    {
                        // parse quoted strings as json strings
                        value = JsonDocument.Parse(value).RootElement.GetString();
                    }
                    else if ((value = match.Groups["Value"].Value).Length > 0)
                    {
                        value = value.Trim();
                    }

                    dict[key] = value;
                }
            }

            return dict;
        }
    }

    enum SingleInstanceBehaviorEnum
    {
        Append,
        Replace,
        NewWindow,
        SpamWindows
    }

    class Configuration
    {
        private static string ConfigPath = Environment.ExpandEnvironmentVariables("%APPDATA%/mpv/mpv-launcher.conf");

        public static SingleInstanceBehaviorEnum? SingleInstanceBehaviorFromString(string str)
        {
            return str switch
            {
                "append" or "Append" => SingleInstanceBehaviorEnum.Append,
                "replace" or "Replace" => SingleInstanceBehaviorEnum.Replace,
                "new-window" or "NewWindow" => SingleInstanceBehaviorEnum.NewWindow,
                "spam-windows" or "SpamWindows" => SingleInstanceBehaviorEnum.SpamWindows,
                _ => null
            };
        }

        public static bool? BoolFromString(string str)
        {
            return str switch
            {
                "true" or "yes" => true,
                "false" or "no" => false,
                _ => null
            };
        }

        public SingleInstanceBehaviorEnum SingleInstanceBehavior = SingleInstanceBehaviorEnum.Replace;
        public bool SortFileExplorerSelection = true;
        public bool RaiseExistingWindow = true;

        public void Update(IDictionary<string, string> config)
        {
            string value;
            config.TryGetValue("single-instance-behavior", out value);
            SingleInstanceBehavior = SingleInstanceBehaviorFromString(value) ?? SingleInstanceBehavior;
            config.TryGetValue("sort-file-explorer-selection", out value);
            SortFileExplorerSelection = BoolFromString(value) ?? SortFileExplorerSelection;
            config.TryGetValue("raise-existing-window", out value);
            RaiseExistingWindow = BoolFromString(value) ?? RaiseExistingWindow;
        }

        public Configuration()
        {
            try
            {
                var configFileReader = File.ReadLines(ConfigPath, Encoding.UTF8);
                var config = ConfigurationParser.Parse(configFileReader);
                Update(config);
            }
            catch (IOException)
            {
                // use default settings if the config file couldn't be opened
            }
        }
    }
}
