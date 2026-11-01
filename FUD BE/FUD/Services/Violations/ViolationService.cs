using FUD.DTOs.Violations;
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

        public ViolationService(IViolationRepository violationRepo, IFileStorage fileStorage)
        {
            _violationRepo = violationRepo;
            _fileStorage = fileStorage;
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

        public async Task<DuplicateViolationCheckResultDto> CheckDuplicateAsync(List<int> studentIds, int violationTypeId, DateTime occurrenceTime)
        {
            if (studentIds == null || studentIds.Count == 0)
            {
                return new DuplicateViolationCheckResultDto { HasDuplicate = false };
            }

            var duplicates = await _violationRepo.FindDuplicateViolationsAsync(studentIds, violationTypeId, occurrenceTime);
            if (duplicates.Count == 0)
            {
                return new DuplicateViolationCheckResultDto { HasDuplicate = false };
            }

            var duplicateDtos = duplicates.Select(v => new ViolationItemDto
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
                Status = v.Status,
                RecordedByName = v.RecordedBy?.User?.FullName ?? string.Empty,
                Evidences = v.ViolationEvidences.Select(e => new ViolationEvidenceDto
                {
                    Id = e.Id,
                    FileUrl = e.FileUrl,
                    Source = e.Source
                }).ToList()
            }).ToList();

            var studentNames = string.Join(", ", duplicateDtos.Select(d => d.StudentName).Distinct());
            return new DuplicateViolationCheckResultDto
            {
                HasDuplicate = true,
                ExistingViolations = duplicateDtos,
                Message = $"Phát hiện {duplicateDtos.Count} hồ sơ vi phạm trùng lặp cho sinh viên ({studentNames}) cùng thời điểm và cùng lỗi vi phạm!"
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

            var occTime = request.OccurrenceTime == default ? DateTime.Now : request.OccurrenceTime;

            // Kiểm tra trùng lặp hồ sơ nếu người dùng chưa bấm xác nhận
            if (!request.ConfirmedDuplicate)
            {
                var duplicateCheck = await CheckDuplicateAsync(request.StudentIds, request.ViolationTypeId, occTime);
                if (duplicateCheck.HasDuplicate)
                {
                    throw new InvalidOperationException($"DUPLICATE_VIOLATION:{duplicateCheck.Message}");
                }
            }

            // 1. Upload ảnh trực tiếp lên Cloudinary
            var uploadedUrls = await _fileStorage.UploadImagesAsync(request.Evidences, "violations");
            if (uploadedUrls.Count == 0)
            {
                throw new InvalidOperationException("Không thể tải lên ảnh minh chứng lên Cloudinary.");
            }

            // 2. Tạo bản ghi vi phạm cho từng sinh viên
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
                    InitialLevel = "Cảnh cáo",
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

            // 3. Map kết quả trả về
            return violationsToCreate.Select(v => new ViolationItemDto
            {
                Id = v.Id,
                Code = v.Code,
                StudentId = v.StudentId,
                RoomId = v.RoomId,
                ViolationTypeId = v.ViolationTypeId,
                OccurrenceTime = v.OccurrenceTime,
                Description = v.Description,
                Status = v.Status,
                Evidences = v.ViolationEvidences.Select(e => new ViolationEvidenceDto
                {
                    Id = e.Id,
                    FileUrl = e.FileUrl,
                    Source = e.Source
                }).ToList()
            }).ToList();
        }

        public async Task<List<ViolationItemDto>> GetViolationsAsync(int? roomId = null, int? studentId = null)
        {
            var violations = await _violationRepo.GetViolationsAsync(roomId, studentId);
            return violations.Select(v => new ViolationItemDto
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
                Status = v.Status,
                RecordedByName = v.RecordedBy?.User?.FullName ?? string.Empty,
                Evidences = v.ViolationEvidences.Select(e => new ViolationEvidenceDto
                {
                    Id = e.Id,
                    FileUrl = e.FileUrl,
                    Source = e.Source
                }).ToList()
            }).ToList();
        }
    }
}
