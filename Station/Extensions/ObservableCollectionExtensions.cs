using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Station.Extensions;

public static class ObservableCollectionExtensions
{
    public static void Reset<T>(this ObservableCollection<T> collection, IEnumerable<T> newItems)
    {
        collection.Clear();
        foreach (var item in newItems)
        {
            collection.Add(item);
        }
    }
}
