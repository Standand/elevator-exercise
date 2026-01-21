using System;

namespace ElevatorSystem.Common
{
    /// <summary>
    /// Represents the result of an operation that can succeed or fail.
    /// </summary>
    /// <typeparam name="T">The type of the success value</typeparam>
    public class Result<T>
    {
        public bool IsSuccess { get; }
        public T? Value { get; }
        public string? Error { get; }

        private Result(bool isSuccess, T? value, string? error)
        {
            IsSuccess = isSuccess;
            Value = value;
            Error = error;
        }

        /// <summary>
        /// Creates a successful result with a value.
        /// </summary>
        public static Result<T> Success(T value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value), "Success value cannot be null");

            return new Result<T>(true, value, null);
        }

        /// <summary>
        /// Creates a failed result with an error message.
        /// </summary>
        public static Result<T> Failure(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
                throw new ArgumentException("Error message cannot be empty", nameof(error));

            return new Result<T>(false, default, error);
        }

        /// <summary>
        /// Pattern matching support for Result.
        /// </summary>
        public TResult Match<TResult>(
            Func<T, TResult> onSuccess,
            Func<string, TResult> onFailure)
        {
            return IsSuccess ? onSuccess(Value!) : onFailure(Error!);
        }

        public override string ToString()
        {
            return IsSuccess ? $"Success({Value})" : $"Failure({Error})";
        }
    }
}
