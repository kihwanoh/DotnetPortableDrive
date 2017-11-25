using System.Collections.Generic;
using System.Management;

namespace external_drive_lib.Helpers
{
    internal static class WmiHelpers
    {
        public static List<Dictionary<string, string>> FindObjects(string type)
        {
            var result = new List<Dictionary<string, string>>();
            var search = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM " + type);
            foreach (var managementBaseObject in search.Get())
            {
                var o = (ManagementObject) managementBaseObject;
                var properties = new Dictionary<string, string>();
                foreach (var p in o.Properties)
                {
                    if (p.Value != null) properties.Add(p.Name, p.Value.ToString());
                }
                result.Add(properties);
            }

            return result;
        }
    }
}
