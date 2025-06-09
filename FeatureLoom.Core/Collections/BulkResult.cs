using FeatureLoom.Collections;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace FeatureLoom.Collections
{
    /// <summary>
    /// Represents the result of a bulk operation, providing counts and descriptions for successes, errors, and warnings.
    /// </summary>
    public interface IBulkResult
    {
        /// <summary>
        /// Gets the number of successful results.
        /// Returns the number of explicit successes, or 1 if no results (success, error, or warning) were added,
        /// or 0 if only errors or warnings were added.
        /// </summary>
        int SuccessCount { get; }

        /// <summary>
        /// Gets the descriptions of all successful results.
        /// </summary>
        IEnumerable<string> SuccessDescriptions { get; }

        /// <summary>
        /// Gets the number of error results.
        /// </summary>
        int ErrorCount { get; }

        /// <summary>
        /// Gets the descriptions of all error results.
        /// </summary>
        IEnumerable<string> ErrorDescriptions { get; }

        /// <summary>
        /// Gets the number of warning results.
        /// </summary>
        int WarningCount { get; }

        /// <summary>
        /// Gets the descriptions of all warning results.
        /// </summary>
        IEnumerable<string> WarningDescriptions { get; }
    }

    /// <summary>
    /// Represents the result of a bulk operation with result values of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the result values.</typeparam>
    public interface IBulkResult<T> : IBulkResult
    {
        /// <summary>
        /// Gets the result values, optionally filtering by success, warning, or error.
        /// </summary>
        /// <param name="ignoreSuccesses">If true, success values are not included.</param>
        /// <param name="ignoreWarnings">If true, warning values are not included.</param>
        /// <param name="ignoreErrors">If true, error values are not included.</param>
        /// <returns>An enumerable of result values.</returns>
        IEnumerable<T> GetResultValues(bool ignoreSuccesses = false, bool ignoreWarnings = true, bool ignoreErrors = true);
    }

    /// <summary>
    /// Provides extension methods for <see cref="IBulkResult"/>.
    /// </summary>
    public static class BulkResultExtensions
    {
        /// <summary>
        /// Determines whether the result contains any successes and is not considered failed.
        /// </summary>
        /// <param name="result">The bulk result.</param>
        /// <param name="ignoreWarnings">If true, warnings are ignored when determining failure.</param>
        /// <returns>True if succeeded; otherwise, false.</returns>
        public static bool Succeeded(this IBulkResult result, bool ignoreWarnings = true) => result.HasAnySuccess() && !result.Failed(ignoreWarnings);

        /// <summary>
        /// Determines whether the result contains any errors and, optionally, warnings.
        /// </summary>
        /// <param name="result">The bulk result.</param>
        /// <param name="ignoreWarnings">If true, warnings are ignored when determining failure.</param>
        /// <returns>True if failed; otherwise, false.</returns>
        public static bool Failed(this IBulkResult result, bool ignoreWarnings = true) => result.HasAnyError() && (ignoreWarnings || result.HasAnyWarning());

        /// <summary>
        /// Determines whether the result contains any successes.
        /// </summary>
        /// <param name="result">The bulk result.</param>
        /// <returns>True if any successes exist; otherwise, false.</returns>
        public static bool HasAnySuccess(this IBulkResult result) => result.SuccessCount > 0;

        /// <summary>
        /// Determines whether the result contains any errors.
        /// </summary>
        /// <param name="result">The bulk result.</param>
        /// <returns>True if any errors exist; otherwise, false.</returns>
        public static bool HasAnyError(this IBulkResult result) => result.ErrorCount > 0;

        /// <summary>
        /// Determines whether the result contains any warnings.
        /// </summary>
        /// <param name="result">The bulk result.</param>
        /// <returns>True if any warnings exist; otherwise, false.</returns>
        public static bool HasAnyWarning(this IBulkResult result) => result.WarningCount > 0;
    }

    /// <summary>
    /// Represents the result of a bulk operation with result values of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the result values.</typeparam>
    public struct BulkResult<T> : IBulkResult<T>
    {
        LazyList<ResultInfo> successInfos;
        LazyList<ResultInfo> errorInfos;
        LazyList<ResultInfo> warningInfos;
        MicroValueLock listsLock;

        /// <summary>
        /// Gets or sets whether locking is disabled for this instance. Default is false (locking enabled).
        /// </summary>
        public bool LockingDisabled { get; set; }

        private void AddSuccess(ResultInfo resultInfo)
        {
            if (!LockingDisabled) listsLock.Enter();
            successInfos.Add(resultInfo);
            if (!LockingDisabled) listsLock.Exit();
        }

        private void AddError(ResultInfo resultInfo)
        {
            if (!LockingDisabled) listsLock.Enter();
            errorInfos.Add(resultInfo);
            if (!LockingDisabled) listsLock.Exit();
        }

        private void AddWarning(ResultInfo resultInfo)
        {
            if (!LockingDisabled) listsLock.Enter();
            warningInfos.Add(resultInfo);
            if (!LockingDisabled) listsLock.Exit();
        }

        /// <summary>
        /// Adds a success result with no description or value.
        /// </summary>
        public void AddSuccess() => AddSuccess(new ResultInfo());

        /// <summary>
        /// Adds a success result with a description.
        /// </summary>
        /// <param name="description">The description of the success.</param>
        public void AddSuccess(string description) => AddSuccess(new ResultInfo(description));

        /// <summary>
        /// Adds a success result with a value and optional description.
        /// </summary>
        /// <param name="resultValue">The result value.</param>
        /// <param name="description">The description of the success.</param>
        public void AddSuccess(T resultValue, string description = null) => AddSuccess(new ResultInfo(resultValue, description));

        /// <summary>
        /// Adds a success result with a value and a function to extract the description.
        /// </summary>
        /// <param name="resultValue">The result value.</param>
        /// <param name="extractDescription">A function to extract the description from the value.</param>
        public void AddSuccess(T resultValue, Func<T, string> extractDescription) => AddSuccess(new ResultInfo(resultValue, extractDescription));

        /// <summary>
        /// Adds an error result with a description.
        /// </summary>
        /// <param name="description">The description of the error.</param>
        public void AddError(string description) => AddError(new ResultInfo(description));

        /// <summary>
        /// Adds an error result with a value and description.
        /// </summary>
        /// <param name="resultValue">The result value.</param>
        /// <param name="description">The description of the error.</param>
        public void AddError(T resultValue, string description) => AddError(new ResultInfo(resultValue, description));

        /// <summary>
        /// Adds an error result with a value and a function to extract the description.
        /// </summary>
        /// <param name="resultValue">The result value.</param>
        /// <param name="extractDescription">A function to extract the description from the value.</param>
        public void AddError(T resultValue, Func<T, string> extractDescription) => AddError(new ResultInfo(resultValue, extractDescription));

        /// <summary>
        /// Adds a warning result with a description.
        /// </summary>
        /// <param name="description">The description of the warning.</param>
        public void AddWarning(string description) => AddWarning(new ResultInfo(description));

        /// <summary>
        /// Adds a warning result with a value and description.
        /// </summary>
        /// <param name="resultValue">The result value.</param>
        /// <param name="description">The description of the warning.</param>
        public void AddWarning(T resultValue, string description) => AddWarning(new ResultInfo(resultValue, description));

        /// <summary>
        /// Adds a warning result with a value and a function to extract the description.
        /// </summary>
        /// <param name="resultValue">The result value.</param>
        /// <param name="extractDescription">A function to extract the description from the value.</param>
        public void AddWarning(T resultValue, Func<T, string> extractDescription) => AddWarning(new ResultInfo(resultValue, extractDescription));

        /// <inheritdoc/>
        public int SuccessCount
        {
            get
            {
                int count = successInfos.Count;
                if (count > 0) return count;
                // If no error or warning was added, it is a success
                if (ErrorCount + WarningCount == 0) return 1;
                return 0;
            }
        }

        /// <inheritdoc/>
        public IEnumerable<string> SuccessDescriptions
        {
            get
            {
                if (!LockingDisabled) listsLock.Enter();
                var result = successInfos.Select(info => info.ResultDescription).Where(desc => desc != null).ToArray();
                if (!LockingDisabled) listsLock.Exit();
                return result;
            }
        }

        /// <inheritdoc/>
        public int ErrorCount => errorInfos.Count;

        /// <inheritdoc/>
        public IEnumerable<string> ErrorDescriptions
        {
            get
            {
                if (!LockingDisabled) listsLock.Enter();
                var result = errorInfos.Select(info => info.ResultDescription).Where(desc => desc != null).ToArray();
                if (!LockingDisabled) listsLock.Exit();
                return result;
            }
        }

        /// <inheritdoc/>
        public int WarningCount => warningInfos.Count;

        /// <inheritdoc/>
        public IEnumerable<string> WarningDescriptions
        {
            get
            {
                if (!LockingDisabled) listsLock.Enter();
                var result = warningInfos.Select(info => info.ResultDescription).Where(desc => desc != null).ToArray();
                if (!LockingDisabled) listsLock.Exit();
                return result;
            }
        }

        /// <inheritdoc/>
        public IEnumerable<T> GetResultValues(bool ignoreSuccesses = false, bool ignoreWarnings = true, bool ignoreErrors = true)
        {
            if (!LockingDisabled) listsLock.Enter();
            IEnumerable<T> result = Array.Empty<T>();
            if (!ignoreSuccesses && successInfos.Count > 0)
            {
                result = result.Union(successInfos.Where(info => info.HasResultValue).Select(info => info.ResultValue));
            }
            if (!ignoreWarnings && warningInfos.Count > 0)
            {
                result = result.Union(warningInfos.Where(info => info.HasResultValue).Select(info => info.ResultValue));
            }
            if (!ignoreErrors && errorInfos.Count > 0)
            {
                result = result.Union(errorInfos.Where(info => info.HasResultValue).Select(info => info.ResultValue));
            }
            if (!LockingDisabled) listsLock.Exit();
            return result;
        }

        /// <summary>
        /// Holds information about a single result, including value and description.
        /// </summary>
        public readonly struct ResultInfo
        {
            /// <summary>
            /// Initializes a new instance with a description only.
            /// </summary>
            /// <param name="resultDescription">The result description.</param>
            public ResultInfo(string resultDescription)
            {
                HasResultValue = false;
                ResultValue = default;
                description = resultDescription;
                extractDescription = null;
            }

            /// <summary>
            /// Initializes a new instance with a value and description.
            /// </summary>
            /// <param name="resultValue">The result value.</param>
            /// <param name="resultDescription">The result description.</param>
            public ResultInfo(T resultValue, string resultDescription)
            {
                HasResultValue = true;
                ResultValue = resultValue;
                description = resultDescription;
                extractDescription = null;
            }

            /// <summary>
            /// Initializes a new instance with a value and a function to extract the description.
            /// </summary>
            /// <param name="resultValue">The result value.</param>
            /// <param name="extractDescription">A function to extract the description from the value.</param>
            public ResultInfo(T resultValue, Func<T, string> extractDescription)
            {
                HasResultValue = true;
                ResultValue = resultValue;
                description = null;
                this.extractDescription = extractDescription;
            }
            
            private readonly Func<T, string> extractDescription;
            private readonly string description;

            /// <summary>
            /// Gets whether this result info has a value.
            /// </summary>
            public readonly bool HasResultValue { get; }

            /// <summary>
            /// Gets the result value.
            /// </summary>
            public readonly T ResultValue { get; }

            /// <summary>
            /// Gets the result description, either from the description or by extracting from the value.
            /// </summary>
            public readonly string ResultDescription => description ?? extractDescription?.Invoke(ResultValue) ?? null;

        }
    }

    /// <summary>
    /// Represents the result of a bulk operation without result values.
    /// </summary>
    public struct BulkResult : IBulkResult
    {
        LazyList<ResultInfo> successInfos;
        LazyList<ResultInfo> errorInfos;
        LazyList<ResultInfo> warningInfos;
        MicroValueLock listsLock;

        /// <summary>
        /// Gets or sets whether locking is disabled for this instance. Default is false (locking enabled).
        /// </summary>
        public bool LockingDisabled { get; set; }

        private void AddSuccess(ResultInfo resultInfo)
        {
            if (!LockingDisabled) listsLock.Enter();
            successInfos.Add(resultInfo);
            if (!LockingDisabled) listsLock.Exit();
        }

        private void AddError(ResultInfo resultInfo)
        {
            if (!LockingDisabled) listsLock.Enter();
            errorInfos.Add(resultInfo);
            if (!LockingDisabled) listsLock.Exit();
        }

        private void AddWarning(ResultInfo resultInfo)
        {
            if (!LockingDisabled) listsLock.Enter();
            warningInfos.Add(resultInfo);
            if (!LockingDisabled) listsLock.Exit();
        }

        /// <summary>
        /// Adds a success result with no description.
        /// </summary>
        public void AddSuccess() => AddSuccess(new ResultInfo());

        /// <summary>
        /// Adds a success result with a description.
        /// </summary>
        /// <param name="description">The description of the success.</param>
        public void AddSuccess(string description) => AddSuccess(new ResultInfo(description));

        /// <summary>
        /// Adds an error result with a description.
        /// </summary>
        /// <param name="description">The description of the error.</param>
        public void AddError(string description) => AddError(new ResultInfo(description));

        /// <summary>
        /// Adds a warning result with a description.
        /// </summary>
        /// <param name="description">The description of the warning.</param>
        public void AddWarning(string description) => AddWarning(new ResultInfo(description));

        /// <inheritdoc/>
        public int SuccessCount
        {
            get 
            {
                int count = successInfos.Count;
                if (count > 0) return count;
                // If no error or warning was added, it is a success
                if (ErrorCount + WarningCount == 0) return 1;
                return 0;
            }
        }

        /// <inheritdoc/>
        public IEnumerable<string> SuccessDescriptions
        {
            get
            {
                if (!LockingDisabled) listsLock.Enter();
                var result = successInfos.Select(info => info.ResultDescription).Where(desc => desc != null).ToArray();
                if (!LockingDisabled) listsLock.Exit();
                return result;
            }
        }

        /// <inheritdoc/>
        public int ErrorCount => errorInfos.Count;

        /// <inheritdoc/>
        public IEnumerable<string> ErrorDescriptions
        {
            get
            {
                if (!LockingDisabled) listsLock.Enter();
                var result = errorInfos.Select(info => info.ResultDescription).Where(desc => desc != null).ToArray();
                if (!LockingDisabled) listsLock.Exit();
                return result;
            }
        }

        /// <inheritdoc/>
        public int WarningCount => warningInfos.Count;

        /// <inheritdoc/>
        public IEnumerable<string> WarningDescriptions
        {
            get
            {
                if (!LockingDisabled) listsLock.Enter();
                var result = warningInfos.Select(info => info.ResultDescription).Where(desc => desc != null).ToArray();
                if (!LockingDisabled) listsLock.Exit();
                return result;
            }
        }

        /// <summary>
        /// Holds information about a single result, including description.
        /// </summary>
        public readonly struct ResultInfo
        {
            /// <summary>
            /// Initializes a new instance with a description.
            /// </summary>
            /// <param name="resultDescription">The result description.</param>
            public ResultInfo(string resultDescription)
            {
                description = resultDescription;
            }

            private readonly string description;

            /// <summary>
            /// Gets the result description.
            /// </summary>
            public readonly string ResultDescription => description;

        }
    }

}
