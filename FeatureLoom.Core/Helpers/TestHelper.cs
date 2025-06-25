using FeatureLoom.MessageFlow;
using FeatureLoom.Logging;
using FeatureLoom.Storages;
using FeatureLoom.Synchronization;
using System.Collections.Generic;
using FeatureLoom.DependencyInversion;
using System.Linq;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System;

namespace FeatureLoom.Helpers
{
    /// <summary>
    /// Provides utilities for preparing and managing isolated test contexts, including log capturing and safe port management for parallel test execution.
    /// </summary>
    public static class TestHelper
    {
        /// <summary>
        /// Represents the context for a single test execution, encapsulating log messages and borrowed ports.
        /// Use <see cref="TestHelper.PrepareTestContext"/> to create and initialize a new context for each test.
        /// </summary>
        public class TestContext : IDisposable
        {
            /// <summary>
            /// Lock for synchronizing access to context data.
            /// </summary>
            public FeatureLock contextLock = new FeatureLock();

            /// <summary>
            /// List of captured log messages with level ERROR.
            /// </summary>
            public List<LogMessage> logErrors = new List<LogMessage>();

            /// <summary>
            /// List of captured log messages with level WARNING.
            /// </summary>
            public List<LogMessage> logWarnings = new List<LogMessage>();

            /// <summary>
            /// List of TCP ports borrowed by this test context.
            /// </summary>
            public List<int> borrowedPorts = new List<int>();

            /// <summary>
            /// Returns all ports borrowed by this context to the port pool when disposed.
            /// </summary>
            public void Dispose()
            {
                if (borrowedPorts.Any())
                {
                    PortPoolService.ReturnBorrowedPorts(this);
                }
            }

            /// <summary>
            /// Borrows a free TCP port from the pool and tracks it in this context.
            /// Throws <see cref="InvalidOperationException"/> if no free ports are available.
            /// </summary>
            /// <returns>A free TCP port number.</returns>
            public int BorrowPort()
            {
                return TestHelper.PortPoolService.BorrowPort(this);
            }

            /// <summary>
            /// Returns all ports borrowed by this context to the port pool.
            /// </summary>
            public void ReturnBorrowedPorts()
            {
                TestHelper.PortPoolService.ReturnBorrowedPorts(this);
            }
        }

        /// <summary>
        /// Static service to manage a pool of available TCP port numbers for tests.
        /// Ensures that tests running in parallel do not collide by borrowing ports from a managed pool.
        /// </summary>
        public static class PortPoolService
        {
            private static readonly ConcurrentQueue<int> availablePorts = new ConcurrentQueue<int>();
            private static readonly HashSet<int> allPorts = new HashSet<int>();
            private static readonly FeatureLock poolLock = new FeatureLock();
            private static bool initialized = false;

            static PortPoolService()
            {
                // Default port range: 54000-54500 (commonly free for tests)
                Initialize(new[] { (54000, 54500) });
            }

            /// <summary>
            /// Initializes the port pool with a set of port ranges.
            /// All ports in the specified ranges will be available for borrowing.
            /// </summary>
            /// <param name="portRanges">Each tuple is (start, end) inclusive, representing a range of ports.</param>
            public static void Initialize(IEnumerable<(int start, int end)> portRanges)
            {
                using (poolLock.Lock())
                {
                    allPorts.Clear();
                    while (availablePorts.TryDequeue(out _)) { }
                    foreach (var (start, end) in portRanges)
                    {
                        for (int port = start; port <= end; port++)
                        {
                            allPorts.Add(port);
                            availablePorts.Enqueue(port);
                        }
                    }
                    initialized = true;
                }
            }

