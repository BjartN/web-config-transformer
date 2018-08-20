using Microsoft.Web.XmlTransform;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Transformer
{
    class Program
    {
        static void Main(string[] args)
        {
            var defaultName = "{default}";
            var workingFolder = ConfigurationManager.AppSettings["workingFolder"];
            var sourceFolder = ConfigurationManager.AppSettings["sourceFolder"];
            var targetFile = ConfigurationManager.AppSettings["targetFile"];

            var webConfig = Path.Combine(sourceFolder, "web.config");
            var defaults = ReadAppsettings(webConfig);

            var files = Directory.GetFiles(sourceFolder, "web*.config")
                .Where(x => new FileInfo(x).Name.ToLower() != "web.config");

            foreach (var transform in files)
            {
                var fi = new FileInfo(transform);
                Transformer.TransformConfig(webConfig, transform, Path.Combine(workingFolder, fi.Name));
            }

            var clientSettings = new Dictionary<string, IDictionary<string, string>>();
            clientSettings["default"] = defaults;

            var clients = new List<string>();
            foreach (var f in Directory.GetFiles(workingFolder, "*.config"))
            {
                var client = new FileInfo(f).Name.ToLower()
                    .Replace("web.", "")
                    .Replace(".config", "");

                clients.Add(client);

                var dic = ReadAppsettings(f);
                clientSettings[client] = dic;
            }

            var allSettings = clientSettings.SelectMany(x => x.Value.Select(y => y.Key))
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

            //RemoveSettingWhenAllSameAsDefault(allSettings, defaults, clientSettings);

            var separator = ",";
            var header = separator + "default," + string.Join(separator, clients);
            var csv = header + Environment.NewLine;
            foreach (var setting in allSettings)
            {
                var row = setting + separator;
                foreach (var clientSetting in clientSettings)
                {
                    if (clientSetting.Value.ContainsKey(setting))
                    {
                        var value = clientSetting.Value[setting];

                        var isDefaultValue = clientSetting.Key != "default" && defaults.ContainsKey(setting) && defaults[setting] == value;
                        if (isDefaultValue)
                        {
                            value = defaultName;
                        }

                        row += "\"" + value.Replace("\"", "\"\"") + "\"" + separator;
                    }
                    else
                    {
                        row += separator;
                    }
                }
                row += Environment.NewLine;
                csv += row;
            }

            Console.WriteLine(csv);

            File.WriteAllText(targetFile, csv);
        }

        private static void RemoveSettingWhenAllSameAsDefault(
            string[] allSettings,
            IDictionary<string, string> defaults,
            IDictionary<string, IDictionary<string, string>> clientSettings)
        {
            var settingsToRemove = new List<string>();
            foreach (var setting in allSettings)
            {
                if (!defaults.ContainsKey(setting))
                    continue;

                var defaultValue = defaults[setting];
                if (clientSettings.All(x => x.Value.ContainsKey(setting) && x.Value[setting] == defaultValue))
                {
                    settingsToRemove.Add(setting);
                }
            }

            foreach (var s in settingsToRemove)
            {
                foreach (var d in clientSettings)
                {
                    if (d.Value.ContainsKey(s))
                    {
                        d.Value.Remove(s);
                    }
                }
            }
        }

        private static IDictionary<string, string> ReadAppsettings(string file)
        {
            var doc = XDocument.Load(file);
            var query = doc.Descendants().Where(x => x.Name.LocalName == "appSettings").Descendants().Select(s => new
            {
                key = s.Attribute("key").Value,
                value = s.Attribute("value").Value
            }).GroupBy(x => x.key).Select(x => x.First())

                .ToDictionary(x => x.key, x => x.value);

            return query;
        }
    }

    public class Transformer
    {

        public static void TransformConfig(string configFileName, string transformFileName, string targetFileName)
        {
            var document = new XmlTransformableDocument();
            document.PreserveWhitespace = true;
            document.Load(configFileName);

            var transformation = new XmlTransformation(transformFileName);
            if (!transformation.Apply(document))
            {
                throw new Exception("Transformation Failed");
            }
            document.Save(targetFileName);
        }
    }
}
