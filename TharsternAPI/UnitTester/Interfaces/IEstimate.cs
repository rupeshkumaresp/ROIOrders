using System.Threading.Tasks;

namespace nsTharsternAPI.Interfaces
{
    public interface IEstimate
    {
        Task RetrieveEstRequestSampleAsync(string productCode, int quantity);
        Task CreatePreDeliveryCustomerAsync();
        Task CreateEstimateAsync();
        Task CreatePreDeliveryAsync();
        Task CreateDeliveryAsync();
        Task CreateCustomerAddressAsync();
        Task CreateJobFromEstimateAsync();
    }
}