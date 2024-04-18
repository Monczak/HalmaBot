namespace HalmaBot;

public interface IHalmaPlayer
{
    delegate void PickMoveDelegate(Move? move);
    event PickMoveDelegate MovePicked;

    void OnPlayerTurn(int turn, bool isPlayer1, Board board);
}