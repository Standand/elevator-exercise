using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ElevatorSystem.Common;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.Services;
using ElevatorSystem.Domain.ValueObjects;
using ElevatorSystem.Infrastructure.Metrics;
using ElevatorSystem.Tests.Integration.TestHelpers;
using ElevatorSystem.Tests.TestHelpers;

namespace ElevatorSystem.Tests.Integration
{
    /// <summary>
    /// Integration tests for concurrent operations and thread safety.
    /// </summary>
    public class ConcurrentOperationsIntegrationTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void RequestHallCall_ConcurrentRequests_ThreadSafe()
        {
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(new SystemMetrics())
                .Build();

            var results = new List<Result<HallCall>>();
            var tasks = new List<Task>();

            for (int i = 0; i < 10; i++)
            {
                int floor = i;
                tasks.Add(Task.Run(() =>
                {
                    var result = building.RequestHallCall(floor: floor % 10, direction: Direction.UP);
                    lock (results)
                    {
                        results.Add(result);
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());

            Assert.Equal(10, results.Count);
            Assert.True(results.All(r => r.IsSuccess || r.Error != null));
            
            var status = building.GetStatus();
            Assert.True(status.PendingHallCallsCount >= 0);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void ProcessTick_ConcurrentWithRequests_ThreadSafe()
        {
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(new SystemMetrics())
                .Build();

            var requestTasks = new List<Task>();
            var tickTasks = new List<Task>();
            var exceptions = new List<System.Exception>();

            for (int i = 0; i < 5; i++)
            {
                int floor = i;
                requestTasks.Add(Task.Run(() =>
                {
                    try
                    {
                        building.RequestHallCall(floor: floor, direction: Direction.UP);
                    }
                    catch (System.Exception ex)
                    {
                        lock (exceptions)
                        {
                            exceptions.Add(ex);
                        }
                    }
                }));

                tickTasks.Add(Task.Run(() =>
                {
                    try
                    {
                        building.ProcessTick();
                    }
                    catch (System.Exception ex)
                    {
                        lock (exceptions)
                        {
                            exceptions.Add(ex);
                        }
                    }
                }));
            }

            Task.WaitAll(requestTasks.Concat(tickTasks).ToArray());

            Assert.Empty(exceptions);
            
            var status = building.GetStatus();
            Assert.NotNull(status);
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "P1")]
        public void GetStatus_ConcurrentWithOperations_ReturnsConsistentSnapshot()
        {
            var building = IntegrationTestBuilder.Create()
                .WithSchedulingStrategy(new DirectionAwareStrategy())
                .WithLogger(new MockLogger())
                .WithMetrics(new SystemMetrics())
                .Build();

            var statusTasks = new List<Task<BuildingStatus>>();
            var operationTasks = new List<Task>();

            for (int i = 0; i < 10; i++)
            {
                int floor = i;
                operationTasks.Add(Task.Run(() =>
                {
                    building.RequestHallCall(floor: floor % 10, direction: Direction.UP);
                    building.ProcessTick();
                }));

                statusTasks.Add(Task.Run(() => building.GetStatus()));
            }

            Task.WaitAll(operationTasks.Concat(statusTasks.Cast<Task>()).ToArray());

            var statuses = statusTasks.Select(t => t.Result).ToList();
            
            Assert.Equal(10, statuses.Count);
            Assert.All(statuses, status => Assert.NotNull(status));
            Assert.All(statuses, status => Assert.True(status.Elevators.Count == 4));
        }
    }
}
