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
            // SỬA LỖI KHÓA NGOẠI (Foreign Key):
            // Bảng Price_Lists có ràng buộc CreatedById INT NOT NULL FOREIGN KEY REFERENCES Managers(UserId).
            // Nếu người thao tác là tài khoản Admin (chỉ có trong bảng Admins, không nằm trong Managers),
            // việc insert trực tiếp CreatedById của Admin sẽ gây lỗi vi phạm FK.
            // Giải pháp: Kiểm tra xem CreatedById có trong Managers không, nếu không thì fallback sang Manager hợp lệ đầu tiên.
            var managerExists = await _context.Managers.AnyAsync(m => m.UserId == priceList.CreatedById);
            if (!managerExists)
            {
                var defaultManager = await _context.Managers.Select(m => m.UserId).FirstOrDefaultAsync();
                if (defaultManager > 0)
                {
                    priceList.CreatedById = defaultManager;
                }
            }

            _context.PriceLists.Add(priceList);
            await _context.SaveChangesAsync();
            return priceList;
        }

        public async Task UpdatePriceItemsAsync(int priceListId, List<PriceItem> items)
        {
            var existingItems = await _context.PriceItems
                .Where(pi => pi.PriceListId == priceListId)
                .ToListAsync();

            // SỬA LỖI DUPLICATE KEY CONSTRAINT (UQ_PriceItem):
            // Trước đây dùng RemoveRange(existingItems) rồi AddRange(items).
            // Do cơ chế Change Tracker của EF Core thực thi các lệnh INSERT trước DELETE trong cùng SaveChangesAsync,
            // việc thêm mới các bản ghi có cùng (PriceListId, DormId, RoomTypeId) sẽ kích hoạt lỗi trùng lặp
            // CONSTRAINT UQ_PriceItem UNIQUE (PriceListId, DormId, RoomTypeId) trong SQL Server.
            // Giải pháp: Cập nhật in-place đơn giá của các bản ghi đã tồn tại, chỉ thêm mới nếu chưa có
            // và chỉ xóa những bản ghi không còn trong danh sách.
            foreach (var item in items)
            {
                var existing = existingItems.FirstOrDefault(pi =>
                    pi.DormId == item.DormId && pi.RoomTypeId == item.RoomTypeId);

                if (existing != null)
                {
                    existing.UnitPrice = item.UnitPrice;
                    _context.PriceItems.Update(existing);
                }
                else
                {
                    item.PriceListId = priceListId;
                    _context.PriceItems.Add(item);
                }
            }

            // Xóa các item cũ không còn trong danh sách cập nhật mới
            var incomingKeys = items.Select(i => (i.DormId, i.RoomTypeId)).ToHashSet();
            var toRemove = existingItems.Where(e => !incomingKeys.Contains((e.DormId, e.RoomTypeId))).ToList();
            if (toRemove.Any())
            {
                _context.PriceItems.RemoveRange(toRemove);
            }

            await _context.SaveChangesAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
