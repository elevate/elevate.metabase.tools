using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace metabase_exporter
{
    public static class GeneralExtensions
    {
        /// <summary>
        /// Map each element of a structure to a Task, evaluate these tasks from left to right, and collect the results.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="source"></param>
        /// <param name="f"></param>
        /// <returns></returns>
        public static Task<IReadOnlyList<TResult>> Traverse<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, Task<TResult>> f)
        {
            return source.Select(f).Sequence();
        }

        /// <summary>
        /// Evaluate each Task from left to right and collect the results.
        /// https://msdn.microsoft.com/en-us/library/hh194766.aspx
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="tasks"></param>
        /// <returns></returns>
        public static async Task<IReadOnlyList<TResult>> Sequence<TResult>(this IEnumerable<Task<TResult>> tasks)
        {
            var results = new List<TResult>();
            foreach (var t in tasks)
            {
                results.Add(await t);
            }
            return results;
        }

        public static string MD5Base64(string x)
        {
            var md5Value = System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(x));
            return Convert.ToBase64String(md5Value);
        }

        public static TValue GetOrThrow<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dict, TKey key, string error)
        {
            if (dict.TryGetValue(key, out var value))
            {
                return value;
            }
            throw new KeyNotFoundException($"Key not found: {key}\n{error}");
        }

    }
}
