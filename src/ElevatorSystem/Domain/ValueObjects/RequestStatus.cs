namespace ElevatorSystem.Domain.ValueObjects
{
    /// <summary>
    /// Represents the status of a passenger request (WAITING, IN_TRANSIT, COMPLETED).
    /// </summary>
    public enum RequestStatus
    {
        WAITING,
        IN_TRANSIT,
        COMPLETED
    }
}
