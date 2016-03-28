// ***********************************************************************
// Copyright (c) 2016 Charlie Poole
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// ***********************************************************************

using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using NUnit.Engine.Internal;
using NUnit.Engine.Servers;
using NUnit.Common;

namespace NUnit.Engine.Services
{
    /// <summary>
    /// Enumeration used to report AgentStatus
    /// </summary>
    public enum AgentStatus
    {
        Unknown,
        Starting,
        Ready,
        Busy,
        Stopping
    }

    /// <summary>
    /// The TestAgency class provides RemoteTestAgents
    /// on request and tracks their status. Agents
    /// are wrapped in an instance of the TestAgent
    /// class. Multiple agent types are supported
    /// but only one, RemoteTestAgent is implemented
    /// at this time.
    /// </summary>
    public class TestAgency : MarshalByRefObject, ITestAgency, IService, IDisposable
    {
        static Logger log = InternalTrace.GetLogger(typeof(TestAgency));

        #region Private Fields

        AgentDataBase _agentData = new AgentDataBase();
        IRuntimeFrameworkService _runtimeService;
        IList<ITestServer> _servers = new List<ITestServer>();

        #endregion

        #region Constructors

        public TestAgency() : this( "TestAgency", 0 ) { }

        internal TestAgency(string uri, int port)
        {
            _servers.Add(new RemoteServer(uri, port, this));
        }

        #endregion

        #region Public Methods - Called by Agents

        public void Register( ITestAgent agent )
        {
            AgentRecord r = _agentData[agent.Id];
            if ( r == null )
                throw new ArgumentException(
                    string.Format("Agent {0} is not in the agency database", agent.Id),
                    "agentId");
            r.Agent = agent;
        }

        public void ReportStatus( Guid agentId, AgentStatus status )
        {
            AgentRecord r = _agentData[agentId];

            if ( r == null )
                throw new ArgumentException(
                    string.Format("Agent {0} is not in the agency database", agentId),
                    "agentId" );

            r.Status = status;
        }

        #endregion

        #region Public Methods - Called by Clients

        public ITestAgent GetAgent(TestPackage package, int waitTime)
        {
            // TODO: Decide if we should reuse agents
            //AgentRecord r = FindAvailableRemoteAgent(type);
            //if ( r == null )
            //    r = CreateRemoteAgent(type, framework, waitTime);
            return CreateAgent(package, waitTime);
        }

        public void ReleaseAgent( ITestAgent agent )
        {
            AgentRecord r = _agentData[agent.Id];
            if (r == null)
                log.Error(string.Format("Unable to release agent {0} - not in database", agent.Id));
            else
            {
                r.Status = AgentStatus.Ready;
                log.Debug("Releasing agent " + agent.Id.ToString());
            }
        }

        #endregion
        
        #region InitializeLifetimeService

