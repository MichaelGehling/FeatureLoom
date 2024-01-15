using FeatureLoom.Collections;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace FeatureLoom.Helpers
{
    public interface IBulkResult
    {
        int SuccessCount { get; }
        IEnumerable<string> SuccessDecriptions { get; }
        int ErrorCount { get; }
        IEnumerable<string> ErrorDescriptions { get; }
        int WarningCount { get; }
        IEnumerable<string> WarningDescriptions { get; }
    }

    public interface IBulkResult<T> : IBulkResult
    {
        public IEnumerable<T> GetResultValues(bool ignoreSuccesses = false, bool ignoreWarnings = true, bool ignoreErrors = true);
    }

    public static class BulkResultExtensions
    {
        public static bool Succeeded(this IBulkResult result, bool ignoreWarnings = true) => result.HasAnySuccess() && !result.Failed(ignoreWarnings);
        public static bool Failed(this IBulkResult result, bool ignoreWarnings = true) => result.HasAnyError() && (ignoreWarnings || result.HasAnyWarning());
        public static bool HasAnySuccess(this IBulkResult result) => result.SuccessCount > 0;
        public static bool HasAnyError(this IBulkResult result) => result.ErrorCount > 0;
        public static bool HasAnyWarning(this IBulkResult result) => result.WarningCount > 0;
    }

    public struct BulkResult<T> : IBulkResult<T>
    {
        LazyList<ResultInfo> successInfos;
        LazyList<ResultInfo> errorInfos;
        LazyList<ResultInfo> warningInfos;
        MicroValueLock listsLock;

        private void AddSuccess(ResultInfo resultInfo)
        {
            listsLock.Enter();
            successInfos.Add(resultInfo);
            listsLock.Exit();
        }

        private void AddError(ResultInfo resultInfo)
        {
            listsLock.Enter();
            errorInfos.Add(resultInfo);
            listsLock.Exit();
        }

        private void AddWarning(ResultInfo resultInfo)
        {
            listsLock.Enter();
            warningInfos.Add(resultInfo);
            listsLock.Exit();
        }

        public void AddSuccess() => AddSuccess(new ResultInfo());
        public void AddSuccess(string description) => AddSuccess(new ResultInfo(description));
        public void AddSuccess(T resultValue, string description = null) => AddSuccess(new ResultInfo(resultValue, description));
        public void AddSuccess(T resultValue, Func<T, string> extractDescription) => AddSuccess(new ResultInfo(resultValue, extractDescription));

        public void AddError(string description) => AddError(new ResultInfo(description));
        public void AddError(T resultValue, string description) => AddError(new ResultInfo(resultValue, description));
        public void AddError(T resultValue, Func<T, string> extractDescription) => AddError(new ResultInfo(resultValue, extractDescription));

        public void AddWarning(string description) => AddWarning(new ResultInfo(description));
        public void AddWarning(T resultValue, string description) => AddWarning(new ResultInfo(resultValue, description));
        public void AddWarning(T resultValue, Func<T, string> extractDescription) => AddWarning(new ResultInfo(resultValue, extractDescription));

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

        public IEnumerable<string> SuccessDecriptions
        {
            get
            {
                listsLock.Enter();
                var result = successInfos.Select(info => info.ResultDescription).Where(desc => desc != null).ToArray();
                listsLock.Exit();
                return result;
            }
        }

        public int ErrorCount => errorInfos.Count;

        public IEnumerable<string> ErrorDescriptions
        {
            get
            {
                listsLock.Enter();
                var result = errorInfos.Select(info => info.ResultDescription).Where(desc => desc != null).ToArray();
                listsLock.Exit();
                return result;
            }
        }

        public int WarningCount => warningInfos.Count;

        public IEnumerable<string> WarningDescriptions
        {
            get
            {
                listsLock.Enter();
                var result = warningInfos.Select(info => info.ResultDescription).Where(desc => desc != null).ToArray();
                listsLock.Exit();
                return result;
            }
        }

        public IEnumerable<T> GetResultValues(bool ignoreSuccesses = false, bool ignoreWarnings = true, bool ignoreErrors = true)
        {
            listsLock.Enter();
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
            listsLock.Exit();
            return result;
        }

        public readonly struct ResultInfo
        {
            public ResultInfo(string resultDescription)
            {
                HasResultValue = false;
                ResultValue = default;
                description = resultDescription;
                extractDescription = null;
            }

            public ResultInfo(T resultValue, string resultDescription)
            {
                HasResultValue = true;
                ResultValue = resultValue;
                description = resultDescription;
                extractDescription = null;
            }

            public ResultInfo(T resultValue, Func<T, string> extractDescription)
            {
                HasResultValue = true;
                ResultValue = resultValue;
                description = null;
                this.extractDescription = extractDescription;
            }
            
            private readonly Func<T, string> extractDescription;
            private readonly string description;

            public readonly bool HasResultValue { get; }
            public readonly T ResultValue { get; }


            public readonly string ResultDescription => description ?? extractDescription?.Invoke(ResultValue) ?? null;

        }
    }

    public struct BulkResult : IBulkResult
    {
        LazyList<ResultInfo> successInfos;
        LazyList<ResultInfo> errorInfos;
        LazyList<ResultInfo> warningInfos;
        MicroValueLock listsLock;

        private void AddSuccess(ResultInfo resultInfo)
        {
            listsLock.Enter();
            successInfos.Add(resultInfo);
            listsLock.Exit();
        }

        private void AddError(ResultInfo resultInfo)
        {
            listsLock.Enter();
            errorInfos.Add(resultInfo);
            listsLock.Exit();
        }

        private void AddWarning(ResultInfo resultInfo)
        {
            listsLock.Enter();
            warningInfos.Add(resultInfo);
            listsLock.Exit();
        }

        public void AddSuccess() => AddSuccess(new ResultInfo());
        public void AddSuccess(string description) => AddSuccess(new ResultInfo(description));

        public void AddError(string description) => AddError(new ResultInfo(description));

        public void AddWarning(string description) => AddWarning(new ResultInfo(description));

        
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

        public IEnumerable<string> SuccessDecriptions
        {
            get
            {
                listsLock.Enter();
                var result = successInfos.Select(info => info.ResultDescription).Where(desc => desc != null).ToArray();
                listsLock.Exit();
                return result;
            }
        }

        public int ErrorCount => errorInfos.Count;

        public IEnumerable<string> ErrorDescriptions
        {
            get
            {
                listsLock.Enter();
                var result = errorInfos.Select(info => info.ResultDescription).Where(desc => desc != null).ToArray();
                listsLock.Exit();
                return result;
            }
        }

        public int WarningCount => warningInfos.Count;

        public IEnumerable<string> WarningDescriptions
        {
            get
            {
                listsLock.Enter();
                var result = warningInfos.Select(info => info.ResultDescription).Where(desc => desc != null).ToArray();
                listsLock.Exit();
                return result;
            }
        }

        public readonly struct ResultInfo
        {
            public ResultInfo(string resultDescription)
            {
                description = resultDescription;
            }

            private readonly string description;


            public readonly string ResultDescription => description;

        }
    }

}
