// See https://aka.ms/new-console-template for more information

using HalmaBot;

internal class Program
{
    private static Random random = new(0);
    
    private class TestPlayer : IHalmaPlayer
    {
        public event IHalmaPlayer.PickMoveDelegate? MovePicked;
        
        public void OnPlayerTurn(int turn, bool isPlayer1, Board board)
        {
            Console.WriteLine($"Turn {turn}");
            
            Move[] moves = [..MoveGenerator.GenerateMoves(board, isPlayer1)];
            foreach (var move in moves)
                Console.WriteLine(move);
            
            if (moves.Length == 0)
                MovePicked?.Invoke(null);
            else
            {
                var picked = moves[random.Next(moves.Length)];
                Console.WriteLine($"Picked {picked}");
                MovePicked?.Invoke(picked);
            }
        }
    }
    
    public static void Main(string[] args)
    {
        var player1 = new TestPlayer();
        var player2 = new TestPlayer();
        
        var game = new HalmaGame(player1, player2, random: random);
        Console.WriteLine(game.Board.Print());
        game.BoardUpdated += (sender, args) =>
        {
            Console.WriteLine(args.Board.Print());
            Console.ReadKey();
        };
        
        game.StartGame().GetAwaiter().GetResult();
    }
}