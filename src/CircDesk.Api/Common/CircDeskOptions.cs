namespace CircDesk.Api.Common;

/// <summary>Circulation policy knobs, bound from the "Circulation" config section.</summary>
public class CircDeskOptions
{
    public const string SectionName = "Circulation";

    public int LoanPeriodDays { get; set; } = 14;
    public int RenewalExtensionDays { get; set; } = 7;
    public int MaxRenewalsPerLoan { get; set; } = 1;
    public int MaxActiveLoansPerMember { get; set; } = 5;
    public int HoldExpirationHours { get; set; } = 48;
    public decimal FinePerDayLate { get; set; } = 1.00m;
    public decimal MaxFinePerLoan { get; set; } = 20.00m;
    public decimal OutstandingFineBlockThreshold { get; set; } = 50.00m;
}
