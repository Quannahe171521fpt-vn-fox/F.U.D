using FUD.DTOs.Pricing;
using FUD.Models;

namespace FUD.Repositories.Interfaces
{
    public interface IPricingRepository
    {
        Task<List<Term>> GetAllTermsAsync();
        Task<Term?> GetTermByIdAsync(int termId);
        Task<PriceList?> GetPriceListByTermIdAsync(int termId);
        Task<List<Dorm>> GetAllDormsAsync();
        Task<List<RoomType>> GetAllRoomTypesAsync();
        Task<PriceList> CreatePriceListAsync(PriceList priceList);
        Task UpdatePriceItemsAsync(int priceListId, List<PriceItem> items);
        Task SaveChangesAsync();
    }
}

