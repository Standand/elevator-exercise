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
        private const int TIMEOUT_SECONDS = 10; // Consider opposite direction after 10 seconds
        private const int LOAD_PENALTY_PER_STOP = 2; // Ticks penalty per additional stop
        private const int OPPOSITE_DIRECTION_PENALTY = 500; // Large penalty for opposite direction

        public Elevator? SelectBestElevator(HallCall hallCall, List<Elevator> elevators)
        {
            // First, try normal candidates (same direction or IDLE)
            var candidates = elevators.Where(e => e.CanAcceptHallCall(hallCall)).ToList();

            if (candidates.Any())
            {
                // Normal assignment: select based on time cost
                return SelectBestByTimeCost(hallCall, candidates);
            }

            // No normal candidates available - check for timeout-based fallback
            var hallCallAge = hallCall.GetAge();
            if (hallCallAge.TotalSeconds >= TIMEOUT_SECONDS)
            {
                // Timeout reached: consider opposite direction elevators as emergency fallback
                var oppositeDirectionElevators = GetOppositeDirectionElevators(hallCall, elevators);
                
                if (oppositeDirectionElevators.Any())
                {
                    // Use opposite direction with heavy penalty
                    return SelectBestByTimeCostWithOppositePenalty(hallCall, oppositeDirectionElevators);
                }
            }

            // No candidates available, even with timeout fallback
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

        /// <summary>
        /// Selects the best elevator based on time-based cost calculation with tie-breaking.
        /// </summary>
        private static Elevator SelectBestByTimeCost(HallCall hallCall, List<Elevator> elevators)
        {
            return elevators
                .OrderBy(e => CalculateTimeCost(hallCall, e))
                .ThenBy(e => e.Id)  // Tie-breaker: prefer lower ID for consistency
                .First();
        }

        /// <summary>
        /// Selects the best elevator with opposite direction penalty (for timeout fallback).
        /// </summary>
        private static Elevator SelectBestByTimeCostWithOppositePenalty(HallCall hallCall, List<Elevator> elevators)
        {
            return elevators
                .OrderBy(e => CalculateTimeCostForOppositeDirection(hallCall, e))
                .ThenBy(e => e.Id)
                .First();
        }

        /// <summary>
        /// Calculates the estimated time (in ticks) for elevator to reach hall call floor.
        /// Uses real-time configuration ticks (movementTicks, doorOpenDuration) from elevator.
        /// Includes movement time, loading time at intermediate stops, and load penalty.
        /// Lower time = better choice.
        /// </summary>
        private static int CalculateTimeCost(HallCall hallCall, Elevator elevator)
        {
            // Get timing configuration from elevator (real-time values)
            var movementTicks = elevator.GetMovementTicks();
            var doorOpenDuration = elevator.GetDoorOpenDuration();
            var distance = Math.Abs(elevator.CurrentFloor - hallCall.Floor);

            int baseTime;

            if (elevator.State == ElevatorState.IDLE)
            {
                // IDLE elevator: time = distance * movementTicks
                // No intermediate stops, no loading time
                baseTime = distance * movementTicks;
            }
            else if (elevator.Direction == hallCall.Direction)
            {
                // Same direction: check if on route or needs route extension
                var furthestDest = elevator.GetFurthestDestination();
                
                if (furthestDest.HasValue)
                {
                    bool isOnRoute = elevator.Direction == Direction.UP
                        ? elevator.CurrentFloor < hallCall.Floor && hallCall.Floor <= furthestDest.Value
                        : elevator.CurrentFloor > hallCall.Floor && hallCall.Floor >= furthestDest.Value;

                    if (isOnRoute)
                    {
                        // Hall call is on route: time = floors to hall call * movementTicks + loading at intermediate stops
                        var intermediateStops = elevator.GetIntermediateStopsCount(hallCall.Floor);
                        var floorsToHallCall = distance;
                        baseTime = (floorsToHallCall * movementTicks) + (intermediateStops * doorOpenDuration);
                    }
                    else
                    {
                        // Route extension needed: time to complete current route + time to reach hall call
                        var floorsToFurthest = Math.Abs(elevator.CurrentFloor - furthestDest.Value);
                        var intermediateStopsToFurthest = elevator.GetIntermediateStopsCount(furthestDest.Value);
                        var timeToCompleteRoute = (floorsToFurthest * movementTicks) + (intermediateStopsToFurthest * doorOpenDuration);
                        
                        var floorsFromFurthestToHallCall = Math.Abs(furthestDest.Value - hallCall.Floor);
                        var timeToReachHallCall = floorsFromFurthestToHallCall * movementTicks;
                        
                        baseTime = timeToCompleteRoute + timeToReachHallCall;
                    }
                }
                else
                {
                    // Should not happen, but safe fallback
                    baseTime = distance * movementTicks;
                }
            }
            else
            {
                // Should not reach here with current CanAcceptHallCall logic, but safe fallback
                baseTime = int.MaxValue;
            }

            // Load consideration: add penalty for elevators with many stops
            // More stops = more delays = higher cost
            // Uses real-time configuration: penalty is in ticks
            var loadPenalty = elevator.GetDestinationCount() * LOAD_PENALTY_PER_STOP;

            return baseTime + loadPenalty;
        }

        /// <summary>
        /// Calculates time cost for opposite direction elevators (timeout fallback).
        /// Uses real-time configuration ticks from elevator.
        /// Includes heavy penalty to ensure this is only used when no other options exist.
        /// </summary>
        private static int CalculateTimeCostForOppositeDirection(HallCall hallCall, Elevator elevator)
        {
            // Get timing configuration from elevator (real-time values)
            var movementTicks = elevator.GetMovementTicks();
            var doorOpenDuration = elevator.GetDoorOpenDuration();
            
            // Calculate time to complete current route, then reach hall call
            var furthestDest = elevator.GetFurthestDestination();
            
            if (!furthestDest.HasValue)
            {
                return int.MaxValue; // Invalid state
            }

            // Time to complete current route (using real-time ticks)
            var floorsToFurthest = Math.Abs(elevator.CurrentFloor - furthestDest.Value);
            var intermediateStopsToFurthest = elevator.GetIntermediateStopsCount(furthestDest.Value);
            var timeToCompleteRoute = (floorsToFurthest * movementTicks) + (intermediateStopsToFurthest * doorOpenDuration);
            
            // Time from route end to hall call (using real-time ticks)
            var floorsFromFurthestToHallCall = Math.Abs(furthestDest.Value - hallCall.Floor);
            var timeToReachHallCall = floorsFromFurthestToHallCall * movementTicks;
            
            var baseTime = timeToCompleteRoute + timeToReachHallCall;
            
            // Load penalty (using real-time configuration)
            var loadPenalty = elevator.GetDestinationCount() * LOAD_PENALTY_PER_STOP;
            
            // Heavy penalty for opposite direction (ensures this is last resort)
            return baseTime + loadPenalty + OPPOSITE_DIRECTION_PENALTY;
        }
    }
}
