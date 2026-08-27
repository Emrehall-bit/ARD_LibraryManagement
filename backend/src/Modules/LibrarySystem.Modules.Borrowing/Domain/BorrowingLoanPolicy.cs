namespace LibrarySystem.Modules.Borrowing.Domain;

public static class BorrowingLoanPolicy
{
    public const int DefaultLoanPeriodDays = 14;
    public const int RenewalPeriodDays = 7;
    public const int MaxRenewalCount = 1;
    public const int MaxActiveBorrowCount = 3;
    public const int MaxLoanPeriodMonths = 1;
}
