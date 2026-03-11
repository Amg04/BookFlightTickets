namespace BookFlightTickets.Core.Domain.ResultPattern
{
    public class Result<T>
    {
        public bool IsSuccess { get; }
        public T? Value { get; }
        public Error? Error { get; }

        private Result(bool isSuccess, T? value, Error? error)
        {
            if (isSuccess && value is null)
                throw new ArgumentNullException(nameof(value), "Value cannot be null when success is true.");
            if (!isSuccess && error is null)
                throw new ArgumentNullException(nameof(error), "Error cannot be null when success is false.");
            IsSuccess = isSuccess;
            Value = isSuccess ? value! : default;
            Error = isSuccess ? null : error!;
        }

        public static Result<T> Success(T value) => new(true, value, null);
        public static Result<T> Failure(Error error) => new(false, default, error);
    }
}



   
