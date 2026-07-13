namespace LabelWise.Application.Models.Nutrition
{
    public sealed class SanitizationResult<T>
    {
        private SanitizationResult(bool isSuccess, T? value, IReadOnlyList<string> errors)
        {
            IsSuccess = isSuccess;
            Value = value;
            Errors = errors;
        }

        public bool IsSuccess { get; }

        public T? Value { get; }

        public IReadOnlyList<string> Errors { get; }

        public static SanitizationResult<T> Success(T value)
        {
            return new SanitizationResult<T>(true, value, Array.Empty<string>());
        }

        public static SanitizationResult<T> Failure(params string[] errors)
        {
            return new SanitizationResult<T>(false, default, errors.Where(e => !string.IsNullOrWhiteSpace(e)).ToArray());
        }
    }
}
