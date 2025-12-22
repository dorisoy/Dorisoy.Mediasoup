using System.Threading.Tasks;

namespace Dorisoy.Meeting.Server
{
    public interface IHubClient
    {
        Task Notify(MeetingNotification notification);
    }
}
