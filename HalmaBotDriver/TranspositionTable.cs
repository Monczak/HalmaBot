namespace HalmaBot;

public class TranspositionTable
{
    private struct Entry
    {
        public float Eval { get; init; }
        public int Depth { get; init; }
        public NodeType NodeType { get; init; }
        public Move? Move { get; init; }
    }

    public enum QueryResult
    {
        Success,
        Failed
    }

    public enum NodeType
    {
        Exact,
        UpperBound,
        LowerBound
    }

    private readonly Dictionary<ulong, Entry> entries = new();
    public int Size => entries.Count;
    
    public void Clear()
    {
        entries.Clear();
    }

    public QueryResult Query(Board board, int depth, float alpha, float beta, out (Move? move, float eval) result)
    {
        result.eval = 0;
        result.move = null;
        if (!entries.TryGetValue(board.ZobristKey, out var entry))
        {
            return QueryResult.Failed;
        }

        if (entry.Depth >= depth)
        {
            if (entry.NodeType == NodeType.Exact)
            {
                result.eval = entry.Eval;
                result.move = entry.Move;
                return QueryResult.Success;
            }

            if (entry.NodeType == NodeType.UpperBound && entry.Eval <= alpha)
            {
                result.eval = entry.Eval;
                result.move = entry.Move;
                return QueryResult.Success;
            }
            
            if (entry.NodeType == NodeType.LowerBound && entry.Eval >= beta)
            {
                result.eval = entry.Eval;
                result.move = entry.Move;
                return QueryResult.Success;
            }
        }
        
        return QueryResult.Failed;
    }

    public void RecordState(Board board, float eval, int depth, NodeType evalType, Move? move)
    {
        entries[board.ZobristKey] = new Entry
        {
            Eval = eval,
            Depth = depth,
            NodeType = evalType,
            Move = move
        };
    }
}