using System.Collections.Generic;
using Unity.Collections;

internal static class ReleasedBlockInteractionQueue
{
    private static readonly List<ReleasedBlockInteractionRequest> Requests = new(8);

    public static int Count => Requests.Count;

    public static void Enqueue(in ReleasedBlockInteractionRequest request)
    {
        Requests.Add(request);
    }

    public static void CopyToAndClear(NativeArray<ReleasedBlockInteractionRequest> destination)
    {
        for (int i = 0; i < Requests.Count; i++)
            destination[i] = Requests[i];
        Requests.Clear();
    }
}
