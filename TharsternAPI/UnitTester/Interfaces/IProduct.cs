using System.Threading.Tasks;

namespace nsTharsternAPI.Interfaces
{
    public interface IProduct
    {
        Task RetrieveProductAsync(string productCode, int quantity);
    }
}