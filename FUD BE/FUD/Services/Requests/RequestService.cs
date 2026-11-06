using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FUD.DTOs.Requests;
using FUD.Infrastructure.Email;
using FUD.Infrastructure.Storage;
using FUD.Models;
using FUD.Repositories.Interfaces.Requests;
using FUD.Repositories.Interfaces.Violations;
using FUD.Services.Interfaces.Requests;

namespace FUD.Services.Requests
{
    public class RequestService : IRequestService
    {
        private readonly IRequestRepository _requestRepo;
        private readonly IViolationRepository _violationRepo;
        private readonly IFileStorage _fileStorage;
        private readonly IEmailSender _emailSender;

        public RequestService(
            IRequestRepository requestRepo,
            IViolationRepository violationRepo,
            IFileStorage fileStorage,
            IEmailSender emailSender)
        {
            _requestRepo = requestRepo;
            _violationRepo = violationRepo;
            _fileStorage = fileStorage;
            _emailSender = emailSender;
        }

        public async Task<List<RequestItemDto>> GetAllRequestsAsync(string? requestType = null, string? status = null)
        {
            var requests = await _requestRepo.GetAllRequestsAsync(requestType, status);
            return requests.Select(MapToDto).ToList();
        }

        public async Task<List<RequestItemDto>> GetStudentRequestsAsync(int studentId)
        {
            var requests = await _requestRepo.GetStudentRequestsAsync(studentId);
            return requests.Select(MapToDto).ToList();
        }

        public async Task<RequestItemDto> CreateRequestAsync(int studentId, CreateRequestDto dto)
        {
            // Kiểm tra trạng thái tài khoản sinh viên: Nếu bị đình chỉ thì khóa toàn bộ quyền gửi đơn (Request), bao gồm Booking, BedTransfer, v.v.
            var studentUser = await _requestRepo.GetUserByIdAsync(studentId);
            if (studentUser != null && (studentUser.AccountStatus == "Suspended" || !string.IsNullOrEmpty(studentUser.SuspensionReason)))
            {
                throw new InvalidOperationException(
                    $"Tài khoản của bạn đã bị ĐÌNH CHỈ NỘI TRÚ ({studentUser.SuspensionReason ?? "Đình chỉ nội trú do vi phạm quy chế"}). " +
                    "Hệ thống đã khóa toàn bộ quyền gửi đơn từ & booking giữ chỗ. Chỉ khi Ban Quản lý mở lại tài khoản bạn mới có thể thực hiện thao tác tiếp.");
            }

            int termId = dto.TermId ?? 0;
            if (termId == 0)
            {
                var activeTerm = await _requestRepo.GetCurrentActiveTermAsync();
                if (activeTerm == null)
                    throw new InvalidOperationException("Không tìm thấy học kỳ hoạt động.");
                termId = activeTerm.Id;
            }

            var request = new Request
            {
                StudentId = studentId,
                TermId = termId,
                RequestType = dto.RequestType,
                Status = "Pending",
                Reason = dto.Reason ?? dto.Content,
                CreatedTime = DateTime.UtcNow,
            };

            await _requestRepo.CreateRequestAsync(request);

            if (dto.RequestType == "Complaint")
            {
                var complaint = new ComplaintRequest
                {
                    RequestId = request.Id,
                    TargetType = dto.TargetType ?? "Violation",
                    ViolationId = dto.TargetViolationId,
                    Content = dto.Content ?? dto.Reason ?? string.Empty
                };

                var evidences = new List<ComplaintEvidence>();
                if (dto.Evidences != null && dto.Evidences.Any())
                {
                    var uploadedUrls = await _fileStorage.UploadImagesAsync(dto.Evidences, "complaint-evidences");
                    foreach (var url in uploadedUrls)
                    {
                        evidences.Add(new ComplaintEvidence
                        {
                            ComplaintRequestId = request.Id,
                            FileUrl = url
                        });
                    }
                }

                await _requestRepo.AddComplaintRequestAsync(complaint, evidences);
            }

            var reloaded = await _requestRepo.GetRequestByIdAsync(request.Id);
            return MapToDto(reloaded!);
        }

