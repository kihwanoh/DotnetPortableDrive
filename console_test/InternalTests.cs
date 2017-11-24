﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using external_drive_lib;
using external_drive_lib.bulk;
using external_drive_lib.interfaces;
using external_drive_lib.monitor;
using external_drive_lib.util;

namespace console_test
{
    /* 
     * These are internal tests, ran by me, the author of the lib 
     * Run them at your own risk!
     */
    static class InternalTests
    {
        private static log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        static string new_temp_path() {
            var temp_dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\external_drive_temp\\test-" + DateTime.Now.Ticks;
            Directory.CreateDirectory(temp_dir);
            return temp_dir;
        }

        static void dump_folders_and_files(IEnumerable<IFolder> folders, IEnumerable<IFile> files, int indent) {
            Console.WriteLine("");
            Console.WriteLine("Level " + (indent+1));
            Console.WriteLine("");
            foreach ( var f in folders)
                Console.WriteLine(new string(' ', indent * 2) + f.full_path + " " + f.name);
            foreach ( var f in files)
                Console.WriteLine(new string(' ', indent * 2) + f.full_path + " " + f.name + " " + f.size + " " + f.last_write_time);
        } 

        static void traverse_drive(IDrive d, int levels) {
            var folders = d.folders.ToList();
            // level 1
            dump_folders_and_files(folders, d.files, 0);
            for (int i = 1; i < levels; ++i) {
                var child_folders = new List<IFolder>();
                var child_files = new List<IFile>();
                foreach (var f in folders) {
                    try {
                        child_folders.AddRange(f.child_folders);
                    } catch(Exception e) {
                        // could be unauthorized access
                        Console.WriteLine(new string(' ', i * 2) + f.full_path + " *** UNAUTHORIZED folders " + e);
                    }
                    try {
                        child_files.AddRange(f.files);
                    } catch {
                        // could be unauthorized access
                        Console.WriteLine(new string(' ', i * 2) + f.full_path + " *** UNAUTHORIZED files");
                    }
                }
                dump_folders_and_files(child_folders, child_files, i);
                folders = child_folders;
            }
        }

        // these are files from my drive
        static void test_win_parse_files() {
            Debug.Assert(PortableDriveRoot.inst.parse_file("D:\\cool_pics\\a00\\b0\\c0\\20161115_035718.jPg").size == 4532595);
            Debug.Assert(PortableDriveRoot.inst.parse_file("D:\\cool_pics\\a00\\b0\\c0\\20161115_104952.jPg").size == 7389360);
            Debug.Assert(PortableDriveRoot.inst.parse_folder("D:\\cool_pics\\a10").files.Count() == 25);
            Debug.Assert(PortableDriveRoot.inst.parse_folder("D:\\cool_pics").child_folders.Count() == 8);

            Debug.Assert(PortableDriveRoot.inst.parse_file("D:\\cool_pics\\a00\\b0\\c0\\20161115_035718.jPg").full_path == "D:\\cool_pics\\a00\\b0\\c0\\20161115_035718.jPg");
            Debug.Assert(PortableDriveRoot.inst.parse_folder("D:\\cool_pics\\a10").full_path == "D:\\cool_pics\\a10");
        }

        static void test_parent_folder() {
            Debug.Assert(PortableDriveRoot.inst.parse_file("D:\\cool_pics\\a00\\b0\\c0\\20161115_035718.jPg").folder.full_path == "D:\\cool_pics\\a00\\b0\\c0");
            Debug.Assert(PortableDriveRoot.inst.parse_file("D:\\cool_pics\\a00\\b0\\c0\\20161115_035718.jPg").folder.parent.full_path == "D:\\cool_pics\\a00\\b0");
        }


        ///////////////////////////////////////////////////////////////////
        // Android tests

