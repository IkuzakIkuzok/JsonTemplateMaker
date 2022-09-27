
// (c) 2022 Kazuki KOHZUKI

namespace JsonTemplateMaker
{
    internal static class IterHelper
    {
        internal static IEnumerable<(TKey, TValue)> Items<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
        {
            foreach (var key in dictionary.Keys)
                yield return (key, dictionary[key]);
        } // internal static IEnumerable<(TKey, TValue)> Items<TKey, TValue> (this IDictionary<TKey, TValue>)
    } // internal static class IterHelper
} // namespace JsonTemplateMaker
