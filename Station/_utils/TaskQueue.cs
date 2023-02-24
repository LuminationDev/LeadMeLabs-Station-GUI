using System;
using System.Collections.Generic;
using System.Threading;

namespace Station
{
    /// <summary>
    /// Allow serial and parallel tasks to be queued, where serial tasks will be executed one after the 
    /// other, and parallel tasks will be executed in any order, but in parallel. This gives you the ability 
    /// to serialize tasks where necessary, also have parallel tasks, but do this as tasks are received 
    /// i.e. you do not need to know about the entire sequence up-front, execution order is maintained 
    /// dynamically.
    /// </summary>
    public static class TaskQueue
    {
        private static readonly object _syncObj = new object();
        private static readonly Queue<QTask> _tasks = new Queue<QTask>();
        private static int _runningTaskCount;

        public static void Queue(bool isParallel, Action task)
        {
            lock (_syncObj)
            {
                _tasks.Enqueue(new QTask { IsParallel = isParallel, Task = task });
            }

            ProcessTaskQueue();
        }

        public static int Count
        {
            get { lock (_syncObj) { return _tasks.Count; } }
        }

        private static void ProcessTaskQueue()
        {
            lock (_syncObj)
            {
                if (_runningTaskCount != 0) return;

                while (_tasks.Count > 0 && _tasks.Peek().IsParallel)
                {
                    QTask parallelTask = _tasks.Dequeue();

                    QueueUserWorkItem(parallelTask);
                }

                if (_tasks.Count > 0 && _runningTaskCount == 0)
                {
                    QTask serialTask = _tasks.Dequeue();

                    QueueUserWorkItem(serialTask);
                }
            }
        }

        private static void QueueUserWorkItem(QTask qTask)
        {
            Action completionTask = () =>
            {
                qTask.Task();

                OnTaskCompleted();
            };

            _runningTaskCount++;

            ThreadPool.QueueUserWorkItem(_ => completionTask());
        }

        private static void OnTaskCompleted()
        {
            lock (_syncObj)
            {
                if (--_runningTaskCount == 0)
                {
                    ProcessTaskQueue();
                }
            }
        }

        private class QTask
        {
            public Action Task { get; set; }
            public bool IsParallel { get; set; }
        }
    }
}
