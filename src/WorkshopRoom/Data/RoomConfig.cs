namespace WorkshopRoom.Data;

// Small bits of room configuration resolved at startup.
public sealed class RoomConfig
{
    // Where a "new desk" folder is created when the operator types a bare name
    // (defaults to the classroom directory, the parent of all desks).
    public string DesksBaseDir { get; init; } = "";
}
