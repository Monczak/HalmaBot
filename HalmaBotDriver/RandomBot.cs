namespace HalmaBot;

public class RandomBot : IHalmaPlayer
{
    public event IHalmaPlayer.PickMoveDelegate? MovePicked;
    
    public void OnPlayerTurn(int turn, bool isPlayer1, Board board)
    {
        Span<Move> moves = new Move[1024];
        MoveGenerator.GenerateMoves(board, isPlayer1, ref moves);

        if (moves.Length == 0)
            MovePicked?.Invoke(null);
        else
            MovePicked?.Invoke(moves[new Random().Next(moves.Length)]);
    }
}