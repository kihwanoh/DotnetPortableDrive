﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace external_drive_lib.Helpers
{
    internal static class UsbHelpers
    {
        public static bool PnpDeviceIdToVidpidAndUniqueId(string device_id, ref string vid_pid, ref string unique_id) {
            device_id = device_id.ToLower();
            vid_pid = unique_id = "";
            var valid = device_id.StartsWith("usb\\") && device_id.Contains("vid") && device_id.Contains("pid") && device_id.Count(c => c == '\\') >= 2;
            if (valid) {
                device_id = device_id.Substring(4);
                var idx = device_id.IndexOf("\\");
                vid_pid = device_id.Substring(0, idx);
                unique_id = device_id.Substring(idx + 1).Trim();
                if (vid_pid.Count(c => c == '&') > 1)
                    // some USB devices also expose an external removable drive (which can contain drivers to install) - we ignore that
                    return false;
                return true;
            }
            return false;
        }

        // Dependent=\\JOHN\root\cimv2:Win32_PnPEntity.DeviceID="USB\\VID_05AC&PID_12A8\\4ACD729ACDCE23221851F00CC985CE81F1FA8F53"
        public static bool DependentToVidpidAndUniqueId(string device_id, ref string vid_pid, ref string unique_id) {
            device_id = device_id.ToLower();
            if (device_id.Contains("deviceid=\""))
                device_id = device_id.Substring(device_id.IndexOf("deviceid=\"") + 10);
            vid_pid = unique_id = "";
            while (device_id.Contains("\\\\"))
                device_id = device_id.Replace("\\\\", "\\");
            while (device_id.EndsWith("\\") || device_id.EndsWith("\""))
                device_id = device_id.Substring(0, device_id.Length - 1);
            var valid = device_id.StartsWith("usb\\") && device_id.Contains("vid") && device_id.Contains("pid") && device_id.Count(c => c == '\\') >= 2;
            if (valid) {
                device_id = device_id.Substring(4);
                var idx = device_id.IndexOf("\\");
                vid_pid = device_id.Substring(0, idx);
                unique_id = device_id.Substring(idx + 1).Trim();
                if (vid_pid.Count(c => c == '&') > 1)
                    // some USB devices also expose an external removable drive (which can contain drivers to install) - we ignore that
                    return false;
                return true;
            }
            return false;
        }


        // Example:
        // ::{20D04FE0-3AEA-1069-A2D8-08002B30309D}\\\\\\?\\usb#vid_04e8&pid_6860&ms_comp_mtp&samsung_android#6&1a1242af4&0&0000#{6b327878-a6fa-4155-b985-f98e491d4f33}
        public static bool PortablePathToVidpid(string path, ref string vid_pid) {
            var idx = path.IndexOf("vid_");
            if (idx >= 0) {
                var idx2 = path.IndexOf("pid_", idx);
                if (idx2 >= 0) {
                    var idx3 = idx2 + 4;
                    while (UsbHelpers.IsHexDigit(path[idx3]))
                        ++idx3;
                    vid_pid = path.Substring(idx, idx3 - idx);
                    return true;
                }
            }

            Debug.Assert(false);
            return false;
        }
        
        // for testing - run into problems? please run this:
        /* 
            foreach ( var p in usb_util.get_all_portable_paths())
                Console.WriteLine(p);
            foreach ( var p in usb_util.get_all_usb_pnp_device_ids())
                Console.WriteLine(p);
            foreach ( var p in usb_util.get_all_usb_dependent_ids())
                Console.WriteLine(p);
         */
        public static List<string> GetAllPortablePaths() {
            var portable_devices = PortableDeviceHelpers.GetPortableConnectedDeviceDrives();
            return portable_devices.Select(d => d.Path).ToList();
        }

        // for testing - run into problems? please run this:
        /* 
            foreach ( var p in usb_util.get_all_portable_paths())
                Console.WriteLine(p);
            foreach ( var p in usb_util.get_all_usb_pnp_device_ids())
                Console.WriteLine(p);
            foreach ( var p in usb_util.get_all_usb_dependent_ids())
                Console.WriteLine(p);
         */
        public static List<string> GetAllUsbPnpDeviceIds() {
            var existing_devices = WmiHelpers.FindObjects("Win32_USBHub");
            return existing_devices.Select(d => d.ContainsKey("PNPDeviceID") ? d["PNPDeviceID"] : "--INVALID-DEVICE--").ToList();            
        }

        public static List<string> GetAllUsbDependentIds() {
            var existing_devices = WmiHelpers.FindObjects("Win32_USBControllerDevice");
            return existing_devices.Select(d => d.ContainsKey("Dependent") ? d["Dependent"] : "--INVALID-DEVICE--").ToList();            
        }

        public static List<Dictionary<string, string>> GetAllUsbPnpDeviceInfo() {
            return WmiHelpers.FindObjects("Win32_USBHub");
        }

        private static bool IsHexDigit(char ch) {
            return (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F');
        }

        private static string GetFirstHexDigits(string s) {
            int idx = 0;
            foreach ( var ch in s.ToCharArray())
                if (IsHexDigit(ch))
                    break;
            s = s.Substring(idx);

            while (s.Length > 0 && !IsHexDigit(s[0]))
                s = s.Substring(1);

            idx = 0;
            foreach ( var ch in s.ToCharArray())
                if (!IsHexDigit(ch))
                    break;
                else
                    ++idx;

            return s.Substring(0, idx);
        }

        // just in case we can't find a vid/pid to unique_id match
        public static string UniqueIdFromRootPath(string root_path) {
            root_path = root_path.ToLower();
            var vid_idx = root_path.IndexOf("vid_");
            var pid_idx = root_path.IndexOf("pid_");
            // for windows CE devices
            var active_sync = root_path.IndexOf("activesync");
            var umb = root_path.IndexOf("umb");

            // 6ac27878-a6fa-4155-ba85-f98f491d4f33 -> GUID_DEVINTERFACE_WPD  (https://docs.microsoft.com/en-us/windows-hardware/drivers/install/guid-devinterface-wpd)
            if (root_path.Contains("6ac27878-a6fa-4155-ba85-f98f491d4f33"))
                root_path = root_path.Substring(0, root_path.IndexOf("6ac27878-a6fa-4155-ba85-f98f491d4f33") - 1);

            var result = "";
            if (vid_idx > 0 || pid_idx > 0) {
                // we're using vidpid
                var vidpid_idx = Math.Max(vid_idx + 4, pid_idx + 4);
                root_path = root_path.Substring(vidpid_idx);
                // ignore the end of the pid
                var ignore = GetFirstHexDigits(root_path);
                result = GetFirstHexDigits(root_path.Substring( ignore.Length));
            }
            else if (active_sync > 0) {
                var start = Math.Max(active_sync + 10, umb + 3);
                root_path = root_path.Substring(start);
                var windows = root_path.IndexOf("windows");
                var mobile = root_path.IndexOf("mobile");
                if (windows < 0)
                    windows = int.MaxValue;
                if (mobile < 0)
                    mobile = int.MaxValue;
                var end = Math.Min(windows, mobile);
                if (end != int.MaxValue) 
                    root_path = root_path.Substring(0, end);
                var begin_of_id = root_path.LastIndexOfAny("&#".ToCharArray());
                if (begin_of_id >= 0)
                    result = root_path.Substring(begin_of_id + 1);
            }
            else 
                result = GetFirstHexDigits(root_path);

            // if result it's too small, there was something wrong
            return ( result.Length > 4) ? result :  "";
        }
    }
}
