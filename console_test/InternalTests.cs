using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using external_drive_lib;
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
                Console.WriteLine(new string(' ', indent * 2) + f.FullPath + " " + f.Name);
            foreach ( var f in files)
                Console.WriteLine(new string(' ', indent * 2) + f.FullPath + " " + f.Name + " " + f.Size + " " + f.LastWriteTime);
        } 

        static void traverse_drive(IDrive d, int levels) {
            var folders = d.EnumerateFolders().ToList();
            // level 1
            dump_folders_and_files(folders, d.EnumerateFiles(), 0);
            for (int i = 1; i < levels; ++i) {
                var child_folders = new List<IFolder>();
                var child_files = new List<IFile>();
                foreach (var f in folders) {
                    try {
                        child_folders.AddRange(f.EnumerateChildFolders());
                    } catch(Exception e) {
                        // could be unauthorized access
                        Console.WriteLine(new string(' ', i * 2) + f.FullPath + " *** UNAUTHORIZED folders " + e);
                    }
                    try {
                        child_files.AddRange(f.EnumerateFiles());
                    } catch {
                        // could be unauthorized access
                        Console.WriteLine(new string(' ', i * 2) + f.FullPath + " *** UNAUTHORIZED EnumerateFiles()");
                    }
                }
                dump_folders_and_files(child_folders, child_files, i);
                folders = child_folders;
            }
        }

        // these are EnumerateFiles() from my drive
        static void test_win_ParseFiles() {
            //Debug.Assert(ExternalDriveRoot.Instance.ParseFile("D:\\cool_pics\\a00\\b0\\c0\\20161115_035718.jPg").Size == 4532595);
            //Debug.Assert(ExternalDriveRoot.Instance.ParseFile("D:\\cool_pics\\a00\\b0\\c0\\20161115_104952.jPg").Size == 7389360);
            //Debug.Assert(ExternalDriveRoot.Instance.ParseFolder("D:\\cool_pics\\a10").EnumerateFiles().Count() == 25);
            //Debug.Assert(ExternalDriveRoot.Instance.ParseFolder("D:\\cool_pics").EnumerateChildFolders().Count() == 8);

            //Debug.Assert(ExternalDriveRoot.Instance.ParseFile("D:\\cool_pics\\a00\\b0\\c0\\20161115_035718.jPg").FullPath == "D:\\cool_pics\\a00\\b0\\c0\\20161115_035718.jPg");
            //Debug.Assert(ExternalDriveRoot.Instance.ParseFolder("D:\\cool_pics\\a10").FullPath == "D:\\cool_pics\\a10");
        }

        static void test_parent_folder() {
            //Debug.Assert(ExternalDriveRoot.Instance.ParseFile("D:\\cool_pics\\a00\\b0\\c0\\20161115_035718.jPg").Folder.FullPath == "D:\\cool_pics\\a00\\b0\\c0");
            //Debug.Assert(ExternalDriveRoot.Instance.ParseFile("D:\\cool_pics\\a00\\b0\\c0\\20161115_035718.jPg").Folder.Parent.FullPath == "D:\\cool_pics\\a00\\b0");
        }


        ///////////////////////////////////////////////////////////////////
        // Android tests

        static void android_test_ParseFiles() {
           // Debug.Assert(ExternalDriveRoot.Instance.ParseFile("[a0]:/*/dcim/camera/20171005_121557.jPg").Size == 4598747);
            //Debug.Assert(ExternalDriveRoot.Instance.ParseFile("[a0]:/*/dcim/camera/20171005_121601.jPg").Size == 3578988);
            //Debug.Assert(ExternalDriveRoot.Instance.ParseFolder("[a0]:/*/dcim/camera") != null);

            //            Debug.Assert(drive_root.Instance.ParseFolder("[a0]:/*/dcim/camera").full_path.ToLower() == "[a0]:/*\\dcim\\camera");
        }

        static void android_test_parent_folder() {
            // need to care about [a0] in full_path
            //Debug.Assert(false);

            // ... uses file.parent
            //Debug.Assert(ExternalDriveRoot.Instance.ParseFile("[a0]:/*/dcim/camera/20171005_121557.jPg").Folder.FullPath.ToLower() 
              //           == "[a0]:/*\\dcim\\camera");
            // ... uses file.parent and folder.parent
            //Debug.Assert(ExternalDriveRoot.Instance.ParseFile("[a0]:/*/dcim/camera/20171005_121557.jPg").Folder.Parent.FullPath.ToLower() 
                //         == "[a0]:/*\\dcim");

            //Debug.Assert(ExternalDriveRoot.Instance.ParseFile("[a0]:/*/dcim/camera/20171005_121557.jPg").FullPath.ToLower() 
                  //       == "[a0]:/*\\dcim\\camera\\20171005_121557.jpg");
            //Debug.Assert(ExternalDriveRoot.Instance.ParseFile("[a0]:/*/dcim/camera/20171005_121601.jPg").FullPath.ToLower() 
                    //     == "[a0]:/*\\dcim\\camera\\20171005_121601.jpg");            

        }

        static void android_test_create_delete_folder() {
            //Debug.Assert(ExternalDriveRoot.Instance.NewFolder("[a0]:/*/dcim/testing123") != null);
            ExternalDriveRoot.Instance.ParseFolder("[a0]:/*/dcim/testing123").DeleteSync();
            try {
                ExternalDriveRoot.Instance.ParseFolder("[a0]:/*/dcim/testing123");
                Debug.Assert(false);

            } catch {
                // ok - the folder should not exist anymore
            }            
        }

        static void android_test_copy_and_delete_file() {
            var camera = ExternalDriveRoot.Instance.ParseFolder("[a0]:/*/dcim/camera");
            var first_file = camera.EnumerateFiles().ToList()[0];
            first_file.CopySync(camera.Parent.FullPath);

            // copy : android to windows
            var dir = new_temp_path();
            first_file.CopySync(dir);
            var name = first_file.Name;
            //Debug.Assert(first_file.Size == new FileInfo(dir + "\\" + name).Length);

            // copy: windows to android
            var renamed = dir + "\\" + name + ".renamed.jpg";
            File.Move(dir + "\\" + name, renamed);
            ExternalDriveRoot.Instance.ParseFile(renamed).CopySync("[a0]:/*/dcim/");
            //Debug.Assert(first_file.Size == ExternalDriveRoot.Instance.ParseFile("[a0]:/*/dcim/" + name + ".renamed.jpg").Size);
        }


        // what I want is to find out how fast is this, compared to Windows Explorer (roughly)
        // 80738 millis on 452 items (1.8Gb) in Debug
        // 77477 millis on 452 items (1.8Gb) in Release
        //
        // 67 secs copy from xplorer (clearly, this was a bulk copy)
        static void android_test_copy_full_dir_to_windows() {
            DateTime start = DateTime.Now;
            var dest_dir = new_temp_path();
            var camera = ExternalDriveRoot.Instance.ParseFolder("[a0]:/*/dcim/camera");
            foreach (var f in camera.EnumerateFiles()) {
                Console.WriteLine(f.Name);
                f.CopySync(dest_dir);
            }
            var spent_time = (DateTime.Now - start).TotalMilliseconds;
            Console.WriteLine("spent " + spent_time.ToString("f2") + " ms");
        }

        // END OF Android tests
        ///////////////////////////////////////////////////////////////////

        // copies all files from this folder into a sub-folder we create
        // after we do that, we delete the sub-folder
        static void test_copy_and_delete_files(string src_path) {
            var src = ExternalDriveRoot.Instance.ParseFolder(src_path);
            var old_folder_count = src.EnumerateChildFolders().Count();
            var child_dir = src_path + "/child1/child2/child3/";
            var dest = src.Drive.CreateFolder(child_dir);
            foreach ( var child in src.EnumerateFiles())
                child.CopySync(child_dir);
            long src_size = src.EnumerateFiles().Sum(f => f.Size);
            long dest_size = dest.EnumerateFiles().Sum(f => f.Size);
            //Debug.Assert(src_size == dest_size);
            //Debug.Assert(src.EnumerateChildFolders().Count() == old_folder_count + 1);
            foreach (var child in dest.EnumerateFiles())
                child.DeleteSync();

            var first_child = dest.Parent.Parent;
            first_child.DeleteSync();

            Debug.Assert(src.EnumerateChildFolders().Count() == old_folder_count );
        }


        static void test_copy_files(string src_path, string dest_path) {
            var src = ExternalDriveRoot.Instance.ParseFolder(src_path);
            var dest = ExternalDriveRoot.Instance.NewFolder(dest_path);
            foreach (var child in src.EnumerateFiles()) {
                Console.Write(child.FullPath);
                child.CopySync(dest_path);
                Console.WriteLine(" - done");
            }

            long src_size = src.EnumerateFiles().Sum(f => f.Size);
            long dest_size = dest.EnumerateFiles().Sum(f => f.Size);
            Debug.Assert(src_size == dest_size);
        }

        static void test_copy_files_android_to_win_and_viceversa() {
            // first from android to win, then vice versa
            var temp_dir = new_temp_path();
            test_copy_files("[a0]:/*/dcim/facebook", temp_dir);
            test_copy_files(temp_dir, "[a0]:/*/dcim/facebook_copy");
            ExternalDriveRoot.Instance.ParseFolder(temp_dir).DeleteSync();
            ExternalDriveRoot.Instance.ParseFolder("[a0]:/*/dcim/facebook_copy").DeleteSync();            
        }

        static void test_long_android_copy(string file_name) {
            var temp_dir = new_temp_path();
            var src_file = ExternalDriveRoot.Instance.ParseFile(file_name);
            src_file.CopySync(temp_dir);
            var dest_file = temp_dir + "\\" + src_file.Name;
            Debug.Assert(src_file.Size == ExternalDriveRoot.Instance.ParseFile(dest_file).Size);
            File.Move(dest_file, dest_file + ".renamed");
            ExternalDriveRoot.Instance.ParseFile(dest_file + ".renamed").CopySync("[a0]:/*/dcim");
            Debug.Assert(ExternalDriveRoot.Instance.ParseFile("[a0]:/*/dcim/" + src_file.Name + ".renamed").Size == src_file.Size);
            ExternalDriveRoot.Instance.ParseFile("[a0]:/*/dcim/" + src_file.Name + ".renamed").DeleteSync();
        }

        static void test_bulk_copy() {
            var src_win = "D:\\cool_pics\\a00\\b0\\c0";
            var dest_win = new_temp_path();
            var dest_android = "[a0]:/*/dcim/bulk";
            int i = 0;
            // take "even" EnumerateFiles()
            var src_files_win = ExternalDriveRoot.Instance.ParseFolder(src_win).EnumerateFiles().Where(f => i++ % 2 == 0).ToList();
            var src_files_size = src_files_win.Sum(f => f.Size);
            // win to win
            PortableBulkOperation.BulkCopySync(src_files_win, dest_win);
            var dest_win_size = ExternalDriveRoot.Instance.ParseFolder(dest_win).EnumerateFiles().Sum(f => f.Size);
            Debug.Assert(dest_win_size == src_files_size);

            // win to android
            PortableBulkOperation.BulkCopySync(src_files_win, dest_android);
            var dest_android_size = ExternalDriveRoot.Instance.ParseFolder(dest_android).EnumerateFiles().Sum(f => f.Size);
            Debug.Assert(dest_android_size == src_files_size);

            // android to android
            i = 0;
            var dest_android_copy = "[a0]:/*/dcim/bulk_copy";
            var src_files_android = ExternalDriveRoot.Instance.ParseFolder(dest_android).EnumerateFiles().Where(f => i++ % 2 == 0).ToList();
            var src_files_android_size = src_files_android.Sum(f => f.Size);
            PortableBulkOperation.BulkCopySync(src_files_android, dest_android_copy);
            var dest_files_android_size = ExternalDriveRoot.Instance.ParseFolder(dest_android_copy).EnumerateFiles().Sum(f => f.Size);
            Debug.Assert(src_files_android_size == dest_files_android_size);

            // android to win
            var dest_win_copy = new_temp_path();
            PortableBulkOperation.BulkCopySync(src_files_android, dest_win_copy);
            var dest_win_copy_size = ExternalDriveRoot.Instance.ParseFolder(dest_win_copy).EnumerateFiles().Sum(f => f.Size);
            Debug.Assert(dest_win_copy_size == src_files_android_size);

            ExternalDriveRoot.Instance.ParseFolder(dest_win).DeleteSync();
            ExternalDriveRoot.Instance.ParseFolder(dest_win_copy).DeleteSync();

            ExternalDriveRoot.Instance.ParseFolder(dest_android).DeleteSync();
            ExternalDriveRoot.Instance.ParseFolder(dest_android_copy).DeleteSync();
        }

        static void test_android_disconnected() {
            var camera = "[a0]:/*/dcim/camera";
            var camera_folder = ExternalDriveRoot.Instance.ParseFolder(camera);
            var first_file = ExternalDriveRoot.Instance.ParseFolder(camera).EnumerateFiles().ToList()[0];
            Debug.Assert(camera_folder.Exists);
            Debug.Assert(first_file.Exists);
            Debug.Assert(first_file.Drive.IsConnected());
            Debug.Assert(camera_folder.Drive.IsConnected());

            logger.Debug("camera file " + first_file.Size);
            logger.Debug("Disconnect the phone now.");
            Console.ReadLine();
            Debug.Assert(!camera_folder.Exists);
            Debug.Assert(!first_file.Exists);
            Debug.Assert(!first_file.Drive.IsConnected());
            Debug.Assert(!camera_folder.Drive.IsConnected());
        }

        private static void print_device_properties(Dictionary<string, string> properties, string prefix) {
            Console.WriteLine(prefix);
            foreach ( var p in properties)
                Console.WriteLine("  " + p.Key + "=" + p.Value);
        }

        private static void test_long_android_copy_async(string file_name) {
            logger.Debug("android to win");
            ExternalDriveRoot.Instance.AutoCloseWinDialogs = false;
            var temp_dir = new_temp_path();
            var src_file = ExternalDriveRoot.Instance.ParseFile(file_name);
            src_file.CopyAsync(temp_dir);
            Thread.Sleep(15000);

            logger.Debug("android to android");
            ExternalDriveRoot.Instance.AutoCloseWinDialogs = false;
            src_file.CopyAsync("[a0]:/phone/dcim");
            Thread.Sleep(15000);

            logger.Debug("win to android");
            var dest_file = temp_dir + "\\" + src_file.Name;
            File.Move(dest_file, dest_file + ".renamed");
            ExternalDriveRoot.Instance.ParseFile(dest_file + ".renamed").CopyAsync("[a0]:/phone/dcim");
            Thread.Sleep(15000);

            logger.Debug("win to win");
            var temp_dir2 = new_temp_path();
            ExternalDriveRoot.Instance.ParseFile(dest_file + ".renamed").CopyAsync(temp_dir2);
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
        public static void monitor_usb_devices()
        {
            var md = new DevicesMonitor();
            md.DeviceAdded += add_dump_info;
            md.DeviceDeleted += del_dump_info;
            //md.monitor("Win32_USBHub");
            md.MonitorChanges("Win32_USBControllerDevice");
        }
        
        //public static void print_uniqueid_from_path() {
        //    var wce_root_path = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}\\\\\\\\\\\\?\\\\activesyncwpdenumerator#umb#2&306b293b&2&aceecamez1500windowsmobile5#{6ac27878-a6fa-4155-ba85-f98f491d4f33}";
        //    Console.WriteLine(UsbHelpers.unique_id_from_root_path(wce_root_path));
        //}

    }


}
