using System;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using SentinelAI.Core;

namespace SentinelAI.Service
{
    /// <summary>
    /// SentinelAI background service: real-time protection. Runs as a Windows
    /// service so it starts at boot and protects even before login.
    /// </summary>
    public class SentinelService : ServiceBase
    {
        private Timer _ransomwareTimer;
        private Timer _processTimer;
        private Timer _persistenceTimer;
        private readonly BehaviorMonitor _monitor = new BehaviorMonitor();

        public SentinelService() { ServiceName = "SentinelAI"; }

        static void Main(string[] args)
        {
            if (Environment.UserInteractive)
            {
                // Run as console for testing
                var svc = new SentinelService();
                svc.OnStart(null);
                Console.WriteLine("SentinelAI running. Press Enter to stop.");
                Console.ReadLine();
                svc.OnStop();
            }
            else
            {
                Run(new SentinelService());
            }
        }

        protected override void OnStart(string[] args)
        {
            System.Diagnostics.Trace.WriteLine("SentinelAI service starting.");
            Console.WriteLine("SentinelAI service starting.");

            _monitor.ThreatDetected += (src, desc) =>
            {
                System.Diagnostics.Trace.WriteLine($"[THREAT] {src}: {desc}");
                Console.WriteLine($"[THREAT] {src}: {desc}");
            };

            // Ransomware canaries: 10s
            _ransomwareTimer = new Timer(_ => _monitor.CheckRansomwareCanaries(), null, 0, 10000);
            // Process sweep: 30s
            _processTimer = new Timer(_ => _monitor.SweepProcesses(), null, 5000, 30000);
            // Persistence sweep: 5 min
            _persistenceTimer = new Timer(_ => _monitor.SweepPersistence(), null, 10000, 300000);
        }

        protected override void OnStop()
        {
            _ransomwareTimer?.Dispose();
            _processTimer?.Dispose();
            _persistenceTimer?.Dispose();
            System.Diagnostics.Trace.WriteLine("SentinelAI service stopped.");
            Console.WriteLine("SentinelAI service stopped.");
        }
    }
}