        static void android_test_parse_files() {
            Debug.Assert(PortableDriveRoot.inst.parse_file("[a0]:/*/dcim/camera/20171005_121557.jPg").size == 4598747);
            Debug.Assert(PortableDriveRoot.inst.parse_file("[a0]:/*/dcim/camera/20171005_121601.jPg").size == 3578988);
            Debug.Assert(PortableDriveRoot.inst.parse_folder("[a0]:/*/dcim/camera") != null);

            //            Debug.Assert(drive_root.inst.parse_folder("[a0]:/*/dcim/camera").full_path.ToLower() == "[a0]:/*\\dcim\\camera");
        }

        static void android_test_parent_folder() {
            // need to care about [a0] in full_path
            Debug.Assert(false);

            // ... uses file.parent
            Debug.Assert(PortableDriveRoot.inst.parse_file("[a0]:/*/dcim/camera/20171005_121557.jPg").folder.full_path.ToLower() 
                         == "[a0]:/*\\dcim\\camera");
            // ... uses file.parent and folder.parent
            Debug.Assert(PortableDriveRoot.inst.parse_file("[a0]:/*/dcim/camera/20171005_121557.jPg").folder.parent.full_path.ToLower() 
                         == "[a0]:/*\\dcim");

            Debug.Assert(PortableDriveRoot.inst.parse_file("[a0]:/*/dcim/camera/20171005_121557.jPg").full_path.ToLower() 
                         == "[a0]:/*\\dcim\\camera\\20171005_121557.jpg");
            Debug.Assert(PortableDriveRoot.inst.parse_file("[a0]:/*/dcim/camera/20171005_121601.jPg").full_path.ToLower() 
                         == "[a0]:/*\\dcim\\camera\\20171005_121601.jpg");            

        }

        static void android_test_create_delete_folder() {
            Debug.Assert(PortableDriveRoot.inst.new_folder("[a0]:/*/dcim/testing123") != null);
            PortableDriveRoot.inst.parse_folder("[a0]:/*/dcim/testing123").delete_sync();
            try {
                PortableDriveRoot.inst.parse_folder("[a0]:/*/dcim/testing123");
                Debug.Assert(false);

            } catch {
                // ok - the folder should not exist anymore
            }            
        }

        static void android_test_copy_and_delete_file() {
            var camera = PortableDriveRoot.inst.parse_folder("[a0]:/*/dcim/camera");
            var first_file = camera.files.ToList()[0];
            first_file.copy_sync(camera.parent.full_path);

            // copy : android to windows
            var dir = new_temp_path();
            first_file.copy_sync(dir);
            var name = first_file.name;
            Debug.Assert(first_file.size == new FileInfo(dir + "\\" + name).Length);

            // copy: windows to android
            var renamed = dir + "\\" + name + ".renamed.jpg";
            File.Move(dir + "\\" + name, renamed);
            PortableDriveRoot.inst.parse_file(renamed).copy_sync("[a0]:/*/dcim/");
            Debug.Assert(first_file.size == PortableDriveRoot.inst.parse_file("[a0]:/*/dcim/" + name + ".renamed.jpg").size);
        }


        // what I want is to find out how fast is this, compared to Windows Explorer (roughly)
        // 80738 millis on 452 items (1.8Gb) in Debug
        // 77477 millis on 452 items (1.8Gb) in Release
        //
        // 67 secs copy from xplorer (clearly, this was a bulk copy)
        static void android_test_copy_full_dir_to_windows() {
            DateTime start = DateTime.Now;
            var dest_dir = new_temp_path();
            var camera = PortableDriveRoot.inst.parse_folder("[a0]:/*/dcim/camera");
            foreach (var f in camera.files) {
                Console.WriteLine(f.name);
                f.copy_sync(dest_dir);
            }
            var spent_time = (DateTime.Now - start).TotalMilliseconds;
            Console.WriteLine("spent " + spent_time.ToString("f2") + " ms");
        }

        // END OF Android tests
        ///////////////////////////////////////////////////////////////////

