using System.Net;
using System.Net.Http.Json;
using FUD.Common;

namespace FUD.Tests.Integration
{
    /// <summary>
    /// Integration test cho API /api/admin/users.
    /// Mỗi test tạo user riêng để tránh ảnh hưởng lẫn nhau
    /// (suspend/restore làm thay đổi trạng thái và token version).
    /// </summary>
    public class AdminUsersApiTests : IClassFixture<AdminApiFactory>
    {
        private readonly AdminApiFactory _factory;
        private int _seq;

        public AdminUsersApiTests(AdminApiFactory factory)
        {
            _factory = factory;
        }

        private HttpClient AdminClient() => _factory.CreateClientWithRoles(_factory.AdminId, AccountConstants.RoleAdmin);

        private string NextTag() => $"t{Interlocked.Increment(ref _seq)}";

        // ── Create Account ────────────────────────────────────────

        [Fact]
        public async Task Create_WithValidData_Returns201_AndUserIsActiveWithRole()
        {
            var client = AdminClient();
            var tag = NextTag();

            var response = await client.PostAsJsonAsync("/api/admin/users", new
            {
                username = $"newstudent{tag}",
                fullName = "Lê Văn C",
                email = $"newstudent{tag}@fud.local",
                password = "Pass1234",
                roleName = AccountConstants.RoleStudent
            });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var body = await AdminApiFactory.ReadJsonAsync(response);
            Assert.True(body.GetProperty("success").GetBoolean());
            Assert.Equal($"newstudent{tag}", body.GetProperty("data").GetProperty("username").GetString());
            Assert.Equal(AccountConstants.StatusActive, body.GetProperty("data").GetProperty("status").GetString());
            Assert.Contains(AccountConstants.RoleStudent,
                body.GetProperty("data").GetProperty("roles").EnumerateArray().Select(x => x.GetString()));
        }

