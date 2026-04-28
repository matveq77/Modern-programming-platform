using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MyTestFramework
{
    public class ThreadPoolEventArgs : EventArgs
    {
        public int ThreadId { get; }
        public string Message { get; }
        public DateTime Timestamp { get; }

        public ThreadPoolEventArgs(int threadId, string message)
        {
            ThreadId = threadId;
            Message = message;
            Timestamp = DateTime.Now;
        }
    }

    public class MyThreadPool : IDisposable
    {
        public event EventHandler<ThreadPoolEventArgs> ThreadCreated;
        public event EventHandler<ThreadPoolEventArgs> ThreadRemoved;
        public event EventHandler<ThreadPoolEventArgs> TaskStarted;
        public event EventHandler<ThreadPoolEventArgs> TaskCompleted;
        public event EventHandler<ThreadPoolEventArgs> ThreadHanged;

        private readonly Queue<Action> _taskQueue = new Queue<Action>();
        private readonly List<WorkerThread> _threads = new List<WorkerThread>();

        private readonly int _minThreads;
        private readonly int _maxThreads;
        private readonly int _idleTimeoutMs;
        private readonly int _taskMaxDurationMs;

        private volatile bool _isDisposed = false;

        private int _tasksEnqueued = 0;
        private int _tasksCompleted = 0;
        private int _tasksFailed = 0;

        public int CurrentThreadCount { get { lock (_threads) return _threads.Count; } }
        public int TasksInQueue { get { lock (_taskQueue) return _taskQueue.Count; } }
        public int TasksCompleted => _tasksCompleted;
        public int TasksFailed => _tasksFailed;
        public int TasksEnqueued => _tasksEnqueued;

        private readonly CountdownEvent _completionEvent;

        public MyThreadPool(int minThreads = 2, int maxThreads = 10, int idleTimeoutMs = 2000, int taskMaxDurationMs = 5000)
        {
            _minThreads = minThreads;
            _maxThreads = maxThreads;
            _idleTimeoutMs = idleTimeoutMs;
            _taskMaxDurationMs = taskMaxDurationMs;
            _completionEvent = new CountdownEvent(1);

            for (int i = 0; i < _minThreads; i++)
                CreateWorker();

            Thread monitor = new Thread(MonitorPool) { IsBackground = true, Name = "PoolMonitor" };
            monitor.Start();
        }

        public void Execute(Action task)
        {
            Interlocked.Increment(ref _tasksEnqueued);
            lock (_taskQueue)
            {
                _taskQueue.Enqueue(() =>
                {
                    try { task(); }
                    catch { Interlocked.Increment(ref _tasksFailed); }
                });
                Monitor.Pulse(_taskQueue);
            }
            AdjustPoolSize();
        }

        public void WaitAllTasks(int timeoutMs = 60000)
        {
            var deadline = DateTime.Now.AddMilliseconds(timeoutMs);
            while (DateTime.Now < deadline)
            {
                bool queueEmpty;
                bool allIdle;
                lock (_taskQueue) { queueEmpty = _taskQueue.Count == 0; }
                lock (_threads) { allIdle = _threads.All(t => t.IsIdle); }
                if (queueEmpty && allIdle) return;
                Thread.Sleep(100);
            }
        }

        private void CreateWorker()
        {
            lock (_threads)
            {
                var worker = new WorkerThread(this);
                _threads.Add(worker);
                worker.Start();
                ThreadCreated?.Invoke(this, new ThreadPoolEventArgs(worker.Id, $"Поток создан. Всего потоков: {_threads.Count}"));
            }
        }

        private void AdjustPoolSize()
        {
            lock (_taskQueue)
            {
                lock (_threads)
                {
                    if (_taskQueue.Count > 0 && _threads.Count < _maxThreads)
                        CreateWorker();
                }
            }
        }

        private void MonitorPool()
        {
            while (!_isDisposed)
            {
                Thread.Sleep(500);
                DateTime now = DateTime.Now;

                lock (_threads)
                {
                    if (_threads.Count > _minThreads)
                    {
                        var idleWorker = _threads.FirstOrDefault(w =>
                            w.IsIdle && (now - w.LastActiveTime).TotalMilliseconds > _idleTimeoutMs);

                        if (idleWorker != null)
                        {
                            idleWorker.Stop();
                            _threads.Remove(idleWorker);
                            ThreadRemoved?.Invoke(this, new ThreadPoolEventArgs(idleWorker.Id, $"Поток удалён (простой > {_idleTimeoutMs}мс). Осталось: {_threads.Count}"));
                            lock (_taskQueue) { Monitor.PulseAll(_taskQueue); }
                        }
                    }

                    for (int i = 0; i < _threads.Count; i++)
                    {
                        var w = _threads[i];
                        if (w.IsWorking && (now - w.TaskStartTime).TotalMilliseconds > _taskMaxDurationMs)
                        {
                            ThreadHanged?.Invoke(this, new ThreadPoolEventArgs(w.Id, $"Зависание обнаружено! Поток заменяется."));
                            w.Abandon();
                            _threads.RemoveAt(i);
                            CreateWorker();
                            break;
                        }
                    }
                }
            }
        }

        internal void OnTaskStarted(int threadId)
        {
            TaskStarted?.Invoke(this, new ThreadPoolEventArgs(threadId, "Задача начата"));
        }

        internal void OnTaskCompleted(int threadId, bool failed)
        {
            if (failed) Interlocked.Increment(ref _tasksFailed);
            else Interlocked.Increment(ref _tasksCompleted);
            TaskCompleted?.Invoke(this, new ThreadPoolEventArgs(threadId, "Задача завершена"));
        }

        public void Dispose()
        {
            _isDisposed = true;
            lock (_taskQueue) { Monitor.PulseAll(_taskQueue); }
        }

        private class WorkerThread
        {
            private readonly MyThreadPool _pool;
            private Thread _thread;
            private bool _running = true;
            public int Id { get; }

            public bool IsIdle { get; private set; } = true;
            public bool IsWorking => !IsIdle;
            public DateTime LastActiveTime { get; private set; } = DateTime.Now;
            public DateTime TaskStartTime { get; private set; }

            public WorkerThread(MyThreadPool pool)
            {
                _pool = pool;
                _thread = new Thread(Run) { IsBackground = true };
                Id = _thread.ManagedThreadId;
            }

            public void Start() => _thread.Start();
            public void Stop() => _running = false;
            public void Abandon() => _running = false;

            private void Run()
            {
                while (_running && !_pool._isDisposed)
                {
                    Action task = null;
                    lock (_pool._taskQueue)
                    {
                        while (_pool._taskQueue.Count == 0 && _running && !_pool._isDisposed)
                        {
                            IsIdle = true;
                            Monitor.Wait(_pool._taskQueue, 1000);
                        }

                        if (!_running || _pool._isDisposed) break;
                        if (_pool._taskQueue.Count > 0)
                            task = _pool._taskQueue.Dequeue();
                    }

                    if (task != null)
                    {
                        IsIdle = false;
                        TaskStartTime = DateTime.Now;
                        _pool.OnTaskStarted(Id);

                        bool failed = false;
                        try { task(); }
                        catch { failed = true; }

                        _pool.OnTaskCompleted(Id, failed);
                        IsIdle = true;
                        LastActiveTime = DateTime.Now;
                    }
                }
            }
        }
    }
}