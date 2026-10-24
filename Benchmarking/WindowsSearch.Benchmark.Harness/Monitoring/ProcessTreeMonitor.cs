using System.Diagnostics;
using System.Management;
using WindowsSearch.Benchmark.Core.Models;
using WindowsSearch.Common.Logging;

namespace WindowsSearch.Benchmark.Harness.Monitoring;

public sealed class ProcessTreeMonitor
{
    private readonly int hubProcessId;
    private TimeSpan lastCpuTime = TimeSpan.Zero;
    private DateTime lastSampleTime = DateTime.MinValue;
    private readonly List<int> cachedChildPids = new();
    private DateTime lastPidDiscovery = DateTime.MinValue;
    public ProcessTreeMonitor(int hubProcessId)
    {
        this.hubProcessId = hubProcessId;
    }

    public SystemResourceSample Sample()
    {
        var sample = new SystemResourceSample
        {
            Timestamp = DateTime.UtcNow
        };

        Process? hubProcess = null;
        try
        {
            hubProcess = Process.GetProcessById(hubProcessId);
        }
        catch { }

        if (hubProcess is null || hubProcess.HasExited)
        {
            return sample;
        }

        try
        {
            hubProcess.Refresh();
            sample.HubWorkingSetMb = Math.Round(hubProcess.WorkingSet64 / (1024.0 * 1024.0), 2);
            sample.HubPrivateMemoryMb = Math.Round(hubProcess.PrivateMemorySize64 / (1024.0 * 1024.0), 2);

            var childPids = GetChildProcessIds(hubProcessId);
            sample.ProviderProcessCount = childPids.Count;

            var totalWorkingSet = hubProcess.WorkingSet64;
            var totalPrivateMemory = hubProcess.PrivateMemorySize64;
            var totalCpuTime = hubProcess.TotalProcessorTime;

            foreach (var pid in childPids)
            {
                try
                {
                    using var childProcess = Process.GetProcessById(pid);
                    if (!childProcess.HasExited)
                    {
                        totalWorkingSet += childProcess.WorkingSet64;
                        totalPrivateMemory += childProcess.PrivateMemorySize64;
                        totalCpuTime += childProcess.TotalProcessorTime;
                    }
                }
                catch { }
            }

            sample.TotalWorkingSetMb = Math.Round(totalWorkingSet / (1024.0 * 1024.0), 2);
            sample.TotalPrivateMemoryMb = Math.Round(totalPrivateMemory / (1024.0 * 1024.0), 2);

            var now = DateTime.UtcNow;
            if (lastSampleTime != DateTime.MinValue && now > lastSampleTime)
            {
                var rawTimeWindow = (now - lastSampleTime).TotalMilliseconds;
                var timeWindow = Math.Max(rawTimeWindow, 15.625);
                var cpuDelta = (totalCpuTime - lastCpuTime).TotalMilliseconds;
                var cpuPercentage = (cpuDelta / (timeWindow * Environment.ProcessorCount)) * 100.0;
                sample.CpuPercentage = Math.Clamp(Math.Round(cpuPercentage, 2), 0.0, 100.0);
            }

            lastCpuTime = totalCpuTime;
            lastSampleTime = now;
        }
        catch (Exception ex)
        {
            AppLogger.Error("[ProcessTreeMonitor] Failed to sample process resources:", ex);
        }

        return sample;
    }

    private List<int> GetChildProcessIds(int parentPid)
    {
        var now = DateTime.UtcNow;
        bool needRefresh = cachedChildPids.Count == 0 || (now - lastPidDiscovery).TotalSeconds > 3;
        if (!needRefresh)
        {
            for (int i = 0; i < cachedChildPids.Count; i++)
            {
                try
                {
                    using var p = Process.GetProcessById(cachedChildPids[i]);
                    if (p.HasExited)
                    {
                        needRefresh = true;
                        break;
                    }
                }
                catch
                {
                    needRefresh = true;
                    break;
                }
            }
        }

        if (!needRefresh)
        {
            return cachedChildPids;
        }

        cachedChildPids.Clear();
        lastPidDiscovery = now;

        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT ProcessId FROM Win32_Process WHERE ParentProcessId = {parentPid}");
            using var results = searcher.Get();

            foreach (ManagementObject item in results.Cast<ManagementObject>())
            {
                cachedChildPids.Add(Convert.ToInt32(item["ProcessId"]));
            }
        }
        catch { }

        return cachedChildPids;
    }
}
