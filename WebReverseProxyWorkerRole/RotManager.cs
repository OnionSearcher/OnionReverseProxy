using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace WebReverseProxyWorkerRole
{
    /// <summary>
    /// Called from the Role, not in the same space than the IIS process, don't mix theses call (not the same BaseDirectory)
    /// </summary>
    public class RotManager : IDisposable
    {

        public static void TryKillTorIfRequired()
        {
            try
            {
                // sometime Tor is not well killed (at last in dev mode)
                foreach (var oldProcess in Process.GetProcessesByName("rot"))
                    oldProcess.Kill(); //permission issue on Azure may occur
            }
            catch (Exception ex)
            {
                Trace.TraceError("RotManager.killTorIfRequired Exception : " + ex.GetBaseException().Message);  // No right usualy, simple message to keep.
            }
        }
        
        private void OutputHandler(object sender, DataReceivedEventArgs e)
        {
            if (!String.IsNullOrWhiteSpace(e.Data))
            {
                if (e.Data.Contains("[err]"))
                    ErrorOutputHandler(sender, e);
                else
                {
                    Trace.TraceInformation("RotManager : " + e.Data);
                }
            }
        }

        private void ErrorOutputHandler(object sender, DataReceivedEventArgs e)
        {
            if (!String.IsNullOrWhiteSpace(e.Data))
            {
                Trace.TraceError("RotManager : " + e.Data);
                if (e.Data.Contains("Out of memory") || e.Data.Contains("Dying"))
                    process.Kill();
            }
        }

        private Process process;
        private static readonly string basePath = AppDomain.CurrentDomain.BaseDirectory;
        public RotManager(int i)
        {
            string confFile = Path.Combine(basePath, @"ExpertBundle\Data\rotrc_IN_" + i.ToString());

#if DEBUG      // else DEBUG will publish and take the production .onion !
            if (File.Exists(Path.Combine(basePath, @"ExpertBundle\data\hs0\hostname"))) // copy keyfiles, need to be writen else the azure cloud service won't have the right for rewrite the file (don't kwow why Tor rewrite the same file...)
            {
                File.Delete(Path.Combine(basePath, @"ExpertBundle\data\hs0\hostname"));
                File.Delete(Path.Combine(basePath, @"ExpertBundle\data\hs0\private_key"));
            }
            if (File.Exists(Path.Combine(basePath, @"ExpertBundle\data\hs1\hostname"))) // copy keyfiles, need to be writen else the azure cloud service won't have the right for rewrite the file (don't kwow why Tor rewrite the same file...)
            {
                File.Delete(Path.Combine(basePath, @"ExpertBundle\data\hs1\hostname"));
                File.Delete(Path.Combine(basePath, @"ExpertBundle\data\hs1\private_key"));
            }
            if (File.Exists(Path.Combine(basePath, @"ExpertBundle\data\hs2\hostname"))) // copy keyfiles, need to be writen else the azure cloud service won't have the right for rewrite the file (don't kwow why Tor rewrite the same file...)
            {
                File.Delete(Path.Combine(basePath, @"ExpertBundle\data\hs2\hostname"));
                File.Delete(Path.Combine(basePath, @"ExpertBundle\data\hs2\private_key"));
            }
#endif

            string text = File.ReadAllText(confFile);
            text = text.Replace("127.0.0.1:80", RoleEnvironment.GetConfigurationSettingValue("HiddenServicePortDests")); // TOFIX : works only once : need redeply else
            text = text.Replace("127.0.0.1:443", RoleEnvironment.GetConfigurationSettingValue("HiddenServiceSSLPortDests")); // TOFIX : works only once : need redeply else
            File.WriteAllText(confFile, text);
            
            process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    WorkingDirectory = basePath, // changing that doesn't seems to work well with azure emulator
                    FileName = @"ExpertBundle\Rot\rot.exe",
                    Arguments = "-f \"" + confFile + "\" --defaults-torrc \"" + Path.Combine(basePath, @"ExpertBundle\Data\rotrc-defaults") + "\"", // full path not mandatory but avoid a warning...
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
            process.ErrorDataReceived += new DataReceivedEventHandler(ErrorOutputHandler);
            process.Start();
            // after start
            process.PriorityClass = ProcessPriorityClass.AboveNormal;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        

        public bool IsProcessOk()
        {
            if (process != null)
            {
                if (!process.HasExited)
                {
                    if (process.Responding)
                    {
                        return true;
                    }
                    else
                    {
                        Trace.TraceError("RotManager.IsProcessOk : process is not responding");
                    }
                }
                else
                {
                    Trace.TraceError("RotManager.IsProcessOk : process has exited");
                }
            }
            else
            {
                Trace.TraceError("RotManager.IsProcessOk : process is null");
            }
            return false;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (process != null)
                    {
                        if (!process.HasExited)
                        {
                            try
                            {
                                process.Kill();
                                while (!process.HasExited)
                                {
                                    Task.Delay(200).Wait();
                                }
                            }
                            catch (Exception ex)
                            {
                                Trace.TraceError("RotManager.Dispose process.Kill Exception : " + ex.GetBaseException().ToString());
                            }
                        }
                        try
                        {
                            process.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Trace.TraceError("RotManager.Dispose process.Dispose Exception : " + ex.GetBaseException().ToString());
                        }
                        process = null;
                    }
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion

    }
}