using Microsoft.Web.XmlTransform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Transformer
{
    class Program
    {
        static void Main(string[] args)
        {
            var targetFolder = @"c:\temp\transformed";
            var folder = @"C:\dev\folder-with-web-config-and-transform-files";
            var webConfig = Path.Combine(folder, "web.config");

            var files = Directory.GetFiles(folder, "web*.config")
                .Where(x => new FileInfo(x).Name.ToLower() != "web.config");

            foreach (var transform in files)
            {
                var fi = new FileInfo(transform);
                Transformer.TransformConfig(webConfig, transform, Path.Combine(targetFolder, fi.Name));
            }

            var clientSettings = new Dictionary<string, IDictionary<string, string>>();
            var clients = new List<string>();
            foreach (var f in Directory.GetFiles(targetFolder, "*.config"))
            {
                var client = new FileInfo(f).Name.ToLower()
                    .Replace("web.", "")
                    .Replace(".config", "");

                clients.Add(client);

                var dic = ReadAppsettings(f);
                clientSettings[client] = dic;
            }

            var allSettings = clientSettings.SelectMany(x => x.Value.Select(y => y.Key)).Distinct();

            var separator = ",";
            var header = separator + string.Join(separator, clients);

            var csv = header + Environment.NewLine;
            foreach (var setting in allSettings)
            {
                var row = setting + separator;
                foreach (var client in clients)
                {
                    if (clientSettings[client].ContainsKey(setting))
                    {
                        var value = clientSettings[client][setting];
                        //if (value.Contains(separator))
                        //{
                        //    throw new Exception();
                        //}

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

            File.WriteAllText(@"c:\temp\foo.csv", csv);
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
