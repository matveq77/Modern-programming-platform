using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MyTestFramework
{
    public class MyThreadPool : IDisposable
    {
        private readonly Queue<Action> _taskQueue = new Queue<Action>();
        private readonly List<WorkerThread> _threads = new List<WorkerThread>();

        private readonly int _minThreads;
        private readonly int _maxThreads;
        private readonly int _idleTimeoutMs;
        private readonly int _taskMaxDurationMs;

        private bool _isDisposed = false;

        public int CurrentThreadCount { get { lock (_threads) return _threads.Count; } }
        public int TasksInQueue { get { lock (_taskQueue) return _taskQueue.Count; } }

        public MyThreadPool(int minThreads = 2, int maxThreads = 10, int idleTimeoutMs = 2000, int taskMaxDurationMs = 5000)
        {
            _minThreads = minThreads;
            _maxThreads = maxThreads;
            _idleTimeoutMs = idleTimeoutMs;
            _taskMaxDurationMs = taskMaxDurationMs;

            for (int i = 0; i < _minThreads; i++)
            {
                CreateWorker();
            }

            Thread monitor = new Thread(MonitorPool) { IsBackground = true, Name = "PoolMonitor" };
            monitor.Start();
        }

        public void Execute(Action task)
        {
            lock (_taskQueue)
            {
                _taskQueue.Enqueue(task);
                Monitor.Pulse(_taskQueue);
            }
            AdjustPoolSize();
        }

        private void CreateWorker()
        {
            lock (_threads)
            {
                var worker = new WorkerThread(this);
                _threads.Add(worker);
                worker.Start();
                Log($"[Pool] Created new thread. Total: {_threads.Count}", ConsoleColor.Cyan);
            }
        }

        private void AdjustPoolSize()
        {
            lock (_taskQueue)
            {
                lock (_threads)
                {
                    if (_taskQueue.Count > 0 && _threads.Count < _maxThreads)
                    {
                        CreateWorker();
                    }
                }
            }
        }

        private void MonitorPool()
        {
            while (!_isDisposed)
            {
                Thread.Sleep(500); // Даем потокам работать
                DateTime now = DateTime.Now;

                lock (_threads)
                {
                    // 1. Адаптивное сжатие
                    if (_threads.Count > _minThreads)
                    {
                        var idleWorker = _threads.FirstOrDefault(w =>
                            w.IsIdle && (now - w.LastActiveTime).TotalMilliseconds > _idleTimeoutMs);

                        if (idleWorker != null)
                        {
                            idleWorker.Stop();
                            _threads.Remove(idleWorker);
                            Log($"[Pool] Thread {idleWorker.Id} removed due to inactivity. Remaining: {_threads.Count}", ConsoleColor.DarkYellow);

                            // Просыпаемся, чтобы проверить условие цикла _running
                            lock (_taskQueue) { Monitor.PulseAll(_taskQueue); }
                        }
                    }

                    // 2. Замена зависших потоков
                    for (int i = 0; i < _threads.Count; i++)
                    {
                        var w = _threads[i];
                        if (w.IsWorking && (now - w.TaskStartTime).TotalMilliseconds > _taskMaxDurationMs)
                        {
                            Log($"[Pool] HANGING THREAD DETECTED {w.Id}! Replacing...", ConsoleColor.Magenta);
                            w.Abandon();
                            _threads.RemoveAt(i);
                            CreateWorker();
                            break;
                        }
                    }
                }
                Console.Title = $"Threads: {CurrentThreadCount} | Tasks: {TasksInQueue}";
            }
        }

        private void Log(string msg, ConsoleColor color)
        {
            lock (Console.Out)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(msg);
                Console.ResetColor();
            }
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
                            // LastActiveTime устанавливается ОДИН РАЗ при переходе в режим ожидания
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
                        try { task(); } catch { }

                        IsIdle = true;
                        LastActiveTime = DateTime.Now; // Засекаем начало простоя
                    }
                }
            }
        }
    }
}