using System;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Serialization;

namespace LastExit.Editor.ArtLab
{
    public static class ArtLabReportJson
    {
        public static string Serialize(object report, bool prettyPrint = false)
        {
            string json = JsonUtility.ToJson(report, prettyPrint);
            return RenameFields(json, report.GetType(), true);
        }

        public static T Deserialize<T>(string json)
        {
            return (T)Deserialize(json, typeof(T));
        }

        public static object Deserialize(string json, Type reportType)
        {
            if (string.IsNullOrEmpty(json))
            {
                return JsonUtility.FromJson(json, reportType);
            }

            string currentJson = RenameFields(json, reportType, false);
            return JsonUtility.FromJson(currentJson, reportType);
        }

        private static string RenameFields(string json, Type reportType, bool useLegacyNames)
        {
            FieldInfo[] fields = reportType.GetFields(BindingFlags.Instance | BindingFlags.Public);

            foreach (FieldInfo field in fields)
            {
                FormerlySerializedAsAttribute alias = field.GetCustomAttribute<FormerlySerializedAsAttribute>();

                if (alias != null)
                {
                    string originalName = useLegacyNames ? field.Name : alias.oldName;
                    string replacementName = useLegacyNames ? alias.oldName : field.Name;
                    // JsonUtility does not apply these aliases to plain report DTOs in Unity 2022.3.
                    // Match field tokens with optional JSON whitespace before their colon, never escaped text.
                    string pattern = "(?<!\\\\)\"" + Regex.Escape(originalName) + "\"(?=\\s*:)";
                    json = Regex.Replace(json, pattern, "\"" + replacementName + "\"");
                }
            }

            return json;
        }
    }
}
