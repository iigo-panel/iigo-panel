using System;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.ProcessStatus;

namespace IIGO.Services
{
    public class SystemStatusService
    {
        PERFORMANCE_INFORMATION pi = new PERFORMANCE_INFORMATION();
        bool HasPerformanceInfo = false;
        private void GetPerformance()
        {
            PERFORMANCE_INFORMATION p = new PERFORMANCE_INFORMATION();
            HasPerformanceInfo = PInvoke.GetPerformanceInfo(ref p, (uint)Marshal.SizeOf(p));
            pi = p;
        }

        public long GetPhysicalAvailableMemoryInMiB(bool force = false)
        {
            if (!HasPerformanceInfo || force)
                GetPerformance();
            if (HasPerformanceInfo)
            {
                return (long)pi.PhysicalAvailable * (long)pi.PageSize / 1048576L;
            }
            else
            {
                return -1;
            }
        }

        public long GetTotalMemoryInMiB(bool force = false)
        {
            if (!HasPerformanceInfo || force)
                GetPerformance();
            if (HasPerformanceInfo)
            {
                return (long)pi.PhysicalTotal * (long)pi.PageSize / 1048576L;
            }
            else
            {
                return -1;
            }
        }

        public int GetProcessCount(bool force = false)
        {
            if (!HasPerformanceInfo || force)
                GetPerformance();
            if (HasPerformanceInfo)
            {
                return (int)pi.ProcessCount;
            }
            else
            {
                return -1;
            }
        }

        public long GetPagedPool(bool force = false)
        {
            if (!HasPerformanceInfo || force)
                GetPerformance();
            if (HasPerformanceInfo)
            {
                return (long)pi.KernelPaged * (long)pi.PageSize / 1048576L;
            }
            else
            {
                return -1;
            }
        }

        public long GetNonPagedPool(bool force = false)
        {
            if (!HasPerformanceInfo || force)
                GetPerformance();
            if (HasPerformanceInfo)
            {
                return (long)pi.KernelNonpaged * (long)pi.PageSize / 1048576L;
            }
            else
            {
                return -1;
            }
        }
    }
}
