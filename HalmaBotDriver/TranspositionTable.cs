namespace HalmaBot;

public class TranspositionTable
{
    public struct Entry
    {
        public ulong ZobristKey { get; init; }
        public float Eval { get; init; }
    }

    private readonly Dictionary<ulong, Entry> entries = new();
    public int Size => entries.Count;
    
    public void Clear()
    {
        entries.Clear();
    }

    public bool ContainsKey(ulong zobristKey)
    {
        return entries.ContainsKey(zobristKey);
    }

    public Entry Query(ulong zobristKey)
    {
        return entries[zobristKey];
    }

    public void RecordState(Board board, float eval)
    {
        entries[board.ZobristKey] = new Entry { ZobristKey = board.ZobristKey, Eval = eval };
    }

    public void RecordStateForceHash(ulong hash, float eval)
    {
        entries[hash] = new Entry { ZobristKey = hash, Eval = eval };
    }
}