        // copies all files from this folder into a sub-folder we create
        // after we do that, we delete the sub-folder
        static void test_copy_and_delete_files(string src_path) {
            var src = PortableDriveRoot.inst.parse_folder(src_path);
            var old_folder_count = src.child_folders.Count();
            var child_dir = src_path + "/child1/child2/child3/";
            var dest = src.drive.create_folder(child_dir);
            foreach ( var child in src.files)
                child.copy_sync(child_dir);
            long src_size = src.files.Sum(f => f.size);
            long dest_size = dest.files.Sum(f => f.size);
            Debug.Assert(src_size == dest_size);
            Debug.Assert(src.child_folders.Count() == old_folder_count + 1);
            foreach (var child in dest.files)
                child.delete_sync();

            var first_child = dest.parent.parent;
            first_child.delete_sync();

            Debug.Assert(src.child_folders.Count() == old_folder_count );
        }


        static void test_copy_files(string src_path, string dest_path) {
            var src = PortableDriveRoot.inst.parse_folder(src_path);
            var dest = PortableDriveRoot.inst.new_folder(dest_path);
            foreach (var child in src.files) {
                Console.Write(child.full_path);
                child.copy_sync(dest_path);
                Console.WriteLine(" - done");
            }

            long src_size = src.files.Sum(f => f.size);
            long dest_size = dest.files.Sum(f => f.size);
            Debug.Assert(src_size == dest_size);
        }

        static void test_copy_files_android_to_win_and_viceversa() {
            // first from android to win, then vice versa
            var temp_dir = new_temp_path();
            test_copy_files("[a0]:/*/dcim/facebook", temp_dir);
            test_copy_files(temp_dir, "[a0]:/*/dcim/facebook_copy");
            PortableDriveRoot.inst.parse_folder(temp_dir).delete_sync();
            PortableDriveRoot.inst.parse_folder("[a0]:/*/dcim/facebook_copy").delete_sync();            
        }

        static void test_long_android_copy(string file_name) {
            var temp_dir = new_temp_path();
            var src_file = PortableDriveRoot.inst.parse_file(file_name);
            src_file.copy_sync(temp_dir);
            var dest_file = temp_dir + "\\" + src_file.name;
            Debug.Assert(src_file.size == PortableDriveRoot.inst.parse_file(dest_file).size);
            File.Move(dest_file, dest_file + ".renamed");
            PortableDriveRoot.inst.parse_file(dest_file + ".renamed").copy_sync("[a0]:/*/dcim");
            Debug.Assert(PortableDriveRoot.inst.parse_file("[a0]:/*/dcim/" + src_file.name + ".renamed").size == src_file.size);
            PortableDriveRoot.inst.parse_file("[a0]:/*/dcim/" + src_file.name + ".renamed").delete_sync();
        }

        static void test_bulk_copy() {
            var src_win = "D:\\cool_pics\\a00\\b0\\c0";
            var dest_win = new_temp_path();
            var dest_android = "[a0]:/*/dcim/bulk";
            int i = 0;
            // take "even" files
            var src_files_win = PortableDriveRoot.inst.parse_folder(src_win).files.Where(f => i++ % 2 == 0).ToList();
            var src_files_size = src_files_win.Sum(f => f.size);
            // win to win
            bulk.bulk_copy_sync(src_files_win, dest_win);
            var dest_win_size = PortableDriveRoot.inst.parse_folder(dest_win).files.Sum(f => f.size);
            Debug.Assert(dest_win_size == src_files_size);

            // win to android
            bulk.bulk_copy_sync(src_files_win, dest_android);
            var dest_android_size = PortableDriveRoot.inst.parse_folder(dest_android).files.Sum(f => f.size);
            Debug.Assert(dest_android_size == src_files_size);

            // android to android
            i = 0;
            var dest_android_copy = "[a0]:/*/dcim/bulk_copy";
            var src_files_android = PortableDriveRoot.inst.parse_folder(dest_android).files.Where(f => i++ % 2 == 0).ToList();
            var src_files_android_size = src_files_android.Sum(f => f.size);
            bulk.bulk_copy_sync(src_files_android, dest_android_copy);
            var dest_files_android_size = PortableDriveRoot.inst.parse_folder(dest_android_copy).files.Sum(f => f.size);
            Debug.Assert(src_files_android_size == dest_files_android_size);

            // android to win
            var dest_win_copy = new_temp_path();
            bulk.bulk_copy_sync(src_files_android, dest_win_copy);
            var dest_win_copy_size = PortableDriveRoot.inst.parse_folder(dest_win_copy).files.Sum(f => f.size);
            Debug.Assert(dest_win_copy_size == src_files_android_size);

            PortableDriveRoot.inst.parse_folder(dest_win).delete_sync();
            PortableDriveRoot.inst.parse_folder(dest_win_copy).delete_sync();

            PortableDriveRoot.inst.parse_folder(dest_android).delete_sync();
            PortableDriveRoot.inst.parse_folder(dest_android_copy).delete_sync();
        }