        [Fact]
        public async Task Create_WithMissingFields_Returns400()
        {
            var client = AdminClient();

            var response = await client.PostAsJsonAsync("/api/admin/users", new { username = $"nopass{NextTag()}" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Create_WithWeakPassword_Returns400()
        {
            var client = AdminClient();
            var tag = NextTag();

            var response = await client.PostAsJsonAsync("/api/admin/users", new
            {
                username = $"weakpass{tag}",
                fullName = "Yếu",
                email = $"weakpass{tag}@fud.local",
                password = "abc",
                roleName = AccountConstants.RoleStudent
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Create_WithInvalidEmail_Returns400()
        {
            var client = AdminClient();
            var tag = NextTag();

            var response = await client.PostAsJsonAsync("/api/admin/users", new
            {
                username = $"bademail{tag}",
                fullName = "Không đúng",
                email = "not-an-email",
                password = "Pass1234",
                roleName = AccountConstants.RoleStudent
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Create_WithDuplicateUsername_Returns409()
        {
            var client = AdminClient();
            var tag = NextTag();

            // Tạo một user trước để username bị trùng
            _factory.SeedUser($"dup{tag}", $"dup{tag}@fud.local", "Trùng", AccountConstants.RoleStudent);

            var response = await client.PostAsJsonAsync("/api/admin/users", new
            {
                username = $"dup{tag}",
                fullName = "Trùng",
                email = $"dup2{tag}@fud.local",
                password = "Pass1234",
                roleName = AccountConstants.RoleStudent
            });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task Create_ByNonAdmin_Returns403()
        {
            var studentId = _factory.SeedUser($"nonadmin{NextTag()}", $"nonadmin{NextTag()}@fud.local", "Không quyền", AccountConstants.RoleStudent);
            var client = _factory.CreateClientWithRoles(studentId, AccountConstants.RoleStudent);

            var response = await client.PostAsJsonAsync("/api/admin/users", new
            {
                username = $"sneaky{NextTag()}",
                fullName = "Không có quyền",
                email = $"sneaky{NextTag()}@fud.local",
                password = "Pass1234",
                roleName = AccountConstants.RoleAdmin
            });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Create_WithoutToken_Returns401()
        {
            var client = _factory.CreateClient();

            var response = await client.PostAsJsonAsync("/api/admin/users", new
            {
                username = $"notoken{NextTag()}",
                fullName = "Không token",
                email = $"notoken{NextTag()}@fud.local",
                password = "Pass1234",
                roleName = AccountConstants.RoleStudent
            });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // ── Danh sách & chi tiết ──────────────────────────────────

        [Fact]
        public async Task GetAll_ReturnsPagedResult()
        {
            var client = AdminClient();

            var response = await client.GetAsync("/api/admin/users?page=1&pageSize=10");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await AdminApiFactory.ReadJsonAsync(response);
            var data = body.GetProperty("data");
            Assert.True(data.GetProperty("totalCount").GetInt32() >= 3);
            Assert.True(data.GetProperty("items").GetArrayLength() >= 1);
        }

        [Fact]
        public async Task GetAll_FilterByStatus_ReturnsOnlyMatching()
        {
            var client = AdminClient();

            var response = await client.GetAsync("/api/admin/users?status=Active");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await AdminApiFactory.ReadJsonAsync(response);
            var items = body.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
            Assert.All(items, i => Assert.Equal(AccountConstants.StatusActive, i.GetProperty("status").GetString()));
        }

        [Fact]
        public async Task GetById_NotFound_Returns404()
        {
            var client = AdminClient();

            var response = await client.GetAsync("/api/admin/users/999999");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // ── Assign Roles ──────────────────────────────────────────

        [Fact]
        public async Task AssignRoles_GrantsRole_AndWritesHistory()
        {
            var client = AdminClient();
            var tag = NextTag();
            var userId = _factory.SeedUser($"grant{tag}", $"grant{tag}@fud.local", "Cấp quyền", AccountConstants.RoleStudent);

            var response = await client.PutAsJsonAsync($"/api/admin/users/{userId}/roles",
                new { roleNames = new[] { AccountConstants.RoleStudent, AccountConstants.RoleGuard } });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await AdminApiFactory.ReadJsonAsync(response);
            var roles = body.GetProperty("data").GetProperty("roles").EnumerateArray().Select(x => x.GetString()).ToList();
            Assert.Contains(AccountConstants.RoleGuard, roles);
            Assert.Contains(AccountConstants.RoleStudent, roles);

            var histories = body.GetProperty("data").GetProperty("roleHistories").EnumerateArray().ToList();
            Assert.Contains(histories, h => h.GetProperty("action").GetString() == AccountConstants.RoleActionGrant);
        }

        [Fact]
        public async Task AssignRoles_SelfGrantAdmin_Returns403()
        {
            // Tài khoản KHÔNG có role Admin trong DB, nhưng token đang mang claim Admin
            // (ví dụ token được cấp từ lúc còn là Admin rồi bị hạ quyền).
            // Khi tự cấp thêm role Admin cho chính mình, backend phải từ chối.
            var tag = NextTag();
            var selfId = _factory.SeedUser($"selfadmin{tag}", $"selfadmin{tag}@fud.local", "Tự nâng quyền", AccountConstants.RoleManager);
            var client = _factory.CreateClientWithRoles(selfId, AccountConstants.RoleAdmin);

            var response = await client.PutAsJsonAsync($"/api/admin/users/{selfId}/roles",
                new { roleNames = new[] { AccountConstants.RoleManager, AccountConstants.RoleAdmin } });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task AssignRoles_SelfRevokeAdmin_Returns403()
        {
            // Admin cuối cùng tự thu hồi quyền Admin của chính mình → 403
            // (quy tắc "tự thu hồi" được kiểm tra trước quy tắc "Admin cuối cùng").
            _factory.KeepOnlyAdmin(_factory.AdminId);

            var client = AdminClient();
            var response = await client.PutAsJsonAsync($"/api/admin/users/{_factory.AdminId}/roles",
                new { roleNames = new[] { AccountConstants.RoleManager } });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task AssignRoles_RevokeAdminOfLastAdmin_Returns409()
        {
            // Gỡ role Admin của mọi tài khoản khác → chỉ còn Admin đầu tiên là Admin cuối cùng.
            _factory.KeepOnlyAdmin(_factory.AdminId);

            var client = AdminClient();
            var response = await client.PutAsJsonAsync($"/api/admin/users/{_factory.AdminId}/roles",
                new { roleNames = new[] { AccountConstants.RoleManager } });

            // Admin tự thu hồi quyền của chính mình → 403
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task AssignRoles_RevokeAdminOfLastAdminByOtherAdmin_Returns409()
        {
            // Admin thứ hai (có role Admin thật trong DB) thao tác lên Admin đầu tiên.
            var tag = NextTag();
            var otherAdminId = _factory.SeedUser($"admin2{tag}", $"admin2{tag}@fud.local", "Admin thứ hai", AccountConstants.RoleAdmin);

            // Gỡ role Admin của Admin thứ hai (chỉ để Admin đầu tiên là Admin cuối cùng),
            // nhưng token của Admin thứ hai vẫn còn claim Admin.
            _factory.KeepOnlyAdmin(_factory.AdminId);

            var client = _factory.CreateClientWithRoles(otherAdminId, AccountConstants.RoleAdmin);
            var response = await client.PutAsJsonAsync($"/api/admin/users/{_factory.AdminId}/roles",
                new { roleNames = new[] { AccountConstants.RoleManager } });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task AssignRoles_RevokeRole_WritesRevokeHistory()
        {
            var client = AdminClient();
            var tag = NextTag();
            var userId = _factory.SeedUser($"revoke{tag}", $"revoke{tag}@fud.local", "Thu hồi",
                AccountConstants.RoleStudent, AccountConstants.RoleGuard);

            var response = await client.PutAsJsonAsync($"/api/admin/users/{userId}/roles",
                new { roleNames = new[] { AccountConstants.RoleStudent } });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await AdminApiFactory.ReadJsonAsync(response);
            var roles = body.GetProperty("data").GetProperty("roles").EnumerateArray().Select(x => x.GetString()).ToList();
            Assert.DoesNotContain(AccountConstants.RoleGuard, roles);

            var histories = body.GetProperty("data").GetProperty("roleHistories").EnumerateArray().ToList();
            Assert.Contains(histories, h => h.GetProperty("action").GetString() == AccountConstants.RoleActionRevoke);
        }

        // ── Suspend ───────────────────────────────────────────────

        [Fact]
        public async Task Suspend_SetsSuspended_WritesHistory_AndKeepsUserRow()
        {
            var client = AdminClient();
            var tag = NextTag();
            var userId = _factory.SeedUser($"susp{tag}", $"susp{tag}@fud.local", "Tạm ngưng", AccountConstants.RoleStudent);

            var response = await client.PostAsJsonAsync($"/api/admin/users/{userId}/suspend",
                new { reason = AccountConstants.ReasonViolation, notes = "Vi phạm quy định" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await AdminApiFactory.ReadJsonAsync(response);
            Assert.Equal(AccountConstants.StatusSuspended, body.GetProperty("data").GetProperty("status").GetString());
            Assert.Equal(AccountConstants.NotificationSent, body.GetProperty("data").GetProperty("notificationStatus").GetString());

            // Dữ liệu user vẫn còn nguyên, có lịch sử
            var detail = await client.GetAsync($"/api/admin/users/{userId}");
            var detailBody = await AdminApiFactory.ReadJsonAsync(detail);
            Assert.Equal(AccountConstants.StatusSuspended, detailBody.GetProperty("data").GetProperty("status").GetString());
            Assert.True(detailBody.GetProperty("data").GetProperty("statusHistories").GetArrayLength() >= 1);
        }

        [Fact]
        public async Task Suspend_Twice_Returns409()
        {
            var client = AdminClient();
            var tag = NextTag();
            var userId = _factory.SeedUser($"susp2{tag}", $"susp2{tag}@fud.local", "Tạm ngưng 2", AccountConstants.RoleStudent);

            await client.PostAsJsonAsync($"/api/admin/users/{userId}/suspend",
                new { reason = AccountConstants.ReasonGraduation, notes = "đã tốt nghiệp" });

            var second = await client.PostAsJsonAsync($"/api/admin/users/{userId}/suspend",
                new { reason = AccountConstants.ReasonGraduation, notes = "lần hai" });

            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        }

        [Fact]
        public async Task Suspend_Self_Returns403()
        {
            var client = AdminClient();

            var response = await client.PostAsJsonAsync($"/api/admin/users/{_factory.AdminId}/suspend",
                new { reason = AccountConstants.ReasonOther, notes = "tự khóa" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Suspend_WithoutReason_Returns400()
        {
            var client = AdminClient();
            var tag = NextTag();
            var userId = _factory.SeedUser($"noreason{tag}", $"noreason{tag}@fud.local", "Không lý do", AccountConstants.RoleStudent);

            var response = await client.PostAsJsonAsync($"/api/admin/users/{userId}/suspend",
                new { reason = "", notes = "" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Suspend_SuspendedUser_CannotUseOldToken()
        {
            var tag = NextTag();
            var userId = _factory.SeedUser($"locked{tag}", $"locked{tag}@fud.local", "Bị khóa", AccountConstants.RoleStudent);

            // Token cấp TRƯỚC khi suspend
            var studentClient = _factory.CreateClientWithRoles(userId, AccountConstants.RoleStudent);

            var adminClient = AdminClient();
            await adminClient.PostAsJsonAsync($"/api/admin/users/{userId}/suspend",
                new { reason = AccountConstants.ReasonTermination, notes = "nghỉ việc" });

            // Token cũ bị chặn ngay trong middleware
            var after = await studentClient.GetAsync("/api/admin/users");
            Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);

            var body = await AdminApiFactory.ReadJsonAsync(after);
            Assert.Contains("tạm ngưng", body.GetProperty("message").GetString()!.ToLower());
        }

        // ── Restore ───────────────────────────────────────────────

        [Fact]
        public async Task Restore_SuspendedAccount_SetsActive_AndWritesHistory()
        {
            var client = AdminClient();
            var tag = NextTag();
            var userId = _factory.SeedUser($"restore{tag}", $"restore{tag}@fud.local", "Khôi phục", AccountConstants.RoleStudent);

            await client.PostAsJsonAsync($"/api/admin/users/{userId}/suspend",
                new { reason = AccountConstants.ReasonOther, notes = "kiểm tra" });

            var response = await client.PostAsJsonAsync($"/api/admin/users/{userId}/restore",
                new { reason = "Đã kiểm tra hệ thống hoàn tất" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await AdminApiFactory.ReadJsonAsync(response);
            Assert.Equal(AccountConstants.StatusActive, body.GetProperty("data").GetProperty("status").GetString());

            var detail = await client.GetAsync($"/api/admin/users/{userId}");
            var detailBody = await AdminApiFactory.ReadJsonAsync(detail);
            Assert.True(detailBody.GetProperty("data").GetProperty("statusHistories").GetArrayLength() >= 2);
        }

        [Fact]
        public async Task Restore_NotSuspended_Returns409()
        {
            var client = AdminClient();
            var tag = NextTag();
            var userId = _factory.SeedUser($"notsusp{tag}", $"notsusp{tag}@fud.local", "Chưa tạm ngưng", AccountConstants.RoleStudent);

            var response = await client.PostAsJsonAsync($"/api/admin/users/{userId}/restore",
                new { reason = "Khôi phục không cần thiết" });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task Restore_WithoutReason_Returns400()
        {
            var client = AdminClient();
            var tag = NextTag();
            var userId = _factory.SeedUser($"restreason{tag}", $"restreason{tag}@fud.local", "Thiếu lý do", AccountConstants.RoleStudent);

            var response = await client.PostAsJsonAsync($"/api/admin/users/{userId}/restore",
                new { reason = "" });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
