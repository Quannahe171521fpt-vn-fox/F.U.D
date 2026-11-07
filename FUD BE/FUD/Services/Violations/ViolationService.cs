using FUD.DTOs.Violations;
using FUD.Infrastructure.Email;
using FUD.Infrastructure.Storage;
using FUD.Models;
using FUD.Repositories.Interfaces.Violations;
using FUD.Services.Interfaces.Violations;

namespace FUD.Services.Violations
{
    public class ViolationService : IViolationService
    {
        private readonly IViolationRepository _violationRepo;
        private readonly IFileStorage _fileStorage;
        private readonly IEmailSender _emailSender;

        public ViolationService(IViolationRepository violationRepo, IFileStorage fileStorage, IEmailSender emailSender)
        {
            _violationRepo = violationRepo;
            _fileStorage = fileStorage;
            _emailSender = emailSender;
        }

        public async Task<List<ViolationTypeDto>> GetViolationTypesAsync()
        {
            var types = await _violationRepo.GetViolationTypesAsync();
            return types.Select(t => new ViolationTypeDto
            {
                Id = t.Id,
                Name = t.Name
            }).ToList();
        }

        public async Task<List<RoomStudentDto>> GetStudentsByRoomAsync(int roomId)
        {
            var residencies = await _violationRepo.GetActiveResidenciesByRoomIdAsync(roomId);
            return residencies.Select(r => new RoomStudentDto
            {
                StudentId = r.StudentId,
                StudentCode = r.Student?.User?.Code ?? string.Empty,
                FullName = r.Student?.User?.FullName ?? string.Empty,
                BedCode = r.Bed?.BedCode ?? string.Empty,
                StatusDescription = $"Đang ở · Giường {r.Bed?.BedCode ?? "N/A"}",
                ResidencyId = r.Id
            }).ToList();
        }

        public async Task<RoomStudentDto?> SearchStudentByCodeAsync(string studentCode, int? currentRoomId = null)
        {
            var student = await _violationRepo.GetStudentByCodeAsync(studentCode.Trim());
            if (student == null) return null;

            var residency = await _violationRepo.GetCurrentResidencyByStudentIdAsync(student.UserId);
            var bedCode = residency?.Bed?.BedCode ?? "Không có";
            var roomInfo = residency?.Bed?.Room != null ? $"Phòng {residency.Bed.Room.RoomNumber}" : "Chưa xếp phòng";

            return new RoomStudentDto
            {
                StudentId = student.UserId,
                StudentCode = student.User?.Code ?? string.Empty,
                FullName = student.User?.FullName ?? string.Empty,
                BedCode = bedCode,
                StatusDescription = residency != null ? $"Đang ở · {roomInfo} (Giường {bedCode})" : "Chưa có lưu trú",
                ResidencyId = residency?.Id
            };
        }

        public async Task<List<ViolationItemDto>> RecordViolationsAsync(CreateViolationRequestDto request, int guardUserId)
        {
            if (request.StudentIds == null || request.StudentIds.Count == 0)
            {
                throw new ArgumentException("Phải chọn ít nhất 1 sinh viên vi phạm.");
            }

            if (!await _violationRepo.RoomExistsAsync(request.RoomId))
            {
                throw new ArgumentException("Phòng không tồn tại.");
            }

            if (string.IsNullOrWhiteSpace(request.Description))
            {
                throw new ArgumentException("Mô tả vi phạm không được để trống.");
            }

            if (request.Evidences == null || request.Evidences.Count == 0)
            {
                throw new ArgumentException("Yêu cầu tối thiểu 1 ảnh minh chứng.");
            }

            var occTime = request.OccurrenceTime == default ? DateTime.UtcNow : request.OccurrenceTime.ToUniversalTime();

            // 1. Upload ảnh trực tiếp lên Cloudinary
            var uploadedUrls = await _fileStorage.UploadImagesAsync(request.Evidences, "violations");
            if (uploadedUrls.Count == 0)
            {
                throw new InvalidOperationException("Không thể tải lên ảnh minh chứng lên Cloudinary.");
            }

            // 2. Tạo bản ghi vi phạm cho từng sinh viên (Bỏ kiểm tra trùng lặp theo yêu cầu)
            var violationsToCreate = new List<Violation>();

            for (int i = 0; i < request.StudentIds.Count; i++)
            {
                var sId = request.StudentIds[i];
                var residency = await _violationRepo.GetCurrentResidencyByStudentIdAsync(sId);
                var code = await _violationRepo.GenerateViolationCodeAsync();
                
                if (violationsToCreate.Any(v => v.Code == code))
                {
                    code = $"{code}-{i + 1}";
                }

                var violation = new Violation
                {
                    Code = code,
                    StudentId = sId,
                    RoomId = request.RoomId,
                    ResidencyId = residency?.Id,
                    ViolationTypeId = request.ViolationTypeId,
                    RecordedById = guardUserId,
                    OccurrenceTime = occTime,
                    Description = request.Description,
                    InitialLevel = null,
                    Status = "Recorded",
                    ViolationEvidences = uploadedUrls.Select(url => new ViolationEvidence
                    {
                        Source = "Guard",
                        FileUrl = url
                    }).ToList()
                };

                violationsToCreate.Add(violation);
            }

            await _violationRepo.AddViolationsAsync(violationsToCreate);

            return violationsToCreate.Select(MapToDto).ToList();
        }

