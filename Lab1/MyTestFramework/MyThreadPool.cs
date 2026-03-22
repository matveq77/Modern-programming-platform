using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly int _taskMaxDurationMs; // Для замены зависших потоков

        private bool _isDisposed = false;

        public int CurrentThreadCount => _threads.Count;
        public int TasksInQueue => _taskQueue.Count;

        public MyThreadPool(int minThreads = 2, int maxThreads = 10, int idleTimeoutMs = 3000, int taskMaxDurationMs = 5000)
        {
            _minThreads = minThreads;
            _maxThreads = maxThreads;
            _idleTimeoutMs = idleTimeoutMs;
            _taskMaxDurationMs = taskMaxDurationMs;

            for (int i = 0; i < _minThreads; i++)
            {
                CreateWorker();
            }

            // Поток мониторинга состояния пула
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
                Log($"[Pool] Создан новый поток. Всего: {_threads.Count}", ConsoleColor.Cyan);
            }
        }

        private void AdjustPoolSize()
        {
            lock (_taskQueue)
            {
                // Если очередь растет и мы не достигли максимума — создаем поток
                if (_taskQueue.Count > 0 && _threads.Count < _maxThreads)
                {
                    CreateWorker();
                }
            }
        }

        private void MonitorPool()
        {
            while (!_isDisposed)
            {
                Thread.Sleep(1000);
                DateTime now = DateTime.Now;

                lock (_threads)
                {
                    // 1. Адаптивное сжатие (удаление лишних простаивающих потоков)
                    if (_threads.Count > _minThreads)
                    {
                        var idleWorker = _threads.FirstOrDefault(w => w.IsIdle && (now - w.LastActiveTime).TotalMilliseconds > _idleTimeoutMs);
                        if (idleWorker != null)
                        {
                            idleWorker.Stop();
                            _threads.Remove(idleWorker);
                            Log($"[Pool] Поток удален по простою. Осталось: {_threads.Count}", ConsoleColor.DarkYellow);
                        }
                    }

                    // 2. Дополнительно: Замена зависших потоков
                    for (int i = 0; i < _threads.Count; i++)
                    {
                        var w = _threads[i];
                        if (w.IsWorking && (now - w.TaskStartTime).TotalMilliseconds > _taskMaxDurationMs)
                        {
                            Log($"[Pool] Обнаружен зависший поток {w.Id}! Замена...", ConsoleColor.Magenta);
                            w.Abandon(); // Помечаем как "зомби"
                            _threads.RemoveAt(i);
                            CreateWorker(); // Создаем новый взамен
                            break;
                        }
                    }
                }

                // Мониторинг в консоль
                Console.Title = $"Threads: {CurrentThreadCount} | Queue: {TasksInQueue} | Min: {_minThreads} | Max: {_maxThreads}";
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
            public int Id => _thread.ManagedThreadId;
            public bool IsIdle { get; private set; } = true;
            public bool IsWorking => !IsIdle;
            public DateTime LastActiveTime { get; private set; } = DateTime.Now;
            public DateTime TaskStartTime { get; private set; }

            public WorkerThread(MyThreadPool pool)
            {
                _pool = pool;
                _thread = new Thread(Run) { IsBackground = true };
            }

            public void Start() => _thread.Start();
            public void Stop() => _running = false;
            public void Abandon() { _running = false; _thread = null; }

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
                            LastActiveTime = DateTime.Now;
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
                        try { task(); }
                        catch { /* Отказоустойчивость: ловим ошибки внутри теста */ }
                        LastActiveTime = DateTime.Now;
                        IsIdle = true;
                    }
                }
            }
        }
    }
}