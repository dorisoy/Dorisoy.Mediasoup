using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Force.DeepCloner;

namespace Dorisoy.Mediasoup
{
    public class MediasoupServer
    {
        private readonly List<IWorker> _workers = new();

        private int _nextMediasoupWorkerIndex = 0;

        private readonly ReaderWriterLockSlim _workersLock = new();

        /// <summary>
        /// Observer instance.
        /// </summary>
        public EventEmitter Observer { get; } = new EventEmitter();

        /// <summary>
        /// Get a cloned copy of the mediasoup supported RTP capabilities.
        /// </summary>
        public static RtpCapabilities GetSupportedRtpCapabilities()
        {
            return RtpCapabilities.SupportedRtpCapabilities.DeepClone();
        }

        /// <summary>
        /// 解析 Worker 数量。配置值小于等于 0 或未配置时取 min(max(CPU/2, 2), 8)。
        /// </summary>
        public static int ResolveNumberOfWorkers(int? configuredNumberOfWorkers)
        {
            if (configuredNumberOfWorkers > 0)
            {
                return configuredNumberOfWorkers.Value;
            }

            return Math.Min(Math.Max(Environment.ProcessorCount / 2, 2), 8);
        }

        /// <summary>
        /// Get a snapshot of all mediasoup Workers.
        /// </summary>
        public IReadOnlyList<IWorker> GetWorkers()
        {
            _workersLock.EnterReadLock();
            try
            {
                return _workers.ToArray();
            }
            finally
            {
                _workersLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Get index of the Worker, or -1 if it is not registered.
        /// </summary>
        public int GetWorkerIndex(IWorker worker)
        {
            _workersLock.EnterReadLock();
            try
            {
                return _workers.IndexOf(worker);
            }
            finally
            {
                _workersLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Get next mediasoup Worker.
        /// </summary>
        public IWorker GetWorker()
        {
            _workersLock.EnterReadLock();
            try
            {
                if (_nextMediasoupWorkerIndex > _workers.Count - 1)
                {
                    throw new Exception("None worker");
                }

                if (++_nextMediasoupWorkerIndex == _workers.Count)
                {
                    _nextMediasoupWorkerIndex = 0;
                }

                return _workers[_nextMediasoupWorkerIndex];
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Get worker failure: {ex.Message}");
                throw;
            }
            finally
            {
                _workersLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Add worker.
        /// </summary>
        public void AddWorker(IWorker worker)
        {
            if (worker == null)
            {
                throw new ArgumentNullException(nameof(worker));
            }

            _workersLock.EnterWriteLock();
            try
            {
                _workers.Add(worker);

                // Emit observer event.
                Observer.Emit("newworker", worker);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Add worker failure: {ex.Message}");
                throw;
            }
            finally
            {
                _workersLock.ExitWriteLock();
            }
        }
    }
}
