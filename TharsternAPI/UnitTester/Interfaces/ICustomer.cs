using System.Threading.Tasks;

namespace nsTharsternAPI.Interfaces
{
    public interface ICustomer
    {
        Task<string> RetrieveCustomerJsonAsyncForWeb(string customerCode);
        Task RetrieveCustomerAsync(string customerCode);
    }
}