namespace CircDesk.Api.Domain;

public enum MemberRole
{
    Member = 0,
    Librarian = 1
}

public enum CopyStatus
{
    Available = 0,
    OnLoan = 1,
    Reserved = 2,
    Lost = 3
}

/// <summary>
/// Only Active/Returned are persisted. "Overdue" is never stored — it's derived at read time by
/// comparing DueDate against the current time, so no background job is needed to flip it.
/// </summary>
public enum LoanStatus
{
    Active = 0,
    Returned = 1
}

public enum HoldStatus
{
    Queued = 0,
    Ready = 1,
    Fulfilled = 2,
    Expired = 3,
    Cancelled = 4
}

public enum FineStatus
{
    Pending = 0,
    Paid = 1
}
