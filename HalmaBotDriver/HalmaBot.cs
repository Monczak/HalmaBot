using System.Diagnostics;
using System.Numerics;

namespace HalmaBot;

public class HalmaBot : IHalmaPlayer
{
    public event IHalmaPlayer.PickMoveDelegate? MovePicked;

    private readonly TranspositionTable transpositionTable = new();

    private const int MoveBufferLength = 1024;

    private bool useAlphaBetaPruning = false;
    private bool useMoveOrdering = false;
    private bool useTranspositionTable = false;
    private bool useDeepSearchInEndgame = false;

    private float sumOfDistancesWeight = -1f;
    private float piecesEnemyCanJumpOverWeight = -30f;
    private float piecesUnableToJumpWeight = -40f;
    private float piecesInOpposingCampWeight = 100f;
    private float piecesNotInPlayerCampWeight = 100f;
    
    private int nominalSearchDepth = 4;
    private readonly Move[][] moveBuffers;

    private string? logFilePath;

    public HalmaBot UseLogging(string logFilePath)
    {
        this.logFilePath = logFilePath;
        return this;
    }

    public HalmaBot UseAlphaBetaPruning()
    {
        useAlphaBetaPruning = true;
        return this;
    }
    
    public HalmaBot UseMoveOrdering()
    {
        useMoveOrdering = true;
        return this;
    }

    public HalmaBot UseTranspositionTable()
    {
        useTranspositionTable = true;
        return this;
    }

    public HalmaBot UseDeepSearchInEndgame()
    {
        useDeepSearchInEndgame = true;
        return this;
    }

    public HalmaBot WithSumOfDistancesWeight(float weight)
    {
        sumOfDistancesWeight = weight;
        return this;
    }
    
    public HalmaBot WithPiecesEnemyCanJumpOverWeight(float weight)
    {
        piecesEnemyCanJumpOverWeight = weight;
        return this;
    }
    
    public HalmaBot WithPiecesInOpposingCampWeight(float weight)
    {
        piecesInOpposingCampWeight = weight;
        return this;
    }
    
    public HalmaBot WithPiecesUnableToJumpWeight(float weight)
    {
        piecesUnableToJumpWeight = weight;
        return this;
    }
    
    public HalmaBot WithPiecesNotInPlayerCampWeight(float weight)
    {
        piecesNotInPlayerCampWeight = weight;
        return this;
    }

    public HalmaBot WithNominalSearchDepth(int depth)
    {
        nominalSearchDepth = depth;
        return this;
    }
    
    private static class Stats
    {
        public static int NodesVisited { get; set; }
        public static int TranspositionTableHits { get; set; }
    }
    
    private static class Heuristics
    {
        private static bool PieceBelongsTo(Board board, bool isPlayer1, Coord coord)
        {
            return isPlayer1 && (board[coord] & Piece.Player1) != 0 ||
                   !isPlayer1 && (board[coord] & Piece.Player2) != 0;
        }
        
        public static float SumOfDistances(Board board, bool isPlayer1)
        {
            var distSum = 0f;
            for (var y = 0; y < board.BoardSize; y++)
            {
                for (var x = 0; x < board.BoardSize; x++)
                {
                    var coord = new Coord(x, y);
                    if (PieceBelongsTo(board, isPlayer1, coord))
                    {
                        var target = isPlayer1 ? new Vector2(board.BoardSize - 1, board.BoardSize - 1) : new Vector2(0, 0);
                        distSum += (target - new Vector2(x, y)).LengthSquared();
                    }
                }    
            }

            return distSum;
        }

        public static float PiecesInOpposingCamp(Board board, bool isPlayer1)
        {
            var sum = 0;
            var color = isPlayer1 ? Piece.Player1 : Piece.Player2;
            
            for (var y = 0; y < board.BoardSize; y++)
            {
                for (var x = 0; x < board.BoardSize; x++)
                {
                    var coord = new Coord(x, y);
                    sum += board.IsInCamp(coord, !isPlayer1) && board[coord] == color ? 1 : 0;
                }
            }

            return sum;
        }

        public static float PiecesNotInPlayerCamp(Board board, bool isPlayer1)
        {
            var sum = 0;
            var color = isPlayer1 ? Piece.Player1 : Piece.Player2;
            
            for (var y = 0; y < board.BoardSize; y++)
            {
                for (var x = 0; x < board.BoardSize; x++)
                {
                    var coord = new Coord(x, y);
                    sum += !board.IsInCamp(coord, isPlayer1) && board[coord] == color ? 1 : 0;
                }
            }

            return sum;
        }