        public async Task<RequestItemDto> ProcessRequestAsync(int requestId, int managerId, ProcessRequestDto dto)
        {
            var req = await _requestRepo.GetRequestByIdAsync(requestId);
            if (req == null)
                throw new KeyNotFoundException("Không tìm thấy đơn yêu cầu.");

            req.Status = dto.Decision;
            req.ProcessedById = managerId;
            req.ProcessedTime = DateTime.UtcNow;

            if (dto.Decision == "Rejected")
            {
                req.RejectReason = dto.Note;
            }

            if (req.RequestType == "Complaint" && req.ComplaintRequest?.ViolationId != null)
            {
                var violation = await _violationRepo.GetViolationByIdAsync(req.ComplaintRequest.ViolationId.Value);
                if (violation != null)
                {
                    if (dto.Decision == "Approved")
                    {
                        if (!string.IsNullOrWhiteSpace(dto.AdjustedLevel))
                        {
                            violation.FinalLevel = dto.AdjustedLevel;
                            violation.Status = "Finalized";
                        }
                        else
                        {
                            violation.Status = "Dismissed";
                        }
                    }
                    else
                    {
                        violation.FinalLevel = violation.InitialLevel;
                        violation.Status = "Finalized";
                    }

                    violation.ManagerNote = dto.Note;
                    violation.FinalizedTime = DateTime.UtcNow;
                    violation.EvaluatedById = managerId;
                    await _violationRepo.UpdateViolationAsync(violation);

                    // Xử lý khóa / mở đình chỉ dựa trên kết quả giải quyết khiếu nại của Quản lý
                    var studentUser = await _requestRepo.GetUserByIdAsync(req.StudentId);
                    if (studentUser != null)
                    {
                        if (violation.Status == "Finalized" && !string.IsNullOrEmpty(violation.FinalLevel) &&
                            (violation.FinalLevel.Contains("Đình chỉ") || violation.FinalLevel.Contains("Buộc rời")))
                        {
                            studentUser.AccountStatus = "Suspended";
                            studentUser.SuspendedAt = DateTime.UtcNow;
                            studentUser.SuspendedById = managerId;
                            studentUser.SuspensionReason = $"Bị đình chỉ nội trú theo quyết định kỷ luật biên bản {violation.Code} ({violation.FinalLevel})";
                            await _requestRepo.UpdateUserAsync(studentUser);
                        }
                        else if (violation.Status == "Dismissed" || (violation.Status == "Finalized" && !string.IsNullOrEmpty(violation.FinalLevel) && !violation.FinalLevel.Contains("Đình chỉ")))
                        {
                            if (studentUser.AccountStatus == "Suspended")
                            {
                                studentUser.AccountStatus = "Active";
                                studentUser.SuspendedAt = null;
                                studentUser.SuspendedById = null;
                                studentUser.SuspensionReason = null;
                                await _requestRepo.UpdateUserAsync(studentUser);
                            }
                        }
                    }

                    if (req.Student?.User != null && !string.IsNullOrWhiteSpace(req.Student.User.Email))
                    {
                        var studentEmail = req.Student.User.Email;
                        var studentName = req.Student.User.FullName;
                        var statusVi = violation.Status == "Dismissed" ? "Đã bác bỏ biên bản (khiếu nại được duyệt)" :
                                       violation.Status == "Finalized" ? $"Đã có kết luận ({violation.FinalLevel})" : violation.Status;

                        var subject = $"[KTX FUD] Thông báo kết quả xử lý đơn khiếu nại: {violation.Code}";
                        var body = $@"
<div style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
    <h2 style='color: #ea580c;'>Kết quả xử lý Đơn khiếu nại</h2>
    <p>Xin chào <strong>{studentName}</strong>,</p>
    <p>Ban Quản lý Ký túc xá đã hoàn tất xem xét và phản hồi đơn khiếu nại của bạn đối với biên bản vi phạm <strong>{violation.Code}</strong>.</p>
    <div style='background: #f8fafc; border-left: 4px solid #ea580c; padding: 12px; margin: 16px 0;'>
        <p><strong>Biên bản:</strong> {violation.Code}</p>
        <p><strong>Lỗi vi phạm:</strong> {violation.ViolationType?.Name}</p>
        <p><strong>Trạng thái kết luận:</strong> {statusVi}</p>
        <p><strong>Mức độ chính thức:</strong> {violation.FinalLevel ?? "Đã hủy bỏ"}</p>
        <p><strong>Phản hồi từ Ban Quản lý:</strong> {dto.Note ?? "Không có"}</p>
    </div>
    <p>Vui lòng đăng nhập hệ thống F.U.D để xem chi tiết đầy đủ hồ sơ của bạn.</p>
    <hr style='border: none; border-top: 1px solid #e2e8f0; margin: 20px 0;'/>
    <p style='font-size: 12px; color: #64748b;'>Ban Quản lý Ký túc xá Đại học FPT</p>
</div>";
                        try
                        {
                            await _emailSender.SendEmailAsync(studentEmail, subject, body);
                        }
                        catch
                        {
                            // Ignore email sending error
                        }
                    }
                }
            }

            await _requestRepo.UpdateRequestAsync(req);
            return MapToDto(req);
        }

        private static RequestItemDto MapToDto(Request req)
        {
            var res = req.Student?.Residencies?.FirstOrDefault();
            var roomInfo = res?.Bed?.Room != null ? $"P.{res.Bed.Room.RoomNumber} ({res.Bed.Room.Block?.Name})" : "Chưa xếp phòng";

            var dto = new RequestItemDto
            {
                Id = req.Id,
                RequestCode = $"REQ-{req.Id:D6}",
                StudentId = req.StudentId,
                StudentName = req.Student?.User?.FullName ?? string.Empty,
                StudentCode = req.Student?.User?.Code ?? string.Empty,
                RoomInfo = roomInfo,
                RequestType = req.RequestType,
                Status = req.Status,
                Reason = req.Reason,
                RejectReason = req.RejectReason,
                CreatedTime = req.CreatedTime,
                ProcessedTime = req.ProcessedTime,
                ProcessedByName = req.ProcessedBy?.User?.FullName,
                StudentAccountStatus = req.Student?.User?.AccountStatus ?? "Active",
                SuspensionReason = req.Student?.User?.SuspensionReason,
            };

            if (req.ComplaintRequest != null)
            {
                dto.Complaint = new ComplaintDetailDto
                {
                    TargetType = req.ComplaintRequest.TargetType,
                    ViolationId = req.ComplaintRequest.ViolationId,
                    ViolationCode = req.ComplaintRequest.Violation?.Code,
                    ViolationTypeName = req.ComplaintRequest.Violation?.ViolationType?.Name,
                    Content = req.ComplaintRequest.Content,
                    ResolutionNote = req.ComplaintRequest.ResolutionNote,
                    Evidences = req.ComplaintRequest.ComplaintEvidences.Select(e => e.FileUrl).ToList()
                };
            }

            return dto;
        }
    }
}
