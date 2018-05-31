using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.ServiceRuntime;
#if !DEBUG
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
#endif

namespace WebReverseProxyWorkerRole
{
    public class WorkerRole : RoleEntryPoint, IDisposable
    {

        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                Trace.TraceError("WorkerRole.CurrentDomain_UnhandledException : " + ex.GetBaseException().ToString());

#if DEBUG
                if (Debugger.IsAttached) { Debugger.Break(); }
#else
                TelemetryClient ai = new TelemetryClient();
                ai.TrackException(ex);
#endif

                if (ex is OutOfMemoryException)  // may be raised by System.Net.WebClient.DownloadBitsState.RetrieveBytes
                {
                    cancellationTokenSource.Cancel();
                }
            }
            else
            {
                Trace.TraceError("WorkerRole.CurrentDomain_UnhandledException : NULL");
            }
#if DEBUG
            if (Debugger.IsAttached) { Debugger.Break(); }
#endif
        }

        private void RoleEnvironmentChanging(object sender, RoleEnvironmentChangingEventArgs e)
        {
            if ((e.Changes.Any(change => change is RoleEnvironmentConfigurationSettingChange)))
            {
                e.Cancel = true; // take the instance offline
            }
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections
            ServicePointManager.DefaultConnectionLimit = 2048;

#if !DEBUG
            TelemetryConfiguration.Active.InstrumentationKey = RoleEnvironment.GetConfigurationSettingValue("APPINSIGHTS_INSTRUMENTATIONKEY");
#endif

            bool result = base.OnStart();

            Trace.TraceInformation("WebReverseProxyWorkerRole has been started");
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            RoleEnvironment.Changing += RoleEnvironmentChanging;

            return result;
        }

        public override void Run()
        {
            Trace.TraceInformation("WorkerRole is running");
            RotManager.TryKillTorIfRequired();

            try
            {
                this.RunAsync(this.cancellationTokenSource.Token).Wait();
            }
            catch (OperationCanceledException)
            {
                Trace.TraceInformation("WorkerRole.Run OperationCanceled");
            }
            catch (Exception ex)
            {
                Trace.TraceError("WorkerRole.Run Exception : " + ex.GetBaseException().ToString());
#if DEBUG
                if (Debugger.IsAttached) { Debugger.Break(); }
#endif
            }
            finally
            {
                RotManager.TryKillTorIfRequired();
                this.runCompleteEvent.Set();
            }
        }

        private static int GetRoleInstanceNumber()
        {
            string instanceId = RoleEnvironment.CurrentRoleInstance.Id;
            int instanceIndex = 0;
            if (!int.TryParse(instanceId.Substring(instanceId.LastIndexOf("_") + 1), out instanceIndex))
            {
                if (!int.TryParse(instanceId.Substring(instanceId.LastIndexOf(".") + 1), out instanceIndex))
                {
                    Trace.TraceWarning("WorkerRole.GetRoleInstanceNumber not found : " + RoleEnvironment.CurrentRoleInstance.Id);
                }
            }

            Trace.TraceWarning("WorkerRole.GetRoleInstanceNumber : " + instanceIndex.ToString());
            return instanceIndex;
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                using (RotManager rot = new RotManager(GetRoleInstanceNumber()))
                {
                    while (!cancellationToken.IsCancellationRequested && rot.IsProcessOk())
                    {
                        await Task.Delay(30000, cancellationToken);
                    }
                }

            }
            catch (OperationCanceledException) { } // incluse TaskCanceledException
            catch (AggregateException) { }
            catch (Exception ex)
            {
                Trace.TraceError("WorkerRole.RunAsync Exception : " + ex.GetBaseException().ToString());
#if DEBUG
                if (Debugger.IsAttached) { Debugger.Break(); }
#endif
            }

            Trace.TraceInformation("WorkerRole.RunAsync : End");
        }

        public override void OnStop()
        {
            Trace.TraceInformation("WebReverseProxyWorkerRole is stopping");

            this.cancellationTokenSource.Cancel();
            this.runCompleteEvent.WaitOne();

            base.OnStop();

            Trace.TraceInformation("WebReverseProxyWorkerRole has stopped");
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (cancellationTokenSource != null)
                    {
                        cancellationTokenSource.Dispose();
                    }
                    if (runCompleteEvent != null)
                    {
                        runCompleteEvent.Dispose();
                    }
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion

    }
}
