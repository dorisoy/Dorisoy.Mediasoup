namespace Dorisoy.Libuv
{
    public interface IBindable<TType, TEndPoint>
    {
        void Bind(TEndPoint endPoint);
    }
}
