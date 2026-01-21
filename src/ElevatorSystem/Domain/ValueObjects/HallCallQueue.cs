using System;
using System.Collections.Generic;
using System.Linq;
using ElevatorSystem.Domain.Entities;

namespace ElevatorSystem.Domain.ValueObjects
{
    /// <summary>
    /// Manages the queue of hall calls.
    /// Encapsulates hall call storage and retrieval logic.
    /// </summary>
    public class HallCallQueue
    {
        private readonly List<HallCall> _hallCalls = new List<HallCall>();

        /// <summary>
        /// Adds a hall call to the queue.
        /// </summary>
        public void Add(HallCall hallCall)
        {
            if (hallCall == null)
                throw new ArgumentNullException(nameof(hallCall));

            _hallCalls.Add(hallCall);
        }

        /// <summary>
        /// Finds a hall call by floor and direction (for idempotency check).
        /// </summary>
        public HallCall? FindByFloorAndDirection(int floor, Direction direction)
        {
            return _hallCalls.FirstOrDefault(hc =>
                hc.Floor == floor &&
                hc.Direction == direction &&
                hc.Status != HallCallStatus.COMPLETED);
        }

        /// <summary>
        /// Finds a hall call by ID.
        /// </summary>
        public HallCall? FindById(Guid id)
        {
            return _hallCalls.FirstOrDefault(hc => hc.Id == id);
        }

        /// <summary>
        /// Gets all pending hall calls (not yet assigned).
        /// </summary>
        public List<HallCall> GetPending()
        {
            return _hallCalls
                .Where(hc => hc.Status == HallCallStatus.PENDING)
                .ToList();
        }

        /// <summary>
        /// Gets the count of pending hall calls.
        /// </summary>
        public int GetPendingCount()
        {
            return _hallCalls.Count(hc => hc.Status == HallCallStatus.PENDING);
        }

        /// <summary>
        /// Gets all hall calls (for status queries).
        /// </summary>
        public List<HallCall> GetAll()
        {
            return _hallCalls.ToList();
        }
    }
}
