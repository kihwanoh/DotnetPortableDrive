using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
namespace external_drive_lib
{
    public class DevicesMonitor : IDisposable
    {
        private ManagementEventWatcher _insertedEventWatcher;
        private ManagementEventWatcher _removedEventWatcher;

        public event Action<Dictionary<string, string>> DeviceAdded;
        public event Action<Dictionary<string, string>> DeviceDeleted;

        // not used yet, however, tested and works
        // examples: generic_monitor("Win32_USBHub"); generic_monitor("Win32_DiskDrive");
        public void MonitorChanges(string className)
        {
            _insertedEventWatcher?.Dispose();
            _removedEventWatcher?.Dispose();

            var insertQuery = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA '" + className + "'");
            _insertedEventWatcher = new ManagementEventWatcher(insertQuery);
            _insertedEventWatcher.EventArrived += DeviceInstanceHandler;
            _insertedEventWatcher.Start();

            var removeQuery = new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA '" + className + "'");
            _removedEventWatcher = new ManagementEventWatcher(removeQuery);
            _removedEventWatcher.EventArrived += DeviceInstanceHandler;
            _removedEventWatcher.Start();
        }

        private void DeviceInstanceHandler(object sender, EventArrivedEventArgs e)
        {
            try
            {
                var properties = new Dictionary<string, string>();
                var instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                foreach (var p in instance.Properties)
                {
                    if (p.Value != null) properties.Add(p.Name, p.Value.ToString());
                }

                if (ReferenceEquals(sender, _insertedEventWatcher))
                {
                    OnDeviceAdded(properties);
                }
                else
                {
                    OnDeviceDeleted(properties);
                }
            }
            catch (Exception ex)
            {
                throw new IOException("invalid device inserted", ex);
            }
        }
        
        protected virtual void OnDeviceAdded(Dictionary<string, string> obj)
        {
            DeviceAdded?.Invoke(obj);
        }

        protected virtual void OnDeviceDeleted(Dictionary<string, string> obj)
        {
            DeviceDeleted?.Invoke(obj);
        }

        #region IDisposable Support
        private bool _disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (_disposedValue) return;
            if (disposing)
            {
                if (_insertedEventWatcher != null) _insertedEventWatcher.Dispose();
                if (_removedEventWatcher != null) _removedEventWatcher.Dispose();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
            // TODO: set large fields to null.

            _disposedValue = true;
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~MonitorDevices() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