        public async Task<List<ViolationItemDto>> GetViolationsAsync(int? roomId = null, int? studentId = null, string? status = null)
        {
            var violations = await _violationRepo.GetViolationsAsync(roomId, studentId, status);
            var now = DateTime.UtcNow;

            // Tự động Finalize các biên bản đã được định mức (có InitialLevel) nhưng SV không gửi request trong hạn 24h
            foreach (var v in violations.Where(v =>
                v.Status == "Recorded" &&
                v.InitialLevel != null &&
                v.RequestDeadline.HasValue &&
                now > v.RequestDeadline.Value &&
                v.ComplaintRequests != null && !v.ComplaintRequests.Any()))
            {
                v.Status = "Finalized";
                v.FinalLevel = v.InitialLevel ?? "Cảnh cáo";
                v.FinalizedTime = now;
                await _violationRepo.UpdateViolationAsync(v);

                // Khóa đình chỉ sinh viên nếu mức phạt là Đình chỉ / Buộc rời
                if (!string.IsNullOrEmpty(v.FinalLevel) && (v.FinalLevel.Contains("Đình chỉ") || v.FinalLevel.Contains("Buộc rời")))
                {
                    var studentUser = await _violationRepo.GetUserByIdAsync(v.StudentId);
                    if (studentUser != null && studentUser.AccountStatus != "Suspended")
                    {
                        studentUser.AccountStatus = "Suspended";
                        studentUser.SuspendedAt = now;
                        studentUser.SuspendedById = v.EvaluatedById;
                        studentUser.SuspensionReason = $"Bị đình chỉ nội trú theo quyết định kỷ luật biên bản {v.Code} ({v.FinalLevel})";
                        await _violationRepo.UpdateUserAsync(studentUser);
                    }
                }
            }

            return violations.Select(MapToDto).ToList();
        }

        public async Task<ViolationItemDto?> GetViolationByIdAsync(int id)
        {
            var v = await _violationRepo.GetViolationByIdAsync(id);
            if (v == null) return null;
            var dto = MapToDto(v);
            dto.PriorViolationsCount = await _violationRepo.GetStudentPriorViolationsCountAsync(v.StudentId, v.Id);
            return dto;
        }

        // UC19: Danh sách thông báo vi phạm dành cho Sinh viên đang đăng nhập
        public async Task<List<ViolationItemDto>> GetStudentNoticesAsync(int studentUserId)
        {
            var list = await _violationRepo.GetViolationsAsync(studentId: studentUserId);
            var now = DateTime.UtcNow;

            // Kiểm tra và tự động chuyển các biên bản quá hạn 24h mà SV không kháng cáo sang Finalized
            foreach (var v in list.Where(v => v.Status == "Recorded" && v.RequestDeadline.HasValue && now > v.RequestDeadline.Value))
            {
                v.Status = "Finalized";
                v.FinalLevel = v.InitialLevel ?? "Cảnh cáo";
                v.FinalizedTime = now;
                await _violationRepo.UpdateViolationAsync(v);

                // Khóa đình chỉ sinh viên nếu mức phạt là Đình chỉ
                if (!string.IsNullOrEmpty(v.FinalLevel) && (v.FinalLevel.Contains("Đình chỉ") || v.FinalLevel.Contains("Buộc rời")))
                {
                    var studentUser = await _violationRepo.GetUserByIdAsync(v.StudentId);
                    if (studentUser != null && studentUser.AccountStatus != "Suspended")
                    {
                        studentUser.AccountStatus = "Suspended";
                        studentUser.SuspendedAt = now;
                        studentUser.SuspendedById = v.EvaluatedById;
                        studentUser.SuspensionReason = $"Bị đình chỉ nội trú theo quyết định kỷ luật biên bản {v.Code} ({v.FinalLevel})";
                        await _violationRepo.UpdateUserAsync(studentUser);
                    }
                }
            }

            // Các biên bản đã được BQL thông báo (Recorded, Finalized)
            var notices = list.Where(v => v.Status != "Dismissed").ToList();
            return notices.Select(MapToDto).ToList();
        }

