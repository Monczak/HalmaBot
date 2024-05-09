using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace HalmaBot;

public class HalmaGame
{
    private Random random;
    
    private readonly IHalmaPlayer player1;
    private readonly IHalmaPlayer player2;
    private bool player1Turn;

    private int turnCounter;
    
    public Board Board { get; } = new();
    private GameState gameState;

    public readonly record struct BoardUpdatedEventArgs(int Turn, Board Board, Move? PickedMove);
    
    public event EventHandler<BoardUpdatedEventArgs>? BoardUpdated;
    public event EventHandler<GameState>? GameStateUpdated;
    
    private readonly ConcurrentQueue<Task> taskQueue = new();
    
    public HalmaGame(IHalmaPlayer player1, IHalmaPlayer player2, Random? random = null, string? gameStr = null)
    {
        this.random = random ?? new Random();
        this.player1 = player1;
        this.player2 = player2;
        Board.LoadFromString(gameStr ?? "PPPPP11/PPPPP11/PPPP12/PPP13/PP14/16/16/16/16/16/16/14pp/13ppp/12pppp/11ppppp/11ppppp");

        player1.MovePicked += async move =>
        {
            if (player1Turn)
            {
                await EnqueueTask(() =>
                {
                    var continueGame = HandleMove(move);
                    BoardUpdated?.Invoke(this, new BoardUpdatedEventArgs(turnCounter, Board, move));
                    if (continueGame)
                    {
                        player1Turn = false;
                        player2.OnPlayerTurn(++turnCounter, player1Turn, Board);
                    }
                    else
                    {
                        GameStateUpdated?.Invoke(this, gameState);
                    }
                });
            }
        };

        player2.MovePicked += async move =>
        {
            if (!player1Turn)
            {
                await EnqueueTask(() =>
                {
                    var continueGame = HandleMove(move);
                    BoardUpdated?.Invoke(this, new BoardUpdatedEventArgs(turnCounter, Board, move));
                    if (continueGame)
                    {
                        player1Turn = true;
                        player1.OnPlayerTurn(++turnCounter, player1Turn, Board);
                    }
                    else
                    {
                        GameStateUpdated?.Invoke(this, gameState);
                    }
                });
            }
        };
    }

    public async Task StartGame()
    {
        gameState = GameState.InProgress;
        player1Turn = random.Next() % 2 == 0;
        // player1Turn = true;
        
        Board.InitZobrist(player1Turn);
        
        turnCounter = 0;
        if (player1Turn)
            player1.OnPlayerTurn(turnCounter, true, Board);
        else
            player2.OnPlayerTurn(turnCounter, false, Board);

        await Task.Run(ProcessTaskQueue);
    }

    private async Task EnqueueTask(Action action)
    {
        taskQueue.Enqueue(Task.Run(action));
        await Task.Yield();
    }

    private async Task ProcessTaskQueue()
    {
        while (gameState == GameState.InProgress)
        {
            if (taskQueue.TryDequeue(out var task))
            {
                await task;
            }
            else
            {
                await Task.Delay(100);
            }
        }
    }

    private bool HandleMove(Move? move)
    {
        if (move is not null)
        {
            var result = Board.MakeMove(move.Value);
            if (!result)
                throw new Exception($"Illegal move made: {move.Value}");

            var newState = Board.GameState;
            if (newState != gameState)
            {
                gameState = newState;
                return false;
            }
        }

        return true;
    }
}
