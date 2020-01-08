using System.Threading.Tasks;

namespace nsTharsternAPI.Interfaces
{
    public interface IAuthentication
    {
        Task<string> SetApiTokenAsyncForWeb(string email, string password);
        Task SetApiTokenAsync(string email, string password);
    }
}