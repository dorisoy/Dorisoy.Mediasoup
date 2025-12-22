namespace Dorisoy.Libuv
{
    public interface ITrySend<TMessage>
    {
        int TrySend(TMessage message);
    }
}