        public override object InitializeLifetimeService()
        {
            return null;
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        bool _disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    foreach(var server in _servers)
                        server.Dispose();
                }
                _disposed = true;
            }
        }

        #endregion

        #region Helper Methods

        private ITestAgent CreateAgent(TestPackage package, int waitTime)
        {
            string defaultFramework = _runtimeService.SelectRuntimeFramework(package);

            var server = GetTestServer(package);
            AgentRecord agentRecord = server.LaunchAgentProcess(package, defaultFramework);
            _agentData.Add(agentRecord);

            log.Debug("Waiting for agent {0} to register", agentRecord.Id.ToString("B"));

            int pollTime = 200;
            bool infinite = waitTime == Timeout.Infinite;

            while (infinite || waitTime > 0)
            {
                Thread.Sleep(pollTime);
                if (!infinite) waitTime -= pollTime;
                ITestAgent agent = _agentData[agentRecord.Id].Agent;
                if (agent != null)
                {
                    log.Debug("Returning new agent {0}", agentRecord.Id.ToString("B"));
                    return agent;
                }
            }
            return null;
        }

        internal ITestServer GetTestServer(TestPackage package)
        {
            TargetPlatform platform = GetTargetPlaform(package);
            if (platform == TargetPlatform.Multiple)
                throw new NUnitEngineException(string.Format("TargetPlaform.Multiple is an invalid platform for TestPackage {0}", package.Name));

            foreach(var server in _servers)
            {
                if (server.HandlesPlatform(platform))
                    return server;
            }
            throw new NUnitEngineException(string.Format("NUnit does not support platform {0} for TestPackage {1} yet", platform, package.Name));
        }

        internal TargetPlatform GetTargetPlaform(TestPackage package)
        {
            string targetString = package.GetSetting(PackageSettings.ImageTargetPlatform, TargetPlatform.Unknown.ToString());
            try
            {
                return (TargetPlatform)Enum.Parse(typeof(TargetPlatform), targetString);
            }
            catch (Exception)
            {
                return TargetPlatform.Unknown;
            }
        }

        /// <summary>
        /// Return the NUnit Bin Directory for a particular
        /// runtime version, or null if it's not installed.
        /// For normal installations, there are only 1.1 and
        /// 2.0 directories. However, this method accommodates
        /// 3.5 and 4.0 directories for the benefit of NUnit
        /// developers using those runtimes.
        /// </summary>
        private static string GetNUnitBinDirectory(Version v)
        {
            // Get current bin directory
            string dir = NUnitConfiguration.EngineDirectory;

            // Return current directory if current and requested
            // versions are both >= 2 or both 1
            if ((Environment.Version.Major >= 2) == (v.Major >= 2))
                return dir;

            // Check whether special support for version 1 is installed
            if (v.Major == 1)
            {
                string altDir = Path.Combine(dir, "net-1.1");
                if (Directory.Exists(altDir))
                    return altDir;

                // The following is only applicable to the dev environment,
                // which uses parallel build directories. We try to substitute
                // one version number for another in the path.
                string[] search = new string[] { "2.0", "3.0", "3.5", "4.0" };
                string[] replace = v.Minor == 0
                    ? new string[] { "1.0", "1.1" }
                    : new string[] { "1.1", "1.0" };

                // Look for current value in path so it can be replaced
                string current = null;
                foreach (string s in search)
                    if (dir.IndexOf(s) >= 0)
                    {
                        current = s;
                        break;
                    }

                // Try the substitution
                if (current != null)
                {
                    foreach (string target in replace)
                    {
                        altDir = dir.Replace(current, target);
                        if (Directory.Exists(altDir))
                            return altDir;
                    }
                }
            }

            return null;
        }

        #endregion

        #region IService Members

        public IServiceLocator ServiceContext { get; set; }

        public ServiceStatus Status { get; private set; }

        public void StopService()
        {
            try
            {
                foreach (var server in _servers)
                    server.Stop();
            }
            finally
            {
                Status = ServiceStatus.Stopped;
            }
        }

        public void StartService()
        {
            try
            {
                // TestAgency requires on the RuntimeFrameworkService.
                _runtimeService = ServiceContext.GetService<IRuntimeFrameworkService>();

                // Any object returned from ServiceContext is an IService
                if (_runtimeService != null && ((IService)_runtimeService).Status == ServiceStatus.Started)
                {
                    foreach (var server in _servers)
                    {
                        try
                        {
                            server.Start();
                        }
                        catch
                        {
                            Status = ServiceStatus.Error;
                            throw;
                        }
                    }
                }
                else
                {
                    Status = ServiceStatus.Error;
                    return;
                }
            }
            catch
            {
                Status = ServiceStatus.Error;
                throw;
            }
            Status = ServiceStatus.Started;
        }

        #endregion

        #region Nested Class - AgentRecord

        internal class AgentRecord
        {
            public Guid Id;
            public Process Process;
            public ITestAgent Agent;
            public AgentStatus Status;

            public AgentRecord( Guid id, Process p, ITestAgent a, AgentStatus s )
            {
                this.Id = id;
                this.Process = p;
                this.Agent = a;
                this.Status = s;
            }
        }

        #endregion

        #region Nested Class - AgentDataBase

        /// <summary>
        ///  A simple class that tracks data about this
        ///  agencies active and available agents
        /// </summary>
        private class AgentDataBase
        {
            private Dictionary<Guid, AgentRecord> agentData = new Dictionary<Guid, AgentRecord>();

            public AgentRecord this[Guid id]
            {
                get { return agentData[id]; }
                set
                {
                    if ( value == null )
                        agentData.Remove( id );
                    else
                        agentData[id] = value;
                }
            }

            public AgentRecord this[ITestAgent agent]
            {
                get
                {
                    foreach( KeyValuePair<Guid, AgentRecord> entry in agentData)
                    {
                        AgentRecord r = entry.Value;
                        if ( r.Agent == agent )
                            return r;
                    }

                    return null;
                }
            }

            public void Add( AgentRecord r )
            {
                agentData[r.Id] = r;
            }

            public void Remove(Guid agentId)
            {
                agentData.Remove(agentId);
            }

            public void Clear()
            {
                agentData.Clear();
            }
        }

        #endregion
    }
}
