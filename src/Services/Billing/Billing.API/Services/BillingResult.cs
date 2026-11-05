namespace Billing.API.Services;

public enum BillingError
{
    None,
    NotFound,
    Unprocessable,
    Conflict,
    Unavailable
}

public sealed record BillingResult<T>(T? Value, BillingError Error, string? Message = null)
{
    public bool IsSuccess => Error == BillingError.None;
    public static BillingResult<T> Success(T value) => new(value, BillingError.None);
    public static BillingResult<T> Failure(BillingError error, string message) => new(default, error, message);
}
