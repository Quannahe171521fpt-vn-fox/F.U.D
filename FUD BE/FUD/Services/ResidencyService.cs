using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FUD.DTOs.Residencies;
using FUD.Models;
using FUD.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FUD.Services
{
    public class ResidencyService : IResidencyService
    {
        private readonly FudContext _context;

        public ResidencyService(FudContext context)
        {
            _context = context;
        }

        public async Task<List<ResidencyHistoryItemDto>> GetStudentResidencyHistoryAsync(
            int studentUserId,
            ResidencyHistoryQueryDto? query = null)
        {
            var residencies = await _context.Residencies
                .AsNoTracking()
                .Include(r => r.Term)
                .Include(r => r.Bed)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(rm => rm.RoomType)
                .Include(r => r.Bed)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(rm => rm.Block)
                            .ThenInclude(bl => bl.Dorm)
                                .ThenInclude(d => d.Campus)
                .Include(r => r.Handover)
                .Include(r => r.Violations)
                .Where(r => r.StudentId == studentUserId)
                .ToListAsync();

            var items = residencies.Select(MapToItemDto).ToList();

            if (query != null)
            {
                if (!string.IsNullOrWhiteSpace(query.Status))
                {
                    var st = query.Status.Trim();
                    items = items
                        .Where(x => x.Status.Equals(st, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                if (!string.IsNullOrWhiteSpace(query.RegistrationType))
                {
                    var rt = query.RegistrationType.Trim();
                    items = items
                        .Where(x => string.Equals(x.RegistrationType, rt, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                if (!string.IsNullOrWhiteSpace(query.Term))
                {
                    var termKw = query.Term.Trim().ToLower();
                    items = items
                        .Where(x =>
                            x.TermName.ToLower().Contains(termKw) ||
                            x.AcademicYear.ToLower().Contains(termKw) ||
                            $"{x.TermName} {x.AcademicYear}".ToLower().Contains(termKw))
                        .ToList();
                }

                if (!string.IsNullOrWhiteSpace(query.Search))
                {
                    var s = query.Search.Trim().ToLower();
                    items = items
                        .Where(x =>
                            x.ResidencyCode.ToLower().Contains(s) ||
                            x.RoomNumber.ToLower().Contains(s) ||
                            x.BedCode.ToLower().Contains(s) ||
                            x.BlockCode.ToLower().Contains(s) ||
                            x.BlockName.ToLower().Contains(s) ||
                            x.DormName.ToLower().Contains(s) ||
                            x.TermName.ToLower().Contains(s))
                        .ToList();
                }
            }

            return items
                .OrderBy(x => GetStatusSortOrder(x.Status))
                .ThenByDescending(x => x.TermStartDate)
                .ThenByDescending(x => x.CheckInDate ?? DateOnly.MinValue)
                .ThenByDescending(x => x.Id)
                .ToList();
        }

        public async Task<ResidencyHistoryDetailDto?> GetStudentResidencyDetailAsync(
            int studentUserId,
            int residencyId)
        {
            var residency = await _context.Residencies
                .AsNoTracking()
                .Include(r => r.Term)
                .Include(r => r.Bed)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(rm => rm.RoomType)
                .Include(r => r.Bed)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(rm => rm.Block)
                            .ThenInclude(bl => bl.Dorm)
                                .ThenInclude(d => d.Campus)
                .Include(r => r.SourceBooking)
                .Include(r => r.Handover)
                    .ThenInclude(h => h!.Guard)
                        .ThenInclude(g => g.User)
                .Include(r => r.Violations)
                .Include(r => r.CheckoutRequests)
                    .ThenInclude(cr => cr.Request)
                        .ThenInclude(req => req.Handover)
                .Include(r => r.BedTransferRequestFromResidencies)
                    .ThenInclude(bt => bt.Request)
                .Include(r => r.BedTransferRequestFromResidencies)
                    .ThenInclude(bt => bt.FromBed)
                        .ThenInclude(fb => fb.Room)
                            .ThenInclude(rm => rm.Block)
                .Include(r => r.BedTransferRequestFromResidencies)
                    .ThenInclude(bt => bt.ToBed)
                        .ThenInclude(tb => tb.Room)
                            .ThenInclude(rm => rm.Block)
                .Include(r => r.BedTransferRequestNewResidencies)
                    .ThenInclude(bt => bt.Request)
                .Include(r => r.BedTransferRequestNewResidencies)
                    .ThenInclude(bt => bt.FromBed)
                        .ThenInclude(fb => fb.Room)
                            .ThenInclude(rm => rm.Block)
                .Include(r => r.BedTransferRequestNewResidencies)
                    .ThenInclude(bt => bt.ToBed)
                        .ThenInclude(tb => tb.Room)
                            .ThenInclude(rm => rm.Block)
                .FirstOrDefaultAsync(r => r.Id == residencyId && r.StudentId == studentUserId);

            if (residency == null)
            {
                return null;
            }

            var baseDto = MapToItemDto(residency);
            var detail = new ResidencyHistoryDetailDto
            {
                Id = baseDto.Id,
                ResidencyCode = baseDto.ResidencyCode,
                StudentId = baseDto.StudentId,
                Status = baseDto.Status,
                RegistrationType = baseDto.RegistrationType,
                TermId = baseDto.TermId,
                TermName = baseDto.TermName,
                AcademicYear = baseDto.AcademicYear,
                TermStartDate = baseDto.TermStartDate,
                TermEndDate = baseDto.TermEndDate,
                CampusName = baseDto.CampusName,
                DormCode = baseDto.DormCode,
                DormName = baseDto.DormName,
                BlockId = baseDto.BlockId,
                BlockCode = baseDto.BlockCode,
                BlockName = baseDto.BlockName,
                RoomId = baseDto.RoomId,
                RoomNumber = baseDto.RoomNumber,
                FloorLevel = baseDto.FloorLevel,
                RoomTypeName = baseDto.RoomTypeName,
                BedCapacity = baseDto.BedCapacity,
                BedId = baseDto.BedId,
                BedCode = baseDto.BedCode,
                PositionInRoom = baseDto.PositionInRoom,
                CheckInDate = baseDto.CheckInDate,
                PlannedCheckOutDate = baseDto.PlannedCheckOutDate,
                ActualCheckOutDate = baseDto.ActualCheckOutDate,
                SourceRequestId = baseDto.SourceRequestId,
                HasCheckinHandover = baseDto.HasCheckinHandover,
                CheckinHandoverId = baseDto.CheckinHandoverId,
                ViolationsCount = baseDto.ViolationsCount,

                SourceRequestStatus = residency.SourceBooking?.Status,
                SourceRequestCreatedTime = residency.SourceBooking?.CreatedTime,

                CheckinHandoverStatus = residency.Handover?.Status,
                CheckinCompletedTime = residency.Handover?.CompletedTime ?? residency.Handover?.CreatedTime,
                CheckinGuardName = residency.Handover?.Guard?.User?.FullName,
                HasDamageOrMissingAtCheckin = residency.Handover?.HasDamageOrMissingFlag ?? false
            };

            // 1. Lịch sử chuyển giường liên quan (cả chiều chuyển đi từ đợt lưu trú này hoặc chuyển đến đợt lưu trú này)
            var transfers = new List<ResidencyTransferRecordDto>();

            foreach (var bt in residency.BedTransferRequestFromResidencies)
            {
                transfers.Add(new ResidencyTransferRecordDto
                {
                    RequestId = bt.RequestId,
                    Direction = "Outgoing",
                    Status = bt.Request?.Status ?? "Pending",
                    FromRoomNumber = bt.FromBed?.Room?.RoomNumber ?? string.Empty,
                    FromBlockCode = bt.FromBed?.Room?.Block?.Code ?? string.Empty,
                    FromBedCode = bt.FromBed?.BedCode ?? string.Empty,
                    ToRoomNumber = bt.ToBed?.Room?.RoomNumber ?? string.Empty,
                    ToBlockCode = bt.ToBed?.Room?.Block?.Code ?? string.Empty,
                    ToBedCode = bt.ToBed?.BedCode ?? string.Empty,
                    TransferReason = bt.TransferReason,
                    TransferDate = bt.TransferDate,
                    PriceDifference = bt.PriceDifference,
                    CreatedTime = bt.Request?.CreatedTime ?? DateTime.MinValue
                });
            }

            foreach (var bt in residency.BedTransferRequestNewResidencies)
            {
                if (transfers.Any(t => t.RequestId == bt.RequestId)) continue;
                transfers.Add(new ResidencyTransferRecordDto
                {
                    RequestId = bt.RequestId,
                    Direction = "Incoming",
                    Status = bt.Request?.Status ?? "Completed",
                    FromRoomNumber = bt.FromBed?.Room?.RoomNumber ?? string.Empty,
                    FromBlockCode = bt.FromBed?.Room?.Block?.Code ?? string.Empty,
                    FromBedCode = bt.FromBed?.BedCode ?? string.Empty,
                    ToRoomNumber = bt.ToBed?.Room?.RoomNumber ?? string.Empty,
                    ToBlockCode = bt.ToBed?.Room?.Block?.Code ?? string.Empty,
                    ToBedCode = bt.ToBed?.BedCode ?? string.Empty,
                    TransferReason = bt.TransferReason,
                    TransferDate = bt.TransferDate,
                    PriceDifference = bt.PriceDifference,
                    CreatedTime = bt.Request?.CreatedTime ?? DateTime.MinValue
                });
            }

            detail.Transfers = transfers.OrderByDescending(t => t.CreatedTime).ToList();

            // 2. Đơn trả phòng (Checkout_Requests) gắn với đợt lưu trú này
            var checkoutHandoverIds = new List<int>();
            foreach (var cr in residency.CheckoutRequests.OrderByDescending(c => c.Request?.CreatedTime))
            {
                var coHandover = cr.Request?.Handover;
                if (coHandover != null)
                {
                    checkoutHandoverIds.Add(coHandover.Id);
                }

                detail.Checkouts.Add(new ResidencyCheckoutRecordDto
                {
                    RequestId = cr.RequestId,
                    Status = cr.Request?.Status ?? "Pending",
                    ExpectedCheckOutDate = cr.ExpectedCheckOutDate,
                    Reason = cr.Request?.Reason,
                    IsExceptionalCase = cr.IsExceptionalCase,
                    ExceptionNote = cr.ExceptionNote,
                    CreatedTime = cr.Request?.CreatedTime ?? DateTime.MinValue,
                    ReviewedTime = cr.ReviewedTime,
                    CheckoutHandoverId = coHandover?.Id,
                    CheckoutHandoverStatus = coHandover?.Status,
                    TotalCompensation = coHandover?.TotalCompensation
                });
            }

            // 3. Tóm tắt các hóa đơn liên quan tới đợt lưu trú (RoomFee từ SourceRequestId, BedTransfer, hoặc Compensation từ Handover)
            var transferRequestIds = detail.Transfers.Select(t => t.RequestId).ToList();
            var handoverIds = new List<int>(checkoutHandoverIds);
            if (residency.Handover != null)
            {
                handoverIds.Add(residency.Handover.Id);
            }

            var invoices = await _context.Invoices
                .AsNoTracking()
                .Where(i => i.StudentId == studentUserId &&
                    ((residency.SourceBookingId.HasValue && i.BookingRequestId == residency.SourceBookingId.Value) ||
                     (i.TransferRequestId.HasValue && transferRequestIds.Contains(i.TransferRequestId.Value)) ||
                     (i.HandoverId.HasValue && handoverIds.Contains(i.HandoverId.Value))))
                .OrderByDescending(i => i.IssuedTime)
                .Select(i => new ResidencyInvoiceSummaryDto
                {
                    Id = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    InvoiceType = i.InvoiceType,
                    Amount = i.Amount,
                    Status = i.Status,
                    IssuedTime = i.IssuedTime,
                    DueDate = i.DueDate,
                    PaidTime = i.PaidTime
                })
                .ToListAsync();

            detail.Invoices = invoices;

            return detail;
        }

        private static ResidencyHistoryItemDto MapToItemDto(Residency r)
        {
            var bed = r.Bed;
            var room = bed?.Room;
            var roomType = room?.RoomType;
            var block = room?.Block;
            var dorm = block?.Dorm;
            var campus = dorm?.Campus;
            var term = r.Term;

            var activeViolationsCount = r.Violations?
                .Count(v => !string.Equals(v.Status, "Dismissed", StringComparison.OrdinalIgnoreCase)) ?? 0;

            return new ResidencyHistoryItemDto
            {
                Id = r.Id,
                ResidencyCode = $"RES-{r.Id:D4}",
                StudentId = r.StudentId,
                Status = r.Status,
                RegistrationType = r.RegistrationType ?? "Booking",
                TermId = r.TermId,
                TermName = term?.Name ?? string.Empty,
                AcademicYear = term?.AcademicYear ?? string.Empty,
                TermStartDate = term?.StartDate ?? DateOnly.MinValue,
                TermEndDate = term?.EndDate ?? DateOnly.MinValue,
                CampusName = campus?.Name ?? "Hòa Lạc",
                DormCode = dorm?.Code ?? string.Empty,
                DormName = dorm?.Name ?? string.Empty,
                BlockId = room?.BlockId ?? 0,
                BlockCode = block?.Code ?? string.Empty,
                BlockName = block?.Name ?? string.Empty,
                RoomId = bed?.RoomId ?? 0,
                RoomNumber = room?.RoomNumber ?? string.Empty,
                FloorLevel = room?.FloorLevel,
                RoomTypeName = roomType?.Name ?? string.Empty,
                BedCapacity = roomType?.BedCapacity ?? 0,
                BedId = r.BedId,
                BedCode = bed?.BedCode ?? string.Empty,
                PositionInRoom = bed?.PositionInRoom,
                CheckInDate = r.CheckInDate,
                PlannedCheckOutDate = r.PlannedCheckOutDate,
                ActualCheckOutDate = r.ActualCheckOutDate,
                SourceRequestId = r.SourceBookingId,
                HasCheckinHandover = r.Handover != null,
                CheckinHandoverId = r.Handover?.Id,
                ViolationsCount = activeViolationsCount
            };
        }

        private static int GetStatusSortOrder(string status)
        {
            return status switch
            {
                "Active" => 0,
                "Reserved" => 1,
                "Ended" => 2,
                "Cancelled" => 3,
                _ => 4
            };
        }
    }
}
