namespace SmartAsso.ProcessAsyncHelper
{
    /// <summary>
    /// Run process result
    /// </summary>
    public struct ProcessAsyncResult
    {
        /// <summary>
        /// Exit code
        /// <para>If NULL, process exited due to timeout</para>
        /// </summary>
        public int? ExitCode;

        /// <summary>
        /// Standard error stream
        /// </summary>
        public string StdErr;

        /// <summary>
        /// Standard output stream
        /// </summary>
        public string StdOut;

        /// <summary>
        /// Execution Time
        /// </summary>
        public long ExecutionTime;

        /// <summary>
        /// Status
        /// <para>Success, Error, Timeout</para>
        /// </summary>
        public ProcessAsyncResultStatus Status;
    }
}
