using FUD.Models;
using FUD.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FUD.Repositories
{
    public class PricingRepository : IPricingRepository
    {
        private readonly FudContext _context;

        public PricingRepository(FudContext context)
        {
            _context = context;
        }

        public async Task<List<Term>> GetAllTermsAsync()
        {
            return await _context.Terms
                .Include(t => t.RegistrationPeriods)
                .OrderByDescending(t => t.StartDate)
                .ToListAsync();
        }

        public async Task<Term?> GetTermByIdAsync(int termId)
        {
            return await _context.Terms
                .Include(t => t.RegistrationPeriods)
                .FirstOrDefaultAsync(t => t.Id == termId);
        }

        public async Task<PriceList?> GetPriceListByTermIdAsync(int termId)
        {
            return await _context.PriceLists
                .Include(pl => pl.PriceItems)
                    .ThenInclude(pi => pi.Dorm)
                .Include(pl => pl.PriceItems)
                    .ThenInclude(pi => pi.RoomType)
                .FirstOrDefaultAsync(pl => pl.TermId == termId);
        }

        public async Task<List<Dorm>> GetAllDormsAsync()
        {
            return await _context.Dorms
                .Where(d => d.Status == "Active")
                .OrderBy(d => d.Name)
                .ToListAsync();
        }

        public async Task<List<RoomType>> GetAllRoomTypesAsync()
        {
            return await _context.RoomTypes
                .OrderBy(rt => rt.BedCapacity)
                .ToListAsync();
        }

        public async Task<PriceList> CreatePriceListAsync(PriceList priceList)
        {
            _context.PriceLists.Add(priceList);
            await _context.SaveChangesAsync();
            return priceList;
        }

        public async Task UpdatePriceItemsAsync(int priceListId, List<PriceItem> items)
        {
            var existingItems = await _context.PriceItems
                .Where(pi => pi.PriceListId == priceListId)
                .ToListAsync();

            _context.PriceItems.RemoveRange(existingItems);
            _context.PriceItems.AddRange(items);
            await _context.SaveChangesAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}

