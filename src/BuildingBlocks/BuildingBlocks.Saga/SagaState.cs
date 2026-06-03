namespace BuildingBlocks.Saga;

public static class SagaStates
{
    public const string Reserved = "Reserved";
    public const string Persisted = "Persisted";
    public const string Published = "Published";
    public const string Completed = "Completed";
    public const string Compensating = "Compensating";
    public const string Failed = "Failed";
}

public static class SagaTypes
{
    public const string CreateRental = "CreateRental";
    public const string CreateSale = "CreateSale";
}
