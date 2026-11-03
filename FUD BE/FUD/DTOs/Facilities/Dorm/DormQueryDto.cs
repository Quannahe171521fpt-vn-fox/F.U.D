namespace FUD.DTOs.Facilities.Dorm
{
    /// <summary>
    /// Query params cho GET /api/dorms (search + paging)
    /// </summary>
    public class DormQueryDto
    {
        public string? CampusName { get; set; }
        public string? DormName { get; set; }
        public string? Status { get; set; }

        private int _page = 1;
        public int Page
        {
            get => _page;
            set => _page = value < 1 ? 1 : value;
        }

        private int _pageSize = 10;
        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = value < 1 ? 10 : (value > 100 ? 100 : value);
        }
    }
}
