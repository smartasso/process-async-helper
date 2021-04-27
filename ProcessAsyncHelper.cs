using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace SmartAsso.ProcessAsyncHelper
{
    public static class ProcessAsyncHelper
    {
        /// <summary>
        /// Run a process asynchronously
        /// <para>To capture STDOUT, set StartInfo.RedirectStandardOutput to TRUE</para>
        /// <para>To capture STDERR, set StartInfo.RedirectStandardError to TRUE</para>
        /// </summary>
        /// <param name="startInfo">ProcessStartInfo object</param>
        /// <param name="timeoutMs">The timeout in milliseconds (null for no timeout)</param>
        /// <returns>ProcessResult object</returns>
        public static async Task<ProcessAsyncResult> RunProcessAsync(ProcessStartInfo startInfo, int? timeoutMs = null)
        {
            var result = new ProcessAsyncResult();

            using var process = new Process() {StartInfo = startInfo, EnableRaisingEvents = true};
            
            // List of tasks to wait for a whole process exit
            var processTasks = new List<Task>();

            // === EXITED Event handling ===
            var processExitEvent = new TaskCompletionSource<object>();
            process.Exited += (sender, args) => { processExitEvent.TrySetResult(true); };
            processTasks.Add(processExitEvent.Task);

            // === STDOUT handling ===
            var stdOutBuilder = new StringBuilder();
            if (process.StartInfo.RedirectStandardOutput)
            {
                var stdOutCloseEvent = new TaskCompletionSource<bool>();

                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data == null)
                    {
                        stdOutCloseEvent.TrySetResult(true);
                    }
                    else
                    {
                        stdOutBuilder.Append(e.Data);
                    }
                };

                processTasks.Add(stdOutCloseEvent.Task);
            }

            // === STDERR handling ===
            var stdErrBuilder = new StringBuilder();
            if (process.StartInfo.RedirectStandardError)
            {
                var stdErrCloseEvent = new TaskCompletionSource<bool>();

                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data == null)
                    {
                        stdErrCloseEvent.TrySetResult(true);
                    }
                    else
                    {
                        stdErrBuilder.Append(e.Data);
                    }
                };

                processTasks.Add(stdErrCloseEvent.Task);
            }

            // === START OF PROCESS ===
            if (!process.Start())
            {
                result.ExitCode = process.ExitCode;
                return result;
            }

            // Reads the output stream first as needed and then waits because deadlocks are possible
            if (process.StartInfo.RedirectStandardOutput)
            {
                process.BeginOutputReadLine();
            }

            if (process.StartInfo.RedirectStandardError)
            {
                process.BeginErrorReadLine();
            }

            // === ASYNC WAIT OF PROCESS ===

            // Process completion = exit AND stdout (if defined) AND stderr (if defined)
            var processCompletionTask = Task.WhenAll(processTasks);

            // Task to wait for exit OR timeout (if defined)
            var awaitingTask = timeoutMs.HasValue
                ? Task.WhenAny(Task.Delay(timeoutMs.Value), processCompletionTask)
                : Task.WhenAny(processCompletionTask);

            // Let's now wait for something to end...
            if ((await awaitingTask.ConfigureAwait(false)) == processCompletionTask)
            {
                // -> Process exited cleanly
                result.ExitCode = process.ExitCode;
            }
            else
            {
                // -> Timeout, let's kill the process
                try
                {
                    process.Kill();
                }
                catch
                {
                    // ignored
                }
            }

            // Read stdout/stderr
            result.StdOut = stdOutBuilder.ToString();
            result.StdErr = stdErrBuilder.ToString();

            return result;
        }

        /// <summary>
        /// Run a command asynchronously
        /// </summary>
        /// <param name="processName">Name of the process</param>
        /// <param name="arguments">Arguments (null for no timeout)</param>
        /// <param name="timeout">The timeout in milliseconds (null for no timeout)</param>
        /// <returns>ProcessResult object</returns>
        public static async Task<ProcessAsyncResult> RunCommandAsync(string processName, string arguments, int? timeout)
        {
            var timer = new Stopwatch();
            timer.Start();


            var processStartInfo = new ProcessStartInfo()
            {
                FileName = processName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            ProcessAsyncResult result;
            try
            {
                result = await RunProcessAsync(processStartInfo, timeout);

                if (result.ExitCode == null)
                {
                    result.Status = ProcessAsyncResultStatus.Timeout;
                }
                else if (!string.IsNullOrWhiteSpace(result.StdErr) && result.ExitCode > 0)
                {
                    result.Status = ProcessAsyncResultStatus.Error;
                }
                else
                {
                    result.Status = ProcessAsyncResultStatus.Success;
                }

                ;
            }
            catch (Exception e)
            {
                return new ProcessAsyncResult()
                {
                    Status = ProcessAsyncResultStatus.Error,
                    ExecutionTime = timer.ElapsedMilliseconds,
                    ExitCode = 27,
                    StdErr = e.Message,
                };
            }

            result.ExecutionTime = timer.ElapsedMilliseconds;

            return result;
        }
    }
}
