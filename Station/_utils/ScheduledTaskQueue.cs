using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Station
{
    /// <summary>
    /// Represents a scheduled task with an associated action and delay.
    /// </summary>
    class ScheduledTask
    {
        public Func<Task> TaskAction { get; set; }
        public TimeSpan Delay { get; set; }
    }

    /// <summary>
    /// Provides a static queue to manage scheduled tasks and their execution.
    /// </summary>
    static class ScheduledTaskQueue
    {
        private static Queue<ScheduledTask> taskQueue = new Queue<ScheduledTask>();
        private static bool isProcessing = false;

        /// <summary>
        /// Enqueues a task with a given action and delay.
        /// </summary>
        /// <param name="taskAction">The action to be executed as the task.</param>
        /// <param name="delay">The delay before the task should be executed.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static void EnqueueTask(Action taskAction, TimeSpan delay)
        {
            var scheduledTask = new ScheduledTask
            {
                TaskAction = () =>
                {
                    return Task.Run(taskAction);
                },
                Delay = delay
            };

            taskQueue.Enqueue(scheduledTask);

            if (!isProcessing)
            {
                isProcessing = true;
                Task.Run(() => ProcessQueue()); // Start the processing as a new task
            }
        }

        private static async Task ProcessQueue()
        {
            while (taskQueue.Count > 0)
            {
                var scheduledTask = taskQueue.Peek();
                var delay = scheduledTask.Delay;

                await Task.Delay(delay);

                taskQueue.Dequeue();
                await scheduledTask.TaskAction.Invoke(); // Execute the task without waiting
            }

            isProcessing = false;
        }
    }
}
