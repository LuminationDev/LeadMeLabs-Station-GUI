using Station;
using System;
using Xunit;
using System.Collections.Generic;
using System.Threading;
using LeadMeLabsLibrary;

namespace StationTests._utils
{
    public class TaskQueueTests
    {
        /// <summary>
        /// Wnqueue a single task and wait for it to complete, 
        /// then assert that the task was executed.
        /// </summary>
        [Fact]
        public void Queue_WithSingleTask_ExecutesTask()
        {
            // Arrange
            bool taskExecuted = false;
            Action task = () => { taskExecuted = true; };

            // Act
            TaskQueue.Queue(isParallel: true, task);
            Thread.Sleep(50); // Wait for task to complete

            // Assert
            Assert.True(taskExecuted);
        }

        /// <summary>
        /// Enqueue three tasks and wait for them to complete, 
        /// then assert that they were executed in the correct order.
        /// </summary>
        [Fact]
        public void Queue_WithMultipleTasks_ExecutesTasksInOrder()
        {
            // Arrange
            List<string> taskResults = new List<string>();
            Action task1 = () => { taskResults.Add("Task 1"); };
            Action task2 = () => { taskResults.Add("Task 2"); };
            Action task3 = () => { taskResults.Add("Task 3"); };

            // Act
            TaskQueue.Queue(isParallel: true, task1);
            Thread.Sleep(50); // Wait for tasks to complete
            TaskQueue.Queue(isParallel: true, task2);
            Thread.Sleep(50); // Wait for tasks to complete
            TaskQueue.Queue(isParallel: true, task3);
            Thread.Sleep(50); // Wait for tasks to complete

            // Assert
            Assert.Equal(new List<string> { "Task 1", "Task 2", "Task 3" }, taskResults);
        }

        /// <summary>
        /// Enqueue two parallel tasks and one serial task, and 
        /// wait for them to complete, then assert that they were 
        /// executed in the correct order.
        /// </summary>
        [Fact]
        public void Queue_WithSerialTask_ExecutesTasksInOrder()
        {
            // Arrange
            List<string> taskResults = new List<string>();
            Action task1 = () => { taskResults.Add("Parallel Task 1"); };
            Action task2 = () => { taskResults.Add("Parallel Task 2"); };
            Action task3 = () => { taskResults.Add("Serial Task 3"); };

            // Act
            TaskQueue.Queue(isParallel: true, task1);
            TaskQueue.Queue(isParallel: true, task2);
            TaskQueue.Queue(isParallel: false, task3);
            Thread.Sleep(50); // Wait for tasks to complete

            // Assert
            Assert.Equal(new List<string> { "Parallel Task 1", "Parallel Task 2", "Serial Task 3" }, taskResults);
        }
    }
}
