using System;
using System.Collections.Generic;
using System.Linq;
using ElevatorSystem.Domain.Entities;
using ElevatorSystem.Domain.ValueObjects;

namespace ElevatorSystem.Domain.Services
{
    /// <summary>
    /// Direction-aware scheduling strategy with time-based cost calculation.
    /// Features:
    /// - Time-based cost (movement time + loading time at stops) using real-time configuration ticks
    /// - Load consideration (penalty for elevators with many stops)
    /// - Timeout-based opposite direction fallback (for long-waiting calls)
    /// </summary>
    public class DirectionAwareStrategy : ISchedulingStrategy
    {
        private const int TIMEOUT_SECONDS = 10;
        private const int LOAD_PENALTY_PER_STOP = 2;
        private const int OPPOSITE_DIRECTION_PENALTY = 500;

        public Elevator? SelectBestElevator(HallCall hallCall, List<Elevator> elevators)
        {
            var perfectMatch = elevators.FirstOrDefault(e => 
                e.State == ElevatorState.IDLE && 
                e.CurrentFloor == hallCall.Floor);
            if (perfectMatch != null)
            {
                return perfectMatch;
            }

            var candidates = elevators.Where(e => e.CanAcceptHallCall(hallCall)).ToList();

            if (candidates.Count > 0)
            {
                return SelectBestByTimeCost(hallCall, candidates);
            }

            var hallCallAge = hallCall.GetAge();
            if (hallCallAge.TotalSeconds >= TIMEOUT_SECONDS)
            {
                var oppositeDirectionElevators = GetOppositeDirectionElevators(hallCall, elevators);
                
                if (oppositeDirectionElevators.Count > 0)
                {
                    return SelectBestByTimeCostWithOppositePenalty(hallCall, oppositeDirectionElevators);
                }
            }

            return null;
        }

        /// <summary>
        /// Gets elevators moving in opposite direction (for timeout fallback).
        /// </summary>
        private static List<Elevator> GetOppositeDirectionElevators(HallCall hallCall, List<Elevator> elevators)
        {
            return elevators
                .Where(e => e.State == ElevatorState.MOVING && 
                           e.Direction != Direction.IDLE &&
                           e.Direction != hallCall.Direction)
                .ToList();
        }

        private static Elevator SelectBestByTimeCost(HallCall hallCall, List<Elevator> elevators)
        {
            return SelectBest(elevators, e => CalculateTimeCost(hallCall, e));
        }

        private static Elevator SelectBestByTimeCostWithOppositePenalty(HallCall hallCall, List<Elevator> elevators)
        {
            return SelectBest(elevators, e => CalculateTimeCostForOppositeDirection(hallCall, e));
        }

        private static Elevator SelectBest(List<Elevator> elevators, Func<Elevator, int> costCalculator)
        {
            Elevator? best = null;
            int bestCost = int.MaxValue;
            int bestId = int.MaxValue;

            foreach (var elevator in elevators)
            {
                var cost = costCalculator(elevator);
                
                if (cost < bestCost || (cost == bestCost && elevator.Id < bestId))
                {
                    best = elevator;
                    bestCost = cost;
                    bestId = elevator.Id;
                }
            }

            return best!;
        }

        private static int CalculateTimeCost(HallCall hallCall, Elevator elevator)
        {
            var movementTicks = elevator.GetMovementTicks();
            var doorOpenDuration = elevator.GetDoorOpenDuration();
            var distance = Math.Abs(elevator.CurrentFloor - hallCall.Floor);

            int baseTime;

            if (elevator.State == ElevatorState.IDLE)
            {
                baseTime = distance * movementTicks;
            }
            else if (elevator.Direction == hallCall.Direction)
            {
                var furthestDest = elevator.GetFurthestDestination();
                
                if (!furthestDest.HasValue)
                {
                    baseTime = distance * movementTicks;
                }
                else
                {
                    bool isOnRoute = elevator.Direction == Direction.UP
                        ? elevator.CurrentFloor < hallCall.Floor && hallCall.Floor <= furthestDest.Value
                        : elevator.CurrentFloor > hallCall.Floor && hallCall.Floor >= furthestDest.Value;

                    if (isOnRoute)
                    {
                        var intermediateStops = elevator.GetIntermediateStopsCount(hallCall.Floor);
                        baseTime = (distance * movementTicks) + (intermediateStops * doorOpenDuration);
                    }
                    else
                    {
                        baseTime = CalculateRouteExtensionTime(
                            elevator, furthestDest.Value, hallCall.Floor, movementTicks, doorOpenDuration);
                    }
                }
            }
            else
            {
                baseTime = int.MaxValue;
            }

            var loadPenalty = elevator.GetDestinationCount() * LOAD_PENALTY_PER_STOP;

            return baseTime + loadPenalty;
        }

        private static int CalculateTimeCostForOppositeDirection(HallCall hallCall, Elevator elevator)
        {
            var movementTicks = elevator.GetMovementTicks();
            var doorOpenDuration = elevator.GetDoorOpenDuration();
            var furthestDest = elevator.GetFurthestDestination();
            
            if (!furthestDest.HasValue)
            {
                return int.MaxValue;
            }

            var baseTime = CalculateRouteExtensionTime(
                elevator, furthestDest.Value, hallCall.Floor, movementTicks, doorOpenDuration);
            
            var loadPenalty = elevator.GetDestinationCount() * LOAD_PENALTY_PER_STOP;
            
            return baseTime + loadPenalty + OPPOSITE_DIRECTION_PENALTY;
        }

        private static int CalculateRouteExtensionTime(
            Elevator elevator, 
            int furthestDestination, 
            int targetFloor, 
            int movementTicks, 
            int doorOpenDuration)
        {
            var floorsToFurthest = Math.Abs(elevator.CurrentFloor - furthestDestination);
            var intermediateStopsToFurthest = elevator.GetIntermediateStopsCount(furthestDestination);
            var timeToCompleteRoute = (floorsToFurthest * movementTicks) + (intermediateStopsToFurthest * doorOpenDuration);
            
            var floorsFromFurthestToTarget = Math.Abs(furthestDestination - targetFloor);
            var timeToReachTarget = floorsFromFurthestToTarget * movementTicks;
            
            return timeToCompleteRoute + timeToReachTarget;
        }
    }
}