            /// <summary>
            /// Borrows a port from the pool, ensuring it is actually free, and stores it in the provided test context.
            /// Throws <see cref="InvalidOperationException"/> if no free ports are available in the pool.
            /// </summary>
            /// <param name="testContext">The test context to associate the borrowed port with.</param>
            /// <returns>A free TCP port number.</returns>
            /// <exception cref="InvalidOperationException">Thrown if no free ports are available in the pool.</exception>
            public static int BorrowPort(TestContext testContext)
            {
                EnsureInitialized();

                int attempts = 0;
                while (attempts < allPorts.Count)
                {
                    if (!availablePorts.TryDequeue(out int port))
                        throw new InvalidOperationException("No ports available in the pool.");

                    if (IsPortFree(port))
                    {
                        using (testContext.contextLock.Lock())
                        {
                            testContext.borrowedPorts.Add(port);
                        }
                        return port;
                    }
                    else
                    {
                        // Port is not free, put it back at the end of the queue
                        availablePorts.Enqueue(port);
                        attempts++;
                    }
                }
                throw new InvalidOperationException("No free ports available in the pool.");
            }

            /// <summary>
            /// Returns all ports borrowed by the provided test context to the pool.
            /// </summary>
            /// <param name="testContext">The test context whose borrowed ports should be returned.</param>
            public static void ReturnBorrowedPorts(TestContext testContext)
            {
                List<int> portsToReturn;
                using (testContext.contextLock.Lock())
                {
                    portsToReturn = testContext.borrowedPorts.ToList();
                    testContext.borrowedPorts.Clear();
                }
                foreach (var port in portsToReturn)
                {
                    availablePorts.Enqueue(port);
                }
            }

            /// <summary>
            /// Returns a specific port to the pool (if not already present in the pool).
            /// </summary>
            /// <param name="port">The port number to return to the pool.</param>
            public static void ReturnPort(int port)
            {
                using (poolLock.Lock())
                {
                    if (allPorts.Contains(port))
                    {
                        availablePorts.Enqueue(port);
                    }
                }
            }

            /// <summary>
            /// Ensures the port pool is initialized. If not, initializes with a default port range.
            /// </summary>
            private static void EnsureInitialized()
            {
                if (!initialized)
                {
                    // Default port range: 50000-50100
                    Initialize(new[] { (50000, 50100) });
                }
            }

            /// <summary>
            /// Checks if a TCP port is free by attempting to bind to it.
            /// </summary>
            /// <param name="port">The port number to check.</param>
            /// <returns>True if the port is free, otherwise false.</returns>
            private static bool IsPortFree(int port)
            {
                try
                {
                    TcpListener listener = new TcpListener(IPAddress.Loopback, port);
                    listener.Start();
                    listener.Stop();
                    return true;
                }
                catch (SocketException)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Prepares a new isolated test context, optionally disconnecting loggers and using in-memory storage.
        /// Captures log messages and enables safe port borrowing for parallel test execution.
        /// The returned <see cref="TestContext"/> should be disposed after the test to ensure resources are released.
        /// </summary>
        /// <param name="disconnectLoggers">If true, disconnects all loggers to avoid cross-test interference.</param>
        /// <param name="useMemoryStorage">If true, uses in-memory storage for test isolation.</param>
        /// <returns>A new <see cref="TestContext"/> instance for the test.</returns>
        public static TestContext PrepareTestContext(bool disconnectLoggers = true, bool useMemoryStorage = true)
        {
            ServiceRegistry.CreateLocalInstancesForAllServices();

            // Create a new test context via Service<TestContext> to ensure it will be disposed automatically
            var testContext = Service<TestContext>.Instance;
            if (disconnectLoggers)
            {
                Log.QueuedLogSource.DisconnectAll();
                Log.SyncLogSource.DisconnectAll();
            }

            Log.SyncLogSource.ProcessMessage<LogMessage>(msg =>
            {
                using (testContext.contextLock.Lock())
                {
                    if (msg.level == Loglevel.ERROR) testContext.logErrors.Add(msg);
                    else if (msg.level == Loglevel.WARNING) testContext.logWarnings.Add(msg);
                }
            });

            if (useMemoryStorage)
            {
                Storage.DefaultReaderFactory = (category) => new MemoryStorage(category);
                Storage.DefaultWriterFactory = (category) => new MemoryStorage(category);
                Storage.RemoveAllReaderAndWriter();
            }
            return testContext;
        }

        public static bool HasAnyLogError(TestContext context = null)
        {
            if (context == null) context = Service<TestContext>.Instance;
            return context.logErrors.Any() || context.logWarnings.Any();
        }
    }
}