        // UC21: Lịch sử vi phạm cá nhân của sinh viên
        public async Task<List<ViolationItemDto>> GetStudentHistoryAsync(int studentUserId, string? term = null, string? status = null)
        {
            var list = await _violationRepo.GetViolationsAsync(studentId: studentUserId);
            var query = list.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(v => v.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(term))
            {
                query = query.Where(v => v.Residency?.Term?.Name != null && v.Residency.Term.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            return query.Select(MapToDto).ToList();
        }

        // UC23: Quản lý đánh giá mức độ vi phạm ban đầu, gửi thông báo & set hạn gửi request
        public async Task<ViolationItemDto> EvaluateViolationAsync(int violationId, int managerUserId, EvaluateViolationRequestDto request)
        {
            var violation = await _violationRepo.GetViolationByIdAsync(violationId);
            if (violation == null)
            {
                throw new ArgumentException("Không tìm thấy hồ sơ vi phạm.");
            }

            if (string.IsNullOrWhiteSpace(request.InitialLevel))
            {
                throw new ArgumentException("Vui lòng chọn mức độ vi phạm.");
            }

            // Nếu quản lý bác bỏ do thiếu bằng chứng (A3: Insufficient evidence)
            if (request.InitialLevel == "Bác bỏ" || request.InitialLevel == "Dismissed")
            {
                if (string.IsNullOrWhiteSpace(request.ManagerNote))
                {
                    throw new ArgumentException("Vui lòng nhập lý do bác bỏ hồ sơ vi phạm.");
                }
                violation.EvaluatedById = managerUserId;
                violation.ManagerNote = request.ManagerNote;
                violation.Status = "Dismissed";
                violation.FinalizedTime = DateTime.UtcNow;
                await _violationRepo.UpdateViolationAsync(violation);
                var dismissedUpdated = await _violationRepo.GetViolationByIdAsync(violation.Id);

                // Gửi email thông báo bác bỏ cho sinh viên
                var studentEmail = dismissedUpdated?.Student?.User?.Email;
                if (!string.IsNullOrWhiteSpace(studentEmail))
                {
                    _ = _emailSender.SendEmailAsync(
                        studentEmail,
                        $"[KTX FUD] Thông báo bác bỏ biên bản vi phạm {violation.Code}",
                        $@"<h3>Kính gửi sinh viên {dismissedUpdated?.Student?.User?.FullName},</h3>
                           <p>Ban Quản lý KTX FUD thông báo biên bản vi phạm <strong>{violation.Code}</strong> đã được <strong>BÁC BỎ</strong>.</p>
                           <p><strong>Lý do:</strong> {request.ManagerNote}</p>
                           <p>Hồ sơ này không ghi nhận kỷ luật vào tài khoản của bạn.</p>
                           <br/><p>Trân trọng,<br/>Ban Quản lý Ký túc xá FUD</p>"
                    );
                }

                return MapToDto(dismissedUpdated!);
            }

            violation.EvaluatedById = managerUserId;
            violation.InitialLevel = request.InitialLevel;
            violation.ManagerNote = request.ManagerNote;
            // 24 giờ cho sinh viên gửi request kháng cáo
            violation.RequestDeadline = DateTime.UtcNow.AddHours(24);
            violation.Status = "Recorded";

            await _violationRepo.UpdateViolationAsync(violation);

            // Nếu mức phạt là Đình chỉ / Buộc rời → Khóa tài khoản ngay lập tức khi ban hành
            if (!string.IsNullOrEmpty(request.InitialLevel) &&
                (request.InitialLevel.Contains("Đình chỉ") || request.InitialLevel.Contains("Buộc rời")))
            {
                var studentUser = await _violationRepo.GetUserByIdAsync(violation.StudentId);
                if (studentUser != null && studentUser.AccountStatus != "Suspended")
                {
                    studentUser.AccountStatus = "Suspended";
                    studentUser.SuspendedAt = DateTime.UtcNow;
                    studentUser.SuspendedById = managerUserId;
                    studentUser.SuspensionReason = $"Bị đình chỉ nội trú theo quyết định kỷ luật biên bản {violation.Code} ({request.InitialLevel})";
                    await _violationRepo.UpdateUserAsync(studentUser);
                }
            }

            var updated = await _violationRepo.GetViolationByIdAsync(violation.Id);

            // Gửi email thông báo quyết định xử lý vi phạm cho sinh viên
            var email = updated?.Student?.User?.Email;
            if (!string.IsNullOrWhiteSpace(email))
            {
                _ = _emailSender.SendEmailAsync(
                    email,
                    $"[KTX FUD] Thông báo xử lý vi phạm kỷ luật {violation.Code}",
                    $@"<h3>Kính gửi sinh viên {updated?.Student?.User?.FullName},</h3>
                       <p>Ban Quản lý KTX FUD thông báo về việc xử lý biên bản vi phạm <strong>{violation.Code}</strong> của bạn như sau:</p>
                       <ul>
                         <li><strong>Hành vi vi phạm:</strong> {updated?.ViolationType?.Name}</li>
                         <li><strong>Mức độ đánh giá:</strong> <span style='color:red;font-weight:bold;'>{request.InitialLevel}</span></li>
                         <li><strong>Thời điểm vi phạm:</strong> {updated?.OccurrenceTime:dd/MM/yyyy HH:mm}</li>
                         <li><strong>Ghi chú từ BQL:</strong> {request.ManagerNote ?? "Không có"}</li>
                       </ul>
                       <p>Nếu bạn thấy quyết định này chưa thỏa đáng, vui lòng truy cập hệ thống và gửi <strong>Đơn khiếu nại (Request)</strong> trong vòng 24 giờ tới.</p>
                       <br/><p>Trân trọng,<br/>Ban Quản lý Ký túc xá FUD</p>"
                );
            }

            return MapToDto(updated!);
        }

        public async Task UnsuspendStudentAsync(int studentId, int managerUserId, string? note = null)
        {
            var studentUser = await _violationRepo.GetUserByIdAsync(studentId);
            if (studentUser == null)
            {
                throw new ArgumentException("Không tìm thấy thông tin sinh viên.");
            }

            studentUser.AccountStatus = "Active";
            studentUser.SuspendedAt = null;
            studentUser.SuspendedById = null;
            studentUser.SuspensionReason = null;
            await _violationRepo.UpdateUserAsync(studentUser);

            if (!string.IsNullOrWhiteSpace(studentUser.Email))
            {
                _ = _emailSender.SendEmailAsync(
                    studentUser.Email,
                    "[KTX FUD] Thông báo Mở khóa quyền nội trú & Giữ phòng",
                    $@"<h3>Kính gửi sinh viên {studentUser.FullName},</h3>
                       <p>Ban Quản lý Ký túc xá FUD xin thông báo: Quyền đăng ký nội trú, giữ chỗ và book phòng của bạn đã được <strong>MỞ KHÓA</strong> thành công.</p>
                       <p><strong>Ghi chú từ BQL:</strong> {note ?? "Đã hoàn thành thời hạn đình chỉ hoặc có quyết định xóa kỷ luật."}</p>
                       <br/><p>Trân trọng,<br/>Ban Quản lý Ký túc xá FUD</p>"
                );
            }
        }

        private static ViolationItemDto MapToDto(Violation v)
        {
            return new ViolationItemDto
            {
                Id = v.Id,
                Code = v.Code,
                StudentId = v.StudentId,
                StudentCode = v.Student?.User?.Code ?? string.Empty,
                StudentName = v.Student?.User?.FullName ?? string.Empty,
                RoomId = v.RoomId,
                RoomNumber = v.Room?.RoomNumber ?? string.Empty,
                BlockName = v.Room?.Block?.Name ?? string.Empty,
                ViolationTypeId = v.ViolationTypeId,
                ViolationTypeName = v.ViolationType?.Name ?? string.Empty,
                OccurrenceTime = v.OccurrenceTime,
                Description = v.Description,
                InitialLevel = v.InitialLevel,
                FinalLevel = v.FinalLevel,
                ManagerNote = v.ManagerNote,
                RequestDeadline = v.RequestDeadline,
                Status = v.Status,
                RecordedByName = v.RecordedBy?.User?.FullName ?? string.Empty,
                EvaluatedByName = v.EvaluatedBy?.User?.FullName ?? string.Empty,
                FinalizedTime = v.FinalizedTime,
                TermName = v.Residency?.Term?.Name ?? "Chưa rõ kỳ",
                Evidences = v.ViolationEvidences.Select(e => new ViolationEvidenceDto
                {
                    Id = e.Id,
                    FileUrl = e.FileUrl,
                    Source = e.Source
                }).ToList(),
                StudentAccountStatus = v.Student?.User?.AccountStatus ?? "Active",
                SuspensionReason = v.Student?.User?.SuspensionReason,
                LinkedRequestId = v.ComplaintRequests?.Select(c => c.RequestId).FirstOrDefault()
            };
        }
    }
}
