﻿using Microsoft.VisualStudio.Services.Agent.Util;
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Services.Agent.Listener
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            using (HostContext context = new HostContext("Agent"))
            {
                return MainAsync(context, args).GetAwaiter().GetResult();
            }
        }

        // Return code definition: (this will be used by service host to determine whether it will re-launch agent.listener)
        // 0: Agent exit
        // 1: Terminate failure
        // 2: Retriable failure
        // 3: Exit for self update
        public async static Task<int> MainAsync(IHostContext context, string[] args)
        {
            Tracing trace = context.GetTrace("AgentProcess");
            trace.Info($"Agent is built for {Constants.Agent.Platform} - {BuildConstants.AgentPackage.PackageName}.");
            trace.Info($"RuntimeInformation: {RuntimeInformation.OSDescription}.");
            var terminal = context.GetService<ITerminal>();

            // Validate the binaries intended for one OS are not running on a different OS.
            switch (Constants.Agent.Platform)
            {
                case Constants.OSPlatform.Linux:
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        terminal.WriteLine(StringUtil.Loc("NotLinux"));
                        return Constants.Agent.ReturnCode.TerminatedError;
                    }
                    break;
                case Constants.OSPlatform.OSX:
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        terminal.WriteLine(StringUtil.Loc("NotOSX"));
                        return Constants.Agent.ReturnCode.TerminatedError;
                    }
                    break;
                case Constants.OSPlatform.Windows:
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        terminal.WriteLine(StringUtil.Loc("NotWindows"));
                        return Constants.Agent.ReturnCode.TerminatedError;
                    }
                    break;
                default:
                    terminal.WriteLine(StringUtil.Loc("PlatformNotSupport", RuntimeInformation.OSDescription, Constants.Agent.Platform.ToString()));
                    return Constants.Agent.ReturnCode.TerminatedError;
            }

            try
            {
                trace.Info($"Version: {Constants.Agent.Version}");
                trace.Info($"Commit: {BuildConstants.Source.CommitHash}");
                trace.Info($"Culture: {CultureInfo.CurrentCulture.Name}");
                trace.Info($"UI Culture: {CultureInfo.CurrentUICulture.Name}");

                //
                // TODO (bryanmac): Need VsoAgent.exe compat shim for SCM
                //                  That shim will also provide a compat arg parse 
                //                  and translate / to -- etc...
                //

                // Parse the command line args.
                var command = new CommandSettings(context, args);
                trace.Info("Arguments parsed");

                // Defer to the Agent class to execute the command.
                IAgent agent = context.GetService<IAgent>();
                using (agent.TokenSource = new CancellationTokenSource())
                {
                    try
                    {
                        return await agent.ExecuteCommand(command);
                    }
                    catch (OperationCanceledException) when (agent.TokenSource.IsCancellationRequested)
                    {
                        trace.Info("Agent execution been cancelled.");
                        return Constants.Agent.ReturnCode.Success;
                    }
                }
            }
            catch (Exception e)
            {
                terminal.WriteError(StringUtil.Loc("ErrorOccurred", e.Message));
                trace.Error(e);
                return Constants.Agent.ReturnCode.RetryableError;
            }
        }
    }
}