        public static float PiecesEnemyCanJumpOver(Board board, bool isPlayer1)
        {
            var pieceCount = 0;
            for (var y = 0; y < board.BoardSize; y++)
            {
                for (var x = 0; x < board.BoardSize; x++)
                {
                    var coord = new Coord(x, y);
                    if (!PieceBelongsTo(board, isPlayer1, coord)) continue;
                    
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        for (var dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) 
                                continue;
                            if (x + dx < 0 || x + dx >= board.BoardSize || y + dy < 0 || y + dy >= board.BoardSize
                                || x - dx < 0 || x - dx >= board.BoardSize || y - dy < 0 || y - dy >= board.BoardSize)
                                continue;
                            
                            var delta = new Coord(dx, dy);
                            if (PieceBelongsTo(board, !isPlayer1, coord + delta) && board[coord - delta] == Piece.None)
                                pieceCount++;
                        }
                    }
                }
            }

            return pieceCount;
        }

        public static float PiecesUnableToJump(Board board, bool isPlayer1)
        {
            if (PiecesInOpposingCamp(board, isPlayer1) > 15)
                return 0;
            
            var pieceCount = 0;
            for (var y = 0; y < board.BoardSize; y++)
            {
                for (var x = 0; x < board.BoardSize; x++)
                {
                    var coord = new Coord(x, y);
                    if (!PieceBelongsTo(board, isPlayer1, coord)) continue;
                    if (board.IsInCamp(coord, !isPlayer1)) continue;

                    var canJump = false;
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        for (var dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) 
                                continue;
                            if (x + dx < 0 || x + dx >= board.BoardSize || y + dy < 0 || y + dy >= board.BoardSize
                                || x - dx < 0 || x - dx >= board.BoardSize || y - dy < 0 || y - dy >= board.BoardSize)
                                continue;
                            if (x + dx * 2 < 0 || x + dx * 2 >= board.BoardSize || y + dy * 2 < 0 || y + dy * 2 >= board.BoardSize
                                || x - dx * 2 < 0 || x - dx * 2 >= board.BoardSize || y - dy * 2 < 0 || y - dy * 2 >= board.BoardSize)
                                continue;
                            
                            var delta = new Coord(dx, dy);
                            if ((board[coord + delta] & Piece.Occupied) != 0 &&
                                board[coord + delta + delta] == Piece.None)
                            {
                                canJump = true;
                                break;
                            }
                        }

                        if (canJump) break;
                    }

                    if (!canJump) pieceCount++;
                }
            }

            return pieceCount;
        }
    }

    public HalmaBot()
    {
        moveBuffers = new Move[nominalSearchDepth + 6][];
        for (var i = 0; i < moveBuffers.Length; i++)
            moveBuffers[i] = new Move[MoveBufferLength];
    }
    
    // TODO: Maybe use a genetic algorithm to tune the bot - determine the best multipliers for heuristics?
    private float EvaluateFromPlayer(Board board, bool isPlayer1)
    {
        return
            Heuristics.SumOfDistances(board, isPlayer1) * sumOfDistancesWeight
            + Heuristics.PiecesEnemyCanJumpOver(board, isPlayer1) * piecesEnemyCanJumpOverWeight
            + Heuristics.PiecesUnableToJump(board, isPlayer1) * piecesUnableToJumpWeight
            + Heuristics.PiecesInOpposingCamp(board, isPlayer1) * piecesInOpposingCampWeight
            + Heuristics.PiecesNotInPlayerCamp(board, isPlayer1) * piecesNotInPlayerCampWeight;
    }

    private float Evaluate(Board board, bool isPlayer1)
    {
        var eval = EvaluateFromPlayer(board, isPlayer1) - EvaluateFromPlayer(board, !isPlayer1);
        return eval;
    }
    
    private (float eval, Move? bestMove) Search(Board board, bool isPlayer1, int depth, float alpha = float.NegativeInfinity, float beta = float.PositiveInfinity)
    {
        var alphaOrig = alpha;
        Move? bestMove = null;
        const float immediateWinScore = 1000000;
        
        if (depth == 0)
            return (Evaluate(board, isPlayer1), null);

        if (useTranspositionTable && transpositionTable.Query(board, depth, alpha, beta, out var ttEval) == TranspositionTable.QueryResult.Success)
        {
            Stats.TranspositionTableHits++;
            return (ttEval.eval, ttEval.move);
        }

        Span<Move> moves = moveBuffers[depth];
        MoveGenerator.GenerateMoves(board, isPlayer1, ref moves);
        if (moves.Length == 0)
        {
            if ((board.GameState & GameState.Ended) != 0)
                return (immediateWinScore + depth, null);
            return (float.NegativeInfinity, null);
        }
        
        if (useMoveOrdering)
            OrderMoves(board, isPlayer1, ref moves);

        var maxEval = float.NegativeInfinity;
        foreach (var move in moves)
        {
            board.MakeMove(move);
            var eval = -Search(board, !isPlayer1, depth - 1, -beta, -alpha).eval;
            board.UnmakeMove(move);
            Stats.NodesVisited++;
            
            if (eval > maxEval)
            {
                maxEval = eval;
                bestMove = move;
            }

            if (useAlphaBetaPruning)
            {
                beta = Math.Min(beta, eval); // Seems to prune way faster than alpha = Math.Max(alpha, eval)
                                             // and still yields the same results
                if (alpha >= beta)
                    break;
            }
        }

        if (useTranspositionTable)
        {
            TranspositionTable.NodeType nodeType;
            if (maxEval <= alphaOrig)
                nodeType = TranspositionTable.NodeType.LowerBound;
            else if (maxEval >= beta)
                nodeType = TranspositionTable.NodeType.UpperBound;
            else
                nodeType = TranspositionTable.NodeType.Exact;
            transpositionTable.RecordState(board, maxEval, depth, nodeType, bestMove);
        }
        
        return (maxEval, bestMove);
    }
    
    private void OrderMoves(Board board, bool isPlayer1, ref Span<Move> moveList)
    {
        Span<float> scores = new float[moveList.Length];
        var target = isPlayer1 
            ? new Vector2(board.BoardSize - 1, board.BoardSize - 1) 
            : new Vector2(0, 0);

        for (var i = 0; i < moveList.Length; i++)
        {
            var move = moveList[i];
            var finalSquare = move.FinalSquare;
            scores[i] = -(target - new Vector2(finalSquare.X, finalSquare.Y)).LengthSquared();
        }

        Quicksort(ref moveList, ref scores, 0, moveList.Length - 1);
    }

    private static void Quicksort(ref Span<Move> values, ref Span<float> scores, int low, int high)
    {
        while (true)
        {
            if (low < high)
            {
                var pivotIndex = Partition(ref values, ref scores, low, high);
                Quicksort(ref values, ref scores, low, pivotIndex - 1);
                low = pivotIndex + 1;
                continue;
            }

            break;
        }
    }

    private static int Partition(ref Span<Move> values, ref Span<float> scores, int low, int high)
    {
        var pivotScore = scores[high];
        var i = low - 1;

        for (var j = low; j <= high - 1; j++)
        {
            if (scores[j] > pivotScore)
            {
                i++;
                (values[i], values[j]) = (values[j], values[i]);
                (scores[i], scores[j]) = (scores[j], scores[i]);
            }
        }
        (values[i + 1], values[high]) = (values[high], values[i + 1]);
        (scores[i + 1], scores[high]) = (scores[high], scores[i + 1]);

        return i + 1;
    }
    
    public void OnPlayerTurn(int turn, bool isPlayer1, Board board)
    {
        if (logFilePath is not null && turn is 0 or 1)
        {
            if (File.Exists(logFilePath))
                File.Delete(logFilePath);
            
            using var file = File.AppendText(logFilePath);
            file.WriteLine($"Turn;ElapsedSeconds;NodesPerSecond;TTSize;TTHits;Eval;BestMoveEval;BestMove;NodesVisited");
        }
        
        transpositionTable.Clear();
        
        Stats.NodesVisited = 0;
        Stats.TranspositionTableHits = 0;

        var piecesInOpposingCamp = Heuristics.PiecesInOpposingCamp(board, isPlayer1);
        var depth = useDeepSearchInEndgame ? piecesInOpposingCamp switch
        {
            14 or 15 => nominalSearchDepth + 2,
            16 => nominalSearchDepth + 3,
            17 => nominalSearchDepth + 4,
            18 => nominalSearchDepth + 5,
            _ => nominalSearchDepth
        } : nominalSearchDepth;
        
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var eval = Evaluate(board, isPlayer1);
        Console.WriteLine($"Searching at {depth} ply | Eval: {eval}");
        var (bestMoveEval, bestMove) = Search(board, isPlayer1, depth);
        
        var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
        
        Console.WriteLine($"Search took {elapsedSeconds:F3} s ({Stats.NodesVisited / elapsedSeconds:F1} nodes/s)");
        Console.WriteLine($"Transposition table entry count: {transpositionTable.Size} | Hits: {Stats.TranspositionTableHits} ({(double)Stats.TranspositionTableHits / Stats.NodesVisited:P} of all nodes)");
        Console.WriteLine($"Best move ({bestMoveEval}): {bestMove} | Nodes visited: {Stats.NodesVisited}");

        if (logFilePath is not null)
        {
            using var file = File.AppendText(logFilePath);
            file.WriteLine($"{turn / 2};{elapsedSeconds:F3};{Stats.NodesVisited / elapsedSeconds:F1};{transpositionTable.Size};{Stats.TranspositionTableHits};{eval};{bestMoveEval};{bestMove};{Stats.NodesVisited}");
        }
        
        MovePicked?.Invoke(bestMove);
    }
}