        static void test_android_disconnected() {
            var camera = "[a0]:/*/dcim/camera";
            var camera_folder = PortableDriveRoot.inst.parse_folder(camera);
            var first_file = PortableDriveRoot.inst.parse_folder(camera).files.ToList()[0];
            Debug.Assert(camera_folder.exists);
            Debug.Assert(first_file.exists);
            Debug.Assert(first_file.drive.is_connected());
            Debug.Assert(camera_folder.drive.is_connected());

            logger.Debug("camera file " + first_file.size);
            logger.Debug("Disconnect the phone now.");
            Console.ReadLine();
            Debug.Assert(!camera_folder.exists);
            Debug.Assert(!first_file.exists);
            Debug.Assert(!first_file.drive.is_connected());
            Debug.Assert(!camera_folder.drive.is_connected());
        }

        private static void print_device_properties(Dictionary<string, string> properties, string prefix) {
            Console.WriteLine(prefix);
            foreach ( var p in properties)
                Console.WriteLine("  " + p.Key + "=" + p.Value);
        }

        private static void test_long_android_copy_async(string file_name) {
            logger.Debug("android to win");
            PortableDriveRoot.inst.auto_close_win_dialogs = false;
            var temp_dir = new_temp_path();
            var src_file = PortableDriveRoot.inst.parse_file(file_name);
            src_file.copy_async(temp_dir);
            Thread.Sleep(15000);

            logger.Debug("android to android");
            PortableDriveRoot.inst.auto_close_win_dialogs = false;
            src_file.copy_async("[a0]:/phone/dcim");
            Thread.Sleep(15000);

            logger.Debug("win to android");
            var dest_file = temp_dir + "\\" + src_file.name;
            File.Move(dest_file, dest_file + ".renamed");
            PortableDriveRoot.inst.parse_file(dest_file + ".renamed").copy_async("[a0]:/phone/dcim");
            Thread.Sleep(15000);

            logger.Debug("win to win");
            var temp_dir2 = new_temp_path();
            PortableDriveRoot.inst.parse_file(dest_file + ".renamed").copy_async(temp_dir2);
            Thread.Sleep(15000);
        }

        private static void add_dump_info(Dictionary<string, string> properties) {
            Console.WriteLine("---------- ADDED");
            foreach ( var p in properties)
                Console.WriteLine(p.Key + " [=] " + p.Value);
        }
        private static void del_dump_info(Dictionary<string, string> properties) {
            Console.WriteLine("---------- DEL");
            foreach ( var p in properties)
                Console.WriteLine(p.Key + " [=] " + p.Value);
        }
        public static void monitor_usb_devices() {
            var md = new monitor_devices() {added_device = add_dump_info, deleted_device = del_dump_info};
            //md.monitor("Win32_USBHub");
            md.monitor("Win32_USBControllerDevice");
        }


        public static void print_uniqueid_from_path() {
            var wce_root_path = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}\\\\\\\\\\\\?\\\\activesyncwpdenumerator#umb#2&306b293b&2&aceecamez1500windowsmobile5#{6ac27878-a6fa-4155-ba85-f98f491d4f33}";
            Console.WriteLine(usb_util.unique_id_from_root_path(wce_root_path));
        }

    }


}
