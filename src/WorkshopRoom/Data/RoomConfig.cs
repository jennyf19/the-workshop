namespace WorkshopRoom.Data;

// Small bits of room configuration resolved at startup.
public sealed class RoomConfig
{
    // Where a "new workshop" repo is created + cloned. Defaults to the drive
    // root so workshops land beside each other, not inside the product repo.
    public string WorkshopsBaseDir { get; init; } = "";
}
